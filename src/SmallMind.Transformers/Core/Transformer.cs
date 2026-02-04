using SmallMind.Core.Exceptions;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;
using SmallMind.Core.Optimized;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using SmallMind.Core.Validation;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Decoder-only Transformer model (GPT-style) implemented in pure C#.
    /// Uses custom Tensor and neural network layers.
    /// </summary>
    public class TransformerModel
    {
        private readonly int _blockSize;
        private readonly int _vocabSize;
        private readonly int _nEmbd;
        private readonly int _nLayer;
        private readonly int _nHead;
        private readonly double _dropout;
        private readonly Random _random;
        
        /// <summary>
        /// Whether the model is in training mode.
        /// </summary>
        private bool _isTraining = true;

        // Embedding layers
        private readonly Embedding _tokenEmbedding;
        private readonly Embedding _positionEmbedding;
        private readonly Dropout _embDropout;

        // Transformer blocks
        private readonly List<TransformerBlock> _blocks;

        // Final layer norm and linear head
        private readonly LayerNorm _lnFinal;
        private readonly Linear _lmHead;
        
        // Cached position indices to avoid recreating each forward pass
        private readonly Dictionary<int, Tensor> _positionIndicesCache;
        
        // Workspace for reusing intermediate tensors during forward pass
        private readonly TensorWorkspace _workspace;

        public List<Tensor> Parameters { get; private set; }
        
        /// <summary>
        /// Vocabulary size (number of unique tokens).
        /// </summary>
        public int VocabSize => _vocabSize;
        
        /// <summary>
        /// Maximum sequence length (context window).
        /// </summary>
        public int BlockSize => _blockSize;
        
        /// <summary>
        /// Embedding dimension.
        /// </summary>
        public int EmbedDim => _nEmbd;
        
        /// <summary>
        /// Number of attention heads per layer.
        /// </summary>
        public int NumHeads => _nHead;
        
        /// <summary>
        /// Number of transformer layers.
        /// </summary>
        public int NumLayers => _nLayer;

        public TransformerModel(int vocabSize, int blockSize, int nEmbd, int nLayer, int nHead, double dropout, int seed = 42)
        {
            Guard.GreaterThan(vocabSize, 0);
            Guard.GreaterThan(blockSize, 0);
            Guard.GreaterThan(nEmbd, 0);
            Guard.GreaterThan(nLayer, 0);
            Guard.GreaterThan(nHead, 0);
            Guard.InRange(dropout, 0.0, 1.0);
            
            if (nEmbd % nHead != 0)
            {
                throw new ValidationException(
                    $"Embedding dimension {nEmbd} must be divisible by number of heads {nHead}",
                    nameof(nEmbd));
            }
            
            _vocabSize = vocabSize;
            _blockSize = blockSize;
            _nEmbd = nEmbd;
            _nLayer = nLayer;
            _nHead = nHead;
            _dropout = dropout;
            _random = new Random(seed);
            
            // Initialize position indices cache
            _positionIndicesCache = new Dictionary<int, Tensor>();
            
            // Initialize tensor workspace for reusing intermediate tensors
            _workspace = new TensorWorkspace();

            // Token and position embeddings
            _tokenEmbedding = new Embedding(_vocabSize, _nEmbd, _random);
            _positionEmbedding = new Embedding(_blockSize, _nEmbd, _random);
            _embDropout = new Dropout((float)dropout, _random);

            // Stack of transformer blocks - pre-size to layer count
            _blocks = new List<TransformerBlock>(_nLayer);
            for (int i = 0; i < _nLayer; i++)
            {
                _blocks.Add(new TransformerBlock(_nEmbd, _nHead, _blockSize, (float)dropout, _random));
            }

            // Final layer norm and language model head
            _lnFinal = new LayerNorm(_nEmbd);
            _lmHead = new Linear(_nEmbd, _vocabSize, useBias: false, _random);

            // Collect all parameters
            Parameters = new List<Tensor>();
            Parameters.AddRange(_tokenEmbedding.Parameters);
            Parameters.AddRange(_positionEmbedding.Parameters);
            for (int i = 0; i < _blocks.Count; i++)
            {
                Parameters.AddRange(_blocks[i].Parameters);
            }
            Parameters.AddRange(_lnFinal.Parameters);
            Parameters.AddRange(_lmHead.Parameters);

            long totalParams = GetTotalParameterCount();
            Console.WriteLine($"TransformerModel initialized: vocab={_vocabSize}, block_size={_blockSize}, " +
                            $"n_embd={_nEmbd}, n_layer={_nLayer}, n_head={_nHead}, dropout={_dropout}");
            Console.WriteLine($"Total parameters: {totalParams:N0} ({Parameters.Count} tensors)");
            
            // Warn if approaching billion-parameter scale
            if (totalParams > 500_000_000)
            {
                Console.WriteLine($"WARNING: Large model detected ({totalParams / 1_000_000}M parameters). " +
                                "Consider using quantization (Q8/Q4) for memory efficiency.");
            }
        }

        public Tensor Forward(Tensor idx)
        {
            return Forward(idx, positionOffset: 0);
        }

        /// <summary>
        /// Forward pass with optional position offset for incremental decoding.
        /// Tier-0 optimization: Support for prefill + incremental decode with correct absolute positions.
        /// </summary>
        /// <param name="idx">Token indices (batch_size, sequence_length)</param>
        /// <param name="positionOffset">Starting position for position embeddings (0 for prefill, currentSeqLen for decode)</param>
        public Tensor Forward(Tensor idx, int positionOffset)
        {
            Guard.NotNull(idx);
            
            // idx shape: (batch_size, sequence_length)
            int B = idx.Shape[0];
            int T = idx.Shape[1];

            if (T + positionOffset > _blockSize)
            {
                throw new ArgumentException($"Sequence length {T} + offset {positionOffset} exceeds block size {_blockSize}");
            }

            // Token embeddings: (B, T) -> (B, T, n_embd)
            // Reuse workspace tensor for token embeddings
            var tokEmbDest = _workspace.GetOrCreate("tokEmb", new int[] { B, T, _nEmbd }, _isTraining);
            var tokEmb = _tokenEmbedding.Forward(idx, tokEmbDest);

            // Position embeddings: get cached position indices or create new
            // Tier-0 optimization: Use positionOffset to support incremental decode
            int cacheKey = T * 100000 + positionOffset; // Unique key combining T and offset
            if (!_positionIndicesCache.TryGetValue(cacheKey, out var posIndices))
            {
                posIndices = new Tensor(new float[T], new int[] { T });
                for (int i = 0; i < T; i++)
                {
                    posIndices.Data[i] = positionOffset + i; // Apply offset for absolute position
                }
                _positionIndicesCache[cacheKey] = posIndices;
            }
            
            // Reuse workspace tensor for position embeddings
            var posEmbDest = _workspace.GetOrCreate("posEmb", new int[] { T, _nEmbd }, _isTraining);
            var posEmb = _positionEmbedding.Forward(posIndices, posEmbDest);

            // Add token and position embeddings: (B, T, n_embd)
            // Reuse workspace for the result
            var addEmbDest = _workspace.GetOrCreate("addEmb", new int[] { B, T, _nEmbd }, _isTraining);
            var x = AddPositionEmbeddings(tokEmb, posEmb, addEmbDest, B, T, _nEmbd);
            x = _embDropout.Forward(x);

            // Pass through transformer blocks
            for (int i = 0; i < _blocks.Count; i++)
            {
                x = _blocks[i].Forward(x);
            }

            // Final layer norm: (B, T, n_embd)
            x = _lnFinal.Forward(x);

            // Language model head: (B, T, n_embd) -> (B, T, vocab_size)
            var logits = _lmHead.Forward(x);

            return logits;
        }

        private Tensor AddPositionEmbeddings(Tensor tokEmb, Tensor posEmb, Tensor dest, int B, int T, int nEmbd)
        {
            int vectorSize = Vector<float>.Count;
            
            // posEmb is (T, nEmbd), need to broadcast to (B, T, nEmbd)
            // Optimization: Pre-calculate offsets to reduce redundant calculations
            for (int b = 0; b < B; b++)
            {
                for (int t = 0; t < T; t++)
                {
                    int resultOffset = (b * T + t) * nEmbd;
                    int tokEmbOffset = (b * T + t) * nEmbd;
                    int posEmbOffset = t * nEmbd;
                    
                    // SIMD-accelerated addition
                    int e = 0;
                    for (; e <= nEmbd - vectorSize; e += vectorSize)
                    {
                        var vTok = new Vector<float>(tokEmb.Data.AsSpan(tokEmbOffset + e));
                        var vPos = new Vector<float>(posEmb.Data.AsSpan(posEmbOffset + e));
                        (vTok + vPos).CopyTo(dest.Data.AsSpan(resultOffset + e));
                    }
                    
                    // Scalar remainder
                    for (; e < nEmbd; e++)
                    {
                        dest.Data[resultOffset + e] = 
                            tokEmb.Data[tokEmbOffset + e] + posEmb.Data[posEmbOffset + e];
                    }
                }
            }
            
            // Backward
            if (tokEmb.RequiresGrad || posEmb.RequiresGrad)
            {
                dest.SetBackward(() =>
                {
                    if (tokEmb.RequiresGrad)
                    {
                        for (int i = 0; i < dest.Size; i++)
                        {
                            tokEmb.Grad[i] += dest.Grad[i];
                        }
                    }
                    if (posEmb.RequiresGrad)
                    {
                        for (int b = 0; b < B; b++)
                        {
                            for (int t = 0; t < T; t++)
                            {
                                for (int e = 0; e < nEmbd; e++)
                                {
                                    posEmb.Grad[t * nEmbd + e] += dest.Grad[(b * T + t) * nEmbd + e];
                                }
                            }
                        }
                    }
                });
            }
            
            return dest;
        }

        public void Train()
        {
            _isTraining = true;
            _tokenEmbedding.Train();
            _positionEmbedding.Train();
            _embDropout.Train();
            _lnFinal.Train();
            _lmHead.Train();
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].Train();
            }
        }

        public void Eval()
        {
            _isTraining = false;
            _tokenEmbedding.Eval();
            _positionEmbedding.Eval();
            _embDropout.Eval();
            _lnFinal.Eval();
            _lmHead.Eval();
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].Eval();
            }
        }

        /// <summary>
        /// Calculate total number of parameters in the model.
        /// Uses long to support billion-parameter models.
        /// </summary>
        /// <returns>Total parameter count.</returns>
        public long GetTotalParameterCount()
        {
            long total = 0;
            foreach (var param in Parameters)
            {
                total += param.Size;
            }
            return total;
        }

        /// <summary>
        /// Get memory footprint estimate in bytes.
        /// </summary>
        /// <param name="includingGradients">Include gradient memory.</param>
        /// <returns>Estimated bytes.</returns>
        public long GetMemoryFootprintBytes(bool includingGradients = false)
        {
            long totalParams = GetTotalParameterCount();
            long bytes = totalParams * sizeof(float); // FP32 parameters
            
            if (includingGradients)
            {
                bytes += totalParams * sizeof(float); // FP32 gradients
            }
            
            return bytes;
        }
    }

    /// <summary>
    /// Single Transformer block with masked multi-head self-attention and feed-forward MLP.
    /// </summary>
    public class TransformerBlock
    {
        private readonly LayerNorm _ln1;
        private readonly MultiHeadAttention _attn;
        private readonly LayerNorm _ln2;
        private readonly MLP _mlp;
        
        private bool _isTraining = true;
        
        // Workspace for reusing intermediate tensors
        private readonly TensorWorkspace _workspace;

        public List<Tensor> Parameters { get; private set; }

        public TransformerBlock(int nEmbd, int nHead, int blockSize, float dropout, Random random)
        {
            _ln1 = new LayerNorm(nEmbd);
            _attn = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout, random);
            _ln2 = new LayerNorm(nEmbd);
            _mlp = new MLP(nEmbd, dropout, random);
            
            _workspace = new TensorWorkspace();

            Parameters = new List<Tensor>();
            Parameters.AddRange(_ln1.Parameters);
            Parameters.AddRange(_attn.Parameters);
            Parameters.AddRange(_ln2.Parameters);
            Parameters.AddRange(_mlp.Parameters);
        }

        public Tensor Forward(Tensor x)
        {
            // Pre-norm architecture with residual connections
            // x: (B, T, n_embd)
            
            // Use workspace tensors for LayerNorm outputs and residual connections
            var ln1Out = _workspace.GetOrCreate("ln1Out", x.Shape, _isTraining);
            _ln1.Forward(x, ln1Out);
            
            var attnOut = _attn.Forward(ln1Out);
            
            // Reuse workspace for residual connection
            var residual1 = _workspace.GetOrCreate("residual1", x.Shape, _isTraining);
            x = AddTensors(x, attnOut, residual1);
            
            // Second residual connection
            var ln2Out = _workspace.GetOrCreate("ln2Out", x.Shape, _isTraining);
            _ln2.Forward(x, ln2Out);
            
            var mlpOut = _mlp.Forward(ln2Out);
            
            var residual2 = _workspace.GetOrCreate("residual2", x.Shape, _isTraining);
            x = AddTensors(x, mlpOut, residual2);
            
            return x;
        }

        private Tensor AddTensors(Tensor a, Tensor b, Tensor? dest = null)
        {
            var result = dest ?? new Tensor(a.Shape, requiresGrad: _isTraining);
            
            // SIMD-accelerated forward pass
            int vectorSize = Vector<float>.Count;
            int i = 0;
            
            // SIMD loop
            for (; i <= a.Size - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Data.AsSpan(i));
                var vb = new Vector<float>(b.Data.AsSpan(i));
                (va + vb).CopyTo(result.Data.AsSpan(i));
            }
            
            // Scalar remainder
            for (; i < a.Size; i++)
            {
                result.Data[i] = a.Data[i] + b.Data[i];
            }
            
            if (a.RequiresGrad || b.RequiresGrad)
            {
                result.SetBackward(() =>
                {
                    if (a.RequiresGrad)
                    {
                        // SIMD-accelerated gradient accumulation
                        int j = 0;
                        for (; j <= a.Size - vectorSize; j += vectorSize)
                        {
                            var vGrad = new Vector<float>(result.Grad.AsSpan(j));
                            var vAGrad = new Vector<float>(a.Grad.AsSpan(j));
                            (vAGrad + vGrad).CopyTo(a.Grad.AsSpan(j));
                        }
                        for (; j < a.Size; j++)
                            a.Grad[j] += result.Grad[j];
                    }
                    if (b.RequiresGrad)
                    {
                        int j = 0;
                        for (; j <= b.Size - vectorSize; j += vectorSize)
                        {
                            var vGrad = new Vector<float>(result.Grad.AsSpan(j));
                            var vBGrad = new Vector<float>(b.Grad.AsSpan(j));
                            (vBGrad + vGrad).CopyTo(b.Grad.AsSpan(j));
                        }
                        for (; j < b.Size; j++)
                            b.Grad[j] += result.Grad[j];
                    }
                });
            }
            
            return result;
        }

        public void Train()
        {
            _isTraining = true;
            _ln1.Train();
            _attn.Train();
            _ln2.Train();
            _mlp.Train();
        }

        public void Eval()
        {
            _isTraining = false;
            _ln1.Eval();
            _attn.Eval();
            _ln2.Eval();
            _mlp.Eval();
        }
    }

    /// <summary>
    /// Masked multi-head self-attention implemented in pure C#.
    /// </summary>
    public class MultiHeadAttention
    {
        private readonly int _nEmbd;
        private readonly int _nHead;
        private readonly int _headSize;
        private readonly Linear _qkv;
        private readonly Linear _proj;
        private readonly Dropout _attnDropout;
        private readonly Dropout _projDropout;
        private readonly bool[,] _causalMask;
        private readonly int _blockSize;
        
        // Workspace tensors for reuse in forward pass (avoid allocations)
        // These persist across forward passes and are sized dynamically
        // They use regular Tensor (not PooledTensor) to avoid premature disposal
        private Tensor? _qWorkspace;
        private Tensor? _kWorkspace;
        private Tensor? _vWorkspace;
        private Tensor? _scoresWorkspace;
        private Tensor? _attnOutputWorkspace;
        private Tensor? _reshapedOutputWorkspace;  // Added for ReshapeAttentionOutput
        
        // KV-Cache support for efficient autoregressive generation
        private Tensor? _cachedKeys;    // Cached keys from previous tokens
        private Tensor? _cachedValues;  // Cached values from previous tokens
        private int _cachePosition;     // Current position in the cache
        private bool _useKVCache;       // Whether to use KV-cache (inference mode)
        
        private bool _isTraining = true;

        public List<Tensor> Parameters { get; private set; }

        public MultiHeadAttention(int nEmbd, int nHead, int blockSize, float dropout, Random random)
        {
            _nEmbd = nEmbd;
            _nHead = nHead;
            _headSize = nEmbd / nHead;
            _blockSize = blockSize;
            _useKVCache = false;
            _cachePosition = 0;

            if (_nEmbd % _nHead != 0)
            {
                throw new ArgumentException("Embedding dimension must be divisible by number of heads");
            }

            // Linear projection for Q, K, V combined
            _qkv = new Linear(_nEmbd, 3 * _nEmbd, random: random);
            _proj = new Linear(_nEmbd, _nEmbd, random: random);
            _attnDropout = new Dropout(dropout, random);
            _projDropout = new Dropout(dropout, random);

            // Create causal mask: lower triangular matrix
            _causalMask = new bool[blockSize, blockSize];
            for (int i = 0; i < blockSize; i++)
            {
                for (int j = 0; j < blockSize; j++)
                {
                    _causalMask[i, j] = i >= j;
                }
            }

            Parameters = new List<Tensor>();
            Parameters.AddRange(_qkv.Parameters);
            Parameters.AddRange(_proj.Parameters);
        }
        
        /// <summary>
        /// Enable KV-cache for efficient autoregressive generation.
        /// Call this before inference to reuse past key/value computations.
        /// </summary>
        public void EnableKVCache()
        {
            _useKVCache = true;
            _cachePosition = 0;
        }
        
        /// <summary>
        /// Disable KV-cache and reset cache state.
        /// </summary>
        public void DisableKVCache()
        {
            _useKVCache = false;
            _cachePosition = 0;
            _cachedKeys = null;
            _cachedValues = null;
        }
        
        /// <summary>
        /// Reset KV-cache position to start a new sequence.
        /// Keeps the cache allocated but resets the position counter.
        /// </summary>
        public void ResetKVCache()
        {
            _cachePosition = 0;
        }
        
        /// <summary>
        /// Get or allocate workspace tensor for the given shape.
        /// Reuses existing workspace if shape matches, otherwise allocates new one.
        /// Uses regular Tensor (not PooledTensor) because these workspaces persist across forward passes.
        /// </summary>
        private Tensor GetOrAllocateWorkspace(ref Tensor? workspace, int[] shape)
        {
            // Check if we can reuse existing workspace
            if (workspace != null && workspace.Shape.Length == shape.Length)
            {
                bool shapeMatches = true;
                for (int i = 0; i < shape.Length; i++)
                {
                    if (workspace.Shape[i] != shape[i])
                    {
                        shapeMatches = false;
                        break;
                    }
                }
                
                if (shapeMatches)
                {
                    // MatMul and other operations clear their own output buffers.
                    // Pre-clearing workspace tensors causes double-clear and 400%+ regression.
                    return workspace;
                }
            }
            
            // Allocate new workspace (use regular Tensor, not PooledTensor)
            // These persist across forward passes so we don't want them returned to pool
            workspace = new Tensor(shape, requiresGrad: _isTraining);
            return workspace;
        }

        public Tensor Forward(Tensor x)
        {
            // x shape: (B, T, n_embd)
            int B = x.Shape[0];
            int T = x.Shape[1];

            // Compute Q, K, V: (B, T, n_embd) -> (B, T, 3 * n_embd)
            var qkv = _qkv.Forward(x);

            // Use workspace tensors for Q, K, V to avoid allocations
            var qShape = new int[] { B, _nHead, T, _headSize };
            var q = GetOrAllocateWorkspace(ref _qWorkspace, qShape);
            var k = GetOrAllocateWorkspace(ref _kWorkspace, qShape);
            var v = GetOrAllocateWorkspace(ref _vWorkspace, qShape);
            
            // Split into Q, K, V and reshape to (B, nHead, T, headSize)
            ExtractAndReshapeQKVInPlace(qkv, q, 0, B, T);
            ExtractAndReshapeQKVInPlace(qkv, k, 1, B, T);
            ExtractAndReshapeQKVInPlace(qkv, v, 2, B, T);

            // KV-Cache: Use cached keys/values if available
            Tensor kFull, vFull;
            int fullSeqLen;
            
            if (_useKVCache && !_isTraining)
            {
                // Initialize cache on first use
                if (_cachedKeys == null)
                {
                    var cacheShape = new int[] { B, _nHead, _blockSize, _headSize };
                    _cachedKeys = new Tensor(cacheShape, requiresGrad: false);
                    _cachedValues = new Tensor(cacheShape, requiresGrad: false);
                }
                
                // Append new K, V to cache
                int cacheOffset = _cachePosition * _headSize;
                for (int b = 0; b < B; b++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        int bhOffset = (b * _nHead + h) * T * _headSize;
                        int cacheIndex = (b * _nHead + h) * _blockSize * _headSize + cacheOffset;
                        
                        // Copy new keys and values to cache
                        Array.Copy(k.Data, bhOffset, _cachedKeys.Data, cacheIndex, T * _headSize);
                        Array.Copy(v.Data, bhOffset, _cachedValues.Data, cacheIndex, T * _headSize);
                    }
                }
                
                // Use full cache (past + current)
                fullSeqLen = _cachePosition + T;
                kFull = _cachedKeys;
                vFull = _cachedValues;
                
                // Increment cache position for next forward pass
                _cachePosition += T;
            }
            else
            {
                // No caching: use current K, V
                kFull = k;
                vFull = v;
                fullSeqLen = T;
            }

            // Use workspace for attention scores
            var scoresShape = new int[] { B, _nHead, T, fullSeqLen };
            var att = GetOrAllocateWorkspace(ref _scoresWorkspace, scoresShape);
            ComputeAttentionScoresInPlace(q, kFull, att, B, T, fullSeqLen);

            // Use workspace for attention output
            var y = GetOrAllocateWorkspace(ref _attnOutputWorkspace, qShape);
            ApplyAttentionInPlace(att, vFull, y, B, T, fullSeqLen);

            // Reshape back: (B, nHead, T, headSize) -> (B, T, n_embd)
            // Use workspace to avoid allocation
            var reshapedShape = new int[] { B, T, _nEmbd };
            var yReshaped = GetOrAllocateWorkspace(ref _reshapedOutputWorkspace, reshapedShape);
            ReshapeAttentionOutputInPlace(y, yReshaped, B, T);

            // Final projection and dropout
            var output = _proj.Forward(yReshaped);
            output = _projDropout.Forward(output);

            return output;
        }

        private Tensor ExtractAndReshapeQKV(Tensor qkv, int index, int B, int T)
        {
            // Extract Q, K, or V from concatenated QKV
            // qkv: (B, T, 3 * n_embd)
            // Extract one third and reshape to (B, nHead, T, headSize)
            
            var extracted = new Tensor(new int[] { B, _nHead, T, _headSize }, requiresGrad: true);
            
            // Optimized version: process by head chunks to improve cache locality
            // Each head processes contiguous memory blocks
            int embdOffset = index * _nEmbd;  // Offset for Q (0), K (_nEmbd), or V (2*_nEmbd)
            
            // Parallelize over batch when B >= 4
            if (B >= 4)
            {
                Parallel.For(0, B, b =>
                {
                    int batchInOffset = b * T * 3 * _nEmbd;
                    int batchOutOffset = b * _nHead * T * _headSize;
                    
                    for (int h = 0; h < _nHead; h++)
                    {
                        int headInOffset = embdOffset + h * _headSize;
                        int headOutOffset = batchOutOffset + h * T * _headSize;
                        
                        for (int t = 0; t < T; t++)
                        {
                            int srcIdx = batchInOffset + t * 3 * _nEmbd + headInOffset;
                            int dstIdx = headOutOffset + t * _headSize;
                            
                            // Copy entire head dimension at once using Array.Copy (faster than element-by-element)
                            Array.Copy(qkv.Data, srcIdx, extracted.Data, dstIdx, _headSize);
                        }
                    }
                });
            }
            else
            {
                // Original sequential code for small batches
                for (int b = 0; b < B; b++)
                {
                    int batchInOffset = b * T * 3 * _nEmbd;
                    int batchOutOffset = b * _nHead * T * _headSize;
                    
                    for (int h = 0; h < _nHead; h++)
                    {
                        int headInOffset = embdOffset + h * _headSize;
                        int headOutOffset = batchOutOffset + h * T * _headSize;
                        
                        for (int t = 0; t < T; t++)
                        {
                            int srcIdx = batchInOffset + t * 3 * _nEmbd + headInOffset;
                            int dstIdx = headOutOffset + t * _headSize;
                            
                            // Copy entire head dimension at once using Array.Copy (faster than element-by-element)
                            Array.Copy(qkv.Data, srcIdx, extracted.Data, dstIdx, _headSize);
                        }
                    }
                }
            }
            
            return extracted;
        }
        
        /// <summary>
        /// In-place version of ExtractAndReshapeQKV that writes to a pre-allocated tensor.
        /// Avoids allocation overhead for repeated forward passes.
        /// </summary>
        private void ExtractAndReshapeQKVInPlace(Tensor qkv, Tensor dest, int index, int B, int T)
        {
            // Extract Q, K, or V from concatenated QKV into dest
            // qkv: (B, T, 3 * n_embd)
            // dest: (B, nHead, T, headSize) - pre-allocated
            
            int embdOffset = index * _nEmbd;  // Offset for Q (0), K (_nEmbd), or V (2*_nEmbd)
            
            // Parallelize over batch when B >= 4
            if (B >= 4)
            {
                Parallel.For(0, B, b =>
                {
                    int batchInOffset = b * T * 3 * _nEmbd;
                    int batchOutOffset = b * _nHead * T * _headSize;
                    
                    for (int h = 0; h < _nHead; h++)
                    {
                        int headInOffset = embdOffset + h * _headSize;
                        int headOutOffset = batchOutOffset + h * T * _headSize;
                        
                        for (int t = 0; t < T; t++)
                        {
                            int srcIdx = batchInOffset + t * 3 * _nEmbd + headInOffset;
                            int dstIdx = headOutOffset + t * _headSize;
                            
                            Array.Copy(qkv.Data, srcIdx, dest.Data, dstIdx, _headSize);
                        }
                    }
                });
            }
            else
            {
                // Sequential for small batches
                for (int b = 0; b < B; b++)
                {
                    int batchInOffset = b * T * 3 * _nEmbd;
                    int batchOutOffset = b * _nHead * T * _headSize;
                    
                    for (int h = 0; h < _nHead; h++)
                    {
                        int headInOffset = embdOffset + h * _headSize;
                        int headOutOffset = batchOutOffset + h * T * _headSize;
                        
                        for (int t = 0; t < T; t++)
                        {
                            int srcIdx = batchInOffset + t * 3 * _nEmbd + headInOffset;
                            int dstIdx = headOutOffset + t * _headSize;
                            
                            Array.Copy(qkv.Data, srcIdx, dest.Data, dstIdx, _headSize);
                        }
                    }
                }
            }
        }

        private Tensor ComputeAttentionScores(Tensor q, Tensor k, int B, int T)
        {
            // q, k: (B, nHead, T, headSize)
            // output: (B, nHead, T, T)
            
            var scores = new Tensor(new int[] { B, _nHead, T, T }, requiresGrad: true);
            float scale = 1.0f / MathF.Sqrt(_headSize);
            
            // Parallelize over batch and head dimensions for better performance
            // Use parallel processing when B * nHead >= 4
            int totalParallelWork = B * _nHead;
            if (totalParallelWork >= 4)
            {
                Parallel.For(0, totalParallelWork, bh =>
                {
                    int b = bh / _nHead;
                    int h = bh % _nHead;
                    int bhOffset = (b * _nHead + h) * T * _headSize;
                    int scoreOffset = (b * _nHead + h) * T * T;
                    
                    for (int i = 0; i < T; i++)
                    {
                        int qOffset = bhOffset + i * _headSize;
                        int scoreRowOffset = scoreOffset + i * T;
                        
                        // Only compute for valid positions (causal mask: j <= i)
                        for (int j = 0; j <= i; j++)
                        {
                            int kOffset = bhOffset + j * _headSize;
                            
                            // Use SIMD-accelerated dot product from MatMulOps
                            float sum = MatMulOps.DotProduct(
                                new ReadOnlySpan<float>(q.Data, qOffset, _headSize),
                                new ReadOnlySpan<float>(k.Data, kOffset, _headSize)
                            );
                            
                            scores.Data[scoreRowOffset + j] = sum * scale;
                        }
                        
                        // Positions j > i are already zero (tensor initialized to zeros)
                        // Set them to NegativeInfinity for softmax to ignore
                        for (int j = i + 1; j < T; j++)
                        {
                            scores.Data[scoreRowOffset + j] = float.NegativeInfinity;
                        }
                    }
                });
            }
            else
            {
                // Sequential for small batches
                for (int b = 0; b < B; b++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        int bhOffset = (b * _nHead + h) * T * _headSize;
                        int scoreOffset = (b * _nHead + h) * T * T;
                        
                        for (int i = 0; i < T; i++)
                        {
                            int qOffset = bhOffset + i * _headSize;
                            int scoreRowOffset = scoreOffset + i * T;
                            
                            // Only compute for valid positions (causal mask: j <= i)
                            for (int j = 0; j <= i; j++)
                            {
                                int kOffset = bhOffset + j * _headSize;
                                
                                // Use SIMD-accelerated dot product from MatMulOps
                                float sum = MatMulOps.DotProduct(
                                    new ReadOnlySpan<float>(q.Data, qOffset, _headSize),
                                    new ReadOnlySpan<float>(k.Data, kOffset, _headSize)
                                );
                                
                                scores.Data[scoreRowOffset + j] = sum * scale;
                            }
                            
                            // Positions j > i are already zero (tensor initialized to zeros)
                            // Set them to NegativeInfinity for softmax to ignore
                            for (int j = i + 1; j < T; j++)
                            {
                                scores.Data[scoreRowOffset + j] = float.NegativeInfinity;
                            }
                        }
                    }
                }
            }
            
            // Apply softmax over last dimension
            return ApplySoftmax(scores, B, T);
        }

        private Tensor ApplySoftmax(Tensor scores, int B, int T)
        {
            var result = new Tensor(scores.Shape, requiresGrad: true);
            
            // Parallelize softmax computation over batch and head dimensions
            int totalParallelWork = B * _nHead;
            if (totalParallelWork >= 4)
            {
                Parallel.For(0, totalParallelWork, bh =>
                {
                    int b = bh / _nHead;
                    int h = bh % _nHead;
                    
                    for (int i = 0; i < T; i++)
                    {
                        int offset = ((b * _nHead + h) * T + i) * T;
                        
                        // Find max for numerical stability (only over valid positions: j <= i for causal mask)
                        float max = float.NegativeInfinity;
                        for (int j = 0; j <= i; j++)
                        {
                            if (scores.Data[offset + j] > max)
                                max = scores.Data[offset + j];
                        }
                        
                        // Exp and sum - branchless for valid positions
                        float sum = 0;
                        for (int j = 0; j <= i; j++)
                        {
                            float exp = MathF.Exp(scores.Data[offset + j] - max);
                            result.Data[offset + j] = exp;
                            sum += exp;
                        }
                        
                        // Clear masked positions (i+1 to T-1) - already zero from tensor init
                        
                        // Normalize only valid positions
                        if (sum > 0)
                        {
                            float invSum = 1.0f / sum;
                            for (int j = 0; j <= i; j++)
                            {
                                result.Data[offset + j] *= invSum;
                            }
                        }
                    }
                });
            }
            else
            {
                // Sequential for small batches
                for (int b = 0; b < B; b++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        for (int i = 0; i < T; i++)
                        {
                            int offset = ((b * _nHead + h) * T + i) * T;
                            
                            // Find max for numerical stability (only over valid positions: j <= i for causal mask)
                            float max = float.NegativeInfinity;
                            for (int j = 0; j <= i; j++)
                            {
                                if (scores.Data[offset + j] > max)
                                    max = scores.Data[offset + j];
                            }
                            
                            // Exp and sum - branchless for valid positions
                            float sum = 0;
                            for (int j = 0; j <= i; j++)
                            {
                                float exp = MathF.Exp(scores.Data[offset + j] - max);
                                result.Data[offset + j] = exp;
                                sum += exp;
                            }
                            
                            // Clear masked positions (i+1 to T-1) - already zero from tensor init
                            
                            // Normalize only valid positions
                            if (sum > 0)
                            {
                                float invSum = 1.0f / sum;
                                for (int j = 0; j <= i; j++)
                                {
                                    result.Data[offset + j] *= invSum;
                                }
                            }
                        }
                    }
                }
            }
            
            return _attnDropout.Forward(result);
        }

        private Tensor ApplyAttention(Tensor att, Tensor v, int B, int T)
        {
            // att: (B, nHead, T, T)
            // v: (B, nHead, T, headSize)
            // output: (B, nHead, T, headSize)
            
            var output = new Tensor(new int[] { B, _nHead, T, _headSize }, requiresGrad: true);
            
            // Parallelize attention application over batch and head dimensions
            int totalParallelWork = B * _nHead;
            if (totalParallelWork >= 4)
            {
                Parallel.For(0, totalParallelWork, bh =>
                {
                    int b = bh / _nHead;
                    int h = bh % _nHead;
                    
                    for (int i = 0; i < T; i++)
                    {
                        for (int d = 0; d < _headSize; d++)
                        {
                            float sum = 0;
                            for (int j = 0; j < T; j++)
                            {
                                int attIdx = ((b * _nHead + h) * T + i) * T + j;
                                int vIdx = ((b * _nHead + h) * T + j) * _headSize + d;
                                sum += att.Data[attIdx] * v.Data[vIdx];
                            }
                            int outIdx = ((b * _nHead + h) * T + i) * _headSize + d;
                            output.Data[outIdx] = sum;
                        }
                    }
                });
            }
            else
            {
                // Sequential for small batches
                for (int b = 0; b < B; b++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        for (int i = 0; i < T; i++)
                        {
                            for (int d = 0; d < _headSize; d++)
                            {
                                float sum = 0;
                                for (int j = 0; j < T; j++)
                                {
                                    int attIdx = ((b * _nHead + h) * T + i) * T + j;
                                    int vIdx = ((b * _nHead + h) * T + j) * _headSize + d;
                                    sum += att.Data[attIdx] * v.Data[vIdx];
                                }
                                int outIdx = ((b * _nHead + h) * T + i) * _headSize + d;
                                output.Data[outIdx] = sum;
                            }
                        }
                    }
                }
            }
            
            return output;
        }

        private Tensor ReshapeAttentionOutput(Tensor y, int B, int T)
        {
            // y: (B, nHead, T, headSize) -> (B, T, n_embd)
            var output = new Tensor(new int[] { B, T, _nEmbd }, requiresGrad: true);
            
            // Optimized version: process by head chunks with Array.Copy
            for (int b = 0; b < B; b++)
            {
                int batchInOffset = b * _nHead * T * _headSize;
                int batchOutOffset = b * T * _nEmbd;
                
                for (int t = 0; t < T; t++)
                {
                    int timeOutOffset = batchOutOffset + t * _nEmbd;
                    
                    for (int h = 0; h < _nHead; h++)
                    {
                        int srcIdx = batchInOffset + h * T * _headSize + t * _headSize;
                        int dstIdx = timeOutOffset + h * _headSize;
                        
                        // Copy entire head dimension at once
                        Array.Copy(y.Data, srcIdx, output.Data, dstIdx, _headSize);
                    }
                }
            }
            
            return output;
        }
        
        /// <summary>
        /// In-place version of ReshapeAttentionOutput that writes to a pre-allocated tensor.
        /// Reshapes from (B, nHead, T, headSize) to (B, T, n_embd).
        /// </summary>
        private void ReshapeAttentionOutputInPlace(Tensor y, Tensor output, int B, int T)
        {
            // y: (B, nHead, T, headSize) -> output: (B, T, n_embd)
            
            // Optimized version: process by head chunks with Array.Copy
            for (int b = 0; b < B; b++)
            {
                int batchInOffset = b * _nHead * T * _headSize;
                int batchOutOffset = b * T * _nEmbd;
                
                for (int t = 0; t < T; t++)
                {
                    int timeOutOffset = batchOutOffset + t * _nEmbd;
                    
                    for (int h = 0; h < _nHead; h++)
                    {
                        int srcIdx = batchInOffset + h * T * _headSize + t * _headSize;
                        int dstIdx = timeOutOffset + h * _headSize;
                        
                        // Copy entire head dimension at once
                        Array.Copy(y.Data, srcIdx, output.Data, dstIdx, _headSize);
                    }
                }
            }
        }
        
        /// <summary>
        /// In-place version of ComputeAttentionScores that writes to a pre-allocated tensor.
        /// Computes Q * K^T / sqrt(d_k) with causal masking and softmax.
        /// Uses fused scale+mask+softmax operation for better performance.
        /// Supports KV-cache where K may have more positions than Q.
        /// </summary>
        private void ComputeAttentionScoresInPlace(Tensor q, Tensor k, Tensor scores, int B, int T, int kSeqLen)
        {
            // q: (B, nHead, T, headSize) - query for current tokens
            // k: (B, nHead, kSeqLen, headSize) - keys (may include cached past)
            // scores: (B, nHead, T, kSeqLen) - pre-allocated, will be modified in-place
            
            float scale = 1.0f / MathF.Sqrt(_headSize);
            
            int totalParallelWork = B * _nHead;
            if (totalParallelWork >= 4)
            {
                Parallel.For(0, totalParallelWork, bh =>
                {
                    int b = bh / _nHead;
                    int h = bh % _nHead;
                    int qOffset = (b * _nHead + h) * T * _headSize;
                    int kOffset = (b * _nHead + h) * kSeqLen * _headSize;
                    int scoreOffset = (b * _nHead + h) * T * kSeqLen;
                    
                    // Step 1: Batched matrix multiplication Q @ K^T
                    ReadOnlySpan<float> qMatrix = new ReadOnlySpan<float>(q.Data, qOffset, T * _headSize);
                    ReadOnlySpan<float> kMatrix = new ReadOnlySpan<float>(k.Data, kOffset, kSeqLen * _headSize);
                    Span<float> scoresMatrix = new Span<float>(scores.Data, scoreOffset, T * kSeqLen);
                    
                    // Compute Q @ K^T using optimized batched MatMul
                    MatMulOps.MatMulTransposeB(qMatrix, kMatrix, scoresMatrix, T, _headSize, kSeqLen);
                    
                    // Step 2: Apply fused scale+mask+softmax
                    // For KV-cache, the causal mask needs to account for cache offset
                    int cacheOffset = kSeqLen - T;
                    OptimizedOps.FusedScaleMaskSoftmax(scores.Data, scoreOffset, scale, scores.Data, scoreOffset, T, kSeqLen, cacheOffset);
                });
            }
            else
            {
                for (int b = 0; b < B; b++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        int qOffset = (b * _nHead + h) * T * _headSize;
                        int kOffset = (b * _nHead + h) * kSeqLen * _headSize;
                        int scoreOffset = (b * _nHead + h) * T * kSeqLen;
                        
                        // Step 1: Batched matrix multiplication Q @ K^T
                        ReadOnlySpan<float> qMatrix = new ReadOnlySpan<float>(q.Data, qOffset, T * _headSize);
                        ReadOnlySpan<float> kMatrix = new ReadOnlySpan<float>(k.Data, kOffset, kSeqLen * _headSize);
                        Span<float> scoresMatrix = new Span<float>(scores.Data, scoreOffset, T * kSeqLen);
                        
                        MatMulOps.MatMulTransposeB(qMatrix, kMatrix, scoresMatrix, T, _headSize, kSeqLen);
                        
                        // Step 2: Apply fused scale+mask+softmax for this (batch, head)
                        int cacheOffset = kSeqLen - T;
                        OptimizedOps.FusedScaleMaskSoftmax(scores.Data, scoreOffset, scale, scores.Data, scoreOffset, T, kSeqLen, cacheOffset);
                    }
                }
            }
            
            // Apply dropout if in training mode
            // Note: Since we're using in-place operations, we modify scores directly
            // The dropout is applied to the tensor passed in, which is the workspace
        }
        
        // Overload for backward compatibility (no KV-cache)
        private void ComputeAttentionScoresInPlace(Tensor q, Tensor k, Tensor scores, int B, int T)
        {
            ComputeAttentionScoresInPlace(q, k, scores, B, T, T);
        }
        
        /// <summary>
        /// Apply softmax in-place to the scores tensor.
        /// </summary>
        private void ApplySoftmaxInPlace(Tensor scores, int B, int T)
        {
            int totalParallelWork = B * _nHead;
            if (totalParallelWork >= 4)
            {
                Parallel.For(0, totalParallelWork, bh =>
                {
                    int b = bh / _nHead;
                    int h = bh % _nHead;
                    
                    for (int i = 0; i < T; i++)
                    {
                        int offset = ((b * _nHead + h) * T + i) * T;
                        
                        // Find max for numerical stability
                        float max = float.NegativeInfinity;
                        for (int j = 0; j <= i; j++)
                        {
                            if (scores.Data[offset + j] > max)
                                max = scores.Data[offset + j];
                        }
                        
                        // Exp and sum
                        float sum = 0;
                        for (int j = 0; j <= i; j++)
                        {
                            float exp = MathF.Exp(scores.Data[offset + j] - max);
                            scores.Data[offset + j] = exp;
                            sum += exp;
                        }
                        
                        // Normalize
                        if (sum > 0)
                        {
                            float invSum = 1.0f / sum;
                            for (int j = 0; j <= i; j++)
                            {
                                scores.Data[offset + j] *= invSum;
                            }
                        }
                        
                        // Clear masked positions
                        for (int j = i + 1; j < T; j++)
                        {
                            scores.Data[offset + j] = 0;
                        }
                    }
                });
            }
            else
            {
                for (int b = 0; b < B; b++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        for (int i = 0; i < T; i++)
                        {
                            int offset = ((b * _nHead + h) * T + i) * T;
                            
                            float max = float.NegativeInfinity;
                            for (int j = 0; j <= i; j++)
                            {
                                if (scores.Data[offset + j] > max)
                                    max = scores.Data[offset + j];
                            }
                            
                            float sum = 0;
                            for (int j = 0; j <= i; j++)
                            {
                                float exp = MathF.Exp(scores.Data[offset + j] - max);
                                scores.Data[offset + j] = exp;
                                sum += exp;
                            }
                            
                            if (sum > 0)
                            {
                                float invSum = 1.0f / sum;
                                for (int j = 0; j <= i; j++)
                                {
                                    scores.Data[offset + j] *= invSum;
                                }
                            }
                            
                            for (int j = i + 1; j < T; j++)
                            {
                                scores.Data[offset + j] = 0;
                            }
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// In-place version of ApplyAttention that writes to a pre-allocated tensor.
        /// Computes attention_weights * V using optimized MatMul kernel.
        /// Supports KV-cache where V may have more positions than output.
        /// Tier-0 optimization: Replaced triple nested loops with MatMul kernel for better performance.
        /// </summary>
        private void ApplyAttentionInPlace(Tensor att, Tensor v, Tensor output, int B, int T, int vSeqLen)
        {
            // att: (B, nHead, T, vSeqLen) - attention weights
            // v: (B, nHead, vSeqLen, headSize) - values (may include cached past)
            // output: (B, nHead, T, headSize) - pre-allocated
            
            // For each batch and head, perform: output[b,h] = att[b,h] @ v[b,h]
            // where att[b,h] is (T × vSeqLen) and v[b,h] is (vSeqLen × headSize)
            // resulting in output[b,h] as (T × headSize)
            
            int totalParallelWork = B * _nHead;
            if (totalParallelWork >= 4)
            {
                Parallel.For(0, totalParallelWork, bh =>
                {
                    int b = bh / _nHead;
                    int h = bh % _nHead;
                    
                    // Calculate offsets for this batch and head
                    int attOffset = (b * _nHead + h) * T * vSeqLen;
                    int vOffset = (b * _nHead + h) * vSeqLen * _headSize;
                    int outOffset = (b * _nHead + h) * T * _headSize;
                    
                    // Use MatMul: att[b,h] @ v[b,h] -> output[b,h]
                    // att: (T × vSeqLen), v: (vSeqLen × headSize), output: (T × headSize)
                    SmallMind.Core.Simd.MatMulOps.MatMul(
                        att.Data.AsSpan(attOffset, T * vSeqLen),
                        v.Data.AsSpan(vOffset, vSeqLen * _headSize),
                        output.Data.AsSpan(outOffset, T * _headSize),
                        T, vSeqLen, _headSize
                    );
                });
            }
            else
            {
                for (int b = 0; b < B; b++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        // Calculate offsets for this batch and head
                        int attOffset = (b * _nHead + h) * T * vSeqLen;
                        int vOffset = (b * _nHead + h) * vSeqLen * _headSize;
                        int outOffset = (b * _nHead + h) * T * _headSize;
                        
                        // Use MatMul: att[b,h] @ v[b,h] -> output[b,h]
                        // att: (T × vSeqLen), v: (vSeqLen × headSize), output: (T × headSize)
                        SmallMind.Core.Simd.MatMulOps.MatMul(
                            att.Data.AsSpan(attOffset, T * vSeqLen),
                            v.Data.AsSpan(vOffset, vSeqLen * _headSize),
                            output.Data.AsSpan(outOffset, T * _headSize),
                            T, vSeqLen, _headSize
                        );
                    }
                }
            }
        }
        
        // Overload for backward compatibility (no KV-cache)
        private void ApplyAttentionInPlace(Tensor att, Tensor v, Tensor output, int B, int T)
        {
            ApplyAttentionInPlace(att, v, output, B, T, T);
        }

        public void Train()
        {
            _isTraining = true;
            _qkv.Train();
            _proj.Train();
            _attnDropout.Train();
            _projDropout.Train();
        }

        public void Eval()
        {
            _isTraining = false;
            _qkv.Eval();
            _proj.Eval();
            _attnDropout.Eval();
            _projDropout.Eval();
        }
    }

    /// <summary>
    /// Feed-forward MLP with GELU activation.
    /// </summary>
    public class MLP
    {
        private readonly Linear _fc1;
        private readonly Linear _fc2;
        private readonly Dropout _dropout;
        private readonly int _nEmbd;
        
        private bool _isTraining = true;
        
        // Workspace for reusing intermediate tensors
        private readonly TensorWorkspace _workspace;

        public List<Tensor> Parameters { get; private set; }

        public MLP(int nEmbd, float dropout, Random random)
        {
            _nEmbd = nEmbd;
            
            // Standard Transformer uses 4x expansion
            _fc1 = new Linear(nEmbd, 4 * nEmbd, random: random);
            _fc2 = new Linear(4 * nEmbd, nEmbd, random: random);
            _dropout = new Dropout(dropout, random);
            
            _workspace = new TensorWorkspace();

            Parameters = new List<Tensor>();
            Parameters.AddRange(_fc1.Parameters);
            Parameters.AddRange(_fc2.Parameters);
        }

        public Tensor Forward(Tensor x)
        {
            // x: (B, T, n_embd)
            // Reuse workspace tensors for intermediate results
            int B = x.Shape[0];
            int T = x.Shape[1];
            
            // fc1 output: (B, T, 4*n_embd)
            var fc1Out = _workspace.GetOrCreate("fc1Out", new int[] { B, T, 4 * _nEmbd }, _isTraining);
            _fc1.Forward(x, fc1Out);
            
            // TODO: Optimize GELU to accept dest parameter for in-place computation
            // Current limitation: GELU allocates new tensor for gradient tracking
            // This accounts for ~10-15% of remaining allocations in forward pass
            // Potential optimization: Add Activations.GELU(input, dest) overload
            var geluOut = Activations.GELU(fc1Out);
            
            // fc2 output: (B, T, n_embd) - reuse input shape
            var fc2Out = _workspace.GetOrCreate("fc2Out", new int[] { B, T, _nEmbd }, _isTraining);
            _fc2.Forward(geluOut, fc2Out);
            
            var dropoutOut = _dropout.Forward(fc2Out);
            return dropoutOut;
        }

        public void Train()
        {
            _isTraining = true;
            _fc1.Train();
            _fc2.Train();
            _dropout.Train();
        }

        public void Eval()
        {
            _isTraining = false;
            _fc1.Eval();
            _fc2.Eval();
            _dropout.Eval();
        }
    }
}

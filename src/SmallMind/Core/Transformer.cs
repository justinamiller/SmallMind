using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using SmallMind.Core.Validation;

namespace SmallMind.Core
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

        // Embedding layers
        private readonly Embedding _tokenEmbedding;
        private readonly Embedding _positionEmbedding;
        private readonly Dropout _embDropout;

        // Transformer blocks
        private readonly List<TransformerBlock> _blocks;

        // Final layer norm and linear head
        private readonly LayerNorm _lnFinal;
        private readonly Linear _lmHead;

        public List<Tensor> Parameters { get; private set; }
        
        // Public properties for inference session creation
        public int BlockSize => _blockSize;
        public int NumLayers => _nLayer;
        public int NumHeads => _nHead;
        public int EmbeddingDim => _nEmbd;
        public int HeadDim => _nEmbd / _nHead;

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
                throw new Exceptions.ValidationException(
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

            // Token and position embeddings
            _tokenEmbedding = new Embedding(_vocabSize, _nEmbd, _random);
            _positionEmbedding = new Embedding(_blockSize, _nEmbd, _random);
            _embDropout = new Dropout((float)dropout, _random);

            // Stack of transformer blocks - pre-size to layer count
            _blocks = new List<TransformerBlock>(_nLayer);
            for (int i = 0; i < _nLayer; i++)
            {
                var block = new TransformerBlock(_nEmbd, _nHead, _blockSize, (float)dropout, _random);
                block.Attention.LayerIndex = i;  // Set layer index for KV cache
                _blocks.Add(block);
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

            Console.WriteLine($"TransformerModel initialized: vocab={_vocabSize}, block_size={_blockSize}, " +
                            $"n_embd={_nEmbd}, n_layer={_nLayer}, n_head={_nHead}, dropout={_dropout}");
            Console.WriteLine($"Total parameters: {Parameters.Count} tensors");
        }

        public Tensor Forward(Tensor idx)
        {
            Guard.NotNull(idx);
            
            // idx shape: (batch_size, sequence_length)
            int B = idx.Shape[0];
            int T = idx.Shape[1];

            if (T > _blockSize)
            {
                throw new ArgumentException($"Sequence length {T} exceeds block size {_blockSize}");
            }

            // Token embeddings: (B, T) -> (B, T, n_embd)
            var tokEmb = _tokenEmbedding.Forward(idx);

            // Position embeddings: create position indices (T,)
            var posIndices = new Tensor(new float[T], new int[] { T });
            for (int i = 0; i < T; i++)
            {
                posIndices.Data[i] = i;
            }
            var posEmb = _positionEmbedding.Forward(posIndices);

            // Add token and position embeddings: (B, T, n_embd)
            // Broadcast posEmb across batch dimension
            var x = AddPositionEmbeddings(tokEmb, posEmb, B, T, _nEmbd);
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

        /// <summary>
        /// Forward pass with KV caching for efficient inference.
        /// </summary>
        /// <param name="idx">Input token indices (batch_size, sequence_length)</param>
        /// <param name="session">Inference session with KV cache</param>
        /// <param name="isPrefill">True for prefill phase, false for decode phase</param>
        public Tensor Forward(Tensor idx, InferenceSession? session, bool isPrefill)
        {
            Guard.NotNull(idx);
            
            // If no session provided, use standard forward pass
            if (session == null)
            {
                return Forward(idx);
            }
            
            // idx shape: (batch_size, sequence_length)
            int B = idx.Shape[0];
            int T = idx.Shape[1];

            if (T > _blockSize)
            {
                throw new ArgumentException($"Sequence length {T} exceeds block size {_blockSize}");
            }

            // Token embeddings: (B, T) -> (B, T, n_embd)
            var tokEmb = _tokenEmbedding.Forward(idx);

            // Position embeddings: create position indices (T,)
            // For decode, start from current position
            int startPos = session.CurrentPosition;
            var posIndices = new Tensor(new float[T], new int[] { T });
            for (int i = 0; i < T; i++)
            {
                posIndices.Data[i] = startPos + i;
            }
            var posEmb = _positionEmbedding.Forward(posIndices);

            // Add token and position embeddings: (B, T, n_embd)
            // Broadcast posEmb across batch dimension
            var x = AddPositionEmbeddings(tokEmb, posEmb, B, T, _nEmbd);
            x = _embDropout.Forward(x);

            // Pass through transformer blocks with KV cache
            for (int i = 0; i < _blocks.Count; i++)
            {
                x = _blocks[i].Forward(x, session, isPrefill);
            }

            // Final layer norm: (B, T, n_embd)
            x = _lnFinal.Forward(x);

            // Language model head: (B, T, n_embd) -> (B, T, vocab_size)
            var logits = _lmHead.Forward(x);

            // Advance session position
            session.AdvancePosition(T);

            return logits;
        }

        private Tensor AddPositionEmbeddings(Tensor tokEmb, Tensor posEmb, int B, int T, int nEmbd)
        {
            var result = new Tensor(new int[] { B, T, nEmbd }, requiresGrad: true);
            
            // posEmb is (T, nEmbd), need to broadcast to (B, T, nEmbd)
            // Use SIMD vectorization for faster element-wise addition
            int vectorSize = Vector<float>.Count;
            
            for (int b = 0; b < B; b++)
            {
                for (int t = 0; t < T; t++)
                {
                    int resultOffset = (b * T + t) * nEmbd;
                    int tokEmbOffset = (b * T + t) * nEmbd;
                    int posEmbOffset = t * nEmbd;
                    
                    // SIMD vectorized addition for better performance
                    int e = 0;
                    for (; e <= nEmbd - vectorSize; e += vectorSize)
                    {
                        var vTok = new Vector<float>(tokEmb.Data, tokEmbOffset + e);
                        var vPos = new Vector<float>(posEmb.Data, posEmbOffset + e);
                        var vResult = vTok + vPos;
                        vResult.CopyTo(result.Data, resultOffset + e);
                    }
                    
                    // Handle remaining elements
                    for (; e < nEmbd; e++)
                    {
                        result.Data[resultOffset + e] = 
                            tokEmb.Data[tokEmbOffset + e] + posEmb.Data[posEmbOffset + e];
                    }
                }
            }
            
            // Backward
            if (tokEmb.RequiresGrad || posEmb.RequiresGrad)
            {
                result.SetBackward(() =>
                {
                    int vectorSize = Vector<float>.Count;
                    
                    if (tokEmb.RequiresGrad)
                    {
                        // SIMD optimized gradient accumulation for token embeddings
                        int i = 0;
                        for (; i <= result.Size - vectorSize; i += vectorSize)
                        {
                            var vGrad = new Vector<float>(result.Grad, i);
                            var vTokGrad = new Vector<float>(tokEmb.Grad, i);
                            var vSum = vTokGrad + vGrad;
                            vSum.CopyTo(tokEmb.Grad, i);
                        }
                        
                        // Handle remaining elements
                        for (; i < result.Size; i++)
                        {
                            tokEmb.Grad[i] += result.Grad[i];
                        }
                    }
                    if (posEmb.RequiresGrad)
                    {
                        // SIMD optimized gradient accumulation for position embeddings
                        for (int b = 0; b < B; b++)
                        {
                            for (int t = 0; t < T; t++)
                            {
                                int resultOffset = (b * T + t) * nEmbd;
                                int posEmbOffset = t * nEmbd;
                                
                                int e = 0;
                                for (; e <= nEmbd - vectorSize; e += vectorSize)
                                {
                                    var vGrad = new Vector<float>(result.Grad, resultOffset + e);
                                    var vPosGrad = new Vector<float>(posEmb.Grad, posEmbOffset + e);
                                    var vSum = vPosGrad + vGrad;
                                    vSum.CopyTo(posEmb.Grad, posEmbOffset + e);
                                }
                                
                                // Handle remaining elements
                                for (; e < nEmbd; e++)
                                {
                                    posEmb.Grad[posEmbOffset + e] += result.Grad[resultOffset + e];
                                }
                            }
                        }
                    }
                });
            }
            
            return result;
        }

        public void Train()
        {
            _embDropout.Train();
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].Train();
            }
        }

        public void Eval()
        {
            _embDropout.Eval();
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].Eval();
            }
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

        public List<Tensor> Parameters { get; private set; }
        
        // Expose attention layer for layer index setting
        internal MultiHeadAttention Attention => _attn;

        public TransformerBlock(int nEmbd, int nHead, int blockSize, float dropout, Random random)
        {
            _ln1 = new LayerNorm(nEmbd);
            _attn = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout, random);
            _ln2 = new LayerNorm(nEmbd);
            _mlp = new MLP(nEmbd, dropout, random);

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
            var attnOut = _attn.Forward(_ln1.Forward(x));
            x = AddTensors(x, attnOut);
            
            var mlpOut = _mlp.Forward(_ln2.Forward(x));
            x = AddTensors(x, mlpOut);
            
            return x;
        }

        /// <summary>
        /// Forward pass with optional KV caching.
        /// </summary>
        public Tensor Forward(Tensor x, InferenceSession? session, bool isPrefill)
        {
            // Pre-norm architecture with residual connections
            // x: (B, T, n_embd)
            var attnOut = _attn.Forward(_ln1.Forward(x), session, isPrefill);
            x = AddTensors(x, attnOut);
            
            var mlpOut = _mlp.Forward(_ln2.Forward(x));
            x = AddTensors(x, mlpOut);
            
            return x;
        }

        private Tensor AddTensors(Tensor a, Tensor b)
        {
            var result = new Tensor(a.Shape, requiresGrad: true);
            
            // SIMD vectorized addition for better performance
            int vectorSize = Vector<float>.Count;
            int i = 0;
            
            for (; i <= a.Size - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Data, i);
                var vb = new Vector<float>(b.Data, i);
                var vResult = va + vb;
                vResult.CopyTo(result.Data, i);
            }
            
            // Handle remaining elements
            for (; i < a.Size; i++)
            {
                result.Data[i] = a.Data[i] + b.Data[i];
            }
            
            if (a.RequiresGrad || b.RequiresGrad)
            {
                result.SetBackward(() =>
                {
                    int vectorSize = Vector<float>.Count;
                    
                    if (a.RequiresGrad)
                    {
                        // SIMD optimized gradient accumulation
                        int i = 0;
                        for (; i <= a.Size - vectorSize; i += vectorSize)
                        {
                            var vGrad = new Vector<float>(result.Grad, i);
                            var vAGrad = new Vector<float>(a.Grad, i);
                            var vSum = vAGrad + vGrad;
                            vSum.CopyTo(a.Grad, i);
                        }
                        
                        // Handle remaining elements
                        for (; i < a.Size; i++)
                            a.Grad[i] += result.Grad[i];
                    }
                    if (b.RequiresGrad)
                    {
                        // SIMD optimized gradient accumulation
                        int i = 0;
                        for (; i <= b.Size - vectorSize; i += vectorSize)
                        {
                            var vGrad = new Vector<float>(result.Grad, i);
                            var vBGrad = new Vector<float>(b.Grad, i);
                            var vSum = vBGrad + vGrad;
                            vSum.CopyTo(b.Grad, i);
                        }
                        
                        // Handle remaining elements
                        for (; i < b.Size; i++)
                            b.Grad[i] += result.Grad[i];
                    }
                });
            }
            
            return result;
        }

        public void Train()
        {
            _attn.Train();
            _mlp.Train();
        }

        public void Eval()
        {
            _attn.Eval();
            _mlp.Eval();
        }
    }

    /// <summary>
    /// Masked multi-head self-attention implemented in pure C#.
    /// Supports optional KV caching for efficient inference.
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
        
        // Layer index for KV cache access (set by TransformerBlock)
        internal int LayerIndex { get; set; }

        public List<Tensor> Parameters { get; private set; }

        public MultiHeadAttention(int nEmbd, int nHead, int blockSize, float dropout, Random random)
        {
            _nEmbd = nEmbd;
            _nHead = nHead;
            _headSize = nEmbd / nHead;
            _blockSize = blockSize;

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

        public Tensor Forward(Tensor x)
        {
            // x shape: (B, T, n_embd)
            int B = x.Shape[0];
            int T = x.Shape[1];

            // Compute Q, K, V: (B, T, n_embd) -> (B, T, 3 * n_embd)
            var qkv = _qkv.Forward(x);

            // Split into Q, K, V and reshape to (B, nHead, T, headSize)
            var q = ExtractAndReshapeQKV(qkv, 0, B, T);
            var k = ExtractAndReshapeQKV(qkv, 1, B, T);
            var v = ExtractAndReshapeQKV(qkv, 2, B, T);

            // Compute attention scores
            var att = ComputeAttentionScores(q, k, B, T);

            // Apply attention to values
            var y = ApplyAttention(att, v, B, T);

            // Reshape back: (B, nHead, T, headSize) -> (B, T, n_embd)
            var yReshaped = ReshapeAttentionOutput(y, B, T);

            // Final projection and dropout
            var output = _proj.Forward(yReshaped);
            output = _projDropout.Forward(output);

            return output;
        }

        /// <summary>
        /// Forward pass with optional KV caching for efficient inference.
        /// </summary>
        /// <param name="x">Input tensor (B, T, n_embd) where T is the number of NEW tokens</param>
        /// <param name="session">Optional inference session with KV cache</param>
        /// <param name="isPrefill">True if processing full prompt, false if generating single token</param>
        public Tensor Forward(Tensor x, InferenceSession? session, bool isPrefill)
        {
            // If no session, fall back to standard forward
            if (session == null)
            {
                return Forward(x);
            }

            // x shape: (B, T, n_embd)
            // For prefill: T = prompt_length
            // For decode: T = 1 (single new token)
            int B = x.Shape[0];
            int T = x.Shape[1];

            // Compute Q, K, V for new tokens: (B, T, n_embd) -> (B, T, 3 * n_embd)
            var qkv = _qkv.Forward(x);

            // Split into Q, K, V and reshape to (B, nHead, T, headSize)
            var q = ExtractAndReshapeQKV(qkv, 0, B, T);
            var kNew = ExtractAndReshapeQKV(qkv, 1, B, T);
            var vNew = ExtractAndReshapeQKV(qkv, 2, B, T);

            // Get cache buffers for this layer
            float[] keyCache = session.GetKeyCache(LayerIndex);
            float[] valueCache = session.GetValueCache(LayerIndex);

            int startPos = session.CurrentPosition;
            int endPos = startPos + T;
            
            // Store new K, V into cache
            // Cache layout: [position * numHeads * headDim]
            StoreInCache(kNew, keyCache, startPos, B, T);
            StoreInCache(vNew, valueCache, startPos, B, T);

            // For attention, we need full K, V up to current position
            // Create views of K, V that include cached + new
            int totalSeqLen = endPos;
            var kFull = CreateFullKVFromCache(keyCache, 0, totalSeqLen, B);
            var vFull = CreateFullKVFromCache(valueCache, 0, totalSeqLen, B);

            // Compute attention scores
            // q: (B, nHead, T, headSize) - only for new tokens
            // kFull: (B, nHead, totalSeqLen, headSize) - all tokens so far
            var att = ComputeAttentionScoresWithCache(q, kFull, B, T, totalSeqLen);

            // Apply attention to values
            var y = ApplyAttentionWithCache(att, vFull, B, T, totalSeqLen);

            // Reshape back: (B, nHead, T, headSize) -> (B, T, n_embd)
            var yReshaped = ReshapeAttentionOutput(y, B, T);

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
            
            for (int b = 0; b < B; b++)
            {
                for (int t = 0; t < T; t++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        for (int d = 0; d < _headSize; d++)
                        {
                            int qkvIdx = (b * T + t) * (3 * _nEmbd) + index * _nEmbd + h * _headSize + d;
                            int outIdx = ((b * _nHead + h) * T + t) * _headSize + d;
                            extracted.Data[outIdx] = qkv.Data[qkvIdx];
                        }
                    }
                }
            }
            
            return extracted;
        }

        private Tensor ComputeAttentionScores(Tensor q, Tensor k, int B, int T)
        {
            // q, k: (B, nHead, T, headSize)
            // output: (B, nHead, T, T)
            
            var scores = new Tensor(new int[] { B, _nHead, T, T }, requiresGrad: true);
            float scale = 1.0f / MathF.Sqrt(_headSize);
            int vectorSize = Vector<float>.Count;
            
            // Parallelize over batch and head dimensions for better performance
            // Use parallel processing when B * nHead >= 4
            int totalParallelWork = B * _nHead;
            if (totalParallelWork >= 4)
            {
                Parallel.For(0, totalParallelWork, bh =>
                {
                    int b = bh / _nHead;
                    int h = bh % _nHead;
                    
                    for (int i = 0; i < T; i++)
                    {
                        for (int j = 0; j < T; j++)
                        {
                            // SIMD optimized dot product
                            float sum = 0;
                            int qBase = ((b * _nHead + h) * T + i) * _headSize;
                            int kBase = ((b * _nHead + h) * T + j) * _headSize;
                            
                            int d = 0;
                            // Process SIMD-width chunks
                            Vector<float> sumVec = Vector<float>.Zero;
                            for (; d <= _headSize - vectorSize; d += vectorSize)
                            {
                                var vq = new Vector<float>(q.Data, qBase + d);
                                var vk = new Vector<float>(k.Data, kBase + d);
                                sumVec += vq * vk;
                            }
                            
                            // Horizontal sum of vector
                            for (int v = 0; v < vectorSize; v++)
                            {
                                sum += sumVec[v];
                            }
                            
                            // Handle remaining elements
                            for (; d < _headSize; d++)
                            {
                                sum += q.Data[qBase + d] * k.Data[kBase + d];
                            }
                            
                            int scoreIdx = ((b * _nHead + h) * T + i) * T + j;
                            if (_causalMask[i, j])
                            {
                                scores.Data[scoreIdx] = sum * scale;
                            }
                            else
                            {
                                scores.Data[scoreIdx] = float.NegativeInfinity;
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
                            for (int j = 0; j < T; j++)
                            {
                                // SIMD optimized dot product
                                float sum = 0;
                                int qBase = ((b * _nHead + h) * T + i) * _headSize;
                                int kBase = ((b * _nHead + h) * T + j) * _headSize;
                                
                                int d = 0;
                                // Process SIMD-width chunks
                                Vector<float> sumVec = Vector<float>.Zero;
                                for (; d <= _headSize - vectorSize; d += vectorSize)
                                {
                                    var vq = new Vector<float>(q.Data, qBase + d);
                                    var vk = new Vector<float>(k.Data, kBase + d);
                                    sumVec += vq * vk;
                                }
                                
                                // Horizontal sum of vector
                                for (int v = 0; v < vectorSize; v++)
                                {
                                    sum += sumVec[v];
                                }
                                
                                // Handle remaining elements
                                for (; d < _headSize; d++)
                                {
                                    sum += q.Data[qBase + d] * k.Data[kBase + d];
                                }
                                
                                int scoreIdx = ((b * _nHead + h) * T + i) * T + j;
                                if (_causalMask[i, j])
                                {
                                    scores.Data[scoreIdx] = sum * scale;
                                }
                                else
                                {
                                    scores.Data[scoreIdx] = float.NegativeInfinity;
                                }
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
                        
                        // Find max for numerical stability
                        float max = float.NegativeInfinity;
                        for (int j = 0; j < T; j++)
                        {
                            max = Math.Max(max, scores.Data[offset + j]);
                        }
                        
                        // Exp and sum
                        float sum = 0;
                        for (int j = 0; j < T; j++)
                        {
                            if (scores.Data[offset + j] != float.NegativeInfinity)
                            {
                                result.Data[offset + j] = MathF.Exp(scores.Data[offset + j] - max);
                                sum += result.Data[offset + j];
                            }
                        }
                        
                        // Normalize
                        if (sum > 0)
                        {
                            for (int j = 0; j < T; j++)
                            {
                                result.Data[offset + j] /= sum;
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
                            
                            // Find max for numerical stability
                            float max = float.NegativeInfinity;
                            for (int j = 0; j < T; j++)
                            {
                                max = Math.Max(max, scores.Data[offset + j]);
                            }
                            
                            // Exp and sum
                            float sum = 0;
                            for (int j = 0; j < T; j++)
                            {
                                if (scores.Data[offset + j] != float.NegativeInfinity)
                                {
                                    result.Data[offset + j] = MathF.Exp(scores.Data[offset + j] - max);
                                    sum += result.Data[offset + j];
                                }
                            }
                            
                            // Normalize
                            if (sum > 0)
                            {
                                for (int j = 0; j < T; j++)
                                {
                                    result.Data[offset + j] /= sum;
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
            
            for (int b = 0; b < B; b++)
            {
                for (int t = 0; t < T; t++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        for (int d = 0; d < _headSize; d++)
                        {
                            int yIdx = ((b * _nHead + h) * T + t) * _headSize + d;
                            int outIdx = (b * T + t) * _nEmbd + h * _headSize + d;
                            output.Data[outIdx] = y.Data[yIdx];
                        }
                    }
                }
            }
            
            return output;
        }

        /// <summary>
        /// Store new K or V tensors into the cache at the specified position.
        /// </summary>
        private void StoreInCache(Tensor kv, float[] cache, int startPos, int B, int T)
        {
            // kv: (B, nHead, T, headSize) - only works for B=1 currently
            // cache layout: [position * numHeads * headDim]
            
            for (int t = 0; t < T; t++)
            {
                int cachePos = startPos + t;
                for (int h = 0; h < _nHead; h++)
                {
                    for (int d = 0; d < _headSize; d++)
                    {
                        // Source: kv tensor at (b=0, h, t, d)
                        int kvIdx = (h * T + t) * _headSize + d;
                        
                        // Destination: cache at position cachePos
                        int cacheIdx = cachePos * _nHead * _headSize + h * _headSize + d;
                        
                        cache[cacheIdx] = kv.Data[kvIdx];
                    }
                }
            }
        }

        /// <summary>
        /// Create a tensor view of the full K or V from cache up to endPos.
        /// </summary>
        private Tensor CreateFullKVFromCache(float[] cache, int startPos, int endPos, int B)
        {
            // Returns: (B, nHead, seqLen, headSize) where seqLen = endPos - startPos
            int seqLen = endPos - startPos;
            var result = new Tensor(new int[] { B, _nHead, seqLen, _headSize }, requiresGrad: false);
            
            // Copy from cache to tensor (B=1 for now)
            for (int t = 0; t < seqLen; t++)
            {
                int cachePos = startPos + t;
                for (int h = 0; h < _nHead; h++)
                {
                    for (int d = 0; d < _headSize; d++)
                    {
                        int cacheIdx = cachePos * _nHead * _headSize + h * _headSize + d;
                        int resultIdx = (h * seqLen + t) * _headSize + d;
                        result.Data[resultIdx] = cache[cacheIdx];
                    }
                }
            }
            
            return result;
        }

        /// <summary>
        /// Compute attention scores with KV cache support.
        /// q: (B, nHead, qLen, headSize) - queries for new tokens
        /// k: (B, nHead, kvLen, headSize) - keys for all tokens (cached + new)
        /// </summary>
        private Tensor ComputeAttentionScoresWithCache(Tensor q, Tensor k, int B, int qLen, int kvLen)
        {
            // output: (B, nHead, qLen, kvLen)
            var scores = new Tensor(new int[] { B, _nHead, qLen, kvLen }, requiresGrad: true);
            float scale = 1.0f / MathF.Sqrt(_headSize);
            int vectorSize = Vector<float>.Count;
            
            // NOTE: We need to know the starting position for proper causal masking
            // This method is called from Forward(Tensor, InferenceSession, bool) where we know startPos
            // For now, we'll compute it from kvLen and qLen
            // During prefill: startPos = 0, qLen = T, kvLen = T, so startPos = kvLen - qLen = 0
            // During decode: startPos = kvLen - qLen (e.g., if kvLen=6 and qLen=1, startPos=5)
            int startPos = kvLen - qLen;
            
            // For each query position, compute scores against all key positions
            for (int b = 0; b < B; b++)
            {
                for (int h = 0; h < _nHead; h++)
                {
                    for (int i = 0; i < qLen; i++)
                    {
                        // Absolute position of this query
                        int queryPos = startPos + i;
                        
                        for (int j = 0; j < kvLen; j++)
                        {
                            // SIMD optimized dot product
                            float sum = 0;
                            int qBase = ((b * _nHead + h) * qLen + i) * _headSize;
                            int kBase = ((b * _nHead + h) * kvLen + j) * _headSize;
                            
                            int d = 0;
                            // Process SIMD-width chunks
                            Vector<float> sumVec = Vector<float>.Zero;
                            for (; d <= _headSize - vectorSize; d += vectorSize)
                            {
                                var vq = new Vector<float>(q.Data, qBase + d);
                                var vk = new Vector<float>(k.Data, kBase + d);
                                sumVec += vq * vk;
                            }
                            
                            // Horizontal sum of vector
                            for (int v = 0; v < vectorSize; v++)
                            {
                                sum += sumVec[v];
                            }
                            
                            // Handle remaining elements
                            for (; d < _headSize; d++)
                            {
                                sum += q.Data[qBase + d] * k.Data[kBase + d];
                            }
                            
                            int scoreIdx = ((b * _nHead + h) * qLen + i) * kvLen + j;
                            
                            // Apply causal masking: query at position queryPos can only attend to keys at positions <= queryPos
                            // Key at k-index j has absolute position j (since kFull starts from position 0)
                            if (j <= queryPos)
                            {
                                scores.Data[scoreIdx] = sum * scale;
                            }
                            else
                            {
                                scores.Data[scoreIdx] = float.NegativeInfinity;
                            }
                        }
                    }
                }
            }
            
            // Apply softmax over last dimension
            return ApplySoftmaxKVCache(scores, B, qLen, kvLen);
        }

        /// <summary>
        /// Apply softmax for KV cache scenario with different query and key lengths.
        /// </summary>
        private Tensor ApplySoftmaxKVCache(Tensor scores, int B, int qLen, int kvLen)
        {
            var result = new Tensor(scores.Shape, requiresGrad: true);
            
            for (int b = 0; b < B; b++)
            {
                for (int h = 0; h < _nHead; h++)
                {
                    for (int i = 0; i < qLen; i++)
                    {
                        int offset = ((b * _nHead + h) * qLen + i) * kvLen;
                        
                        // Find max for numerical stability
                        float max = float.NegativeInfinity;
                        for (int j = 0; j < kvLen; j++)
                        {
                            max = Math.Max(max, scores.Data[offset + j]);
                        }
                        
                        // Exp and sum
                        float sum = 0;
                        for (int j = 0; j < kvLen; j++)
                        {
                            result.Data[offset + j] = MathF.Exp(scores.Data[offset + j] - max);
                            sum += result.Data[offset + j];
                        }
                        
                        // Normalize
                        if (sum > 0)
                        {
                            for (int j = 0; j < kvLen; j++)
                            {
                                result.Data[offset + j] /= sum;
                            }
                        }
                    }
                }
            }
            
            return _attnDropout.Forward(result);
        }

        /// <summary>
        /// Apply attention with KV cache support.
        /// att: (B, nHead, qLen, kvLen)
        /// v: (B, nHead, kvLen, headSize)
        /// output: (B, nHead, qLen, headSize)
        /// </summary>
        private Tensor ApplyAttentionWithCache(Tensor att, Tensor v, int B, int qLen, int kvLen)
        {
            var output = new Tensor(new int[] { B, _nHead, qLen, _headSize }, requiresGrad: true);
            
            for (int b = 0; b < B; b++)
            {
                for (int h = 0; h < _nHead; h++)
                {
                    for (int i = 0; i < qLen; i++)
                    {
                        for (int d = 0; d < _headSize; d++)
                        {
                            float sum = 0;
                            for (int j = 0; j < kvLen; j++)
                            {
                                int attIdx = ((b * _nHead + h) * qLen + i) * kvLen + j;
                                int vIdx = ((b * _nHead + h) * kvLen + j) * _headSize + d;
                                sum += att.Data[attIdx] * v.Data[vIdx];
                            }
                            int outIdx = ((b * _nHead + h) * qLen + i) * _headSize + d;
                            output.Data[outIdx] = sum;
                        }
                    }
                }
            }
            
            return output;
        }

        public void Train()
        {
            _attnDropout.Train();
            _projDropout.Train();
        }

        public void Eval()
        {
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

        public List<Tensor> Parameters { get; private set; }

        public MLP(int nEmbd, float dropout, Random random)
        {
            // Standard Transformer uses 4x expansion
            _fc1 = new Linear(nEmbd, 4 * nEmbd, random: random);
            _fc2 = new Linear(4 * nEmbd, nEmbd, random: random);
            _dropout = new Dropout(dropout, random);

            Parameters = new List<Tensor>();
            Parameters.AddRange(_fc1.Parameters);
            Parameters.AddRange(_fc2.Parameters);
        }

        public Tensor Forward(Tensor x)
        {
            // x: (B, T, n_embd)
            x = _fc1.Forward(x);
            x = Activations.GELU(x);
            x = _fc2.Forward(x);
            x = _dropout.Forward(x);
            return x;
        }

        public void Train()
        {
            _dropout.Train();
        }

        public void Eval()
        {
            _dropout.Eval();
        }
    }
}

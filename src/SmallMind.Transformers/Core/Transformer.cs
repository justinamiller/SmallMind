using SmallMind.Core.Exceptions;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;
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

        private Tensor AddPositionEmbeddings(Tensor tokEmb, Tensor posEmb, int B, int T, int nEmbd)
        {
            var result = new Tensor(new int[] { B, T, nEmbd }, requiresGrad: true);
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
                        (vTok + vPos).CopyTo(result.Data.AsSpan(resultOffset + e));
                    }
                    
                    // Scalar remainder
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
                    if (tokEmb.RequiresGrad)
                    {
                        for (int i = 0; i < result.Size; i++)
                        {
                            tokEmb.Grad[i] += result.Grad[i];
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
                                    posEmb.Grad[t * nEmbd + e] += result.Grad[(b * T + t) * nEmbd + e];
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
            
            // LayerNorm outputs are allocated but operations are fused
            // TODO: Pool LayerNorm outputs once all downstream ops handle pooled tensors correctly
            var attnOut = _attn.Forward(_ln1.Forward(x));
            x = AddTensors(x, attnOut);
            
            var mlpOut = _mlp.Forward(_ln2.Forward(x));
            x = AddTensors(x, mlpOut);
            
            return x;
        }

        private Tensor AddTensors(Tensor a, Tensor b, Tensor? dest = null)
        {
            var result = dest ?? new Tensor(a.Shape, requiresGrad: true);
            
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

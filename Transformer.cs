using System;
using System.Collections.Generic;

namespace TinyLLM
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

        public TransformerModel(int vocabSize, int blockSize, int nEmbd, int nLayer, int nHead, double dropout, int seed = 42)
        {
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

            // Stack of transformer blocks
            _blocks = new List<TransformerBlock>();
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
            foreach (var block in _blocks)
            {
                Parameters.AddRange(block.Parameters);
            }
            Parameters.AddRange(_lnFinal.Parameters);
            Parameters.AddRange(_lmHead.Parameters);

            Console.WriteLine($"TransformerModel initialized: vocab={_vocabSize}, block_size={_blockSize}, " +
                            $"n_embd={_nEmbd}, n_layer={_nLayer}, n_head={_nHead}, dropout={_dropout}");
            Console.WriteLine($"Total parameters: {Parameters.Count} tensors");
        }

        public Tensor Forward(Tensor idx)
        {
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
            foreach (var block in _blocks)
            {
                x = block.Forward(x);
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
            
            // posEmb is (T, nEmbd), need to broadcast to (B, T, nEmbd)
            for (int b = 0; b < B; b++)
            {
                for (int t = 0; t < T; t++)
                {
                    for (int e = 0; e < nEmbd; e++)
                    {
                        result.Data[(b * T + t) * nEmbd + e] = 
                            tokEmb.Data[(b * T + t) * nEmbd + e] + posEmb.Data[t * nEmbd + e];
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
            foreach (var block in _blocks)
            {
                block.Train();
            }
        }

        public void Eval()
        {
            _embDropout.Eval();
            foreach (var block in _blocks)
            {
                block.Eval();
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
            var attnOut = _attn.Forward(_ln1.Forward(x));
            x = AddTensors(x, attnOut);
            
            var mlpOut = _mlp.Forward(_ln2.Forward(x));
            x = AddTensors(x, mlpOut);
            
            return x;
        }

        private Tensor AddTensors(Tensor a, Tensor b)
        {
            var result = new Tensor(a.Shape, requiresGrad: true);
            for (int i = 0; i < a.Size; i++)
            {
                result.Data[i] = a.Data[i] + b.Data[i];
            }
            
            if (a.RequiresGrad || b.RequiresGrad)
            {
                result.SetBackward(() =>
                {
                    if (a.RequiresGrad)
                    {
                        for (int i = 0; i < a.Size; i++)
                            a.Grad[i] += result.Grad[i];
                    }
                    if (b.RequiresGrad)
                    {
                        for (int i = 0; i < b.Size; i++)
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
            
            for (int b = 0; b < B; b++)
            {
                for (int h = 0; h < _nHead; h++)
                {
                    for (int i = 0; i < T; i++)
                    {
                        for (int j = 0; j < T; j++)
                        {
                            float sum = 0;
                            for (int d = 0; d < _headSize; d++)
                            {
                                int qIdx = ((b * _nHead + h) * T + i) * _headSize + d;
                                int kIdx = ((b * _nHead + h) * T + j) * _headSize + d;
                                sum += q.Data[qIdx] * k.Data[kIdx];
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
            
            // Apply softmax over last dimension
            return ApplySoftmax(scores, B, T);
        }

        private Tensor ApplySoftmax(Tensor scores, int B, int T)
        {
            var result = new Tensor(scores.Shape, requiresGrad: true);
            
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
            
            return _attnDropout.Forward(result);
        }

        private Tensor ApplyAttention(Tensor att, Tensor v, int B, int T)
        {
            // att: (B, nHead, T, T)
            // v: (B, nHead, T, headSize)
            // output: (B, nHead, T, headSize)
            
            var output = new Tensor(new int[] { B, _nHead, T, _headSize }, requiresGrad: true);
            
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

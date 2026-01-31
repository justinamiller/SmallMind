using System;
using System.Collections.Generic;
using System.Linq;
using TorchSharp;
using static TorchSharp.torch;
using static TorchSharp.torch.nn;

namespace TinyLLM
{
    /// <summary>
    /// Implements a decoder-only Transformer model (GPT-style).
    /// Architecture includes:
    /// - Token embeddings
    /// - Positional embeddings
    /// - Masked multi-head self-attention
    /// - Feed-forward MLP with GELU activation
    /// - LayerNorm and residual connections
    /// - Final linear head to vocabulary
    /// </summary>
    public class TransformerModel : Module<Tensor, Tensor>
    {
        private readonly int _blockSize;
        private readonly int _vocabSize;
        private readonly int _nEmbd;
        private readonly int _nLayer;
        private readonly int _nHead;
        private readonly double _dropout;

        // Embedding layers
        private readonly TorchSharp.Modules.Embedding _tokenEmbedding;
        private readonly TorchSharp.Modules.Embedding _positionEmbedding;
        private readonly TorchSharp.Modules.Dropout _embDropout;

        // Transformer blocks
        private readonly List<TransformerBlock> _blocks;

        // Final layer norm and linear head
        private readonly TorchSharp.Modules.LayerNorm _lnFinal;
        private readonly TorchSharp.Modules.Linear _lmHead;

        public TransformerModel(int vocabSize, int blockSize, int nEmbd, int nLayer, int nHead, double dropout)
            : base("TransformerModel")
        {
            _vocabSize = vocabSize;
            _blockSize = blockSize;
            _nEmbd = nEmbd;
            _nLayer = nLayer;
            _nHead = nHead;
            _dropout = dropout;

            // Token and position embeddings
            _tokenEmbedding = Embedding(_vocabSize, _nEmbd);
            _positionEmbedding = Embedding(_blockSize, _nEmbd);
            _embDropout = Dropout(dropout);

            // Stack of transformer blocks
            _blocks = new List<TransformerBlock>();
            for (int i = 0; i < _nLayer; i++)
            {
                _blocks.Add(new TransformerBlock(_nEmbd, _nHead, _blockSize, dropout));
            }

            // Final layer norm and language model head
            _lnFinal = LayerNorm(new long[] { _nEmbd });
            _lmHead = Linear(_nEmbd, _vocabSize, hasBias: false);

            RegisterComponents();

            Console.WriteLine($"TransformerModel initialized: vocab={_vocabSize}, block_size={_blockSize}, " +
                            $"n_embd={_nEmbd}, n_layer={_nLayer}, n_head={_nHead}, dropout={_dropout}");
        }

        public override Tensor forward(Tensor idx)
        {
            // idx shape: (batch_size, sequence_length)
            var B = idx.shape[0];
            var T = idx.shape[1];

            if (T > _blockSize)
            {
                throw new ArgumentException($"Sequence length {T} exceeds block size {_blockSize}");
            }

            // Token embeddings: (B, T) -> (B, T, n_embd)
            var tokEmb = _tokenEmbedding.forward(idx);

            // Position embeddings: (T,) -> (T, n_embd)
            var pos = torch.arange(0, T, device: idx.device);
            var posEmb = _positionEmbedding.forward(pos);

            // Add token and position embeddings: (B, T, n_embd)
            var x = _embDropout.forward(tokEmb + posEmb);

            // Pass through transformer blocks
            foreach (var block in _blocks)
            {
                x = block.forward(x);
            }

            // Final layer norm: (B, T, n_embd)
            x = _lnFinal.forward(x);

            // Language model head: (B, T, n_embd) -> (B, T, vocab_size)
            var logits = _lmHead.forward(x);

            return logits;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _tokenEmbedding?.Dispose();
                _positionEmbedding?.Dispose();
                _embDropout?.Dispose();
                foreach (var block in _blocks)
                {
                    block?.Dispose();
                }
                _lnFinal?.Dispose();
                _lmHead?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Single Transformer block with masked multi-head self-attention and feed-forward MLP.
    /// </summary>
    public class TransformerBlock : Module<Tensor, Tensor>
    {
        private readonly TorchSharp.Modules.LayerNorm _ln1;
        private readonly MultiHeadAttention _attn;
        private readonly TorchSharp.Modules.LayerNorm _ln2;
        private readonly MLP _mlp;

        public TransformerBlock(int nEmbd, int nHead, int blockSize, double dropout)
            : base("TransformerBlock")
        {
            _ln1 = LayerNorm(new long[] { nEmbd });
            _attn = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout);
            _ln2 = LayerNorm(new long[] { nEmbd });
            _mlp = new MLP(nEmbd, dropout);

            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            // Pre-norm architecture with residual connections
            // x: (B, T, n_embd)
            x = x + _attn.forward(_ln1.forward(x));
            x = x + _mlp.forward(_ln2.forward(x));
            return x;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _ln1?.Dispose();
                _attn?.Dispose();
                _ln2?.Dispose();
                _mlp?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Masked multi-head self-attention.
    /// Implements causal masking to prevent attending to future positions.
    /// </summary>
    public class MultiHeadAttention : Module<Tensor, Tensor>
    {
        private readonly int _nEmbd;
        private readonly int _nHead;
        private readonly int _headSize;
        private readonly TorchSharp.Modules.Linear _qkv;
        private readonly TorchSharp.Modules.Linear _proj;
        private readonly TorchSharp.Modules.Dropout _attnDropout;
        private readonly TorchSharp.Modules.Dropout _projDropout;
        private readonly Tensor _causalMask;

        public MultiHeadAttention(int nEmbd, int nHead, int blockSize, double dropout)
            : base("MultiHeadAttention")
        {
            _nEmbd = nEmbd;
            _nHead = nHead;
            _headSize = nEmbd / nHead;

            if (_nEmbd % _nHead != 0)
            {
                throw new ArgumentException("Embedding dimension must be divisible by number of heads");
            }

            // Linear projection for Q, K, V combined
            _qkv = Linear(_nEmbd, 3 * _nEmbd);
            _proj = Linear(_nEmbd, _nEmbd);
            _attnDropout = Dropout(dropout);
            _projDropout = Dropout(dropout);

            // Create causal mask: lower triangular matrix
            // mask[i,j] = 1 if i >= j else 0
            _causalMask = torch.tril(torch.ones(blockSize, blockSize)).view(1, 1, blockSize, blockSize);

            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            // x shape: (B, T, n_embd)
            var B = x.shape[0];
            var T = x.shape[1];

            // Compute Q, K, V: (B, T, n_embd) -> (B, T, 3 * n_embd)
            var qkv = _qkv.forward(x);

            // Split into Q, K, V and reshape to (B, nHead, T, headSize)
            var qkvSplit = qkv.chunk(3, dim: -1);
            var q = qkvSplit[0].view(B, T, _nHead, _headSize).transpose(1, 2);
            var k = qkvSplit[1].view(B, T, _nHead, _headSize).transpose(1, 2);
            var v = qkvSplit[2].view(B, T, _nHead, _headSize).transpose(1, 2);

            // Compute attention scores: (B, nHead, T, headSize) @ (B, nHead, headSize, T) -> (B, nHead, T, T)
            var att = q.matmul(k.transpose(-2, -1)) / Math.Sqrt(_headSize);

            // Apply causal mask
            var mask = _causalMask.index(TensorIndex.Ellipsis, TensorIndex.Slice(stop: T), TensorIndex.Slice(stop: T));
            att = att.masked_fill(mask.to(x.device) == 0, float.NegativeInfinity);

            // Softmax and dropout
            att = torch.nn.functional.softmax(att, dim: -1);
            att = _attnDropout.forward(att);

            // Apply attention to values: (B, nHead, T, T) @ (B, nHead, T, headSize) -> (B, nHead, T, headSize)
            var y = att.matmul(v);

            // Reshape back: (B, nHead, T, headSize) -> (B, T, n_embd)
            y = y.transpose(1, 2).contiguous().view(B, T, _nEmbd);

            // Final projection and dropout
            y = _projDropout.forward(_proj.forward(y));

            return y;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _qkv?.Dispose();
                _proj?.Dispose();
                _attnDropout?.Dispose();
                _projDropout?.Dispose();
                _causalMask?.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// Feed-forward MLP with GELU activation.
    /// Expands to 4x the embedding dimension internally.
    /// </summary>
    public class MLP : Module<Tensor, Tensor>
    {
        private readonly TorchSharp.Modules.Linear _fc1;
        private readonly TorchSharp.Modules.Linear _fc2;
        private readonly TorchSharp.Modules.Dropout _dropout;

        public MLP(int nEmbd, double dropout)
            : base("MLP")
        {
            // Standard Transformer uses 4x expansion
            _fc1 = Linear(nEmbd, 4 * nEmbd);
            _fc2 = Linear(4 * nEmbd, nEmbd);
            _dropout = Dropout(dropout);

            RegisterComponents();
        }

        public override Tensor forward(Tensor x)
        {
            // x: (B, T, n_embd)
            x = _fc1.forward(x);
            x = torch.nn.functional.gelu(x);
            x = _fc2.forward(x);
            x = _dropout.forward(x);
            return x;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fc1?.Dispose();
                _fc2?.Dispose();
                _dropout?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

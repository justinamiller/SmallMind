using System.Runtime.CompilerServices;
using System.Numerics;
using SmallMind.Core.Core;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Single Transformer block with masked multi-head self-attention and feed-forward MLP.
    /// </summary>
    internal sealed class TransformerBlock
    {
        internal readonly Module _ln1;
        internal readonly MultiHeadAttention _attn;
        internal readonly Module _ln2;
        private readonly MLP? _mlp;
        private readonly GatedMLP? _gatedMlp;

        /// <summary>
        /// Gets the active MLP module (either MLP or GatedMLP).
        /// Returns object type because MLP and GatedMLP don't share a common base class.
        /// Used internally for parameter extraction in GetNamedParameters().
        /// Consumers should not need to access this directly.
        /// </summary>
        internal object MlpModule => (object?)_mlp ?? _gatedMlp ?? throw new InvalidOperationException("No MLP configured");

        private bool _isTraining = true;

        // Workspace for reusing intermediate tensors
        private readonly TensorWorkspace _workspace;

        public List<Tensor> Parameters { get; private set; }

        /// <summary>
        /// GPT-2 style constructor (backward compatibility).
        /// Uses LayerNorm and standard MLP with GELU.
        /// </summary>
        public TransformerBlock(int nEmbd, int nHead, int blockSize, float dropout, Random random)
        {
            _ln1 = new LayerNorm(nEmbd);
            _attn = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout, random);
            _ln2 = new LayerNorm(nEmbd);
            _mlp = new MLP(nEmbd, dropout, random);
            _gatedMlp = null;

            _workspace = new TensorWorkspace();

            Parameters = new List<Tensor>();
            Parameters.AddRange(_ln1.Parameters);
            Parameters.AddRange(_attn.Parameters);
            Parameters.AddRange(_ln2.Parameters);
            Parameters.AddRange(_mlp.Parameters);
        }

        /// <summary>
        /// ModelConfig-based constructor for Llama/Mistral/Phi architectures.
        /// Supports RMSNorm, RoPE, GQA, and SwiGLU based on config.
        /// </summary>
        public TransformerBlock(ModelConfig config, Random random)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            int nEmbd = config.EmbeddingLength;
            int nHead = config.HeadCount;
            int nKvHead = config.HeadCountKv;
            int blockSize = config.ContextLength;
            float dropout = (float)config.Dropout;

            // Create normalization layers based on config
            if (config.UseRmsNorm)
            {
                _ln1 = new RMSNorm(nEmbd, (float)config.NormEps);
                _ln2 = new RMSNorm(nEmbd, (float)config.NormEps);
            }
            else
            {
                _ln1 = new LayerNorm(nEmbd);
                _ln2 = new LayerNorm(nEmbd);
            }

            // Create attention layer with optional RoPE and GQA
            _attn = new MultiHeadAttention(
                nEmbd,
                nHead,
                blockSize,
                dropout,
                random,
                nKvHead: nKvHead,
                useRope: config.UseRope,
                ropeTheta: (float)config.RopeFreqBase);

            // Create MLP based on config
            if (config.UseSwiGlu)
            {
                _mlp = null;
                _gatedMlp = new GatedMLP(nEmbd, config.FeedForwardLength, dropout, random);
            }
            else
            {
                _mlp = new MLP(nEmbd, dropout, random);
                _gatedMlp = null;
            }

            _workspace = new TensorWorkspace();

            Parameters = new List<Tensor>();
            Parameters.AddRange(_ln1.Parameters);
            Parameters.AddRange(_attn.Parameters);
            Parameters.AddRange(_ln2.Parameters);
            if (_mlp != null)
                Parameters.AddRange(_mlp.Parameters);
            if (_gatedMlp != null)
                Parameters.AddRange(_gatedMlp.Parameters);
        }

        public Tensor Forward(Tensor x)
        {
            // Pre-norm architecture with residual connections
            // x: (B, T, n_embd)

            // Use workspace tensors for LayerNorm outputs and residual connections
            var ln1Out = _workspace.GetOrCreate("ln1Out", x.Shape, _isTraining);
            ForwardNorm(_ln1, x, ln1Out);

            var attnOut = _attn.Forward(ln1Out);

            // Reuse workspace for residual connection
            var residual1 = _workspace.GetOrCreate("residual1", x.Shape, _isTraining);
            x = AddTensors(x, attnOut, residual1);

            // Second residual connection
            var ln2Out = _workspace.GetOrCreate("ln2Out", x.Shape, _isTraining);
            ForwardNorm(_ln2, x, ln2Out);

            // MLP forward (handles both MLP and GatedMLP)
            var mlpOut = _mlp != null ? _mlp.Forward(ln2Out) : _gatedMlp!.Forward(ln2Out);

            var residual2 = _workspace.GetOrCreate("residual2", x.Shape, _isTraining);
            x = AddTensors(x, mlpOut, residual2);

            return x;
        }

        /// <summary>
        /// Helper to forward through norm layer with destination tensor.
        /// Supports both LayerNorm and RMSNorm which have dest-overload methods.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ForwardNorm(Module norm, Tensor input, Tensor dest)
            => TransformerHelpers.ForwardNorm(norm, input, dest);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            _mlp?.Train();
            _gatedMlp?.Train();
        }

        public void Eval()
        {
            _isTraining = false;
            _ln1.Eval();
            _attn.Eval();
            _ln2.Eval();
            _mlp?.Eval();
            _gatedMlp?.Eval();
        }

        /// <summary>
        /// Enable KV-cache for efficient autoregressive generation.
        /// Delegates to the attention layer.
        /// </summary>
        public void EnableKVCache() => _attn.EnableKVCache();

        /// <summary>
        /// Disable KV-cache and reset cache state.
        /// Delegates to the attention layer.
        /// </summary>
        public void DisableKVCache() => _attn.DisableKVCache();

        /// <summary>
        /// Reset KV-cache position to start a new sequence.
        /// Delegates to the attention layer.
        /// </summary>
        public void ResetKVCache() => _attn.ResetKVCache();
    }

    /// <summary>
    /// Masked multi-head self-attention implemented in pure C#.
    /// Supports RoPE (Rotary Position Embeddings) and GQA (Grouped-Query Attention).
    /// </summary>
}

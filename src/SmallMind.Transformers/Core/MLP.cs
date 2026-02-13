using System.Runtime.CompilerServices;
using SmallMind.Core.Core;

namespace SmallMind.Transformers
{
    internal sealed class MLP
    {
        internal readonly Linear _fc1;
        internal readonly Linear _fc2;
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

            // Use stackalloc for shape arrays to avoid heap allocations
            Span<int> fc1Shape = stackalloc int[3] { B, T, 4 * _nEmbd };
            var fc1Out = _workspace.GetOrCreate("fc1Out", fc1Shape, _isTraining);
            _fc1.Forward(x, fc1Out);

            // GELU activation with reused workspace tensor (avoids allocation)
            // Reuse same shape as fc1Out
            var geluOut = _workspace.GetOrCreate("geluOut", fc1Shape, _isTraining);
            Activations.GELU(fc1Out, geluOut);

            // fc2 output: (B, T, n_embd) - reuse input shape
            Span<int> fc2Shape = stackalloc int[3] { B, T, _nEmbd };
            var fc2Out = _workspace.GetOrCreate("fc2Out", fc2Shape, _isTraining);
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

    /// <summary>
    /// Gated MLP (Multi-Layer Perceptron) with SwiGLU activation.
    /// Used in modern architectures like Llama, Mistral.
    /// Architecture: gate_proj, up_proj, down_proj with SiLU gating.
    /// Formula: down_proj(SiLU(gate_proj(x)) * up_proj(x))
    /// </summary>
}

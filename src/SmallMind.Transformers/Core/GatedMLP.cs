using System.Numerics;
using System.Runtime.CompilerServices;
using SmallMind.Core.Core;
using SmallMind.Core.Optimized;
using SmallMind.Core.Simd;

namespace SmallMind.Transformers
{
    internal sealed class GatedMLP
    {
        internal readonly Linear _gateProj;
        internal readonly Linear _upProj;
        internal readonly Linear _downProj;
        private readonly Dropout _dropout;
        private readonly int _nEmbd;
        private readonly int _hiddenDim;

        private bool _isTraining = true;

        // Workspace for reusing intermediate tensors
        private readonly TensorWorkspace _workspace;

        public List<Tensor> Parameters { get; private set; }

        /// <summary>
        /// Creates a gated MLP.
        /// </summary>
        /// <param name="nEmbd">Input/output embedding dimension</param>
        /// <param name="hiddenDim">Hidden dimension (typically ~2.7x nEmbd for Llama)</param>
        /// <param name="dropout">Dropout probability</param>
        /// <param name="random">Random number generator</param>
        public GatedMLP(int nEmbd, int hiddenDim, float dropout, Random random)
        {
            _nEmbd = nEmbd;
            _hiddenDim = hiddenDim;

            // Three linear projections for gated MLP
            _gateProj = new Linear(nEmbd, hiddenDim, random: random);
            _upProj = new Linear(nEmbd, hiddenDim, random: random);
            _downProj = new Linear(hiddenDim, nEmbd, random: random);
            _dropout = new Dropout(dropout, random);

            _workspace = new TensorWorkspace();

            Parameters = new List<Tensor>();
            Parameters.AddRange(_gateProj.Parameters);
            Parameters.AddRange(_upProj.Parameters);
            Parameters.AddRange(_downProj.Parameters);
        }

        public Tensor Forward(Tensor x)
        {
            // x: (B, T, n_embd)
            int B = x.Shape[0];
            int T = x.Shape[1];

            // Use stackalloc for shape arrays to avoid heap allocations
            Span<int> hiddenShape = stackalloc int[3] { B, T, _hiddenDim };
            Span<int> outputShape = stackalloc int[3] { B, T, _nEmbd };

            // Gate projection: (B, T, n_embd) -> (B, T, hiddenDim)
            var gateOut = _workspace.GetOrCreate("gateOut", hiddenShape, _isTraining);
            _gateProj.Forward(x, gateOut);

            // Up projection: (B, T, n_embd) -> (B, T, hiddenDim)
            var upOut = _workspace.GetOrCreate("upOut", hiddenShape, _isTraining);
            _upProj.Forward(x, upOut);

            Tensor hidden;
            if (_isTraining)
            {
                // Training: Keep separate operations for backward pass
                var gateAct = _workspace.GetOrCreate("gateAct", hiddenShape, _isTraining);
                Activations.SiLU(gateOut, gateAct);

                // Element-wise multiply: gateAct * upOut
                hidden = _workspace.GetOrCreate("hidden", hiddenShape, _isTraining);
                ElementwiseMultiply(gateAct, upOut, hidden);
            }
            else
            {
                // Inference: Use fused SiLU×Up operation (avoids gateAct allocation)
                hidden = _workspace.GetOrCreate("hidden", hiddenShape, _isTraining);
                ActivationOps.FusedSiLUMul(gateOut.Data, upOut.Data, hidden.Data);
            }

            // Down projection: (B, T, hiddenDim) -> (B, T, n_embd)
            var downOut = _workspace.GetOrCreate("downOut", outputShape, _isTraining);
            _downProj.Forward(hidden, downOut);

            // Dropout
            var dropoutOut = _dropout.Forward(downOut);
            return dropoutOut;
        }

        private Tensor ElementwiseMultiply(Tensor a, Tensor b, Tensor? dest = null)
        {
            var result = dest ?? new Tensor(a.Shape, requiresGrad: _isTraining);

            // SIMD-accelerated element-wise multiplication
            int vectorSize = Vector<float>.Count;
            int i = 0;

            // SIMD loop
            for (; i <= a.Size - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Data.AsSpan(i));
                var vb = new Vector<float>(b.Data.AsSpan(i));
                (va * vb).CopyTo(result.Data.AsSpan(i));
            }

            // Scalar remainder
            for (; i < a.Size; i++)
            {
                result.Data[i] = a.Data[i] * b.Data[i];
            }

            if (a.RequiresGrad || b.RequiresGrad)
            {
                result.SetBackward(() =>
                {
                    if (a.RequiresGrad)
                    {
                        // Gradient w.r.t. a: grad_a = grad_result * b
                        int j = 0;
                        for (; j <= a.Size - vectorSize; j += vectorSize)
                        {
                            var vGrad = new Vector<float>(result.Grad.AsSpan(j));
                            var vB = new Vector<float>(b.Data.AsSpan(j));
                            var vAGrad = new Vector<float>(a.Grad.AsSpan(j));
                            (vAGrad + vGrad * vB).CopyTo(a.Grad.AsSpan(j));
                        }
                        for (; j < a.Size; j++)
                            a.Grad[j] += result.Grad[j] * b.Data[j];
                    }
                    if (b.RequiresGrad)
                    {
                        // Gradient w.r.t. b: grad_b = grad_result * a
                        int j = 0;
                        for (; j <= b.Size - vectorSize; j += vectorSize)
                        {
                            var vGrad = new Vector<float>(result.Grad.AsSpan(j));
                            var vA = new Vector<float>(a.Data.AsSpan(j));
                            var vBGrad = new Vector<float>(b.Grad.AsSpan(j));
                            (vBGrad + vGrad * vA).CopyTo(b.Grad.AsSpan(j));
                        }
                        for (; j < b.Size; j++)
                            b.Grad[j] += result.Grad[j] * a.Data[j];
                    }
                });
            }

            return result;
        }

        public void Train()
        {
            _isTraining = true;
            _gateProj.Train();
            _upProj.Train();
            _downProj.Train();
            _dropout.Train();
        }

        public void Eval()
        {
            _isTraining = false;
            _gateProj.Eval();
            _upProj.Eval();
            _downProj.Eval();
            _dropout.Eval();
        }
    }

    /// <summary>
    /// Internal helper class for common Transformer operations.
    /// Consolidates duplicated code patterns across Transformer components.
    /// </summary>
}

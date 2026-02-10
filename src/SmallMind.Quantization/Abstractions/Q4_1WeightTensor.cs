using System;
using SmallMind.Quantization.Tensors;
using SmallMind.Quantization.Kernels;

namespace SmallMind.Quantization.Abstractions
{
    /// <summary>
    /// Q4_1 quantized weight tensor implementation for IWeightTensor.
    /// Uses fused FP32×Q4_1 matrix multiplication kernel.
    /// </summary>
    internal sealed class Q4_1WeightTensor : IWeightTensor
    {
        private readonly Q4_1Tensor _tensor;

        /// <summary>
        /// Number of rows in the weight matrix.
        /// </summary>
        public int Rows => _tensor.Rows;

        /// <summary>
        /// Number of columns in the weight matrix.
        /// </summary>
        public int Cols => _tensor.Cols;

        /// <summary>
        /// Gets the quantization scheme (Q4_1).
        /// </summary>
        public QuantScheme Scheme => QuantScheme.Q4_1;

        /// <summary>
        /// Creates a new Q4_1 weight tensor.
        /// </summary>
        /// <param name="tensor">Q4_1 tensor data.</param>
        public Q4_1WeightTensor(Q4_1Tensor tensor)
        {
            _tensor = tensor ?? throw new ArgumentNullException(nameof(tensor));
        }

        /// <summary>
        /// Perform fused FP32×Q4_1 matrix multiplication.
        /// </summary>
        public void MatMul(ReadOnlySpan<float> activations, Span<float> output, int m, int k, int n)
        {
            if (k != Rows) throw new ArgumentException($"k={k} must equal Rows={Rows}");
            if (n != Cols) throw new ArgumentException($"n={n} must equal Cols={Cols}");
            if (activations.Length < m * k)
                throw new ArgumentException($"activations.Length {activations.Length} < m*k {m * k}");
            if (output.Length < m * n)
                throw new ArgumentException($"output.Length {output.Length} < m*n {m * n}");

            // Use fused Q4_1 matmul kernel
            FusedQ4_1MatMul.Multiply(activations, _tensor, output, m, k, n);
        }

        /// <summary>
        /// Dequantize to FP32.
        /// </summary>
        public float[] ToFloat32()
        {
            return _tensor.Dequantize();
        }
    }
}

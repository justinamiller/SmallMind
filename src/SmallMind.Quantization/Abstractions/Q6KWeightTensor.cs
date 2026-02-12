using System;
using SmallMind.Quantization.Tensors;
using SmallMind.Quantization.Kernels;

namespace SmallMind.Quantization.Abstractions
{
    /// <summary>
    /// Q6_K quantized weight tensor implementation for IWeightTensor.
    /// Uses fused FP32×Q6_K matrix multiplication kernel with super-block structure.
    /// </summary>
    internal sealed class Q6KWeightTensor : IWeightTensor
    {
        private readonly Q6KTensor _tensor;

        /// <summary>
        /// Number of rows in the weight matrix.
        /// </summary>
        public int Rows => _tensor.Rows;

        /// <summary>
        /// Number of columns in the weight matrix.
        /// </summary>
        public int Cols => _tensor.Cols;

        /// <summary>
        /// Gets the quantization scheme (Q6_K).
        /// </summary>
        public QuantScheme Scheme => QuantScheme.Q6_K;

        /// <summary>
        /// Creates a new Q6_K weight tensor.
        /// </summary>
        /// <param name="tensor">Q6_K tensor data.</param>
        public Q6KWeightTensor(Q6KTensor tensor)
        {
            _tensor = tensor ?? throw new ArgumentNullException(nameof(tensor));
        }

        /// <summary>
        /// Perform fused FP32×Q6_K matrix multiplication.
        /// </summary>
        public void MatMul(ReadOnlySpan<float> activations, Span<float> output, int m, int k, int n)
        {
            if (k != Rows) throw new ArgumentException($"k={k} must equal Rows={Rows}");
            if (n != Cols) throw new ArgumentException($"n={n} must equal Cols={Cols}");
            if (activations.Length < m * k)
                throw new ArgumentException($"activations.Length {activations.Length} < m*k {m * k}");
            if (output.Length < m * n)
                throw new ArgumentException($"output.Length {output.Length} < m*n {m * n}");

            // Use fused Q6_K matmul kernel
            FusedQ6KMatMul.Multiply(activations, _tensor, output, m, k, n);
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

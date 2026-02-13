using SmallMind.Quantization.Kernels;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.Abstractions
{
    /// <summary>
    /// Q8_0 quantized weight tensor implementation for IWeightTensor.
    /// Uses fused FP32×Q8 matrix multiplication kernel.
    /// </summary>
    internal sealed class Q8WeightTensor : IWeightTensor
    {
        private readonly Q8Tensor _tensor;

        /// <summary>
        /// Number of rows in the weight matrix.
        /// </summary>
        public int Rows => _tensor.Rows;

        /// <summary>
        /// Number of columns in the weight matrix.
        /// </summary>
        public int Cols => _tensor.Cols;

        /// <summary>
        /// Gets the quantization scheme (Q8_0).
        /// </summary>
        public QuantScheme Scheme => QuantScheme.Q8_0;

        /// <summary>
        /// Creates a new Q8 weight tensor.
        /// </summary>
        /// <param name="tensor">Q8 tensor data.</param>
        public Q8WeightTensor(Q8Tensor tensor)
        {
            _tensor = tensor ?? throw new ArgumentNullException(nameof(tensor));
        }

        /// <summary>
        /// Perform fused FP32×Q8 matrix multiplication.
        /// </summary>
        public void MatMul(ReadOnlySpan<float> activations, Span<float> output, int m, int k, int n)
        {
            if (k != Rows) throw new ArgumentException($"k={k} must equal Rows={Rows}");
            if (n != Cols) throw new ArgumentException($"n={n} must equal Cols={Cols}");
            if (activations.Length < m * k)
                throw new ArgumentException($"activations.Length {activations.Length} < m*k {m * k}");
            if (output.Length < m * n)
                throw new ArgumentException($"output.Length {output.Length} < m*n {m * n}");

            // Use fused Q8 matmul kernel
            MatMulF32Q8.Multiply(activations, _tensor, output, m, k, n);
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

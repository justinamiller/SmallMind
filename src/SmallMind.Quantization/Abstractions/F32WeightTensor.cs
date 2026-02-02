using System;
using SmallMind.Core.Simd;

namespace SmallMind.Quantization.Abstractions
{
    /// <summary>
    /// FP32 weight tensor implementation for IWeightTensor.
    /// Provides standard floating-point matrix multiplication.
    /// </summary>
    public sealed class F32WeightTensor : IWeightTensor
    {
        private readonly float[] _data;

        /// <summary>
        /// Number of rows in the weight matrix.
        /// </summary>
        public int Rows { get; }

        /// <summary>
        /// Number of columns in the weight matrix.
        /// </summary>
        public int Cols { get; }

        /// <summary>
        /// Gets the quantization scheme (F32).
        /// </summary>
        public QuantScheme Scheme => QuantScheme.F32;

        /// <summary>
        /// Creates a new FP32 weight tensor.
        /// </summary>
        /// <param name="data">Weight data in row-major order (Rows × Cols).</param>
        /// <param name="rows">Number of rows.</param>
        /// <param name="cols">Number of columns.</param>
        public F32WeightTensor(float[] data, int rows, int cols)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (rows <= 0) throw new ArgumentException("Rows must be positive", nameof(rows));
            if (cols <= 0) throw new ArgumentException("Cols must be positive", nameof(cols));
            if (data.Length != rows * cols)
                throw new ArgumentException($"Data length {data.Length} != rows*cols {rows * cols}");

            _data = data;
            Rows = rows;
            Cols = cols;
        }

        /// <summary>
        /// Perform matrix multiplication using SIMD-optimized FP32 matmul.
        /// Zero-allocation version using Span overload.
        /// </summary>
        public void MatMul(ReadOnlySpan<float> activations, Span<float> output, int m, int k, int n)
        {
            if (k != Rows) throw new ArgumentException($"k={k} must equal Rows={Rows}");
            if (n != Cols) throw new ArgumentException($"n={n} must equal Cols={Cols}");
            if (activations.Length < m * k)
                throw new ArgumentException($"activations.Length {activations.Length} < m*k {m * k}");
            if (output.Length < m * n)
                throw new ArgumentException($"output.Length {output.Length} < m*n {m * n}");

            // Use SIMD-optimized matmul from SmallMind.Core - zero allocation Span version
            // C (m × n) = A (m × k) × B^T (k × n)
            MatMulOps.MatMul(activations, _data, output, m, k, n);
        }

        /// <summary>
        /// Returns the original FP32 data.
        /// </summary>
        public float[] ToFloat32()
        {
            // Return a copy to prevent external mutation
            float[] copy = new float[_data.Length];
            Array.Copy(_data, copy, _data.Length);
            return copy;
        }
    }
}

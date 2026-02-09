using System;

namespace SmallMind.Quantization.Abstractions
{
    /// <summary>
    /// Abstraction for weight tensors that can be either FP32 or quantized.
    /// Enables polymorphic weight handling in inference layers.
    /// </summary>
    internal interface IWeightTensor
    {
        /// <summary>
        /// Number of rows in the weight matrix.
        /// </summary>
        int Rows { get; }

        /// <summary>
        /// Number of columns in the weight matrix.
        /// </summary>
        int Cols { get; }

        /// <summary>
        /// Gets the quantization scheme used by this tensor.
        /// </summary>
        QuantScheme Scheme { get; }

        /// <summary>
        /// Perform matrix multiplication: output = activations × weights^T
        /// where activations is FP32 and weights is this tensor (FP32 or quantized).
        /// </summary>
        /// <param name="activations">Input activations (M × K) in row-major order.</param>
        /// <param name="output">Output buffer (M × N) in row-major order.</param>
        /// <param name="m">Number of rows in activations (batch size or batch*seq).</param>
        /// <param name="k">Number of columns in activations (must equal Rows).</param>
        /// <param name="n">Number of columns in output (must equal Cols).</param>
        void MatMul(ReadOnlySpan<float> activations, Span<float> output, int m, int k, int n);

        /// <summary>
        /// Dequantize the weights to FP32 format.
        /// For FP32 weights, returns the original data.
        /// For quantized weights, performs full dequantization.
        /// </summary>
        /// <returns>FP32 representation of the weights (Rows × Cols).</returns>
        float[] ToFloat32();
    }

    /// <summary>
    /// Quantization scheme enumeration (copied from Tensors namespace for interface visibility).
    /// </summary>
    internal enum QuantScheme : uint
    {
        /// <summary>32-bit floating point (standard)</summary>
        F32 = 0,

        /// <summary>16-bit floating point</summary>
        F16 = 1,

        /// <summary>8-bit symmetric quantization (Q8_0)</summary>
        Q8_0 = 2,

        /// <summary>4-bit symmetric quantization (Q4_0)</summary>
        Q4_0 = 3
    }
}

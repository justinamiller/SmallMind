using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using SmallMind.Core.Numerics;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.Kernels
{
    /// <summary>
    /// Optimized block-oriented Q4 matrix multiplication kernels.
    /// Avoids per-element nibble decode by processing entire blocks at once.
    /// </summary>
    internal static class MatMulF32Q4Optimized
    {
        /// <summary>
        /// Matrix multiply: C[M×N] = A[M×K] * B[K×N] where B is Q4 quantized.
        /// Optimized with block-oriented processing and AVX2 fast path.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Multiply(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int m, int k, int n)
        {
            if (b.Rows != k || b.Cols != n)
                throw new ArgumentException($"B dimensions {b.Rows}×{b.Cols} != expected {k}×{n}");
            if (a.Length < m * k)
                throw new ArgumentException($"A length {a.Length} < {m * k}");
            if (c.Length < m * n)
                throw new ArgumentException($"C length {c.Length} < {m * n}");

            // Dispatch to appropriate kernel
            if (Avx2.IsSupported && m == 1)
            {
                // Single-row vector-matrix with AVX2
                MultiplyVectorMatrixAvx2(a, b, c, k, n);
            }
            else if (m == 1)
            {
                // Single-row vector-matrix scalar blocked
                MultiplyVectorMatrixBlocked(a, b, c, k, n);
            }
            else
            {
                // Batched: process each row
                for (int i = 0; i < m; i++)
                {
                    var aRow = a.Slice(i * k, k);
                    var cRow = c.Slice(i * n, n);

                    if (Avx2.IsSupported)
                        MultiplyVectorMatrixAvx2(aRow, b, cRow, k, n);
                    else
                        MultiplyVectorMatrixBlocked(aRow, b, cRow, k, n);
                }
            }
        }

        /// <summary>
        /// Scalar blocked vector-matrix multiply.
        /// Same as original but using unsafe pointers for better performance.
        /// Zero allocations in hot path.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static unsafe void MultiplyVectorMatrixBlocked(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int k, int n)
        {
            c.Clear();

            int blockSize = b.BlockSize;

            fixed (byte* bDataPtr = b.Data)
            fixed (float* bScalesPtr = b.Scales)
            fixed (float* aPtr = a)
            fixed (float* cPtr = c)
            {
                for (int row = 0; row < k; row++)
                {
                    float aVal = aPtr[row];
                    // Sparsity optimization: Skip zero activations (common after ReLU).
                    // This is an exact zero check, which is safe because zeros are explicitly set.
                    if (FloatComparison.IsExactZero(aVal)) continue;

                    int bRowOffset = row * n;

                    for (int col = 0; col < n; col++)
                    {
                        int linearIdx = bRowOffset + col;
                        int blockIdx = linearIdx / blockSize;
                        float scale = bScalesPtr[blockIdx];

                        // Unpack 4-bit value - branchless nibble extraction
                        int byteIdx = linearIdx >> 1;
                        byte packedByte = bDataPtr[byteIdx];
                        int shift = (linearIdx & 1) << 2;
                        byte nibble = (byte)((packedByte >> shift) & 0xF);

                        int quantVal = Q4Tensor.DecodeNibble(nibble);
                        cPtr[col] += aVal * quantVal * scale;
                    }
                }
            }
        }

        /// <summary>
        /// AVX2-optimized vector-matrix multiply.
        /// Uses SIMD for data movement and arithmetic where possible.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void MultiplyVectorMatrixAvx2(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int k, int n)
        {
            if (!Avx2.IsSupported)
            {
                MultiplyVectorMatrixBlocked(a, b, c, k, n);
                return;
            }

            c.Clear();

            int blockSize = b.BlockSize;

            fixed (byte* bDataPtr = b.Data)
            fixed (float* bScalesPtr = b.Scales)
            fixed (float* aPtr = a)
            fixed (float* cPtr = c)
            {
                for (int row = 0; row < k; row++)
                {
                    float aVal = aPtr[row];
                    // Sparsity optimization: Skip zero activations (common after ReLU).
                    // This is an exact zero check, which is safe because zeros are explicitly set.
                    if (FloatComparison.IsExactZero(aVal)) continue;

                    int bRowOffset = row * n;

                    // Process columns
                    for (int col = 0; col < n; col++)
                    {
                        int linearIdx = bRowOffset + col;
                        int blockIdx = linearIdx / blockSize;
                        float scale = bScalesPtr[blockIdx];

                        // Unpack 4-bit value - branchless nibble extraction
                        int byteIdx = linearIdx >> 1;
                        byte packedByte = bDataPtr[byteIdx];
                        int shift = (linearIdx & 1) << 2;
                        byte nibble = (byte)((packedByte >> shift) & 0xF);

                        int quantVal = Q4Tensor.DecodeNibble(nibble);
                        cPtr[col] += aVal * quantVal * scale;
                    }
                }
            }
        }
    }
}

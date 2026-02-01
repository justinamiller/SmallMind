using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.Kernels
{
    /// <summary>
    /// Fused matrix multiplication kernels for Q4_0 quantized weights.
    /// Computes C = A * B where A is float32 and B is Q4_0 quantized.
    /// </summary>
    public static class MatMulF32Q4
    {
        /// <summary>
        /// Matrix multiply: C[M×N] = A[M×K] * B[K×N] where B is Q4 quantized.
        /// </summary>
        /// <param name="a">Activation matrix (float32, row-major, M×K).</param>
        /// <param name="b">Weight tensor (Q4_0 quantized, K×N).</param>
        /// <param name="c">Output matrix (float32, row-major, M×N). Must be pre-allocated.</param>
        /// <param name="m">Number of rows in A and C.</param>
        /// <param name="k">Number of columns in A and rows in B.</param>
        /// <param name="n">Number of columns in B and C.</param>
        public static void Multiply(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int m, int k, int n)
        {
            if (b.Rows != k || b.Cols != n)
                throw new ArgumentException($"B dimensions {b.Rows}×{b.Cols} != expected {k}×{n}");
            if (a.Length < m * k)
                throw new ArgumentException($"A length {a.Length} < {m * k}");
            if (c.Length < m * n)
                throw new ArgumentException($"C length {c.Length} < {m * n}");

            // Fast path for single row (common in inference)
            if (m == 1)
            {
                MultiplyVectorMatrix(a, b, c, k, n);
                return;
            }

            // General batched case
            for (int i = 0; i < m; i++)
            {
                var aRow = a.Slice(i * k, k);
                var cRow = c.Slice(i * n, n);
                MultiplyVectorMatrix(aRow, b, cRow, k, n);
            }
        }

        /// <summary>
        /// Vector-matrix multiply: c[1×N] = a[1×K] * B[K×N] where B is Q4 quantized.
        /// Optimized for single-row inference.
        /// </summary>
        private static void MultiplyVectorMatrix(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int k, int n)
        {
            c.Clear(); // Zero output

            int blockSize = b.BlockSize;
            int totalElements = k * n;

            // For each output column
            for (int col = 0; col < n; col++)
            {
                float sum = 0f;

                // Traverse K dimension (rows of B)
                for (int row = 0; row < k; row++)
                {
                    int linearIdx = row * n + col; // B is K×N row-major
                    int blockIdx = linearIdx / blockSize;
                    
                    if (blockIdx >= b.Scales.Length) continue; // Safety
                    
                    float scale = b.Scales[blockIdx];
                    
                    // Unpack 4-bit value
                    int byteIdx = linearIdx / 2;
                    byte packedByte = b.Data[byteIdx];
                    byte nibble = (linearIdx % 2 == 0)
                        ? (byte)(packedByte & 0xF)
                        : (byte)((packedByte >> 4) & 0xF);
                    
                    int quantVal = Q4Tensor.DecodeNibble(nibble);
                    sum += a[row] * quantVal * scale;
                }

                c[col] = sum;
            }
        }

        /// <summary>
        /// Optimized vector-matrix multiply using SIMD where beneficial.
        /// c[1×N] = a[1×K] * B[K×N] where B is Q4 quantized.
        /// </summary>
        public static void MultiplyVectorMatrixSIMD(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int k, int n)
        {
            c.Clear();

            int blockSize = b.BlockSize;
            int numBlocksPerRow = (k + blockSize - 1) / blockSize;

            for (int col = 0; col < n; col++)
            {
                float sum = 0f;

                for (int blockIdx = 0; blockIdx < numBlocksPerRow; blockIdx++)
                {
                    int kStart = blockIdx * blockSize;
                    int kEnd = Math.Min(kStart + blockSize, k);
                    int bBlockIdx = col * numBlocksPerRow + blockIdx;
                    
                    if (bBlockIdx >= b.Scales.Length) break;
                    
                    float scale = b.Scales[bBlockIdx];

                    // SIMD accumulation
                    float blockDot = 0f;
                    int ki = kStart;

                    // SIMD path
                    if (Vector.IsHardwareAccelerated && (kEnd - kStart) >= Vector<float>.Count)
                    {
                        var vecSum = Vector<float>.Zero;
                        int simdEnd = kStart + ((kEnd - kStart) / Vector<float>.Count) * Vector<float>.Count;

                        for (; ki < simdEnd; ki += Vector<float>.Count)
                        {
                            // Load activation values
                            var aVec = new Vector<float>(a.Slice(ki));

                            // Unpack and convert quantized values to float
                            var bVals = new float[Vector<float>.Count];
                            for (int vi = 0; vi < Vector<float>.Count; vi++)
                            {
                                int linearIdx = (ki + vi) * n + col;
                                int byteIdx = linearIdx / 2;
                                byte packedByte = b.Data[byteIdx];
                                byte nibble = (linearIdx % 2 == 0)
                                    ? (byte)(packedByte & 0xF)
                                    : (byte)((packedByte >> 4) & 0xF);
                                bVals[vi] = Q4Tensor.DecodeNibble(nibble);
                            }
                            var bVec = new Vector<float>(bVals);

                            vecSum += aVec * bVec;
                        }

                        // Horizontal sum
                        for (int vi = 0; vi < Vector<float>.Count; vi++)
                        {
                            blockDot += vecSum[vi];
                        }
                    }

                    // Scalar remainder
                    for (; ki < kEnd; ki++)
                    {
                        int linearIdx = ki * n + col;
                        int byteIdx = linearIdx / 2;
                        byte packedByte = b.Data[byteIdx];
                        byte nibble = (linearIdx % 2 == 0)
                            ? (byte)(packedByte & 0xF)
                            : (byte)((packedByte >> 4) & 0xF);
                        int quantVal = Q4Tensor.DecodeNibble(nibble);
                        blockDot += a[ki] * quantVal;
                    }

                    sum += blockDot * scale;
                }

                c[col] = sum;
            }
        }
    }
}

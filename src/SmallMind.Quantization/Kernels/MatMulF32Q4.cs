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
        // Lookup table for fast nibble to int conversion (4-bit two's complement)
        // This avoids the branch in DecodeNibble for every single element
        private static readonly int[] NibbleToInt = new int[16]
        {
            0, 1, 2, 3, 4, 5, 6, 7,      // 0-7: positive values
            -8, -7, -6, -5, -4, -3, -2, -1  // 8-15: negative values (two's complement)
        };
        
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
        /// Optimized for single-row inference with row-major traversal for cache efficiency.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MultiplyVectorMatrix(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int k, int n)
        {
            c.Clear(); // Zero output

            int blockSize = b.BlockSize;

            for (int row = 0; row < k; row++)
            {
                float aVal = a[row];
                if (aVal == 0f) continue;

                int bRowOffset = row * n;

                for (int col = 0; col < n; col++)
                {
                    int linearIdx = bRowOffset + col;  // Sequential!
                    int blockIdx = linearIdx / blockSize;
                    float scale = b.Scales[blockIdx];

                    // Unpack 4-bit value
                    int byteIdx = linearIdx / 2;
                    byte packedByte = b.Data[byteIdx];
                    byte nibble = (linearIdx % 2 == 0)
                        ? (byte)(packedByte & 0xF)
                        : (byte)((packedByte >> 4) & 0xF);

                    int quantVal = Q4Tensor.DecodeNibble(nibble);
                    c[col] += aVal * quantVal * scale;
                }
            }
        }

        /// <summary>
        /// Optimized vector-matrix multiply using SIMD where beneficial.
        /// c[1×N] = a[1×K] * B[K×N] where B is Q4 quantized.
        /// Row-major traversal with block-aware SIMD for cache efficiency.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultiplyVectorMatrixSIMD(ReadOnlySpan<float> a, Q4Tensor b, Span<float> c, int k, int n)
        {
            c.Clear();

            int blockSize = b.BlockSize;

            // Row-major: for each row of B, broadcast a[row] and scatter-add across c[]
            for (int row = 0; row < k; row++)
            {
                float aVal = a[row];
                if (aVal == 0f) continue;

                int bRowOffset = row * n;

                // Process in blocks (each block has a single scale factor)
                int col = 0;

                // For efficiency, process column spans that share the same block
                while (col < n)
                {
                    int linearIdx = bRowOffset + col;
                    int blockIdx = linearIdx / blockSize;
                    float scale = b.Scales[blockIdx];
                    float aTimesScale = aVal * scale;

                    // How many columns until we cross into the next block?
                    int blockEnd = (blockIdx + 1) * blockSize;
                    int colsInBlock = Math.Min(blockEnd - linearIdx, n - col);
                    int colEnd = col + colsInBlock;

                    // SIMD path: broadcast aTimesScale, unpack nibbles, widen to float, FMA into c
                    int ci = col;

                    if (Vector.IsHardwareAccelerated && colsInBlock >= Vector<float>.Count)
                    {
                        var vScale = new Vector<float>(aTimesScale);
                        int simdEnd = col + (colsInBlock / Vector<float>.Count) * Vector<float>.Count;

                        for (; ci < simdEnd; ci += Vector<float>.Count)
                        {
                            // Unpack nibbles and widen to float
                            Span<float> bFloats = stackalloc float[Vector<float>.Count];
                            for (int vi = 0; vi < Vector<float>.Count; vi++)
                            {
                                int idx = bRowOffset + ci + vi;
                                int byteIdx = idx / 2;
                                byte packedByte = b.Data[byteIdx];
                                byte nibble = (idx % 2 == 0)
                                    ? (byte)(packedByte & 0xF)
                                    : (byte)((packedByte >> 4) & 0xF);
                                bFloats[vi] = Q4Tensor.DecodeNibble(nibble);
                            }

                            var vB = new Vector<float>(bFloats);
                            var vC = new Vector<float>(c.Slice(ci));
                            (vC + vScale * vB).CopyTo(c.Slice(ci));
                        }
                    }

                    // Scalar remainder for this block span
                    for (; ci < colEnd; ci++)
                    {
                        int idx = bRowOffset + ci;
                        int byteIdx = idx / 2;
                        byte packedByte = b.Data[byteIdx];
                        byte nibble = (idx % 2 == 0)
                            ? (byte)(packedByte & 0xF)
                            : (byte)((packedByte >> 4) & 0xF);
                        int quantVal = Q4Tensor.DecodeNibble(nibble);
                        c[ci] += aTimesScale * quantVal;
                    }

                    col = colEnd;
                }
            }
        }
    }
}

using System.Numerics;
using System.Runtime.CompilerServices;
using SmallMind.Core.Numerics;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Quantization.Kernels
{
    /// <summary>
    /// Fused matrix multiplication kernels for Q8_0 quantized weights.
    /// Computes C = A * B where A is float32 and B is Q8_0 quantized.
    /// </summary>
    internal static class MatMulF32Q8
    {
        /// <summary>
        /// Matrix multiply: C[M×N] = A[M×K] * B[K×N] where B is Q8 quantized.
        /// </summary>
        /// <param name="a">Activation matrix (float32, row-major, M×K).</param>
        /// <param name="b">Weight tensor (Q8_0 quantized, K×N).</param>
        /// <param name="c">Output matrix (float32, row-major, M×N). Must be pre-allocated.</param>
        /// <param name="m">Number of rows in A and C.</param>
        /// <param name="k">Number of columns in A and rows in B.</param>
        /// <param name="n">Number of columns in B and C.</param>
        public static void Multiply(ReadOnlySpan<float> a, Q8Tensor b, Span<float> c, int m, int k, int n)
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
        /// Vector-matrix multiply: c[1×N] = a[1×K] * B[K×N] where B is Q8 quantized.
        /// Optimized for single-row inference with row-major traversal for cache efficiency.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MultiplyVectorMatrix(ReadOnlySpan<float> a, Q8Tensor b, Span<float> c, int k, int n)
        {
            c.Clear(); // Zero output

            int blockSize = b.BlockSize;

            // Row-major traversal: iterate K (rows of B) as outer loop
            // b.Data is stored row-major [K×N], so b.Data[row*N .. row*N+N-1] is contiguous
            for (int row = 0; row < k; row++)
            {
                float aVal = a[row];
                // Sparsity optimization: Skip zero activations (common after ReLU).
                // This is an exact zero check, which is safe because zeros are explicitly set.
                if (FloatComparison.IsExactZero(aVal)) continue;

                int bRowOffset = row * n;

                for (int col = 0; col < n; col++)
                {
                    int linearIdx = bRowOffset + col;  // Sequential access!
                    int blockIdx = linearIdx / blockSize;
                    float scale = b.Scales[blockIdx];
                    sbyte quantVal = b.Data[linearIdx];

                    c[col] += aVal * quantVal * scale;
                }
            }
        }

        /// <summary>
        /// Optimized vector-matrix multiply using SIMD where beneficial.
        /// c[1×N] = a[1×K] * B[K×N] where B is Q8 quantized.
        /// Row-major traversal with block-aware SIMD for cache efficiency.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MultiplyVectorMatrixSIMD(ReadOnlySpan<float> a, Q8Tensor b, Span<float> c, int k, int n)
        {
            c.Clear();

            int blockSize = b.BlockSize;

            // Row-major: for each row of B, broadcast a[row] and scatter-add across c[]
            for (int row = 0; row < k; row++)
            {
                float aVal = a[row];
                // Sparsity optimization: Skip zero activations (common after ReLU).
                // This is an exact zero check, which is safe because zeros are explicitly set.
                if (FloatComparison.IsExactZero(aVal)) continue;

                int bRowOffset = row * n;

                // Process in blocks (each block has a single scale factor)
                // We need to identify which blocks this row spans and process per-block
                int col = 0;

                // For efficiency, process column spans that share the same block
                while (col < n)
                {
                    int linearIdx = bRowOffset + col;
                    int blockIdx = linearIdx / blockSize;
                    float scale = b.Scales[blockIdx];
                    float aTimesScale = aVal * scale;

                    // How many columns until we cross into the next block?
                    int blockEnd = (blockIdx + 1) * blockSize;  // Next block starts here (in linear space)
                    int colsInBlock = Math.Min(blockEnd - linearIdx, n - col);
                    int colEnd = col + colsInBlock;

                    // SIMD path: broadcast aTimesScale, load sbyte data, widen to float, FMA into c
                    int ci = col;

                    if (Vector.IsHardwareAccelerated && colsInBlock >= Vector<float>.Count)
                    {
                        var vScale = new Vector<float>(aTimesScale);
                        int simdEnd = col + (colsInBlock / Vector<float>.Count) * Vector<float>.Count;

                        for (; ci < simdEnd; ci += Vector<float>.Count)
                        {
                            // Widen sbyte → float (no SIMD intrinsic for sbyte→float, unroll manually)
                            // Use a small stackalloc to stage the conversion
                            Span<float> bFloats = stackalloc float[Vector<float>.Count];
                            int dataOffset = bRowOffset + ci;
                            for (int vi = 0; vi < Vector<float>.Count; vi++)
                            {
                                bFloats[vi] = b.Data[dataOffset + vi];  // sbyte implicit → float
                            }

                            var vB = new Vector<float>(bFloats);
                            var vC = new Vector<float>(c.Slice(ci));
                            (vC + vScale * vB).CopyTo(c.Slice(ci));
                        }
                    }

                    // Scalar remainder for this block span
                    for (; ci < colEnd; ci++)
                    {
                        c[ci] += aTimesScale * b.Data[bRowOffset + ci];
                    }

                    col = colEnd;
                }
            }
        }
    }
}

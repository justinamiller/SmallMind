using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace SmallMind.Simd
{
    /// <summary>
    /// SIMD-accelerated matrix multiplication operations.
    /// Provides optimized implementations using AVX2, AVX-512, and Vector&lt;T&gt; fallbacks.
    /// Uses cache-friendly algorithms and parallel processing for large matrices.
    /// </summary>
    public static class MatMulOps
    {
        private const int PARALLEL_THRESHOLD = 32; // Rows threshold for parallelization
        private const int TILE_SIZE = 32; // Cache tile size for blocking

        /// <summary>
        /// Enhanced matrix multiplication: C = A × B
        /// A: (M × K), B: (K × N), C: (M × N)
        /// Automatically selects best implementation based on CPU capabilities.
        /// </summary>
        public static void MatMul(
            float[] A, float[] B, float[] C,
            int M, int K, int N)
        {
            if (A.Length != M * K || B.Length != K * N || C.Length != M * N)
                throw new ArgumentException("Matrix dimensions don't match buffer sizes");

            // Clear output
            Array.Clear(C, 0, C.Length);

            // Select best implementation based on CPU capabilities
            if (Avx2.IsSupported && Fma.IsSupported && K >= 8)
            {
                MatMulAvx2(A, B, C, M, K, N);
            }
            else if (Avx.IsSupported && K >= 8)
            {
                MatMulAvx(A, B, C, M, K, N);
            }
            else
            {
                MatMulVector(A, B, C, M, K, N);
            }
        }

        /// <summary>
        /// AVX2 + FMA implementation (256-bit, 8 floats per vector)
        /// Uses cache-friendly ikj loop order for better performance.
        /// </summary>
        private static void MatMulAvx2(
            float[] A, float[] B, float[] C,
            int M, int K, int N)
        {
            const int vecSize = 8; // AVX2 processes 8 floats

            if (M >= PARALLEL_THRESHOLD)
            {
                Parallel.For(0, M, i =>
                {
                    MatMulAvx2RowIndexed(A, B, C, i, M, K, N, vecSize);
                });
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    MatMulAvx2Row(A, B, C, i, M, K, N, vecSize);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulAvx2RowIndexed(
            float[] A, float[] B, float[] C,
            int i, int M, int K, int N, int vecSize)
        {
            int aRowStart = i * K;
            int cRowStart = i * N;

            fixed (float* pA = A, pB = B, pC = C)
            {
                for (int k = 0; k < K; k++)
                {
                    // Broadcast A[i,k] to vector
                    Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                    int bRowStart = k * N;

                    int j = 0;

                    // SIMD loop with FMA
                    for (; j <= N - vecSize; j += vecSize)
                    {
                        Vector256<float> vB = Avx.LoadVector256(pB + bRowStart + j);
                        Vector256<float> vC = Avx.LoadVector256(pC + cRowStart + j);

                        // C += A * B (fused multiply-add)
                        vC = Fma.MultiplyAdd(vA, vB, vC);
                        Avx.Store(pC + cRowStart + j, vC);
                    }

                    // Scalar remainder
                    float aVal = pA[aRowStart + k];
                    for (; j < N; j++)
                    {
                        pC[cRowStart + j] += aVal * pB[bRowStart + j];
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulAvx2Row(
            float[] A, float[] B, float[] C,
            int i, int M, int K, int N, int vecSize)
        {
            MatMulAvx2RowIndexed(A, B, C, i, M, K, N, vecSize);
        }

        /// <summary>
        /// AVX implementation (256-bit, 8 floats per vector, no FMA)
        /// </summary>
        private static void MatMulAvx(
            float[] A, float[] B, float[] C,
            int M, int K, int N)
        {
            const int vecSize = 8;

            if (M >= PARALLEL_THRESHOLD)
            {
                Parallel.For(0, M, i =>
                {
                    MatMulAvxRowIndexed(A, B, C, i, M, K, N, vecSize);
                });
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    MatMulAvxRow(A, B, C, i, M, K, N, vecSize);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulAvxRowIndexed(
            float[] A, float[] B, float[] C,
            int i, int M, int K, int N, int vecSize)
        {
            int aRowStart = i * K;
            int cRowStart = i * N;

            fixed (float* pA = A, pB = B, pC = C)
            {
                for (int k = 0; k < K; k++)
                {
                    Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                    int bRowStart = k * N;

                    int j = 0;

                    // SIMD loop (multiply + add separately, no FMA)
                    for (; j <= N - vecSize; j += vecSize)
                    {
                        Vector256<float> vB = Avx.LoadVector256(pB + bRowStart + j);
                        Vector256<float> vC = Avx.LoadVector256(pC + cRowStart + j);

                        // C += A * B
                        vC = Avx.Add(vC, Avx.Multiply(vA, vB));
                        Avx.Store(pC + cRowStart + j, vC);
                    }

                    // Scalar remainder
                    float aVal = pA[aRowStart + k];
                    for (; j < N; j++)
                    {
                        pC[cRowStart + j] += aVal * pB[bRowStart + j];
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulAvxRow(
            float[] A, float[] B, float[] C,
            int i, int M, int K, int N, int vecSize)
        {
            MatMulAvxRowIndexed(A, B, C, i, M, K, N, vecSize);
        }

        /// <summary>
        /// Vector&lt;T&gt; fallback implementation (portable SIMD)
        /// </summary>
        private static void MatMulVector(
            float[] A, float[] B, float[] C,
            int M, int K, int N)
        {
            int vectorSize = Vector<float>.Count;

            if (M >= PARALLEL_THRESHOLD)
            {
                Parallel.For(0, M, i =>
                {
                    MatMulVectorRowIndexed(A, B, C, i, K, N, vectorSize);
                });
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    MatMulVectorRow(A, B, C, i, K, N, vectorSize);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MatMulVectorRowIndexed(
            float[] A, float[] B, float[] C,
            int i, int K, int N, int vectorSize)
        {
            int aRowStart = i * K;
            int cRowStart = i * N;

            ReadOnlySpan<float> ASpan = A;
            ReadOnlySpan<float> BSpan = B;
            Span<float> CSpan = C;

            for (int k = 0; k < K; k++)
            {
                float aVal = ASpan[aRowStart + k];
                var vA = new Vector<float>(aVal);
                int bRowStart = k * N;

                int j = 0;

                // SIMD loop
                for (; j <= N - vectorSize; j += vectorSize)
                {
                    var vB = new Vector<float>(BSpan.Slice(bRowStart + j));
                    var vC = new Vector<float>(CSpan.Slice(cRowStart + j));
                    (vC + vA * vB).CopyTo(CSpan.Slice(cRowStart + j));
                }

                // Scalar remainder
                for (; j < N; j++)
                {
                    CSpan[cRowStart + j] += aVal * BSpan[bRowStart + j];
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MatMulVectorRow(
            float[] A, float[] B, float[] C,
            int i, int K, int N, int vectorSize)
        {
            MatMulVectorRowIndexed(A, B, C, i, K, N, vectorSize);
        }

        /// <summary>
        /// Dot product: sum(a[i] * b[i])
        /// Uses SIMD for acceleration with AVX2/FMA when available.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vectors must have the same length");

            int length = a.Length;

            // Use AVX2 + FMA for best performance
            if (Avx2.IsSupported && Fma.IsSupported && length >= 8)
            {
                return DotProductAvx2(a, b);
            }
            else if (Avx.IsSupported && length >= 8)
            {
                return DotProductAvx(a, b);
            }
            else
            {
                return DotProductVector(a, b);
            }
        }

        /// <summary>
        /// AVX2 + FMA dot product implementation with optimized horizontal sum.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float DotProductAvx2(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int length = a.Length;
            const int vecSize = 8;
            int i = 0;

            Vector256<float> sum1 = Vector256<float>.Zero;
            Vector256<float> sum2 = Vector256<float>.Zero;
            Vector256<float> sum3 = Vector256<float>.Zero;
            Vector256<float> sum4 = Vector256<float>.Zero;

            fixed (float* pA = a, pB = b)
            {
                // Process 4 vectors at a time for better ILP (instruction-level parallelism)
                for (; i <= length - vecSize * 4; i += vecSize * 4)
                {
                    var va1 = Avx.LoadVector256(pA + i);
                    var vb1 = Avx.LoadVector256(pB + i);
                    sum1 = Fma.MultiplyAdd(va1, vb1, sum1);

                    var va2 = Avx.LoadVector256(pA + i + vecSize);
                    var vb2 = Avx.LoadVector256(pB + i + vecSize);
                    sum2 = Fma.MultiplyAdd(va2, vb2, sum2);

                    var va3 = Avx.LoadVector256(pA + i + vecSize * 2);
                    var vb3 = Avx.LoadVector256(pB + i + vecSize * 2);
                    sum3 = Fma.MultiplyAdd(va3, vb3, sum3);

                    var va4 = Avx.LoadVector256(pA + i + vecSize * 3);
                    var vb4 = Avx.LoadVector256(pB + i + vecSize * 3);
                    sum4 = Fma.MultiplyAdd(va4, vb4, sum4);
                }

                // Combine the 4 accumulators
                sum1 = Avx.Add(sum1, sum2);
                sum3 = Avx.Add(sum3, sum4);
                sum1 = Avx.Add(sum1, sum3);

                // Process remaining full vectors
                for (; i <= length - vecSize; i += vecSize)
                {
                    var va = Avx.LoadVector256(pA + i);
                    var vb = Avx.LoadVector256(pB + i);
                    sum1 = Fma.MultiplyAdd(va, vb, sum1);
                }

                // Horizontal sum using AVX2
                // sum1 = [a0, a1, a2, a3, a4, a5, a6, a7]
                Vector128<float> low = sum1.GetLower();   // [a0, a1, a2, a3]
                Vector128<float> high = sum1.GetUpper();  // [a4, a5, a6, a7]
                Vector128<float> sum128 = Sse.Add(low, high); // [a0+a4, a1+a5, a2+a6, a3+a7]

                // Continue horizontal sum
                Vector128<float> shuf = Sse.Shuffle(sum128, sum128, 0b_00_01_10_11); // [a2+a6, a3+a7, a0+a4, a1+a5]
                sum128 = Sse.Add(sum128, shuf); // [a0+a2+a4+a6, a1+a3+a5+a7, ...]
                
                shuf = Sse.MoveHighToLow(shuf, sum128); // [a1+a3+a5+a7, ...]
                sum128 = Sse.Add(sum128, shuf); // [sum, ...]

                float sum = sum128.ToScalar();

                // Scalar remainder
                for (; i < length; i++)
                {
                    sum += pA[i] * pB[i];
                }

                return sum;
            }
        }

        /// <summary>
        /// AVX dot product implementation (no FMA).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe float DotProductAvx(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int length = a.Length;
            const int vecSize = 8;
            int i = 0;

            Vector256<float> sum1 = Vector256<float>.Zero;
            Vector256<float> sum2 = Vector256<float>.Zero;

            fixed (float* pA = a, pB = b)
            {
                // Process 2 vectors at a time
                for (; i <= length - vecSize * 2; i += vecSize * 2)
                {
                    var va1 = Avx.LoadVector256(pA + i);
                    var vb1 = Avx.LoadVector256(pB + i);
                    sum1 = Avx.Add(sum1, Avx.Multiply(va1, vb1));

                    var va2 = Avx.LoadVector256(pA + i + vecSize);
                    var vb2 = Avx.LoadVector256(pB + i + vecSize);
                    sum2 = Avx.Add(sum2, Avx.Multiply(va2, vb2));
                }

                sum1 = Avx.Add(sum1, sum2);

                // Process remaining full vectors
                for (; i <= length - vecSize; i += vecSize)
                {
                    var va = Avx.LoadVector256(pA + i);
                    var vb = Avx.LoadVector256(pB + i);
                    sum1 = Avx.Add(sum1, Avx.Multiply(va, vb));
                }

                // Horizontal sum
                Vector128<float> low = sum1.GetLower();
                Vector128<float> high = sum1.GetUpper();
                Vector128<float> sum128 = Sse.Add(low, high);

                Vector128<float> shuf = Sse.Shuffle(sum128, sum128, 0b_00_01_10_11);
                sum128 = Sse.Add(sum128, shuf);
                
                shuf = Sse.MoveHighToLow(shuf, sum128);
                sum128 = Sse.Add(sum128, shuf);

                float sum = sum128.ToScalar();

                // Scalar remainder
                for (; i < length; i++)
                {
                    sum += pA[i] * pB[i];
                }

                return sum;
            }
        }

        /// <summary>
        /// Vector&lt;T&gt; fallback dot product implementation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float DotProductVector(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            int length = a.Length;
            int vectorSize = Vector<float>.Count;
            int i = 0;

            var sumVec = Vector<float>.Zero;

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i));
                var vb = new Vector<float>(b.Slice(i));
                sumVec += va * vb;
            }

            // Horizontal sum
            float sum = 0f;
            for (int j = 0; j < vectorSize; j++)
            {
                sum += sumVec[j];
            }

            // Scalar remainder
            for (; i < length; i++)
            {
                sum += a[i] * b[i];
            }

            return sum;
        }
    }
}

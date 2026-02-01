using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading.Tasks;

namespace SmallMind.Core.Simd
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
        /// Uses SIMD for acceleration.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProduct(ReadOnlySpan<float> a, ReadOnlySpan<float> b)
        {
            if (a.Length != b.Length)
                throw new ArgumentException("Vectors must have the same length");

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

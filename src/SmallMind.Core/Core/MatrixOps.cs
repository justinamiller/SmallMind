using System.Numerics;
using System.Runtime.CompilerServices;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Optimized matrix operations for backward pass efficiency.
    /// Implements transposed matrix multiply without creating copies.
    /// </summary>
    internal static class MatrixOps
    {
        /// <summary>
        /// Matrix multiplication: C = A × B^T (B transposed) without creating transposed copy.
        /// A: (M × K), B: (N × K) treated as K × N via transpose
        /// C: (M × N)
        /// </summary>
        public static void MatMulTransposeB(
            float[] A,      // M × K
            float[] B,      // N × K (will be treated as K × N via transpose)
            float[] C,      // M × N
            int M, int K, int N)
        {
            // C = A × B^T without creating transposed copy
            // Access pattern: B[j,k] instead of B[k,j]

            int vectorSize = Vector<float>.Count;

            // Parallelize when M >= 32 to amortize overhead
            if (M >= 32)
            {
                Parallel.For(0, M, i =>
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;

                    for (int j = 0; j < N; j++)
                    {
                        int bRowStart = j * K;  // B is N×K, we want column j of B^T = row j of B

                        float sum = 0f;
                        int k = 0;

                        // SIMD dot product
                        for (; k <= K - vectorSize; k += vectorSize)
                        {
                            var va = new Vector<float>(A, aRowStart + k);
                            var vb = new Vector<float>(B, bRowStart + k);
                            sum += Vector.Dot(va, vb);
                        }

                        // Remainder
                        for (; k < K; k++)
                        {
                            sum += A[aRowStart + k] * B[bRowStart + k];
                        }

                        C[cRowStart + j] = sum;
                    }
                });
            }
            else
            {
                // Sequential for small matrices
                for (int i = 0; i < M; i++)
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;

                    for (int j = 0; j < N; j++)
                    {
                        int bRowStart = j * K;

                        float sum = 0f;
                        int k = 0;

                        // SIMD dot product
                        for (; k <= K - vectorSize; k += vectorSize)
                        {
                            var va = new Vector<float>(A, aRowStart + k);
                            var vb = new Vector<float>(B, bRowStart + k);
                            sum += Vector.Dot(va, vb);
                        }

                        // Remainder
                        for (; k < K; k++)
                        {
                            sum += A[aRowStart + k] * B[bRowStart + k];
                        }

                        C[cRowStart + j] = sum;
                    }
                }
            }
        }

        /// <summary>
        /// Matrix multiplication: C = A^T × B (A transposed) without creating transposed copy.
        /// A: (K × M) treated as M × K via transpose
        /// B: (K × N)
        /// C: (M × N)
        /// </summary>
        public static void MatMulTransposeA(
            float[] A,      // K × M (will be treated as M × K via transpose)
            float[] B,      // K × N
            float[] C,      // M × N
            int M, int K, int N)
        {
            // C = A^T × B without creating transposed copy
            // Access pattern: A[k,i] instead of A[i,k]

            // Zero output first (we're accumulating) - use Span.Clear for better performance
            C.AsSpan().Clear();

            int vectorSize = Vector<float>.Count;

            // Parallelize when M >= 32
            if (M >= 32)
            {
                Parallel.For(0, M, i =>
                {
                    int cRowStart = i * N;

                    for (int k = 0; k < K; k++)
                    {
                        float aVal = A[k * M + i];  // A[k,i] = A^T[i,k]
                        int bRowStart = k * N;

                        // SIMD scalar-vector multiply and add
                        int j = 0;
                        var vA = new Vector<float>(aVal);

                        for (; j <= N - vectorSize; j += vectorSize)
                        {
                            var vb = new Vector<float>(B, bRowStart + j);
                            var vc = new Vector<float>(C, cRowStart + j);
                            (vc + vA * vb).CopyTo(C, cRowStart + j);
                        }

                        for (; j < N; j++)
                        {
                            C[cRowStart + j] += aVal * B[bRowStart + j];
                        }
                    }
                });
            }
            else
            {
                // Sequential for small matrices
                for (int i = 0; i < M; i++)
                {
                    int cRowStart = i * N;

                    for (int k = 0; k < K; k++)
                    {
                        float aVal = A[k * M + i];  // A[k,i] = A^T[i,k]
                        int bRowStart = k * N;

                        // SIMD scalar-vector multiply and add
                        int j = 0;
                        var vA = new Vector<float>(aVal);

                        for (; j <= N - vectorSize; j += vectorSize)
                        {
                            var vb = new Vector<float>(B, bRowStart + j);
                            var vc = new Vector<float>(C, cRowStart + j);
                            (vc + vA * vb).CopyTo(C, cRowStart + j);
                        }

                        for (; j < N; j++)
                        {
                            C[cRowStart + j] += aVal * B[bRowStart + j];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// GELU derivative - used in fused backward operations
        /// Approximation: sigmoid(1.702x) based
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GELUDerivative(float x)
        {
            // Derivative of GELU ≈ sigmoid(1.702x) + x * sigmoid(1.702x) * (1 - sigmoid(1.702x)) * 1.702
            float sig = 1f / (1f + MathF.Exp(-1.702f * x));
            return sig + x * sig * (1f - sig) * 1.702f;
        }
    }
}

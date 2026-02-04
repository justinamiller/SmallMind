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
        /// Enhanced matrix multiplication with Span overload: C = A × B
        /// A: (M × K), B: (K × N), C: (M × N)
        /// Zero-allocation version that works directly on spans.
        /// </summary>
        public static void MatMul(
            ReadOnlySpan<float> A, ReadOnlySpan<float> B, Span<float> C,
            int M, int K, int N)
        {
            if (A.Length < M * K || B.Length < K * N || C.Length < M * N)
                throw new ArgumentException("Matrix dimensions don't match buffer sizes");

            // Clear output
            C.Clear();

            // Use unsafe fixed pointers for SIMD operations
            unsafe
            {
                fixed (float* pA = A, pB = B, pC = C)
                {
                    // Select best implementation based on CPU capabilities
                    if (Avx2.IsSupported && Fma.IsSupported && K >= 8)
                    {
                        MatMulAvx2Unsafe(pA, pB, pC, M, K, N);
                    }
                    else if (Avx.IsSupported && K >= 8)
                    {
                        MatMulAvxUnsafe(pA, pB, pC, M, K, N);
                    }
                    else
                    {
                        MatMulVectorUnsafe(pA, pB, pC, M, K, N);
                    }
                }
            }
        }

        /// <summary>
        /// AVX2 + FMA implementation (256-bit, 8 floats per vector)
        /// Uses cache-friendly ikj loop order with tiled blocking for better cache utilization.
        /// For 512×512 matrices, always use tiling for optimal cache performance.
        /// </summary>
        private static void MatMulAvx2(
            float[] A, float[] B, float[] C,
            int M, int K, int N)
        {
            const int vecSize = 8; // AVX2 processes 8 floats

            // Use cache blocking for better L1/L2 cache utilization
            // Always use tiling for matrices >= 512, or when all dimensions are >= TILE_SIZE
            // This ensures 512×512 matrices always get tiled multiplication
            if ((M >= 512 && K >= 512 && N >= 512) || 
                (M >= TILE_SIZE && K >= TILE_SIZE && N >= TILE_SIZE))
            {
                MatMulAvx2Tiled(A, B, C, M, K, N, vecSize);
            }
            else if (M >= PARALLEL_THRESHOLD)
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

        /// <summary>
        /// Cache-blocked (tiled) AVX2 matrix multiplication for better cache utilization.
        /// Processes the matrix in TILE_SIZE x TILE_SIZE blocks to maximize L1 cache hits.
        /// Uses parallelization over row tiles for large matrices to maintain multi-threading benefits.
        /// </summary>
        private static void MatMulAvx2Tiled(
            float[] A, float[] B, float[] C,
            int M, int K, int N, int vecSize)
        {
            // Parallelize over row tiles for large matrices to maintain thread utilization
            int numRowTiles = (M + TILE_SIZE - 1) / TILE_SIZE;
            
            if (numRowTiles >= 4) // Use parallelization when we have enough tiles to distribute
            {
                Parallel.For(0, numRowTiles, i0Tile =>
                {
                    unsafe
                    {
                        fixed (float* pA = A, pB = B, pC = C)
                        {
                            int i0 = i0Tile * TILE_SIZE;
                            int iMax = Math.Min(i0 + TILE_SIZE, M);
                            
                            for (int k0 = 0; k0 < K; k0 += TILE_SIZE)
                            {
                                int kMax = Math.Min(k0 + TILE_SIZE, K);
                                
                                for (int j0 = 0; j0 < N; j0 += TILE_SIZE)
                                {
                                    int jMax = Math.Min(j0 + TILE_SIZE, N);
                                    
                                    // Process this tile
                                    for (int i = i0; i < iMax; i++)
                                    {
                                        int aRowStart = i * K;
                                        int cRowStart = i * N;
                                        
                                        for (int k = k0; k < kMax; k++)
                                        {
                                            Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                                            int bRowStart = k * N;
                                            
                                            int j = j0;
                                            
                                            // SIMD loop within tile
                                            for (; j <= jMax - vecSize; j += vecSize)
                                            {
                                                Vector256<float> vB = Avx.LoadVector256(pB + bRowStart + j);
                                                Vector256<float> vC = Avx.LoadVector256(pC + cRowStart + j);
                                                vC = Fma.MultiplyAdd(vA, vB, vC);
                                                Avx.Store(pC + cRowStart + j, vC);
                                            }
                                            
                                            // Scalar remainder within tile
                                            float aVal = pA[aRowStart + k];
                                            for (; j < jMax; j++)
                                            {
                                                pC[cRowStart + j] += aVal * pB[bRowStart + j];
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }
            else
            {
                // Sequential for smaller matrices
                unsafe
                {
                    fixed (float* pA = A, pB = B, pC = C)
                    {
                        for (int i0 = 0; i0 < M; i0 += TILE_SIZE)
                        {
                            int iMax = Math.Min(i0 + TILE_SIZE, M);
                            
                            for (int k0 = 0; k0 < K; k0 += TILE_SIZE)
                            {
                                int kMax = Math.Min(k0 + TILE_SIZE, K);
                                
                                for (int j0 = 0; j0 < N; j0 += TILE_SIZE)
                                {
                                    int jMax = Math.Min(j0 + TILE_SIZE, N);
                                    
                                    // Process this tile
                                    for (int i = i0; i < iMax; i++)
                                    {
                                        int aRowStart = i * K;
                                        int cRowStart = i * N;
                                        
                                        for (int k = k0; k < kMax; k++)
                                        {
                                            Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                                            int bRowStart = k * N;
                                            
                                            int j = j0;
                                            
                                            // SIMD loop within tile
                                            for (; j <= jMax - vecSize; j += vecSize)
                                            {
                                                Vector256<float> vB = Avx.LoadVector256(pB + bRowStart + j);
                                                Vector256<float> vC = Avx.LoadVector256(pC + cRowStart + j);
                                                vC = Fma.MultiplyAdd(vA, vB, vC);
                                                Avx.Store(pC + cRowStart + j, vC);
                                            }
                                            
                                            // Scalar remainder within tile
                                            float aVal = pA[aRowStart + k];
                                            for (; j < jMax; j++)
                                            {
                                                pC[cRowStart + j] += aVal * pB[bRowStart + j];
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
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
        /// Uses cache-friendly tiled blocking for better cache utilization.
        /// For 512×512 matrices, always use tiling for optimal cache performance.
        /// </summary>
        private static void MatMulVector(
            float[] A, float[] B, float[] C,
            int M, int K, int N)
        {
            int vectorSize = Vector<float>.Count;

            // Use cache blocking for better L1/L2 cache utilization
            // Always use tiling for matrices >= 512, or when all dimensions are >= TILE_SIZE
            if ((M >= 512 && K >= 512 && N >= 512) || 
                (M >= TILE_SIZE && K >= TILE_SIZE && N >= TILE_SIZE))
            {
                MatMulVectorTiled(A, B, C, M, K, N, vectorSize);
            }
            else if (M >= PARALLEL_THRESHOLD)
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

        /// <summary>
        /// Cache-blocked (tiled) Vector implementation for better cache utilization.
        /// Uses parallelization over row tiles for large matrices to maintain multi-threading benefits.
        /// </summary>
        private static void MatMulVectorTiled(
            float[] A, float[] B, float[] C,
            int M, int K, int N, int vectorSize)
        {
            // Parallelize over row tiles for large matrices
            int numRowTiles = (M + TILE_SIZE - 1) / TILE_SIZE;
            
            if (numRowTiles >= 4) // Use parallelization when we have enough tiles to distribute
            {
                Parallel.For(0, numRowTiles, i0Tile =>
                {
                    ReadOnlySpan<float> ASpanLocal = A;
                    ReadOnlySpan<float> BSpanLocal = B;
                    Span<float> CSpanLocal = C;
                    
                    int i0 = i0Tile * TILE_SIZE;
                    int iMax = Math.Min(i0 + TILE_SIZE, M);
                    
                    for (int k0 = 0; k0 < K; k0 += TILE_SIZE)
                    {
                        int kMax = Math.Min(k0 + TILE_SIZE, K);
                        
                        for (int j0 = 0; j0 < N; j0 += TILE_SIZE)
                        {
                            int jMax = Math.Min(j0 + TILE_SIZE, N);
                            
                            // Process this tile
                            for (int i = i0; i < iMax; i++)
                            {
                                int aRowStart = i * K;
                                int cRowStart = i * N;
                                
                                for (int k = k0; k < kMax; k++)
                                {
                                    float aVal = ASpanLocal[aRowStart + k];
                                    var vA = new Vector<float>(aVal);
                                    int bRowStart = k * N;
                                    
                                    int j = j0;
                                    
                                    // SIMD loop within tile
                                    for (; j <= jMax - vectorSize; j += vectorSize)
                                    {
                                        var vB = new Vector<float>(BSpanLocal.Slice(bRowStart + j));
                                        var vC = new Vector<float>(CSpanLocal.Slice(cRowStart + j));
                                        (vC + vA * vB).CopyTo(CSpanLocal.Slice(cRowStart + j));
                                    }
                                    
                                    // Scalar remainder within tile
                                    for (; j < jMax; j++)
                                    {
                                        CSpanLocal[cRowStart + j] += aVal * BSpanLocal[bRowStart + j];
                                    }
                                }
                            }
                        }
                    }
                });
            }
            else
            {
                // Sequential for smaller matrices
                ReadOnlySpan<float> ASpan = A;
                ReadOnlySpan<float> BSpan = B;
                Span<float> CSpan = C;
                
                for (int i0 = 0; i0 < M; i0 += TILE_SIZE)
                {
                    int iMax = Math.Min(i0 + TILE_SIZE, M);
                    
                    for (int k0 = 0; k0 < K; k0 += TILE_SIZE)
                    {
                        int kMax = Math.Min(k0 + TILE_SIZE, K);
                        
                        for (int j0 = 0; j0 < N; j0 += TILE_SIZE)
                        {
                            int jMax = Math.Min(j0 + TILE_SIZE, N);
                            
                            // Process this tile
                            for (int i = i0; i < iMax; i++)
                            {
                                int aRowStart = i * K;
                                int cRowStart = i * N;
                                
                                for (int k = k0; k < kMax; k++)
                                {
                                    float aVal = ASpan[aRowStart + k];
                                    var vA = new Vector<float>(aVal);
                                    int bRowStart = k * N;
                                    
                                    int j = j0;
                                    
                                    // SIMD loop within tile
                                    for (; j <= jMax - vectorSize; j += vectorSize)
                                    {
                                        var vB = new Vector<float>(BSpan.Slice(bRowStart + j));
                                        var vC = new Vector<float>(CSpan.Slice(cRowStart + j));
                                        (vC + vA * vB).CopyTo(CSpan.Slice(cRowStart + j));
                                    }
                                    
                                    // Scalar remainder within tile
                                    for (; j < jMax; j++)
                                    {
                                        CSpan[cRowStart + j] += aVal * BSpan[bRowStart + j];
                                    }
                                }
                            }
                        }
                    }
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
        /// Unsafe AVX2 + FMA implementation working directly on pointers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulAvx2Unsafe(
            float* pA, float* pB, float* pC,
            int M, int K, int N)
        {
            const int vecSize = 8;

            if (M >= PARALLEL_THRESHOLD)
            {
                Parallel.For(0, M, i =>
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;

                    for (int k = 0; k < K; k++)
                    {
                        Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                        int bRowStart = k * N;
                        int j = 0;

                        for (; j <= N - vecSize; j += vecSize)
                        {
                            Vector256<float> vB = Avx.LoadVector256(pB + bRowStart + j);
                            Vector256<float> vC = Avx.LoadVector256(pC + cRowStart + j);
                            vC = Fma.MultiplyAdd(vA, vB, vC);
                            Avx.Store(pC + cRowStart + j, vC);
                        }

                        float aVal = pA[aRowStart + k];
                        for (; j < N; j++)
                        {
                            pC[cRowStart + j] += aVal * pB[bRowStart + j];
                        }
                    }
                });
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;

                    for (int k = 0; k < K; k++)
                    {
                        Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                        int bRowStart = k * N;
                        int j = 0;

                        for (; j <= N - vecSize; j += vecSize)
                        {
                            Vector256<float> vB = Avx.LoadVector256(pB + bRowStart + j);
                            Vector256<float> vC = Avx.LoadVector256(pC + cRowStart + j);
                            vC = Fma.MultiplyAdd(vA, vB, vC);
                            Avx.Store(pC + cRowStart + j, vC);
                        }

                        float aVal = pA[aRowStart + k];
                        for (; j < N; j++)
                        {
                            pC[cRowStart + j] += aVal * pB[bRowStart + j];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Unsafe AVX implementation working directly on pointers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulAvxUnsafe(
            float* pA, float* pB, float* pC,
            int M, int K, int N)
        {
            const int vecSize = 8;

            if (M >= PARALLEL_THRESHOLD)
            {
                Parallel.For(0, M, i =>
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;

                    for (int k = 0; k < K; k++)
                    {
                        Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                        int bRowStart = k * N;
                        int j = 0;

                        for (; j <= N - vecSize; j += vecSize)
                        {
                            Vector256<float> vB = Avx.LoadVector256(pB + bRowStart + j);
                            Vector256<float> vC = Avx.LoadVector256(pC + cRowStart + j);
                            vC = Avx.Add(vC, Avx.Multiply(vA, vB));
                            Avx.Store(pC + cRowStart + j, vC);
                        }

                        float aVal = pA[aRowStart + k];
                        for (; j < N; j++)
                        {
                            pC[cRowStart + j] += aVal * pB[bRowStart + j];
                        }
                    }
                });
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;

                    for (int k = 0; k < K; k++)
                    {
                        Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                        int bRowStart = k * N;
                        int j = 0;

                        for (; j <= N - vecSize; j += vecSize)
                        {
                            Vector256<float> vB = Avx.LoadVector256(pB + bRowStart + j);
                            Vector256<float> vC = Avx.LoadVector256(pC + cRowStart + j);
                            vC = Avx.Add(vC, Avx.Multiply(vA, vB));
                            Avx.Store(pC + cRowStart + j, vC);
                        }

                        float aVal = pA[aRowStart + k];
                        for (; j < N; j++)
                        {
                            pC[cRowStart + j] += aVal * pB[bRowStart + j];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Unsafe Vector&lt;T&gt; fallback implementation working directly on pointers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulVectorUnsafe(
            float* pA, float* pB, float* pC,
            int M, int K, int N)
        {
            int vectorSize = Vector<float>.Count;

            if (M >= PARALLEL_THRESHOLD)
            {
                Parallel.For(0, M, i =>
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;

                    for (int k = 0; k < K; k++)
                    {
                        float aVal = pA[aRowStart + k];
                        int bRowStart = k * N;
                        int j = 0;

                        Vector<float> vA = new Vector<float>(aVal);
                        for (; j <= N - vectorSize; j += vectorSize)
                        {
                            Vector<float> vB = System.Runtime.CompilerServices.Unsafe.Read<Vector<float>>(pB + bRowStart + j);
                            Vector<float> vC = System.Runtime.CompilerServices.Unsafe.Read<Vector<float>>(pC + cRowStart + j);
                            System.Runtime.CompilerServices.Unsafe.Write(pC + cRowStart + j, vC + vA * vB);
                        }

                        for (; j < N; j++)
                        {
                            pC[cRowStart + j] += aVal * pB[bRowStart + j];
                        }
                    }
                });
            }
            else
            {
                for (int i = 0; i < M; i++)
                {
                    int aRowStart = i * K;
                    int cRowStart = i * N;

                    for (int k = 0; k < K; k++)
                    {
                        float aVal = pA[aRowStart + k];
                        int bRowStart = k * N;
                        int j = 0;

                        Vector<float> vA = new Vector<float>(aVal);
                        for (; j <= N - vectorSize; j += vectorSize)
                        {
                            Vector<float> vB = System.Runtime.CompilerServices.Unsafe.Read<Vector<float>>(pB + bRowStart + j);
                            Vector<float> vC = System.Runtime.CompilerServices.Unsafe.Read<Vector<float>>(pC + cRowStart + j);
                            System.Runtime.CompilerServices.Unsafe.Write(pC + cRowStart + j, vC + vA * vB);
                        }

                        for (; j < N; j++)
                        {
                            pC[cRowStart + j] += aVal * pB[bRowStart + j];
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Matrix multiplication with B transposed: C = A × B^T
        /// A: (M × K), B: (N × K) [stored in row-major, will be accessed as transposed], C: (M × N)
        /// This is optimized for computing attention scores: Q @ K^T
        /// where Q and K have shape (T × headSize) and we compute (T × T) scores.
        /// </summary>
        public static void MatMulTransposeB(
            ReadOnlySpan<float> A, ReadOnlySpan<float> B, Span<float> C,
            int M, int K, int N)
        {
            if (A.Length < M * K || B.Length < N * K || C.Length < M * N)
                throw new ArgumentException("Matrix dimensions don't match buffer sizes");

            // Clear output
            C.Clear();

            // For attention: A is Q (T × headSize), B is K (T × headSize)
            // We compute C = Q @ K^T (T × T)
            // This is more cache-friendly than transposing K first
            
            int vectorSize = Vector<float>.Count;
            
            for (int i = 0; i < M; i++)
            {
                int aRowStart = i * K;
                int cRowStart = i * N;
                
                for (int j = 0; j < N; j++)
                {
                    int bRowStart = j * K;  // B is stored row-major, treat as transpose
                    
                    // Dot product between A[i] and B[j] (which becomes B^T[:,j])
                    int k = 0;
                    var sumVec = Vector<float>.Zero;
                    
                    // SIMD loop
                    for (; k <= K - vectorSize; k += vectorSize)
                    {
                        var va = new Vector<float>(A.Slice(aRowStart + k, vectorSize));
                        var vb = new Vector<float>(B.Slice(bRowStart + k, vectorSize));
                        sumVec += va * vb;
                    }
                    
                    // Horizontal sum
                    float sum = 0f;
                    for (int v = 0; v < vectorSize; v++)
                    {
                        sum += sumVec[v];
                    }
                    
                    // Scalar remainder
                    for (; k < K; k++)
                    {
                        sum += A[aRowStart + k] * B[bRowStart + k];
                    }
                    
                    C[cRowStart + j] = sum;
                }
            }
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

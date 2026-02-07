using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
using System.Threading.Tasks;

namespace SmallMind.Core.Simd
{
    /// <summary>
    /// SIMD-accelerated matrix multiplication operations.
    /// Provides optimized implementations using AVX-512, AVX2, and Vector&lt;T&gt; fallbacks.
    /// Uses cache-friendly algorithms and parallel processing for large matrices.
    /// TIER-5 OPTIMIZATION: [SkipLocalsInit] on class to avoid zero-initialization overhead in hot methods.
    /// </summary>
    [SkipLocalsInit]
    public static class MatMulOps
    {
   // Parallelization threshold: Use Parallel.For only when M >= 128
        // Rationale: Thread overhead dominates for smaller matrices
        //   - 32×32: Parallel is 283% slower (overhead >> work)
        //   - 64×64: Parallel is 70% slower (overhead > work)
        //   - 128×128: Break-even point (overhead ≈ work)
        //   - 256×256+: Parallel is 44%+ faster (work >> overhead)
        private const int PARALLEL_THRESHOLD = 128;
        private const int TILE_SIZE = 32; // Cache tile size for blocking
        private const int VEC512_SIZE = 16; // AVX-512 vector width (16 floats)

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

            // NOTE: Caller must ensure C is zeroed before calling
            // Kernels use accumulation (C += A * B) via FMA operations

            // Select best implementation based on CPU capabilities
            if (Avx512F.IsSupported && K >= 16)
            {
                MatMulAvx512(A, B, C, M, K, N);
            }
            else if (Avx2.IsSupported && Fma.IsSupported && K >= 8)
            {
                MatMulAvx2(A, B, C, M, K, N);
            }
            else if (Avx.IsSupported && K >= 8)
            {
                MatMulAvx(A, B, C, M, K, N);
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                MatMulNeonTiled(A, B, C, M, K, N);
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

            // NOTE: Caller must ensure C is zeroed before calling
            // Kernels use accumulation (C += A * B) via FMA operations

            // Use unsafe fixed pointers for SIMD operations
            unsafe
            {
                fixed (float* pA = A, pB = B, pC = C)
                {
                    // Select best implementation based on CPU capabilities
                    if (Avx512F.IsSupported && K >= 16)
                    {
                        MatMulAvx512Unsafe(pA, pB, pC, M, K, N);
                    }
                    else if (Avx2.IsSupported && Fma.IsSupported && K >= 8)
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
        /// AVX-512 + FMA implementation (512-bit, 16 floats per vector)
        /// Uses register-blocked microkernel for optimal performance.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private static void MatMulAvx512(
            float[] A, float[] B, float[] C,
            int M, int K, int N)
        {
            // Use the optimized unsafe implementation with register blocking
            unsafe
            {
                fixed (float* pA = A, pB = B, pC = C)
                {
                    MatMulAvx512Unsafe(pA, pB, pC, M, K, N);
                }
            }
        }

        /// <summary>
        /// AVX2 + FMA implementation (256-bit, 8 floats per vector)
        /// Uses register-blocked microkernel for optimal performance.
        /// </summary>
        private static void MatMulAvx2(
            float[] A, float[] B, float[] C,
            int M, int K, int N)
        {
            // Use the optimized unsafe implementation with register blocking
            unsafe
            {
                fixed (float* pA = A, pB = B, pC = C)
                {
                    MatMulAvx2Unsafe(pA, pB, pC, M, K, N);
                }
            }
        }

        /// <summary>
        /// Cache-blocked (tiled) AVX-512 matrix multiplication for better cache utilization.
        /// Processes the matrix in TILE_SIZE x TILE_SIZE blocks to maximize L1 cache hits.
        /// Uses parallelization over row tiles for large matrices with loop unrolling for FMA saturation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private static void MatMulAvx512Tiled(
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
                                            Vector512<float> vA = Vector512.Create(pA[aRowStart + k]);
                                            int bRowStart = k * N;
                                            
                                            int j = j0;
                                            
                                            // Unrolled SIMD loop (2x unroll, 32 floats per iteration)
                                            for (; j <= jMax - (vecSize * 2); j += vecSize * 2)
                                            {
                                                Vector512<float> vB0 = Avx512F.LoadVector512(pB + bRowStart + j);
                                                Vector512<float> vC0 = Avx512F.LoadVector512(pC + cRowStart + j);
                                                vC0 = Avx512F.FusedMultiplyAdd(vA, vB0, vC0);
                                                Avx512F.Store(pC + cRowStart + j, vC0);
                                                
                                                Vector512<float> vB1 = Avx512F.LoadVector512(pB + bRowStart + j + vecSize);
                                                Vector512<float> vC1 = Avx512F.LoadVector512(pC + cRowStart + j + vecSize);
                                                vC1 = Avx512F.FusedMultiplyAdd(vA, vB1, vC1);
                                                Avx512F.Store(pC + cRowStart + j + vecSize, vC1);
                                            }
                                            
                                            // SIMD remainder (16 floats)
                                            for (; j <= jMax - vecSize; j += vecSize)
                                            {
                                                Vector512<float> vB = Avx512F.LoadVector512(pB + bRowStart + j);
                                                Vector512<float> vC = Avx512F.LoadVector512(pC + cRowStart + j);
                                                vC = Avx512F.FusedMultiplyAdd(vA, vB, vC);
                                                Avx512F.Store(pC + cRowStart + j, vC);
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
                                            Vector512<float> vA = Vector512.Create(pA[aRowStart + k]);
                                            int bRowStart = k * N;
                                            
                                            int j = j0;
                                            
                                            // Unrolled SIMD loop (2x unroll, 32 floats per iteration)
                                            for (; j <= jMax - (vecSize * 2); j += vecSize * 2)
                                            {
                                                Vector512<float> vB0 = Avx512F.LoadVector512(pB + bRowStart + j);
                                                Vector512<float> vC0 = Avx512F.LoadVector512(pC + cRowStart + j);
                                                vC0 = Avx512F.FusedMultiplyAdd(vA, vB0, vC0);
                                                Avx512F.Store(pC + cRowStart + j, vC0);
                                                
                                                Vector512<float> vB1 = Avx512F.LoadVector512(pB + bRowStart + j + vecSize);
                                                Vector512<float> vC1 = Avx512F.LoadVector512(pC + cRowStart + j + vecSize);
                                                vC1 = Avx512F.FusedMultiplyAdd(vA, vB1, vC1);
                                                Avx512F.Store(pC + cRowStart + j + vecSize, vC1);
                                            }
                                            
                                            // SIMD remainder (16 floats)
                                            for (; j <= jMax - vecSize; j += vecSize)
                                            {
                                                Vector512<float> vB = Avx512F.LoadVector512(pB + bRowStart + j);
                                                Vector512<float> vC = Avx512F.LoadVector512(pC + cRowStart + j);
                                                vC = Avx512F.FusedMultiplyAdd(vA, vB, vC);
                                                Avx512F.Store(pC + cRowStart + j, vC);
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

        /// <summary>
        /// AVX-512 row-wise matrix multiplication (for parallel processing).
        /// Processes a single row of the output matrix with 2x loop unrolling.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private static unsafe void MatMulAvx512RowIndexed(
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
                    Vector512<float> vA = Vector512.Create(pA[aRowStart + k]);
                    int bRowStart = k * N;

                    int j = 0;

                    // Unrolled SIMD loop (2x unroll, 32 floats per iteration)
                    for (; j <= N - (vecSize * 2); j += vecSize * 2)
                    {
                        Vector512<float> vB0 = Avx512F.LoadVector512(pB + bRowStart + j);
                        Vector512<float> vC0 = Avx512F.LoadVector512(pC + cRowStart + j);
                        vC0 = Avx512F.FusedMultiplyAdd(vA, vB0, vC0);
                        Avx512F.Store(pC + cRowStart + j, vC0);
                        
                        Vector512<float> vB1 = Avx512F.LoadVector512(pB + bRowStart + j + vecSize);
                        Vector512<float> vC1 = Avx512F.LoadVector512(pC + cRowStart + j + vecSize);
                        vC1 = Avx512F.FusedMultiplyAdd(vA, vB1, vC1);
                        Avx512F.Store(pC + cRowStart + j + vecSize, vC1);
                    }

                    // SIMD remainder (16 floats)
                    for (; j <= N - vecSize; j += vecSize)
                    {
                        Vector512<float> vB = Avx512F.LoadVector512(pB + bRowStart + j);
                        Vector512<float> vC = Avx512F.LoadVector512(pC + cRowStart + j);

                        // C += A * B (fused multiply-add)
                        vC = Avx512F.FusedMultiplyAdd(vA, vB, vC);
                        Avx512F.Store(pC + cRowStart + j, vC);
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
        [SkipLocalsInit]
        private static unsafe void MatMulAvx512Row(
            float[] A, float[] B, float[] C,
            int i, int M, int K, int N, int vecSize)
        {
            MatMulAvx512RowIndexed(A, B, C, i, M, K, N, vecSize);
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
            const int vecSize = 8; // AVX processes 8 floats
            const int TILING_THRESHOLD = 192; // Consistency with AVX2 and Vector implementations

            // For AVX (without FMA), keep it simple - just choose parallel vs sequential
            // No tiling implementation available for AVX-only path
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
            const int TILING_THRESHOLD = 192; // Use tiling for matrices >= 192×192 (36,864 elements)
            
            // Adaptive strategy:
            // - Small matrices (<192×192): Direct SIMD, minimal overhead
            // - Medium matrices (192-511): Tiling for cache efficiency
            // - Large matrices (512+): Tiling + parallelization for max performance
            
            int totalElements = M * N;
            bool shouldTile = (M >= TILING_THRESHOLD || N >= TILING_THRESHOLD || K >= TILING_THRESHOLD)
                             && totalElements >= (TILING_THRESHOLD * TILING_THRESHOLD);
            
            if (shouldTile)
            {
                // Use tiled implementation for better cache utilization
                MatMulVectorTiled(A, B, C, M, K, N, vectorSize);
            }
            else if (M >= PARALLEL_THRESHOLD)
            {
                // Direct SIMD with parallelization (no tiling overhead)
                Parallel.For(0, M, i =>
                {
                    MatMulVectorRowIndexed(A, B, C, i, K, N, vectorSize);
                });
            }
            else
            {
                // Direct SIMD sequential (best for small matrices)
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
        /// Unsafe AVX-512 + FMA implementation with register-blocked microkernel.
        /// Accumulates C tile in registers across K, then stores once per tile.
        /// Uses 16-wide vectors for maximum throughput on AVX-512 CPUs.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private static unsafe void MatMulAvx512Unsafe(
            float* pA, float* pB, float* pC,
            int M, int K, int N)
        {
            const int vecSize = VEC512_SIZE;
            const int TILE_M = 4;  // Process 4 rows of C at a time
            const int TILE_N = 64; // Process 64 cols of C at a time (4 vectors)

            if (M >= PARALLEL_THRESHOLD)
            {
                // Parallel over M tiles to reduce overhead
                int numMTiles = (M + TILE_M - 1) / TILE_M;
                Parallel.For(0, numMTiles, iTile =>
                {
                    int i0 = iTile * TILE_M;
                    int iMax = Math.Min(i0 + TILE_M, M);
                    MatMulAvx512TileKernel(pA, pB, pC, i0, iMax, K, N);
                });
            }
            else
            {
                MatMulAvx512TileKernel(pA, pB, pC, 0, M, K, N);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        [SkipLocalsInit]
        private static unsafe void MatMulAvx512TileKernel(
            float* pA, float* pB, float* pC,
            int i0, int iMax, int K, int N)
        {
            const int vecSize = VEC512_SIZE;

            for (int i = i0; i < iMax; i++)
            {
                int aRowStart = i * K;
                int cRowStart = i * N;
                
                // Process output row in tiles
                int j = 0;
                for (; j <= N - (vecSize * 4); j += vecSize * 4)
                {
                    // Register block: accumulate 4 vectors across K
                    Vector512<float> acc0 = Vector512<float>.Zero;
                    Vector512<float> acc1 = Vector512<float>.Zero;
                    Vector512<float> acc2 = Vector512<float>.Zero;
                    Vector512<float> acc3 = Vector512<float>.Zero;

                    // Accumulate across K dimension
                    for (int k = 0; k < K; k++)
                    {
                        Vector512<float> vA = Vector512.Create(pA[aRowStart + k]);
                        int bRowStart = k * N;

                        Vector512<float> vB0 = Avx512F.LoadVector512(pB + bRowStart + j);
                        Vector512<float> vB1 = Avx512F.LoadVector512(pB + bRowStart + j + vecSize);
                        Vector512<float> vB2 = Avx512F.LoadVector512(pB + bRowStart + j + vecSize * 2);
                        Vector512<float> vB3 = Avx512F.LoadVector512(pB + bRowStart + j + vecSize * 3);

                        acc0 = Avx512F.FusedMultiplyAdd(vA, vB0, acc0);
                        acc1 = Avx512F.FusedMultiplyAdd(vA, vB1, acc1);
                        acc2 = Avx512F.FusedMultiplyAdd(vA, vB2, acc2);
                        acc3 = Avx512F.FusedMultiplyAdd(vA, vB3, acc3);
                    }

                    // Store once per tile (register → memory)
                    Avx512F.Store(pC + cRowStart + j, acc0);
                    Avx512F.Store(pC + cRowStart + j + vecSize, acc1);
                    Avx512F.Store(pC + cRowStart + j + vecSize * 2, acc2);
                    Avx512F.Store(pC + cRowStart + j + vecSize * 3, acc3);
                }

                // Handle remaining full vectors (1-3 vectors)
                for (; j <= N - vecSize; j += vecSize)
                {
                    Vector512<float> acc = Vector512<float>.Zero;
                    for (int k = 0; k < K; k++)
                    {
                        Vector512<float> vA = Vector512.Create(pA[aRowStart + k]);
                        Vector512<float> vB = Avx512F.LoadVector512(pB + k * N + j);
                        acc = Avx512F.FusedMultiplyAdd(vA, vB, acc);
                    }
                    Avx512F.Store(pC + cRowStart + j, acc);
                }

                // Scalar remainder
                for (; j < N; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < K; k++)
                    {
                        sum += pA[aRowStart + k] * pB[k * N + j];
                    }
                    pC[cRowStart + j] = sum;
                }
            }
        }

        /// <summary>
        /// Unsafe AVX2 + FMA implementation with register-blocked microkernel.
        /// Accumulates C tile in registers across K, then stores once per tile.
        /// This eliminates redundant loads/stores and improves performance by ~20-30%.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulAvx2Unsafe(
            float* pA, float* pB, float* pC,
            int M, int K, int N)
        {
            const int vecSize = 8;
            const int TILE_M = 4;  // Process 4 rows of C at a time
            const int TILE_N = 32; // Process 32 cols of C at a time (4 vectors)

            if (M >= PARALLEL_THRESHOLD)
            {
                // Parallel over M tiles to reduce overhead
                int numMTiles = (M + TILE_M - 1) / TILE_M;
                Parallel.For(0, numMTiles, iTile =>
                {
                    int i0 = iTile * TILE_M;
                    int iMax = Math.Min(i0 + TILE_M, M);
                    MatMulAvx2TileKernel(pA, pB, pC, i0, iMax, K, N);
                });
            }
            else
            {
                MatMulAvx2TileKernel(pA, pB, pC, 0, M, K, N);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulAvx2TileKernel(
            float* pA, float* pB, float* pC,
            int i0, int iMax, int K, int N)
        {
            const int vecSize = 8;

            for (int i = i0; i < iMax; i++)
            {
                int aRowStart = i * K;
                int cRowStart = i * N;
                
                // Process output row in tiles
                int j = 0;
                for (; j <= N - (vecSize * 4); j += vecSize * 4)
                {
                    // Register block: accumulate 4 vectors across K
                    Vector256<float> acc0 = Vector256<float>.Zero;
                    Vector256<float> acc1 = Vector256<float>.Zero;
                    Vector256<float> acc2 = Vector256<float>.Zero;
                    Vector256<float> acc3 = Vector256<float>.Zero;

                    // Accumulate across K dimension with 2x unrolling
                    int k = 0;
                    for (; k <= K - 2; k += 2)
                    {
                        // First K iteration
                        Vector256<float> vA0 = Vector256.Create(pA[aRowStart + k]);
                        int bRowStart0 = k * N;

                        Vector256<float> vB0_0 = Avx.LoadVector256(pB + bRowStart0 + j);
                        Vector256<float> vB0_1 = Avx.LoadVector256(pB + bRowStart0 + j + vecSize);
                        Vector256<float> vB0_2 = Avx.LoadVector256(pB + bRowStart0 + j + vecSize * 2);
                        Vector256<float> vB0_3 = Avx.LoadVector256(pB + bRowStart0 + j + vecSize * 3);

                        acc0 = Fma.MultiplyAdd(vA0, vB0_0, acc0);
                        acc1 = Fma.MultiplyAdd(vA0, vB0_1, acc1);
                        acc2 = Fma.MultiplyAdd(vA0, vB0_2, acc2);
                        acc3 = Fma.MultiplyAdd(vA0, vB0_3, acc3);
                        
                        // Second K iteration
                        Vector256<float> vA1 = Vector256.Create(pA[aRowStart + k + 1]);
                        int bRowStart1 = (k + 1) * N;

                        Vector256<float> vB1_0 = Avx.LoadVector256(pB + bRowStart1 + j);
                        Vector256<float> vB1_1 = Avx.LoadVector256(pB + bRowStart1 + j + vecSize);
                        Vector256<float> vB1_2 = Avx.LoadVector256(pB + bRowStart1 + j + vecSize * 2);
                        Vector256<float> vB1_3 = Avx.LoadVector256(pB + bRowStart1 + j + vecSize * 3);

                        acc0 = Fma.MultiplyAdd(vA1, vB1_0, acc0);
                        acc1 = Fma.MultiplyAdd(vA1, vB1_1, acc1);
                        acc2 = Fma.MultiplyAdd(vA1, vB1_2, acc2);
                        acc3 = Fma.MultiplyAdd(vA1, vB1_3, acc3);
                    }
                    
                    // Handle remaining K iteration
                    for (; k < K; k++)
                    {
                        Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                        int bRowStart = k * N;

                        Vector256<float> vB0 = Avx.LoadVector256(pB + bRowStart + j);
                        Vector256<float> vB1 = Avx.LoadVector256(pB + bRowStart + j + vecSize);
                        Vector256<float> vB2 = Avx.LoadVector256(pB + bRowStart + j + vecSize * 2);
                        Vector256<float> vB3 = Avx.LoadVector256(pB + bRowStart + j + vecSize * 3);

                        acc0 = Fma.MultiplyAdd(vA, vB0, acc0);
                        acc1 = Fma.MultiplyAdd(vA, vB1, acc1);
                        acc2 = Fma.MultiplyAdd(vA, vB2, acc2);
                        acc3 = Fma.MultiplyAdd(vA, vB3, acc3);
                    }

                    // Store once per tile (register → memory)
                    Avx.Store(pC + cRowStart + j, acc0);
                    Avx.Store(pC + cRowStart + j + vecSize, acc1);
                    Avx.Store(pC + cRowStart + j + vecSize * 2, acc2);
                    Avx.Store(pC + cRowStart + j + vecSize * 3, acc3);
                }

                // Handle remaining full vectors (1-3 vectors)
                for (; j <= N - vecSize; j += vecSize)
                {
                    Vector256<float> acc = Vector256<float>.Zero;
                    int k = 0;
                    
                    // 2x unrolling for K loop
                    for (; k <= K - 2; k += 2)
                    {
                        Vector256<float> vA0 = Vector256.Create(pA[aRowStart + k]);
                        Vector256<float> vB0 = Avx.LoadVector256(pB + k * N + j);
                        acc = Fma.MultiplyAdd(vA0, vB0, acc);
                        
                        Vector256<float> vA1 = Vector256.Create(pA[aRowStart + k + 1]);
                        Vector256<float> vB1 = Avx.LoadVector256(pB + (k + 1) * N + j);
                        acc = Fma.MultiplyAdd(vA1, vB1, acc);
                    }
                    
                    // Handle remaining K
                    for (; k < K; k++)
                    {
                        Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
                        Vector256<float> vB = Avx.LoadVector256(pB + k * N + j);
                        acc = Fma.MultiplyAdd(vA, vB, acc);
                    }
                    Avx.Store(pC + cRowStart + j, acc);
                }

                // Scalar remainder
                for (; j < N; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < K; k++)
                    {
                        sum += pA[aRowStart + k] * pB[k * N + j];
                    }
                    pC[cRowStart + j] = sum;
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
        /// Uses tiling and SIMD for improved cache locality and throughput.
        /// </summary>
        /// <summary>
        /// Matrix multiplication with B transposed: C = A × B^T
        /// A: (M × K), B: (N × K), C: (M × N)
        /// TIER-2: Added parallelization for long sequences (M >= 64, K >= 64)
        /// </summary>
        public static void MatMulTransposeB(
            ReadOnlySpan<float> A, ReadOnlySpan<float> B, Span<float> C,
            int M, int K, int N)
        {
            if (A.Length < M * K || B.Length < N * K || C.Length < M * N)
                throw new ArgumentException("Matrix dimensions don't match buffer sizes");

            // NOTE: C.Clear() removed - kernel now uses store-once pattern
            // Output is fully overwritten by direct assignment

            // For attention: A is Q (T × headSize), B is K (T × headSize)
            // We compute C = Q @ K^T (T × T)
            // This is more cache-friendly than transposing K first
            
            // TIER-2 OPTIMIZATION: Reintroduce parallelization with unsafe pointer-based approach
            // Parallelize when profitable: M >= 64 AND K >= 64 AND have multiple processors
            // For long sequences (T > 256), internal row-parallelism provides significant speedup
            bool shouldParallelize = M >= 64 && K >= 64 && Environment.ProcessorCount > 1;
            
            unsafe
            {
                fixed (float* pA = A, pB = B, pC = C)
                {
                    if (shouldParallelize)
                    {
                        // Use chunked parallelization to reduce Parallel.For overhead
                        int rangeSize = Math.Max(4, M / (Environment.ProcessorCount * 2));
                        
                        // Copy pointers to local variables to capture in closure
                        float* localPA = pA;
                        float* localPB = pB;
                        float* localPC = pC;
                        int localK = K;
                        int localN = N;
                        
                        Parallel.ForEach(
                            System.Collections.Concurrent.Partitioner.Create(0, M, rangeSize),
                            range =>
                            {
                                for (int i = range.Item1; i < range.Item2; i++)
                                {
                                    MatMulTransposeBRow(localPA, localPB, localPC, i, localK, localN);
                                }
                            });
                    }
                    else
                    {
                        // Sequential path for small matrices
                        for (int i = 0; i < M; i++)
                        {
                            MatMulTransposeBRow(pA, pB, pC, i, K, N);
                        }
                    }
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulTransposeBRow(
            float* pA, float* pB, float* pC,
            int i, int K, int N)
        {
            int aRowStart = i * K;
            int cRowStart = i * N;
            
            // TIER-2 OPTIMIZATION: Add AVX-512 dispatch
            // Dispatch order: AVX-512 → AVX2+FMA → Scalar
            if (Avx512F.IsSupported && K >= 16)
            {
                MatMulTransposeBRowAvx512(pA, pB, pC, aRowStart, cRowStart, K, N);
            }
            else if (Avx2.IsSupported && Fma.IsSupported)
            {
                MatMulTransposeBRowAvx2(pA, pB, pC, aRowStart, cRowStart, K, N);
            }
            else
            {
                MatMulTransposeBRowScalar(pA, pB, pC, aRowStart, cRowStart, K, N);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulTransposeBRowAvx2(
            float* pA, float* pB, float* pC,
            int aRowStart, int cRowStart, int K, int N)
        {
            const int vecSize = 8;
            
            // TIER-2 OPTIMIZATION: 4-way register blocking
            // Compute 4 dot products simultaneously to reduce horizontal sum overhead
            const int BLOCK_SIZE = 4;
            
            int j = 0;
            
            // Process 4 columns at a time (register blocking)
            for (; j <= N - BLOCK_SIZE; j += BLOCK_SIZE)
            {
                int bRowStart0 = (j + 0) * K;
                int bRowStart1 = (j + 1) * K;
                int bRowStart2 = (j + 2) * K;
                int bRowStart3 = (j + 3) * K;
                
                // Four accumulators for 4 simultaneous dot products
                Vector256<float> acc0 = Vector256<float>.Zero;
                Vector256<float> acc1 = Vector256<float>.Zero;
                Vector256<float> acc2 = Vector256<float>.Zero;
                Vector256<float> acc3 = Vector256<float>.Zero;
                
                int k = 0;
                
                // TIER-5 OPTIMIZATION: 2x loop unrolling in K dimension
                // Process 16 floats per iteration (2 * vecSize) to reduce loop overhead
                // and improve instruction-level parallelism
                for (; k <= K - (vecSize * 2); k += vecSize * 2)
                {
                    // First unrolled iteration (k + 0)
                    Vector256<float> vA0 = Avx.LoadVector256(pA + aRowStart + k);
                    
                    Vector256<float> vB0_0 = Avx.LoadVector256(pB + bRowStart0 + k);
                    Vector256<float> vB0_1 = Avx.LoadVector256(pB + bRowStart1 + k);
                    Vector256<float> vB0_2 = Avx.LoadVector256(pB + bRowStart2 + k);
                    Vector256<float> vB0_3 = Avx.LoadVector256(pB + bRowStart3 + k);
                    
                    acc0 = Fma.MultiplyAdd(vA0, vB0_0, acc0);
                    acc1 = Fma.MultiplyAdd(vA0, vB0_1, acc1);
                    acc2 = Fma.MultiplyAdd(vA0, vB0_2, acc2);
                    acc3 = Fma.MultiplyAdd(vA0, vB0_3, acc3);
                    
                    // Second unrolled iteration (k + vecSize)
                    Vector256<float> vA1 = Avx.LoadVector256(pA + aRowStart + k + vecSize);
                    
                    Vector256<float> vB1_0 = Avx.LoadVector256(pB + bRowStart0 + k + vecSize);
                    Vector256<float> vB1_1 = Avx.LoadVector256(pB + bRowStart1 + k + vecSize);
                    Vector256<float> vB1_2 = Avx.LoadVector256(pB + bRowStart2 + k + vecSize);
                    Vector256<float> vB1_3 = Avx.LoadVector256(pB + bRowStart3 + k + vecSize);
                    
                    acc0 = Fma.MultiplyAdd(vA1, vB1_0, acc0);
                    acc1 = Fma.MultiplyAdd(vA1, vB1_1, acc1);
                    acc2 = Fma.MultiplyAdd(vA1, vB1_2, acc2);
                    acc3 = Fma.MultiplyAdd(vA1, vB1_3, acc3);
                }
                
                // Handle remaining vecSize chunk (if K % 16 >= 8)
                for (; k <= K - vecSize; k += vecSize)
                {
                    Vector256<float> vA = Avx.LoadVector256(pA + aRowStart + k);
                    
                    Vector256<float> vB0 = Avx.LoadVector256(pB + bRowStart0 + k);
                    Vector256<float> vB1 = Avx.LoadVector256(pB + bRowStart1 + k);
                    Vector256<float> vB2 = Avx.LoadVector256(pB + bRowStart2 + k);
                    Vector256<float> vB3 = Avx.LoadVector256(pB + bRowStart3 + k);
                    
                    acc0 = Fma.MultiplyAdd(vA, vB0, acc0);
                    acc1 = Fma.MultiplyAdd(vA, vB1, acc1);
                    acc2 = Fma.MultiplyAdd(vA, vB2, acc2);
                    acc3 = Fma.MultiplyAdd(vA, vB3, acc3);
                }
                
                // Horizontal sum for all 4 accumulators
                float sum0 = HorizontalSumAvx2(acc0);
                float sum1 = HorizontalSumAvx2(acc1);
                float sum2 = HorizontalSumAvx2(acc2);
                float sum3 = HorizontalSumAvx2(acc3);
                
                // Scalar remainder for all 4 columns
                for (; k < K; k++)
                {
                    float aVal = pA[aRowStart + k];
                    sum0 += aVal * pB[bRowStart0 + k];
                    sum1 += aVal * pB[bRowStart1 + k];
                    sum2 += aVal * pB[bRowStart2 + k];
                    sum3 += aVal * pB[bRowStart3 + k];
                }
                
                // Store results
                pC[cRowStart + j + 0] = sum0;
                pC[cRowStart + j + 1] = sum1;
                pC[cRowStart + j + 2] = sum2;
                pC[cRowStart + j + 3] = sum3;
            }
            
            // Tail: process remaining columns one at a time
            for (; j < N; j++)
            {
                int bRowStart = j * K;
                
                Vector256<float> acc = Vector256<float>.Zero;
                int k = 0;
                
                // TIER-5 OPTIMIZATION: 2x loop unrolling for tail columns as well
                for (; k <= K - (vecSize * 2); k += vecSize * 2)
                {
                    Vector256<float> vA0 = Avx.LoadVector256(pA + aRowStart + k);
                    Vector256<float> vB0 = Avx.LoadVector256(pB + bRowStart + k);
                    acc = Fma.MultiplyAdd(vA0, vB0, acc);
                    
                    Vector256<float> vA1 = Avx.LoadVector256(pA + aRowStart + k + vecSize);
                    Vector256<float> vB1 = Avx.LoadVector256(pB + bRowStart + k + vecSize);
                    acc = Fma.MultiplyAdd(vA1, vB1, acc);
                }
                
                for (; k <= K - vecSize; k += vecSize)
                {
                    Vector256<float> vA = Avx.LoadVector256(pA + aRowStart + k);
                    Vector256<float> vB = Avx.LoadVector256(pB + bRowStart + k);
                    acc = Fma.MultiplyAdd(vA, vB, acc);
                }
                
                float sum = HorizontalSumAvx2(acc);
                
                for (; k < K; k++)
                {
                    sum += pA[aRowStart + k] * pB[bRowStart + k];
                }
                
                pC[cRowStart + j] = sum;
            }
        }
        
        /// <summary>
        /// Fast horizontal sum for Vector256 using AVX
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float HorizontalSumAvx2(Vector256<float> v)
        {
            // [a0 a1 a2 a3 a4 a5 a6 a7]
            Vector128<float> lo = v.GetLower(); // [a0 a1 a2 a3]
            Vector128<float> hi = v.GetUpper(); // [a4 a5 a6 a7]
            Vector128<float> sum128 = Sse.Add(lo, hi); // [a0+a4 a1+a5 a2+a6 a3+a7]
            Vector128<float> sum64 = Sse.Add(sum128, Sse.Shuffle(sum128, sum128, 0b_11_10_11_10));
            Vector128<float> sum32 = Sse.Add(sum64, Sse.Shuffle(sum64, sum64, 0b_01_01_01_01));
            return sum32.ToScalar();
        }

        /// <summary>
        /// TIER-2: AVX-512 implementation for MatMulTransposeBRow
        /// Uses 512-bit vectors (16 floats) with FMA for maximum performance
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulTransposeBRowAvx512(
            float* pA, float* pB, float* pC,
            int aRowStart, int cRowStart, int K, int N)
        {
            const int vecSize = 16; // AVX-512 processes 16 floats per vector
            
            // Tile j loop for better cache reuse
            const int TILE_J = 64;
            
            for (int j0 = 0; j0 < N; j0 += TILE_J)
            {
                int jMax = Math.Min(j0 + TILE_J, N);
                
                // Process tile of output elements
                for (int j = j0; j < jMax; j++)
                {
                    int bRowStart = j * K;
                    
                    // Compute dot product using AVX-512
                    Vector512<float> acc = Vector512<float>.Zero;
                    int k = 0;
                    
                    // Main SIMD loop
                    for (; k <= K - vecSize; k += vecSize)
                    {
                        Vector512<float> vA = Avx512F.LoadVector512(pA + aRowStart + k);
                        Vector512<float> vB = Avx512F.LoadVector512(pB + bRowStart + k);
                        acc = Avx512F.FusedMultiplyAdd(vA, vB, acc);
                    }
                    
                    // Horizontal sum using AVX-512
                    // Reduce 512-bit vector to scalar
                    Vector256<float> lo256 = acc.GetLower();
                    Vector256<float> hi256 = acc.GetUpper();
                    Vector256<float> sum256 = Avx.Add(lo256, hi256);
                    
                    // Continue reduction with AVX
                    Vector128<float> lo128 = sum256.GetLower();
                    Vector128<float> hi128 = sum256.GetUpper();
                    Vector128<float> sum128 = Sse.Add(lo128, hi128);
                    Vector128<float> sum64 = Sse.Add(sum128, Sse.Shuffle(sum128, sum128, 0b_11_10_11_10));
                    Vector128<float> sum32 = Sse.Add(sum64, Sse.Shuffle(sum64, sum64, 0b_01_01_01_01));
                    float sum = sum32.ToScalar();
                    
                    // Scalar remainder
                    for (; k < K; k++)
                    {
                        sum += pA[aRowStart + k] * pB[bRowStart + k];
                    }
                    
                    pC[cRowStart + j] = sum;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulTransposeBRowScalar(
            float* pA, float* pB, float* pC,
            int aRowStart, int cRowStart, int K, int N)
        {
            int vectorSize = Vector<float>.Count;
            
            for (int j = 0; j < N; j++)
            {
                int bRowStart = j * K;
                
                // Compute dot product using Vector<T>
                Vector<float> acc = Vector<float>.Zero;
                int k = 0;
                
                // SIMD loop
                for (; k <= K - vectorSize; k += vectorSize)
                {
                    var vA = System.Runtime.CompilerServices.Unsafe.Read<Vector<float>>(pA + aRowStart + k);
                    var vB = System.Runtime.CompilerServices.Unsafe.Read<Vector<float>>(pB + bRowStart + k);
                    acc += vA * vB;
                }
                
                // Horizontal sum
                float sum = 0f;
                for (int v = 0; v < vectorSize; v++)
                {
                    sum += acc[v];
                }
                
                // Scalar remainder
                for (; k < K; k++)
                {
                    sum += pA[aRowStart + k] * pB[bRowStart + k];
                }
                
                pC[cRowStart + j] = sum;
            }
        }

        /// <summary>
        /// ARM NEON tiled matrix multiplication for Apple Silicon and ARM servers.
        /// Processes 4 floats per vector using AdvSimd.Arm64 FMA operations.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void MatMulNeonTiled(
            float[] A, float[] B, float[] C,
            int M, int K, int N)
        {
            const int vecSize = 4; // NEON processes 4 floats per vector
            int numRowTiles = (M + TILE_SIZE - 1) / TILE_SIZE;
            
            // Parallelize over row tiles when beneficial
            if (numRowTiles >= 2)
            {
                Parallel.For(0, numRowTiles, i0Idx =>
                {
                    int i0 = i0Idx * TILE_SIZE;
                    int iMax = Math.Min(i0 + TILE_SIZE, M);
                    MatMulNeonTileKernel(A, B, C, i0, iMax, K, N, vecSize);
                });
            }
            else
            {
                MatMulNeonTileKernel(A, B, C, 0, M, K, N, vecSize);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void MatMulNeonTileKernel(
            float[] A, float[] B, float[] C,
            int i0, int iMax, int K, int N, int vecSize)
        {
            fixed (float* pA = A, pB = B, pC = C)
            {
                for (int k0 = 0; k0 < K; k0 += TILE_SIZE)
                {
                    int kMax = Math.Min(k0 + TILE_SIZE, K);
                    
                    for (int j0 = 0; j0 < N; j0 += TILE_SIZE)
                    {
                        int jMax = Math.Min(j0 + TILE_SIZE, N);
                        
                        // Process this tile
                        for (int i = i0; i < iMax; i++)
                        {
                            int aRow = i * K;
                            int cRow = i * N;
                            
                            for (int k = k0; k < kMax; k++)
                            {
                                var vA = AdvSimd.DuplicateToVector128(pA[aRow + k]);
                                int bRow = k * N;
                                int j = j0;
                                
                                // NEON SIMD loop (4 floats at a time)
                                for (; j <= jMax - vecSize; j += vecSize)
                                {
                                    var vB = AdvSimd.LoadVector128(pB + bRow + j);
                                    var vC = AdvSimd.LoadVector128(pC + cRow + j);
                                    // FMA: vC = vC + vB * vA
                                    vC = AdvSimd.FusedMultiplyAdd(vC, vB, vA);
                                    AdvSimd.Store(pC + cRow + j, vC);
                                }
                                
                                // Scalar remainder
                                float aVal = pA[aRow + k];
                                for (; j < jMax; j++)
                                {
                                    pC[cRow + j] += aVal * pB[bRow + j];
                                }
                            }
                        }
                    }
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
            int i = 0;
            float sum = 0f;

            // AVX-512 path (16 floats)
            if (Avx512F.IsSupported && length >= 16)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b)
                    {
                        var sumVec512 = Vector512<float>.Zero;
                        for (; i <= length - 16; i += 16)
                        {
                            var va = Avx512F.LoadVector512(pA + i);
                            var vb = Avx512F.LoadVector512(pB + i);
                            sumVec512 = Avx512F.FusedMultiplyAdd(va, vb, sumVec512);
                        }
                        // Horizontal sum: 512 → scalar
                        sum = SimdCapabilities.HorizontalSum(sumVec512);
                    }
                }
            }
            // ARM NEON path (4 floats)
            else if (AdvSimd.Arm64.IsSupported && length >= 4)
            {
                unsafe
                {
                    fixed (float* pA = a, pB = b)
                    {
                        var sumVec = Vector128<float>.Zero;
                        for (; i <= length - 4; i += 4)
                        {
                            var va = AdvSimd.LoadVector128(pA + i);
                            var vb = AdvSimd.LoadVector128(pB + i);
                            sumVec = AdvSimd.FusedMultiplyAdd(sumVec, va, vb);
                        }
                        // Horizontal sum: manual extraction and add
                        sum = sumVec.GetElement(0) + sumVec.GetElement(1) + 
                              sumVec.GetElement(2) + sumVec.GetElement(3);
                    }
                }
            }

            // Vector<T> fallback
            int vectorSize = Vector<float>.Count;
            var sumVec2 = Vector<float>.Zero;

            // SIMD loop
            for (; i <= length - vectorSize; i += vectorSize)
            {
                var va = new Vector<float>(a.Slice(i));
                var vb = new Vector<float>(b.Slice(i));
                sumVec2 += va * vb;
            }

            // Horizontal sum
            for (int j = 0; j < vectorSize; j++)
            {
                sum += sumVec2[j];
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

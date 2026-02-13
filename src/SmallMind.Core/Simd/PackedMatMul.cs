using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;

namespace SmallMind.Core.Simd
{
    /// <summary>
    /// Cache-blocked GEMM with B-matrix packing for optimal cache utilization.
    /// 
    /// Performance rationale:
    /// - Packing B into contiguous microkernel-friendly layout reduces cache misses
    /// - Amortizes packing cost when B is reused across multiple A matrices (batch inference)
    /// - L1/L2 blocking ensures inner loops fit in cache
    /// - Static thread tiling avoids Parallel.For overhead for large matrices
    /// 
    /// Expected improvement: 1.3-1.8x vs naive tiled MatMul on reused weights
    /// 
    /// Public API for LLM inference with weight reuse:
    /// 1. Pack weight matrix once: var packed = PackedMatMul.Pack(weights, K, N);
    /// 2. Reuse for many batch inferences: PackedMatMul.Multiply(activations, packed, output, M, K, N);
    /// </summary>
    public static class PackedMatMul
    {
        // Cache-friendly block sizes (tuned for typical L1=32KB, L2=256KB)
        private const int MC = 256;  // M-dimension blocking for L2 cache
        private const int KC = 512;  // K-dimension blocking for L1/L2
        private const int NC = 4096; // N-dimension blocking for L3 cache
        
        // Microkernel tile sizes
        private const int MR = 6;   // M-register blocking
        private const int NR = 16;  // N-register blocking (AVX2: 2x8, AVX-512: 1x16)
        
        // Thread tiling threshold
        private const int PARALLEL_THRESHOLD_M = 256;
        
        /// <summary>
        /// Packed B-matrix storage optimized for cache-blocked GEMM.
        /// Layout: [NC panels] -> [KC blocks] -> [NR columns] -> [KC rows]
        /// Ensures microkernel accesses B sequentially (optimal cache line usage).
        /// 
        /// Public API for high-performance LLM inference with reusable weight matrices.
        /// Pack once, reuse for multiple batches to amortize packing cost.
        /// </summary>
        public sealed class PackedMatrix : IDisposable
        {
            internal readonly float[] _data;  // Made internal for access
            private readonly int _rows;
            private readonly int _cols;
            internal readonly int _paddedCols;  // Made internal for access
            private bool _disposed;
            
            /// <summary>
            /// Number of rows (K dimension) in the packed matrix.
            /// </summary>
            public int Rows => _rows;
            
            /// <summary>
            /// Number of columns (N dimension) in the packed matrix.
            /// </summary>
            public int Cols => _cols;
            
            /// <summary>
            /// Read-only view of the packed data.
            /// </summary>
            public ReadOnlySpan<float> Data => _data;
            
            /// <summary>
            /// Padded column count (aligned to NR boundary for vectorization).
            /// </summary>
            public int PaddedCols => _paddedCols;  // Public accessor
            
            internal PackedMatrix(int rows, int cols)
            {
                _rows = rows;
                _cols = cols;
                // Pad to NR boundary for vectorization
                _paddedCols = (cols + NR - 1) / NR * NR;
                // Panel-major layout: [numPanels * rows * NR]
                int numPanels = (_paddedCols + NR - 1) / NR;
                _data = new float[numPanels * rows * NR];
            }
            
            /// <summary>
            /// Packs source matrix B into panel-major layout for optimal cache utilization.
            /// Layout: For each NR-wide column panel, store all K rows contiguously.
            /// Panel i stores B[0:K, i*NR:(i+1)*NR] in row-major order within the panel.
            /// Amortized cost: one pack can be reused for multiple MatMuls with different A.
            /// </summary>
            [MethodImpl(MethodImplOptions.AggressiveOptimization)]
            public void Pack(ReadOnlySpan<float> source, int sourceRows, int sourceCols)
            {
                if (sourceRows != _rows || sourceCols != _cols)
                    throw new ArgumentException($"Source dims {sourceRows}×{sourceCols} != packed dims {_rows}×{_cols}");
                
                int numPanels = (_paddedCols + NR - 1) / NR;
                
                // Pack into panel-major layout: each panel contains K rows × NR cols
                for (int panelIdx = 0; panelIdx < numPanels; panelIdx++)
                {
                    int jStart = panelIdx * NR;
                    int jEnd = Math.Min(jStart + NR, _cols);
                    int panelWidth = jEnd - jStart;
                    
                    // For each row in this panel
                    for (int k = 0; k < _rows; k++)
                    {
                        int destBase = panelIdx * _rows * NR + k * NR;
                        int srcBase = k * sourceCols + jStart;
                        
                        // Copy panel-width elements
                        for (int jj = 0; jj < panelWidth; jj++)
                        {
                            _data[destBase + jj] = source[srcBase + jj];
                        }
                        
                        // Zero-pad remainder
                        for (int jj = panelWidth; jj < NR; jj++)
                        {
                            _data[destBase + jj] = 0f;
                        }
                    }
                }
            }
            
            public void Dispose()
            {
                if (_disposed) return;
                // _data will be GC'd
                _disposed = true;
                GC.SuppressFinalize(this);
            }
        }
        
        /// <summary>
        /// Packs a B-matrix into optimized panel-major layout for cache-blocked GEMM.
        /// Pack once, reuse many times for batch inference (weight reuse).
        /// 
        /// Usage:
        ///   var packed = PackedMatMul.Pack(weightMatrix, K, N);
        ///   // Reuse packed for multiple batches
        ///   PackedMatMul.Multiply(batch1, packed, output1, M1, K, N);
        ///   PackedMatMul.Multiply(batch2, packed, output2, M2, K, N);
        /// </summary>
        /// <param name="B">Source matrix in row-major layout (K × N)</param>
        /// <param name="rows">Number of rows (K dimension)</param>
        /// <param name="cols">Number of columns (N dimension)</param>
        /// <returns>Packed matrix ready for optimized multiplication</returns>
        public static PackedMatrix Pack(ReadOnlySpan<float> B, int rows, int cols)
        {
            var packed = new PackedMatrix(rows, cols);
            packed.Pack(B, rows, cols);
            return packed;
        }
        
        /// <summary>
        /// Creates a packed B-matrix ready for cache-blocked GEMM.
        /// Pack once, reuse many times for batch inference.
        /// (Alias for Pack method for backward compatibility)
        /// </summary>
        [Obsolete("Use Pack(ReadOnlySpan<float>, int, int) instead")]
        public static PackedMatrix CreatePackedMatrix(ReadOnlySpan<float> B, int rows, int cols)
        {
            return Pack(B, rows, cols);
        }
        
        /// <summary>
        /// Matrix multiply with packed B: C[M×N] = A[M×K] × B_packed[K×N]
        /// Uses cache-blocked algorithm with static thread tiling.
        /// </summary>
        /// <param name="accumulate">
        /// If false (default): C is overwritten with A×B (fastest, no pre-zero needed).
        /// If true: C += A×B (accumulates into existing C values).
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Multiply(
            float[] A, PackedMatrix B, float[] C,
            int M, int K, int N, bool accumulate = false)
        {
            if (B.Rows != K || B.Cols != N)
                throw new ArgumentException($"B dims {B.Rows}×{B.Cols} != expected {K}×{N}");
            if (A.Length < M * K)
                throw new ArgumentException($"A length {A.Length} < {M * K}");
            if (C.Length < M * N)
                throw new ArgumentException($"C length {C.Length} < {M * N}");
            
            // Zero output only if not accumulating (overwrite mode)
            if (!accumulate)
            {
                Array.Clear(C);
            }
            
            // Dispatch based on size
            if (M >= PARALLEL_THRESHOLD_M)
            {
                MultiplyParallel(A, B._data, C, M, K, N, B._paddedCols, accumulate);
            }
            else
            {
                MultiplySerial(A.AsSpan(), B.Data, C.AsSpan(), M, K, N, B._paddedCols, accumulate);
            }
        }
        
        /// <summary>
        /// Matrix multiply with packed B (Span overload): C[M×N] = A[M×K] × B_packed[K×N]
        /// Uses cache-blocked algorithm (serial only for Span version).
        /// </summary>
        /// <param name="accumulate">
        /// If false (default): C is overwritten with A×B (fastest, no pre-zero needed).
        /// If true: C += A×B (accumulates into existing C values).
        /// </param>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public static void Multiply(
            ReadOnlySpan<float> A, PackedMatrix B, Span<float> C,
            int M, int K, int N, bool accumulate = false)
        {
            if (B.Rows != K || B.Cols != N)
                throw new ArgumentException($"B dims {B.Rows}×{B.Cols} != expected {K}×{N}");
            if (A.Length < M * K)
                throw new ArgumentException($"A length {A.Length} < {M * K}");
            if (C.Length < M * N)
                throw new ArgumentException($"C length {C.Length} < {M * N}");
            
            // Zero output only if not accumulating (overwrite mode)
            if (!accumulate)
            {
                C.Clear();
            }
            
            // Use serial path (Span can't be captured in Parallel.For)
            MultiplySerial(A, B.Data, C, M, K, N, B._paddedCols, accumulate);
        }
        
        /// <summary>
        /// Serial cache-blocked GEMM for small-to-medium matrices.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void MultiplySerial(
            ReadOnlySpan<float> A, ReadOnlySpan<float> Bpacked, Span<float> C,
            int M, int K, int N, int ldB, bool accumulate)
        {
            fixed (float* pA = A, pB = Bpacked, pC = C)
            {
                // L3 cache blocking (NC)
                for (int nc = 0; nc < N; nc += NC)
                {
                    int nb = Math.Min(NC, N - nc);
                    
                    // L2 cache blocking (KC)
                    for (int kc = 0; kc < K; kc += KC)
                    {
                        int kb = Math.Min(KC, K - kc);
                        
                        // L1 cache blocking (MC)
                        for (int mc = 0; mc < M; mc += MC)
                        {
                            int mb = Math.Min(MC, M - mc);
                            
                            // Microkernel loop
                            // For panel-major layout with KC blocking:
                            // - Pass panelStride = K (full matrix K dimension) for correct panel offset calculation
                            // - Bpacked points to the start of row kc in panel 0
                            // - Microkernel will calculate offsets for other panels using panelStride
                            float* packedBlockStart = pB + kc * NR;
                            
                            GemmMicrokernel(
                                pA + mc * K + kc,
                                packedBlockStart,
                                pC + mc * N + nc,
                                mb, kb, nb, K, K, N);  // panelStride = K (full dimension)
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Parallel cache-blocked GEMM for large matrices.
        /// Uses static thread tiling to avoid Parallel.For overhead.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static void MultiplyParallel(
            float[] A, float[] Bpacked, float[] C,
            int M, int K, int N, int ldB, bool accumulate)
        {
            int numThreads = Environment.ProcessorCount;
            int chunkSize = (M + numThreads - 1) / numThreads;
            
            Parallel.For(0, numThreads, threadId =>
            {
                int mcStart = threadId * chunkSize;
                if (mcStart >= M) return;
                
                int mcEnd = Math.Min(mcStart + chunkSize, M);
                
                // Work with spans (thread-safe, no pinning needed in lambda)
                var spanA = A.AsSpan();
                var spanB = Bpacked.AsSpan();
                var spanC = C.AsSpan();
                
                ProcessChunk(spanA, spanB, spanC, mcStart, mcEnd, M, K, N, ldB, accumulate);
            });
        }
        
        /// <summary>
        /// Process a chunk of the matrix multiply (helper for parallel execution).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void ProcessChunk(
            Span<float> A, Span<float> Bpacked, Span<float> C,
            int mcStart, int mcEnd, int M, int K, int N, int ldB, bool accumulate)
        {
            fixed (float* pA = A, pB = Bpacked, pC = C)
            {
                // L3 cache blocking (NC)
                for (int nc = 0; nc < N; nc += NC)
                {
                    int nb = Math.Min(NC, N - nc);
                    
                    // L2 cache blocking (KC)
                    for (int kc = 0; kc < K; kc += KC)
                    {
                        int kb = Math.Min(KC, K - kc);
                        
                        // This thread's M-range
                        for (int mc = mcStart; mc < mcEnd; mc += MC)
                        {
                            int mb = Math.Min(MC, mcEnd - mc);
                            
                            // Microkernel loop
                            // For panel-major layout with KC blocking:
                            // - Pass panelStride = K (full matrix K dimension) for correct panel offset calculation
                            // - Bpacked points to the start of row kc in panel 0
                            // - Microkernel will calculate offsets for other panels using panelStride
                            float* packedBlockStart = pB + kc * NR;
                            
                            GemmMicrokernel(
                                pA + mc * K + kc,
                                packedBlockStart,
                                pC + mc * N + nc,
                                mb, kb, nb, K, K, N);  // panelStride = K (full dimension)
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Cache-blocked microkernel dispatcher.
        /// Processes MR×NR tiles with optimal SIMD kernels.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static unsafe void GemmMicrokernel(
            float* A, float* Bpacked, float* C,
            int M, int K, int N,
            int ldA, int panelStride, int ldC)
        {
            // panelStride = full K dimension (for panel offset calculation)
            // K = number of rows to process (kb in blocked case)
            if (Avx512F.IsSupported)
            {
                GemmMicrokernelAvx512(A, Bpacked, C, M, K, N, ldA, panelStride, ldC);
            }
            else if (Avx2.IsSupported && Fma.IsSupported)
            {
                GemmMicrokernelAvx2(A, Bpacked, C, M, K, N, ldA, panelStride, ldC);
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                GemmMicrokernelNeon(A, Bpacked, C, M, K, N, ldA, panelStride, ldC);
            }
            else
            {
                GemmMicrokernelScalar(A, Bpacked, C, M, K, N, ldA, ldC);
            }
        }
        
        /// <summary>
        /// AVX-512 microkernel: 6×32 tile (MR=6, NR=32 using 2x16 vectors).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static unsafe void GemmMicrokernelAvx512(
            float* A, float* Bpacked, float* C,
            int M, int K, int N,
            int ldA, int panelStride, int ldC)
        {
            if (!Avx512F.IsSupported)
            {
                GemmMicrokernelAvx2(A, Bpacked, C, M, K, N, ldA, panelStride, ldC);
                return;
            }
            
            const int NR_AVX512 = 32;
            
            for (int i = 0; i < M; i += MR)
            {
                int mr = Math.Min(MR, M - i);
                
                for (int j = 0; j < N; j += NR_AVX512)
                {
                    int nr = Math.Min(NR_AVX512, N - j);
                    
                    if (mr == MR && nr == NR_AVX512)
                    {
                        // Full tile fast path
                        // Panel index for this j
                        int panelIdx = j / NR_AVX512;
                        // Use panelStride (full K) for panel offset, not K (which is kb in blocked case)
                        float* panelBase = Bpacked + panelIdx * panelStride * NR_AVX512;
                        
                        GemmKernelAvx512_6x32(
                            A + i * ldA,
                            panelBase,
                            C + i * ldC + j,
                            K, ldA, ldC);
                    }
                    else
                    {
                        // Edge case: scalar fallback
                        int panelIdx = j / NR_AVX512;
                        float* panelBase = Bpacked + panelIdx * panelStride * NR_AVX512;
                        int jInPanel = j % NR_AVX512;
                        
                        GemmKernelScalar(A + i * ldA, panelBase + jInPanel, C + i * ldC + j,
                                        mr, K, nr, ldA, NR_AVX512, ldC);
                    }
                }
            }
        }
        
        /// <summary>
        /// AVX2 microkernel: 6×16 tile (MR=6, NR=16 using 2x8 vectors).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static unsafe void GemmMicrokernelAvx2(
            float* A, float* Bpacked, float* C,
            int M, int K, int N,
            int ldA, int panelStride, int ldC)
        {
            if (!Avx2.IsSupported || !Fma.IsSupported)
            {
                GemmMicrokernelScalar(A, Bpacked, C, M, K, N, ldA, ldC);
                return;
            }
            
            for (int i = 0; i < M; i += MR)
            {
                int mr = Math.Min(MR, M - i);
                
                for (int j = 0; j < N; j += NR)
                {
                    int nr = Math.Min(NR, N - j);
                    
                    if (mr == MR && nr == NR)
                    {
                        // Full tile fast path
                        // Panel index for this j  
                        int panelIdx = j / NR;
                        // Use panelStride (full K) for panel offset, not K (which is kb in blocked case)
                        float* panelBase = Bpacked + panelIdx * panelStride * NR;
                        
                        GemmKernelAvx2_6x16(
                            A + i * ldA,
                            panelBase,
                            C + i * ldC + j,
                            K, ldA, ldC);
                    }
                    else
                    {
                        // Edge case: scalar fallback
                        int panelIdx = j / NR;
                        float* panelBase = Bpacked + panelIdx * panelStride * NR;
                        int jInPanel = j % NR;
                        
                        GemmKernelScalar(A + i * ldA, panelBase + jInPanel, C + i * ldC + j,
                                        mr, K, nr, ldA, NR, ldC);
                    }
                }
            }
        }
        
        /// <summary>
        /// Scalar microkernel for edge cases (fallback for non-SIMD or partial tiles).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private static unsafe void GemmMicrokernelScalar(
            float* A, float* Bpacked, float* C,
            int M, int K, int N,
            int ldA, int ldC)
        {
            // For panel-major layout, assume NR=16 panel width
            const int NR = 16;
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    float sum = C[i * ldC + j];
                    for (int k = 0; k < K; k++)
                    {
                        sum += A[i * ldA + k] * Bpacked[k * NR + j];
                    }
                    C[i * ldC + j] = sum;
                }
            }
        }
        
        
        /// <summary>
        /// AVX-512 kernel: 6×32 tile with FMA.
        /// Processes 6 rows × 32 columns using register blocking.
        /// Bpacked uses panel-major layout: sequential access pattern k*NR+offset.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static unsafe void GemmKernelAvx512_6x32(
            float* A, float* Bpacked, float* C,
            int K, int ldA, int ldC)
        {
            const int NR = 32;
            
            // Load accumulators (6 rows × 2 AVX-512 vectors)
            Vector512<float> c00 = Avx512F.LoadVector512(C + 0 * ldC + 0);
            Vector512<float> c01 = Avx512F.LoadVector512(C + 0 * ldC + 16);
            Vector512<float> c10 = Avx512F.LoadVector512(C + 1 * ldC + 0);
            Vector512<float> c11 = Avx512F.LoadVector512(C + 1 * ldC + 16);
            Vector512<float> c20 = Avx512F.LoadVector512(C + 2 * ldC + 0);
            Vector512<float> c21 = Avx512F.LoadVector512(C + 2 * ldC + 16);
            Vector512<float> c30 = Avx512F.LoadVector512(C + 3 * ldC + 0);
            Vector512<float> c31 = Avx512F.LoadVector512(C + 3 * ldC + 16);
            Vector512<float> c40 = Avx512F.LoadVector512(C + 4 * ldC + 0);
            Vector512<float> c41 = Avx512F.LoadVector512(C + 4 * ldC + 16);
            Vector512<float> c50 = Avx512F.LoadVector512(C + 5 * ldC + 0);
            Vector512<float> c51 = Avx512F.LoadVector512(C + 5 * ldC + 16);
            
            // K-dimension loop - panel-major access
            for (int k = 0; k < K; k++)
            {
                // Load B (32 elements from panel-major layout)
                Vector512<float> b0 = Avx512F.LoadVector512(Bpacked + k * NR + 0);
                Vector512<float> b1 = Avx512F.LoadVector512(Bpacked + k * NR + 16);
                
                // Broadcast A and FMA
                Vector512<float> a0 = Vector512.Create(A[0 * ldA + k]);
                c00 = Avx512F.FusedMultiplyAdd(a0, b0, c00);
                c01 = Avx512F.FusedMultiplyAdd(a0, b1, c01);
                
                Vector512<float> a1 = Vector512.Create(A[1 * ldA + k]);
                c10 = Avx512F.FusedMultiplyAdd(a1, b0, c10);
                c11 = Avx512F.FusedMultiplyAdd(a1, b1, c11);
                
                Vector512<float> a2 = Vector512.Create(A[2 * ldA + k]);
                c20 = Avx512F.FusedMultiplyAdd(a2, b0, c20);
                c21 = Avx512F.FusedMultiplyAdd(a2, b1, c21);
                
                Vector512<float> a3 = Vector512.Create(A[3 * ldA + k]);
                c30 = Avx512F.FusedMultiplyAdd(a3, b0, c30);
                c31 = Avx512F.FusedMultiplyAdd(a3, b1, c31);
                
                Vector512<float> a4 = Vector512.Create(A[4 * ldA + k]);
                c40 = Avx512F.FusedMultiplyAdd(a4, b0, c40);
                c41 = Avx512F.FusedMultiplyAdd(a4, b1, c41);
                
                Vector512<float> a5 = Vector512.Create(A[5 * ldA + k]);
                c50 = Avx512F.FusedMultiplyAdd(a5, b0, c50);
                c51 = Avx512F.FusedMultiplyAdd(a5, b1, c51);
            }
            
            // Store results
            Avx512F.Store(C + 0 * ldC + 0, c00);
            Avx512F.Store(C + 0 * ldC + 16, c01);
            Avx512F.Store(C + 1 * ldC + 0, c10);
            Avx512F.Store(C + 1 * ldC + 16, c11);
            Avx512F.Store(C + 2 * ldC + 0, c20);
            Avx512F.Store(C + 2 * ldC + 16, c21);
            Avx512F.Store(C + 3 * ldC + 0, c30);
            Avx512F.Store(C + 3 * ldC + 16, c31);
            Avx512F.Store(C + 4 * ldC + 0, c40);
            Avx512F.Store(C + 4 * ldC + 16, c41);
            Avx512F.Store(C + 5 * ldC + 0, c50);
            Avx512F.Store(C + 5 * ldC + 16, c51);
        }
        
        /// <summary>
        /// AVX2 kernel: 6×16 tile with FMA.
        /// Processes 6 rows × 16 columns using register blocking.
        /// Bpacked uses panel-major layout: sequential access pattern k*NR+offset.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static unsafe void GemmKernelAvx2_6x16(
            float* A, float* Bpacked, float* C,
            int K, int ldA, int ldC)
        {
            const int NR = 16;
            
            // Load accumulators (6 rows × 2 AVX2 vectors)
            Vector256<float> c00 = Avx.LoadVector256(C + 0 * ldC + 0);
            Vector256<float> c01 = Avx.LoadVector256(C + 0 * ldC + 8);
            Vector256<float> c10 = Avx.LoadVector256(C + 1 * ldC + 0);
            Vector256<float> c11 = Avx.LoadVector256(C + 1 * ldC + 8);
            Vector256<float> c20 = Avx.LoadVector256(C + 2 * ldC + 0);
            Vector256<float> c21 = Avx.LoadVector256(C + 2 * ldC + 8);
            Vector256<float> c30 = Avx.LoadVector256(C + 3 * ldC + 0);
            Vector256<float> c31 = Avx.LoadVector256(C + 3 * ldC + 8);
            Vector256<float> c40 = Avx.LoadVector256(C + 4 * ldC + 0);
            Vector256<float> c41 = Avx.LoadVector256(C + 4 * ldC + 8);
            Vector256<float> c50 = Avx.LoadVector256(C + 5 * ldC + 0);
            Vector256<float> c51 = Avx.LoadVector256(C + 5 * ldC + 8);
            
            // K-dimension loop - panel-major access
            for (int k = 0; k < K; k++)
            {
                // Load B (16 elements from panel-major layout)
                Vector256<float> b0 = Avx.LoadVector256(Bpacked + k * NR + 0);
                Vector256<float> b1 = Avx.LoadVector256(Bpacked + k * NR + 8);
                
                // Broadcast A and FMA
                Vector256<float> a0 = Vector256.Create(A[0 * ldA + k]);
                c00 = Fma.MultiplyAdd(a0, b0, c00);
                c01 = Fma.MultiplyAdd(a0, b1, c01);
                
                Vector256<float> a1 = Vector256.Create(A[1 * ldA + k]);
                c10 = Fma.MultiplyAdd(a1, b0, c10);
                c11 = Fma.MultiplyAdd(a1, b1, c11);
                
                Vector256<float> a2 = Vector256.Create(A[2 * ldA + k]);
                c20 = Fma.MultiplyAdd(a2, b0, c20);
                c21 = Fma.MultiplyAdd(a2, b1, c21);
                
                Vector256<float> a3 = Vector256.Create(A[3 * ldA + k]);
                c30 = Fma.MultiplyAdd(a3, b0, c30);
                c31 = Fma.MultiplyAdd(a3, b1, c31);
                
                Vector256<float> a4 = Vector256.Create(A[4 * ldA + k]);
                c40 = Fma.MultiplyAdd(a4, b0, c40);
                c41 = Fma.MultiplyAdd(a4, b1, c41);
                
                Vector256<float> a5 = Vector256.Create(A[5 * ldA + k]);
                c50 = Fma.MultiplyAdd(a5, b0, c50);
                c51 = Fma.MultiplyAdd(a5, b1, c51);
            }
            
            // Store results
            Avx.Store(C + 0 * ldC + 0, c00);
            Avx.Store(C + 0 * ldC + 8, c01);
            Avx.Store(C + 1 * ldC + 0, c10);
            Avx.Store(C + 1 * ldC + 8, c11);
            Avx.Store(C + 2 * ldC + 0, c20);
            Avx.Store(C + 2 * ldC + 8, c21);
            Avx.Store(C + 3 * ldC + 0, c30);
            Avx.Store(C + 3 * ldC + 8, c31);
            Avx.Store(C + 4 * ldC + 0, c40);
            Avx.Store(C + 4 * ldC + 8, c41);
            Avx.Store(C + 5 * ldC + 0, c50);
            Avx.Store(C + 5 * ldC + 8, c51);
        }
        
        /// <summary>
        /// ARM64 AdvSimd/NEON microkernel: 4×8 tile (MR=4, NR=8 using 2x4 vectors).
        /// Optimized for Apple Silicon and ARM64 servers.
        /// Uses NEON FMA for high throughput on ARM64.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static unsafe void GemmMicrokernelNeon(
            float* A, float* Bpacked, float* C,
            int M, int K, int N,
            int ldA, int panelStride, int ldC)
        {
            if (!AdvSimd.Arm64.IsSupported)
            {
                GemmMicrokernelScalar(A, Bpacked, C, M, K, N, ldA, ldC);
                return;
            }
            
            // NR=8 for ARM64 (2 NEON vectors of 4 floats each)
            const int MR_NEON = 4;
            const int NR_NEON = 8;
            
            for (int i = 0; i < M; i += MR_NEON)
            {
                int mr = Math.Min(MR_NEON, M - i);
                
                for (int j = 0; j < N; j += NR_NEON)
                {
                    int nr = Math.Min(NR_NEON, N - j);
                    
                    if (mr == MR_NEON && nr == NR_NEON)
                    {
                        // Full tile fast path
                        // Panel index for this j (NR=16 in packed layout, but we process NR_NEON=8 at a time)
                        int panelIdx = j / NR;
                        // Use panelStride (full K) for panel offset
                        float* panelBase = Bpacked + panelIdx * panelStride * NR;
                        int jInPanel = j % NR;
                        
                        GemmKernelNeon_4x8(
                            A + i * ldA,
                            panelBase + jInPanel,
                            C + i * ldC + j,
                            K, ldA, NR, ldC);
                    }
                    else
                    {
                        // Edge case: scalar fallback
                        int panelIdx = j / NR;
                        float* panelBase = Bpacked + panelIdx * panelStride * NR;
                        int jInPanel = j % NR;
                        
                        GemmKernelScalar(A + i * ldA, panelBase + jInPanel, C + i * ldC + j,
                                        mr, K, nr, ldA, NR, ldC);
                    }
                }
            }
        }
        
        /// <summary>
        /// NEON kernel: 4×8 tile with FMA.
        /// Processes 4 rows × 8 columns using NEON register blocking.
        /// Bpacked uses panel-major layout: sequential access pattern k*ldB+offset.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static unsafe void GemmKernelNeon_4x8(
            float* A, float* Bpacked, float* C,
            int K, int ldA, int ldB, int ldC)
        {
            // Load accumulators (4 rows × 2 NEON vectors of 4 floats)
            var c00 = AdvSimd.LoadVector128(C + 0 * ldC + 0);
            var c01 = AdvSimd.LoadVector128(C + 0 * ldC + 4);
            var c10 = AdvSimd.LoadVector128(C + 1 * ldC + 0);
            var c11 = AdvSimd.LoadVector128(C + 1 * ldC + 4);
            var c20 = AdvSimd.LoadVector128(C + 2 * ldC + 0);
            var c21 = AdvSimd.LoadVector128(C + 2 * ldC + 4);
            var c30 = AdvSimd.LoadVector128(C + 3 * ldC + 0);
            var c31 = AdvSimd.LoadVector128(C + 3 * ldC + 4);
            
            // K-dimension loop - panel-major access
            for (int k = 0; k < K; k++)
            {
                // Load B (8 elements from panel-major layout)
                var b0 = AdvSimd.LoadVector128(Bpacked + k * ldB + 0);
                var b1 = AdvSimd.LoadVector128(Bpacked + k * ldB + 4);
                
                // Broadcast A and FMA
                // ARM64 FMA signature: FusedMultiplyAdd(addend, left, right) = addend + (left * right)
                var a0 = AdvSimd.DuplicateToVector128(A[0 * ldA + k]);
                c00 = AdvSimd.FusedMultiplyAdd(c00, a0, b0);
                c01 = AdvSimd.FusedMultiplyAdd(c01, a0, b1);
                
                var a1 = AdvSimd.DuplicateToVector128(A[1 * ldA + k]);
                c10 = AdvSimd.FusedMultiplyAdd(c10, a1, b0);
                c11 = AdvSimd.FusedMultiplyAdd(c11, a1, b1);
                
                var a2 = AdvSimd.DuplicateToVector128(A[2 * ldA + k]);
                c20 = AdvSimd.FusedMultiplyAdd(c20, a2, b0);
                c21 = AdvSimd.FusedMultiplyAdd(c21, a2, b1);
                
                var a3 = AdvSimd.DuplicateToVector128(A[3 * ldA + k]);
                c30 = AdvSimd.FusedMultiplyAdd(c30, a3, b0);
                c31 = AdvSimd.FusedMultiplyAdd(c31, a3, b1);
            }
            
            // Store results
            AdvSimd.Store(C + 0 * ldC + 0, c00);
            AdvSimd.Store(C + 0 * ldC + 4, c01);
            AdvSimd.Store(C + 1 * ldC + 0, c10);
            AdvSimd.Store(C + 1 * ldC + 4, c11);
            AdvSimd.Store(C + 2 * ldC + 0, c20);
            AdvSimd.Store(C + 2 * ldC + 4, c21);
            AdvSimd.Store(C + 3 * ldC + 0, c30);
            AdvSimd.Store(C + 3 * ldC + 4, c31);
        }
        
        /// <summary>
        /// Scalar kernel for edge cases.
        /// Bpacked uses panel-major layout with stride ldB (typically NR).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.AggressiveInlining)]
        private static unsafe void GemmKernelScalar(
            float* A, float* Bpacked, float* C,
            int M, int K, int N,
            int ldA, int ldB, int ldC)
        {
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    float sum = C[i * ldC + j];
                    for (int k = 0; k < K; k++)
                    {
                        // Panel-major access: k * ldB + j
                        sum += A[i * ldA + k] * Bpacked[k * ldB + j];
                    }
                    C[i * ldC + j] = sum;
                }
            }
        }
    }
}

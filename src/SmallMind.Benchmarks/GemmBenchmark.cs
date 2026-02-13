using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using SmallMind.Abstractions.Telemetry;
using SmallMind.Core.Simd;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Standalone GFLOPS benchmark for matrix multiplication.
    /// Measures performance baseline before optimizations using Stopwatch timing.
    /// No BenchmarkDotNet - uses custom harness with proper warmup and GC collection.
    /// </summary>
    internal sealed class GemmBenchmark
    {
        private const int WARMUP_ITERATIONS = 3;
        private const int MEASURED_ITERATIONS = 10;
        private const float CORRECTNESS_TOLERANCE = 0.02f; // 2% relative error tolerance for large matrices
        private const int RANDOM_SEED = 42;

        // Matrix sizes to benchmark (all square for simplicity, plus rectangular)
        private static readonly (int M, int K, int N)[] BenchmarkSizes = new[]
        {
            (128, 128, 128),      // Small, fits L1
            (256, 256, 256),      // Fits L2
            (512, 512, 512),      // Exceeds L2, tests cache blocking
            (1024, 1024, 1024),   // Large, tests threading + cache hierarchy
            (2048, 2048, 2048),   // Stress test
            // Skipped for now - too slow without further optimizations:
            // (4096, 4096, 4096),   // LLM-scale weight matrix
            // (1, 4096, 4096),      // M=1, simulates single-token decode (matvec)
            // (32, 4096, 4096),     // M=32, simulates prefill batch
        };

        /// <summary>
        /// Run complete benchmark suite and output results.
        /// </summary>
        public static void Run(IRuntimeLogger? logger = null)
        {
            var log = logger ?? NullRuntimeLogger.Instance;

            log.Info("╔═══════════════════════════════════════════════════════════════════════════╗");
            log.Info("║           SmallMind GEMM Benchmark - Phase 0 Baseline                    ║");
            log.Info("╚═══════════════════════════════════════════════════════════════════════════╝");
            log.Info("");

            PrintEnvironmentInfo(log);
            log.Info("");

            var results = new List<BenchmarkResult>();

            foreach (var (M, K, N) in BenchmarkSizes)
            {
                log.Info($"Benchmarking {M}×{K}×{N}...");

                // Force full GC collection between different sizes
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var result = BenchmarkSize(M, K, N, log);
                results.Add(result);

                log.Info($"  MatMulOps: {result.MatMulOpsGflops:F2} GFLOPS (kernel: {result.KernelUsed})");
                log.Info($"  GemmMicro: {result.GemmMicroGflops:F2} GFLOPS");
                log.Info($"  PackedMM:  {result.PackedMmGflops:F2} GFLOPS");
                log.Info("");
            }

            PrintMarkdownTable(results, log);
            WriteJsonResults(results, log);
        }

        private static BenchmarkResult BenchmarkSize(int M, int K, int N, IRuntimeLogger log)
        {
            var random = new Random(RANDOM_SEED);

            // Initialize matrices with reproducible random values [-1.0, 1.0]
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            for (int i = 0; i < A.Length; i++)
                A[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            for (int i = 0; i < B.Length; i++)
                B[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            // Pre-allocate output buffers (reused across iterations)
            float[] C_matmul = new float[M * N];
            float[] C_gemm = new float[M * N];
            float[] C_packed = new float[M * N];
            float[] C_reference = new float[M * N];

            // Compute reference result for correctness validation
            NaiveMatMul(A, B, C_reference, M, K, N);

            // Benchmark MatMulOps.MatMul
            var matmulGflops = BenchmarkKernel("MatMulOps", M, K, N, () =>
            {
                Array.Clear(C_matmul);
                MatMulOps.MatMul(A, B, C_matmul, M, K, N);
            });

            // Validate correctness
            if (!ValidateResults(C_reference, C_matmul, C_matmul.Length, K))
                throw new InvalidOperationException("MatMulOps produced incorrect results!");

            // Get kernel used
            string kernelUsed = MatMulOps.LastKernelUsed.ToString();

            // Benchmark GemmMicrokernels.MatMul
            var gemmGflops = BenchmarkKernel("GemmMicro", M, K, N, () =>
            {
                GemmMicrokernels.MatMul(
                    A.AsSpan(0, M * K),
                    B.AsSpan(0, K * N),
                    C_gemm.AsSpan(0, M * N),
                    M, K, N);
            });

            // Validate correctness
            if (!ValidateResults(C_reference, C_gemm, C_gemm.Length, K))
                throw new InvalidOperationException("GemmMicrokernels produced incorrect results!");

            // Benchmark PackedMatMul.Multiply (with pre-packed B)
            var packedGflops = 0.0;
            try
            {
                // Pack B outside the timed region (amortized cost)
                var packedB = PackedMatMul.CreatePackedMatrix(B.AsSpan(0, K * N), K, N);

                packedGflops = BenchmarkKernel("PackedMM", M, K, N, () =>
                {
                    Array.Clear(C_packed);
                    PackedMatMul.Multiply(
                        A.AsSpan(0, M * K),
                        packedB,
                        C_packed.AsSpan(0, M * N),
                        M, K, N);
                });

                // Validate correctness
                if (!ValidateResults(C_reference, C_packed, C_packed.Length, K))
                    throw new InvalidOperationException("PackedMatMul produced incorrect results!");

                packedB.Dispose();
            }
            catch (Exception ex)
            {
                log.Warn($"  Warning: PackedMatMul failed: {ex.Message}");
                packedGflops = 0.0;
            }

            return new BenchmarkResult
            {
                M = M,
                K = K,
                N = N,
                MatMulOpsGflops = matmulGflops,
                GemmMicroGflops = gemmGflops,
                PackedMmGflops = packedGflops,
                KernelUsed = kernelUsed,
                Timestamp = DateTime.UtcNow
            };
        }

        private static double BenchmarkKernel(string name, int M, int K, int N, Action kernel)
        {
            // Warmup iterations
            for (int i = 0; i < WARMUP_ITERATIONS; i++)
            {
                kernel();
            }

            // Measured iterations
            var times = new List<double>();
            for (int i = 0; i < MEASURED_ITERATIONS; i++)
            {
                long startTicks = Stopwatch.GetTimestamp();
                kernel();
                long endTicks = Stopwatch.GetTimestamp();

                double elapsedSeconds = (endTicks - startTicks) / (double)Stopwatch.Frequency;
                times.Add(elapsedSeconds);
            }

            // Calculate GFLOPS: (2 * M * K * N) / (seconds * 1e9)
            // The factor of 2 accounts for multiply + add per output element
            double flops = 2.0 * M * K * N;

            // Use median time for robustness
            times.Sort();
            double medianTime = times[times.Count / 2];
            double gflops = flops / (medianTime * 1e9);

            return gflops;
        }

        /// <summary>
        /// Naive triple-loop matrix multiplication for correctness reference.
        /// C = A × B where A is M×K, B is K×N, C is M×N.
        /// </summary>
        private static void NaiveMatMul(float[] A, float[] B, float[] C, int M, int K, int N)
        {
            Array.Clear(C);
            for (int i = 0; i < M; i++)
            {
                for (int j = 0; j < N; j++)
                {
                    float sum = 0f;
                    for (int k = 0; k < K; k++)
                    {
                        sum += A[i * K + k] * B[k * N + j];
                    }
                    C[i * N + j] = sum;
                }
            }
        }

        /// <summary>
        /// Validate results against reference with adaptive tolerance based on matrix size.
        /// Larger matrices accumulate more floating point error, so tolerance scales with K.
        /// </summary>
        private static bool ValidateResults(float[] expected, float[] actual, int length,
                                            int K, float baseTolerance = 0.005f)
        {
            // Scale tolerance with accumulation depth (K dimension)
            // For K=128: 0.5%, for K=4096: ~1.6%
            float scaledTolerance = baseTolerance * MathF.Sqrt(K / 128f);

            int errorCount = 0;
            const int MAX_ERRORS_TO_SHOW = 5;

            for (int i = 0; i < length; i++)
            {
                float e = expected[i];
                float a = actual[i];

                // Use absolute tolerance for values near zero
                float absDiff = MathF.Abs(e - a);
                if (MathF.Abs(e) < 1e-5f && MathF.Abs(a) < 1e-5f)
                {
                    if (absDiff < 1e-4f) continue; // Both near zero, acceptable
                }

                // Relative error for non-zero values
                float denom = MathF.Max(MathF.Abs(e), 1e-6f);
                float relError = absDiff / denom;

                if (relError > scaledTolerance)
                {
                    errorCount++;
                }
            }

            if (errorCount > 0)
            {
                return errorCount < (length / 1000); // Allow < 0.1% error rate
            }

            return true;
        }

        private static void PrintEnvironmentInfo(IRuntimeLogger log)
        {
            log.Info("Environment:");
            log.Info($"  Date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            log.Info($"  OS: {RuntimeInformation.OSDescription}");
            log.Info($"  Architecture: {RuntimeInformation.ProcessArchitecture}");
            log.Info($"  .NET: {RuntimeInformation.FrameworkDescription}");
            log.Info($"  CPU Cores: {Environment.ProcessorCount}");
            log.Info($"  GC Mode: {(GCSettings.IsServerGC ? "Server" : "Workstation")}");
            log.Info("");
            log.Info("SIMD Capabilities:");
            log.Info($"  Vector<float>.Count: {System.Numerics.Vector<float>.Count}");
            log.Info($"  Vector.IsHardwareAccelerated: {System.Numerics.Vector.IsHardwareAccelerated}");

#if NET7_0_OR_GREATER
            log.Info($"  AVX2: {System.Runtime.Intrinsics.X86.Avx2.IsSupported}");
            log.Info($"  AVX-512F: {System.Runtime.Intrinsics.X86.Avx512F.IsSupported}");
            log.Info($"  FMA: {System.Runtime.Intrinsics.X86.Fma.IsSupported}");
#endif
        }

        private static void PrintMarkdownTable(List<BenchmarkResult> results, IRuntimeLogger log)
        {
            log.Info("");
            log.Info("╔═══════════════════════════════════════════════════════════════════════════╗");
            log.Info("║                           Baseline Results                                ║");
            log.Info("╚═══════════════════════════════════════════════════════════════════════════╝");
            log.Info("");
            log.Info("## Baseline Results — " + results[0].Timestamp.ToString("yyyy-MM-dd"));
            log.Info("");
            log.Info("| Size       | MatMulOps | GemmMicro | PackedMM | Kernel Used |");
            log.Info("|------------|-----------|-----------|----------|-------------|");

            foreach (var r in results)
            {
                string size = FormatSize(r.M, r.K, r.N);
                log.Info($"| {size,-10} | {r.MatMulOpsGflops,9:F2} | {r.GemmMicroGflops,9:F2} | {r.PackedMmGflops,8:F2} | {r.KernelUsed,-11} |");
            }
            log.Info("");
        }

        private static string FormatSize(int M, int K, int N)
        {
            if (M == K && K == N)
                return $"{M}×{M}";
            else
                return $"{M}×{K}×{N}";
        }

        private static void WriteJsonResults(List<BenchmarkResult> results, IRuntimeLogger log)
        {
            var report = new
            {
                Timestamp = DateTime.UtcNow,
                Environment = new
                {
                    OS = RuntimeInformation.OSDescription,
                    Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    Framework = RuntimeInformation.FrameworkDescription,
                    ProcessorCount = Environment.ProcessorCount,
                    GCMode = GCSettings.IsServerGC ? "Server" : "Workstation",
                    VectorFloatCount = System.Numerics.Vector<float>.Count,
                    VectorAccelerated = System.Numerics.Vector.IsHardwareAccelerated,
#if NET7_0_OR_GREATER
                    Avx2Supported = System.Runtime.Intrinsics.X86.Avx2.IsSupported,
                    Avx512FSupported = System.Runtime.Intrinsics.X86.Avx512F.IsSupported,
                    FmaSupported = System.Runtime.Intrinsics.X86.Fma.IsSupported,
#endif
                },
                Phase = "Phase0-Baseline",
                Results = results
            };

            string json = JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            string filename = "benchmark-results.json";
            File.WriteAllText(filename, json);
            log.Info($"Results written to {filename}");
        }

        private class BenchmarkResult
        {
            public int M { get; set; }
            public int K { get; set; }
            public int N { get; set; }
            public double MatMulOpsGflops { get; set; }
            public double GemmMicroGflops { get; set; }
            public double PackedMmGflops { get; set; }
            public string KernelUsed { get; set; } = "";
            public DateTime Timestamp { get; set; }
        }
    }
}

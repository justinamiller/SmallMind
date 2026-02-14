using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using SmallMind.Core.Simd;
using SmallMind.Core.Optimized;
using SmallMind.Benchmarks;

namespace ProfilerBenchmarks
{
    /// <summary>
    /// Profiler-driven performance benchmark harness for SmallMind.
    /// Focuses on CPU + memory + zero-GC hot paths for inference.
    /// NO 3rd-party libraries/packages - uses only .NET / BCL APIs.
    /// </summary>
    class Program
    {
        private static BenchmarkReport _report = new();
        private const int WARMUP_ITERATIONS = 5;
        private const int MEASUREMENT_ITERATIONS = 20;

        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Profiler Benchmarks ===");
            Console.WriteLine("Goal: CPU + memory + zero-GC hot paths for inference\n");
            
            // Collect system information
            Console.WriteLine("Collecting system metadata...");
            _report.SystemInfo = SystemInfoCollector.Collect();
            _report.ReportTimestamp = DateTime.UtcNow;
            
            // Display system info
            PrintSystemInfo(_report.SystemInfo);
            Console.WriteLine();

            // Warn if not Release build
            if (!_report.SystemInfo.Build.IsReleaseBuild)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  WARNING: Running in Debug mode. Results may not be representative.");
                Console.WriteLine("   Build in Release mode for accurate benchmarks.");
                Console.ResetColor();
                Console.WriteLine();
            }

            // Run all benchmarks
            Console.WriteLine("=== Phase 1: MatMul Benchmarks ===\n");
            BenchmarkMatMul_256x256();
            BenchmarkMatMul_512x512();
            BenchmarkMatMul_1024x1024();
            BenchmarkMatMul_Rectangular();

            Console.WriteLine("\n=== Phase 2: Attention Score Compute Benchmarks ===\n");
            BenchmarkAttentionScoreCompute(256, 64);
            BenchmarkAttentionScoreCompute(256, 128);
            BenchmarkAttentionScoreCompute(1024, 64);
            BenchmarkAttentionScoreCompute(1024, 128);
            BenchmarkAttentionScoreCompute(2048, 64);
            BenchmarkAttentionScoreCompute(2048, 128);

            Console.WriteLine("\n=== Phase 3: Softmax Benchmarks ===\n");
            BenchmarkSoftmax(256, 256);
            BenchmarkSoftmax(1024, 1024);
            BenchmarkSoftmax(2048, 2048);

            Console.WriteLine("\n=== Benchmarks Complete ===\n");
            
            // Generate and save reports
            SaveReports();
            
            Console.WriteLine("\nPress any key to exit...");
        }

        static void PrintSystemInfo(SystemInfo info)
        {
            Console.WriteLine($"CPU: {info.Machine.CpuModel}");
            Console.WriteLine($"  Architecture: {info.Machine.CpuArchitecture}");
            Console.WriteLine($"  Logical Cores: {info.Machine.LogicalCores}");
            Console.WriteLine($"  SIMD Vector Size: {info.Machine.SimdVectorSize} floats");
            
            Console.WriteLine($"\nSIMD Capabilities:");
            foreach (var cap in info.Machine.SimdCapabilities)
            {
                if (cap.Value)
                    Console.WriteLine($"  ✓ {cap.Key}");
            }
            
            Console.WriteLine($"\nRuntime: {info.Runtime.DotNetVersion}");
            Console.WriteLine($"  Framework: {info.Runtime.FrameworkDescription}");
            Console.WriteLine($"  GC Mode: {info.Runtime.GcMode}");
            Console.WriteLine($"  Tiered Compilation: {info.Runtime.TieredCompilation}");
            
            Console.WriteLine($"\nMemory: {info.Memory.TotalMemoryFormatted}");
            Console.WriteLine($"  Available: {info.Memory.AvailableMemoryFormatted}");
            
            Console.WriteLine($"\nOS: {info.OperatingSystem.OsName} {info.OperatingSystem.OsVersion}");
            Console.WriteLine($"Build: {info.Build.Configuration}");
        }

        #region MatMul Benchmarks

        static void BenchmarkMatMul_256x256()
        {
            const int M = 256, K = 256, N = 256;
            BenchmarkMatMul("MatMul 256×256", M, K, N);
        }

        static void BenchmarkMatMul_512x512()
        {
            const int M = 512, K = 512, N = 512;
            BenchmarkMatMul("MatMul 512×512", M, K, N);
        }

        static void BenchmarkMatMul_1024x1024()
        {
            const int M = 1024, K = 1024, N = 1024;
            BenchmarkMatMul("MatMul 1024×1024", M, K, N);
        }

        static void BenchmarkMatMul_Rectangular()
        {
            const int M = 512, K = 2048, N = 512;
            BenchmarkMatMul("MatMul 512×2048×512 (rectangular)", M, K, N);
        }

        static void BenchmarkMatMul(string name, int M, int K, int N)
        {
            Console.WriteLine($"--- {name} ---");
            
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C = new float[M * N];
            
            // Initialize with deterministic random data (seeded RNG)
            Random rand = new Random(42);
            for (int i = 0; i < A.Length; i++) A[i] = (float)rand.NextDouble();
            for (int i = 0; i < B.Length; i++) B[i] = (float)rand.NextDouble();

            // Warmup
            for (int i = 0; i < WARMUP_ITERATIONS; i++)
            {
                MatMulOps.MatMul(A, B, C, M, K, N);
            }

            // Measure allocations
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            
            // Benchmark
            var times = new List<double>(MEASUREMENT_ITERATIONS);
            var sw = new Stopwatch();
            
            for (int i = 0; i < MEASUREMENT_ITERATIONS; i++)
            {
                sw.Restart();
                MatMulOps.MatMul(A, B, C, M, K, N);
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }

            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
            long allocatedBytes = allocAfter - allocBefore;

            // Calculate statistics
            times.Sort();
            double medianMs = times[times.Count / 2];
            double minMs = times[0];
            double maxMs = times[times.Count - 1];
            
            long flops = 2L * M * K * N; // 2 ops per multiply-add
            double gflops = (flops / (medianMs / 1000.0)) / 1e9;
            
            // Checksum to prevent dead code elimination
            double checksum = 0;
            for (int i = 0; i < Math.Min(100, C.Length); i++) checksum += C[i];
            
            Console.WriteLine($"  Size: {M}×{K} × {K}×{N} = {M}×{N}");
            Console.WriteLine($"  Median: {medianMs:F3} ms/op");
            Console.WriteLine($"  Min: {minMs:F3} ms, Max: {maxMs:F3} ms");
            Console.WriteLine($"  Performance: {gflops:F2} GFLOPS");
            Console.WriteLine($"  Allocated: {allocatedBytes} bytes ({allocatedBytes / MEASUREMENT_ITERATIONS:F1} bytes/op)");
            Console.WriteLine($"  Checksum: {checksum:F6} (prevents DCE)");
            
            if (allocatedBytes > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ⚠️  ALLOCATION DETECTED: {allocatedBytes} bytes!");
                Console.ResetColor();
            }
            Console.WriteLine();

            // Record result
            var benchResult = new BenchmarkResult
            {
                Name = name,
                Timestamp = DateTime.UtcNow,
                Parameters = new Dictionary<string, object>
                {
                    ["M"] = M,
                    ["K"] = K,
                    ["N"] = N,
                    ["Iterations"] = MEASUREMENT_ITERATIONS,
                    ["Warmup"] = WARMUP_ITERATIONS
                },
                Metrics = new Dictionary<string, double>
                {
                    ["Median (ms/op)"] = medianMs,
                    ["Min (ms)"] = minMs,
                    ["Max (ms)"] = maxMs,
                    ["Performance (GFLOPS)"] = gflops,
                    ["Allocated (bytes)"] = allocatedBytes,
                    ["Allocated (bytes/op)"] = (double)allocatedBytes / MEASUREMENT_ITERATIONS,
                    ["Checksum"] = checksum
                }
            };
            _report.Results.Add(benchResult);
        }

        #endregion

        #region Attention Score Compute Benchmarks

        static void BenchmarkAttentionScoreCompute(int T, int headSize)
        {
            string name = $"Attention Score (T={T}, headSize={headSize})";
            Console.WriteLine($"--- {name} ---");
            
            // Q and K are both (T × headSize)
            // Compute Q @ K^T = (T × T)
            float[] Q = new float[T * headSize];
            float[] K = new float[T * headSize];
            float[] scores = new float[T * T];
            
            // Initialize with deterministic random data
            Random rand = new Random(42);
            for (int i = 0; i < Q.Length; i++) Q[i] = (float)rand.NextDouble();
            for (int i = 0; i < K.Length; i++) K[i] = (float)rand.NextDouble();

            // Warmup
            for (int i = 0; i < WARMUP_ITERATIONS; i++)
            {
                MatMulOps.MatMulTransposeB(Q, K, scores, T, headSize, T);
            }

            // Measure allocations
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            
            // Benchmark
            var times = new List<double>(MEASUREMENT_ITERATIONS);
            var sw = new Stopwatch();
            
            for (int i = 0; i < MEASUREMENT_ITERATIONS; i++)
            {
                sw.Restart();
                MatMulOps.MatMulTransposeB(Q, K, scores, T, headSize, T);
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }

            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
            long allocatedBytes = allocAfter - allocBefore;

            // Calculate statistics
            times.Sort();
            double medianMs = times[times.Count / 2];
            double minMs = times[0];
            double maxMs = times[times.Count - 1];
            
            long flops = 2L * T * headSize * T; // Q @ K^T
            double gflops = (flops / (medianMs / 1000.0)) / 1e9;
            
            // Checksum to prevent dead code elimination
            double checksum = 0;
            for (int i = 0; i < Math.Min(100, scores.Length); i++) checksum += scores[i];
            
            Console.WriteLine($"  Size: Q={T}×{headSize}, K={T}×{headSize}, Scores={T}×{T}");
            Console.WriteLine($"  Median: {medianMs:F3} ms/op");
            Console.WriteLine($"  Min: {minMs:F3} ms, Max: {maxMs:F3} ms");
            Console.WriteLine($"  Performance: {gflops:F2} GFLOPS");
            Console.WriteLine($"  Allocated: {allocatedBytes} bytes ({allocatedBytes / MEASUREMENT_ITERATIONS:F1} bytes/op)");
            Console.WriteLine($"  Checksum: {checksum:F6}");
            
            if (allocatedBytes > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ⚠️  ALLOCATION DETECTED: {allocatedBytes} bytes!");
                Console.ResetColor();
            }
            Console.WriteLine();

            // Record result
            var benchResult = new BenchmarkResult
            {
                Name = name,
                Timestamp = DateTime.UtcNow,
                Parameters = new Dictionary<string, object>
                {
                    ["T"] = T,
                    ["headSize"] = headSize,
                    ["Iterations"] = MEASUREMENT_ITERATIONS,
                    ["Warmup"] = WARMUP_ITERATIONS
                },
                Metrics = new Dictionary<string, double>
                {
                    ["Median (ms/op)"] = medianMs,
                    ["Min (ms)"] = minMs,
                    ["Max (ms)"] = maxMs,
                    ["Performance (GFLOPS)"] = gflops,
                    ["Allocated (bytes)"] = allocatedBytes,
                    ["Allocated (bytes/op)"] = (double)allocatedBytes / MEASUREMENT_ITERATIONS,
                    ["Checksum"] = checksum
                }
            };
            _report.Results.Add(benchResult);
        }

        #endregion

        #region Softmax Benchmarks

        static void BenchmarkSoftmax(int rows, int cols)
        {
            string name = $"Softmax (rows={rows}, cols={cols})";
            Console.WriteLine($"--- {name} ---");
            
            float[] scores = new float[rows * cols];
            float[] output = new float[rows * cols];
            float scale = 1.0f / MathF.Sqrt(cols); // Typical attention scaling
            
            // Initialize with deterministic random data
            Random rand = new Random(42);
            for (int i = 0; i < scores.Length; i++) 
                scores[i] = (float)rand.NextDouble() * 10;

            // Warmup
            for (int i = 0; i < WARMUP_ITERATIONS; i++)
            {
                OptimizedOps.FusedScaleMaskSoftmax(scores, 0, scale, output, 0, rows, cols, 0);
            }

            // Measure allocations
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            
            // Benchmark
            var times = new List<double>(MEASUREMENT_ITERATIONS);
            var sw = new Stopwatch();
            
            for (int i = 0; i < MEASUREMENT_ITERATIONS; i++)
            {
                sw.Restart();
                OptimizedOps.FusedScaleMaskSoftmax(scores, 0, scale, output, 0, rows, cols, 0);
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
            }

            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
            long allocatedBytes = allocAfter - allocBefore;

            // Calculate statistics
            times.Sort();
            double medianMs = times[times.Count / 2];
            double minMs = times[0];
            double maxMs = times[times.Count - 1];
            
            // Checksum to prevent dead code elimination
            double checksum = 0;
            for (int i = 0; i < Math.Min(100, output.Length); i++) checksum += output[i];
            
            Console.WriteLine($"  Size: {rows}×{cols}");
            Console.WriteLine($"  Median: {medianMs:F3} ms/op");
            Console.WriteLine($"  Min: {minMs:F3} ms, Max: {maxMs:F3} ms");
            Console.WriteLine($"  Allocated: {allocatedBytes} bytes ({allocatedBytes / MEASUREMENT_ITERATIONS:F1} bytes/op)");
            Console.WriteLine($"  Checksum: {checksum:F6}");
            
            if (allocatedBytes > 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ⚠️  ALLOCATION DETECTED: {allocatedBytes} bytes!");
                Console.ResetColor();
            }
            Console.WriteLine();

            // Record result
            var benchResult = new BenchmarkResult
            {
                Name = name,
                Timestamp = DateTime.UtcNow,
                Parameters = new Dictionary<string, object>
                {
                    ["Rows"] = rows,
                    ["Cols"] = cols,
                    ["Scale"] = scale,
                    ["Iterations"] = MEASUREMENT_ITERATIONS,
                    ["Warmup"] = WARMUP_ITERATIONS
                },
                Metrics = new Dictionary<string, double>
                {
                    ["Median (ms/op)"] = medianMs,
                    ["Min (ms)"] = minMs,
                    ["Max (ms)"] = maxMs,
                    ["Allocated (bytes)"] = allocatedBytes,
                    ["Allocated (bytes/op)"] = (double)allocatedBytes / MEASUREMENT_ITERATIONS,
                    ["Checksum"] = checksum
                }
            };
            _report.Results.Add(benchResult);
        }

        #endregion

        static void SaveReports()
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                string outputDir = $"profiler-results-{timestamp}";
                Directory.CreateDirectory(outputDir);

                // Generate markdown report
                var markdownReport = MarkdownReportWriter.GenerateReport(_report);
                var markdownPath = Path.Combine(outputDir, "profiler-benchmark-results.md");
                File.WriteAllText(markdownPath, markdownReport);
                Console.WriteLine($"✓ Markdown report saved to: {markdownPath}");

                // Generate JSON report
                var jsonReport = JsonReportWriter.GenerateReport(_report);
                var jsonPath = Path.Combine(outputDir, "profiler-benchmark-results.json");
                File.WriteAllText(jsonPath, jsonReport);
                Console.WriteLine($"✓ JSON report saved to: {jsonPath}");
                
                Console.WriteLine($"\nAll reports saved to: {outputDir}/");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error saving reports: {ex.Message}");
                Console.ResetColor();
            }
        }
    }
}

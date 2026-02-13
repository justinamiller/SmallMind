using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using SmallMind.Abstractions.Telemetry;
using SmallMind.Quantization.Kernels;
using SmallMind.Quantization.Tensors;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Performance benchmark runner for SmallMind.
    /// Measures TTFT, tok/sec, memory, GC, and allocations.
    /// </summary>
    internal sealed class BenchmarkRunner
    {
        private readonly BenchmarkConfig _config;
        private readonly List<BenchmarkResult> _results = new List<BenchmarkResult>();
        private readonly IRuntimeLogger _logger;

        public BenchmarkRunner(BenchmarkConfig? config = null, IRuntimeLogger? logger = null)
        {
            _config = config ?? new BenchmarkConfig();
            _logger = logger ?? NullRuntimeLogger.Instance;
        }

        /// <summary>
        /// Run all benchmarks.
        /// </summary>
        public void RunAll()
        {
            _logger.Info("=== SmallMind Performance Benchmarks ===");
            _logger.Info("");

            CheckConfiguration();
            CollectEnvironmentInfo();

            // Run FP32 MatMul benchmarks (targeting 60+ GFLOPS)
            _logger.Info("\n=== FP32 MatMul Benchmarks (GFLOPS Target: 60+) ===\n");
            RunFP32MatMulBenchmark(64, 64, 64);
            RunFP32MatMulBenchmark(128, 128, 128);
            RunFP32MatMulBenchmark(256, 256, 256);
            RunFP32MatMulBenchmark(512, 512, 512);
            RunFP32MatMulBenchmark(1024, 1024, 1024);
            RunFP32MatMulBenchmark(2048, 2048, 2048);

            // Run Q4 quantized benchmarks (for comparison)
            _logger.Info("\n=== Q4 Quantized MatMul Benchmarks ===\n");
            RunQ4MatMulComparison(128, 128, 128);
            RunQ4MatMulComparison(256, 256, 256);
            RunQ4MatMulComparison(512, 512, 512);

            // Write reports
            WriteReports();
        }

        private void CheckConfiguration()
        {
#if DEBUG
            _logger.Warn("WARNING: Running in Debug mode. Results may not be representative.");
            _logger.Warn("Please run in Release mode for accurate benchmarks.");
            _logger.Info("");
#endif

            _logger.Info($"Configuration:");
            _logger.Info($"  Warmup iterations: {_config.WarmupIterations}");
            _logger.Info($"  Measured iterations: {_config.MeasuredIterations}");
            _logger.Info($"  Seed: {_config.Seed}");
            _logger.Info("");
        }

        private Dictionary<string, object> _environment = new Dictionary<string, object>();

        private void CollectEnvironmentInfo()
        {
            _environment["OS"] = RuntimeInformation.OSDescription;
            _environment["Architecture"] = RuntimeInformation.ProcessArchitecture.ToString();
            _environment["Framework"] = RuntimeInformation.FrameworkDescription;
            _environment["ProcessorCount"] = Environment.ProcessorCount;
            _environment["GCMode"] = GCSettings.IsServerGC ? "Server" : "Workstation";
            _environment["GCLatencyMode"] = GCSettings.LatencyMode.ToString();

            // SIMD capabilities
            _environment["Vector<float>.Count"] = System.Numerics.Vector<float>.Count;
            _environment["Vector.IsHardwareAccelerated"] = System.Numerics.Vector.IsHardwareAccelerated;

#if NET7_0_OR_GREATER
            _environment["Avx2.IsSupported"] = System.Runtime.Intrinsics.X86.Avx2.IsSupported;
            _environment["Avx512F.IsSupported"] = System.Runtime.Intrinsics.X86.Avx512F.IsSupported;
#endif
        }

        /// <summary>
        /// Benchmark FP32 matrix multiplication (pure SIMD performance).
        /// This tests raw MatMul GFLOPS without quantization overhead.
        /// Target: 60+ GFLOPS on 128×128 matrices.
        /// </summary>
        private void RunFP32MatMulBenchmark(int m, int k, int n)
        {
            _logger.Info($"Running FP32 MatMul Benchmark: {m}x{k} * {k}x{n}");

            // Create test data
            var random = new Random(_config.Seed);
            var a = new float[m * k];
            for (int i = 0; i < a.Length; i++)
                a[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            var b = new float[k * n];
            for (int i = 0; i < b.Length; i++)
                b[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            var c = new float[m * n];

            // Warmup
            _logger.Info($"  Warmup ({_config.WarmupIterations} iterations)... ");
            for (int i = 0; i < _config.WarmupIterations; i++)
            {
                Array.Clear(c);
                SmallMind.Core.Simd.MatMulOps.MatMul(a, b, c, m, k, n);
            }
            _logger.Info("Done");

            // Benchmark
            _logger.Info($"  Measuring ({_config.MeasuredIterations} iterations)... ");

            var collector = new MetricsCollector();
            var times = new List<double>();

            collector.Start();

            for (int i = 0; i < _config.MeasuredIterations; i++)
            {
                Array.Clear(c);

                var sw = Stopwatch.StartNew();
                SmallMind.Core.Simd.MatMulOps.MatMul(a, b, c, m, k, n);
                sw.Stop();

                times.Add(sw.Elapsed.TotalMilliseconds);

                if (i % 3 == 0)
                    collector.UpdatePeak();
            }

            var (alloc, gen0, gen1, gen2, peakRSS, managedHeap) = collector.Stop();
            _logger.Info("Done");

            // Calculate statistics
            times.Sort();
            var median = times[times.Count / 2];
            var mean = 0.0;
            foreach (var t in times)
                mean += t;
            mean /= times.Count;

            // Calculate GFLOPS
            // FP32 MatMul: 2*M*N*K FLOPs (multiply-add counted as 2 operations)
            var operations = 2.0 * m * n * k;
            var gflops = operations / (mean * 1e6);

            // Get kernel info
            var kernel = SmallMind.Core.Simd.MatMulOps.LastKernelUsed.ToString();

            _logger.Info($"  Time:      {mean:F3} ms (median: {median:F3} ms)");
            _logger.Info($"  GFLOPS:    {gflops:F2}");
            _logger.Info($"  Kernel:    {kernel}");
            _logger.Info($"  Allocated: {alloc / 1024.0:F2} KB, Gen0: {gen0}");

            // Highlight if we're meeting the 60+ GFLOPS goal
            if (gflops >= 60.0)
            {
                _logger.Info($"  ✓ GOAL MET: {gflops:F2} GFLOPS >= 60.0 GFLOPS");
            }
            else if (gflops >= 40.0)
            {
                _logger.Warn($"  ⚠ CLOSE: {gflops:F2} GFLOPS (goal: 60+ GFLOPS)");
            }
            else
            {
                _logger.Error($"  ✗ BELOW TARGET: {gflops:F2} GFLOPS (goal: 60+ GFLOPS)");
            }
            _logger.Info("");

            // Store result
            var result = new BenchmarkResult
            {
                Name = $"FP32MatMul_{m}x{k}x{n}",
                Config = _config,
                Metrics = new PerformanceMetrics
                {
                    AllocatedBytesForDecode = alloc,
                    Gen0Collections = gen0,
                    Gen1Collections = gen1,
                    Gen2Collections = gen2,
                    PeakRSS = peakRSS,
                    ManagedHeapSize = managedHeap,
                    CustomMetrics = new Dictionary<string, object>
                    {
                        ["TimeMs"] = mean,
                        ["MedianMs"] = median,
                        ["GFLOPS"] = gflops,
                        ["Kernel"] = kernel,
                        ["M"] = m,
                        ["K"] = k,
                        ["N"] = n,
                        ["Operations"] = operations,
                        ["MeetsGoal"] = gflops >= 60.0
                    }
                },
                Environment = _environment
            };

            _results.Add(result);
        }

        /// <summary>
        /// Benchmark Q4 matrix multiplication - comparing original vs optimized.
        /// </summary>
        private void RunQ4MatMulComparison(int m, int k, int n)
        {
            _logger.Info($"Running Q4 MatMul Comparison: {m}x{k} * {k}x{n}");

            // Create test data
            var random = new Random(_config.Seed);
            var a = new float[m * k];
            for (int i = 0; i < a.Length; i++)
                a[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            var bFloat = new float[k * n];
            for (int i = 0; i < bFloat.Length; i++)
                bFloat[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            var b = Q4Tensor.Quantize(bFloat, k, n, blockSize: 64);
            var c = new float[m * n];

            // Warmup
            _logger.Info($"  Warmup ({_config.WarmupIterations} iterations)... ");
            for (int i = 0; i < _config.WarmupIterations; i++)
            {
                Array.Clear(c);
                MatMulF32Q4.Multiply(a, b, c, m, k, n);
            }
            _logger.Info("Done");

            // Benchmark original
            _logger.Info($"  Measuring Original ({_config.MeasuredIterations} iterations)... ");

            var collector = new MetricsCollector();
            var timesOriginal = new List<double>();

            collector.Start();

            for (int i = 0; i < _config.MeasuredIterations; i++)
            {
                Array.Clear(c);

                var sw = Stopwatch.StartNew();
                MatMulF32Q4.Multiply(a, b, c, m, k, n);
                sw.Stop();

                timesOriginal.Add(sw.Elapsed.TotalMilliseconds);

                if (i % 3 == 0)
                    collector.UpdatePeak();
            }

            var (allocOriginal, gen0Original, gen1Original, gen2Original, peakRSSOriginal, managedHeapOriginal) = collector.Stop();
            _logger.Info("Done");

            // Warmup optimized
            for (int i = 0; i < _config.WarmupIterations; i++)
            {
                Array.Clear(c);
                MatMulF32Q4Optimized.Multiply(a, b, c, m, k, n);
            }

            // Benchmark optimized
            _logger.Info($"  Measuring Optimized ({_config.MeasuredIterations} iterations)... ");

            collector = new MetricsCollector();
            var timesOptimized = new List<double>();

            collector.Start();

            for (int i = 0; i < _config.MeasuredIterations; i++)
            {
                Array.Clear(c);

                var sw = Stopwatch.StartNew();
                MatMulF32Q4Optimized.Multiply(a, b, c, m, k, n);
                sw.Stop();

                timesOptimized.Add(sw.Elapsed.TotalMilliseconds);

                if (i % 3 == 0)
                    collector.UpdatePeak();
            }

            var (allocOptimized, gen0Optimized, gen1Optimized, gen2Optimized, peakRSSOptimized, managedHeapOptimized) = collector.Stop();
            _logger.Info("Done");

            // Calculate statistics
            timesOriginal.Sort();
            timesOptimized.Sort();
            var medianOriginal = timesOriginal[timesOriginal.Count / 2];
            var medianOptimized = timesOptimized[timesOptimized.Count / 2];
            var meanOriginal = 0.0;
            var meanOptimized = 0.0;
            foreach (var t in timesOriginal)
                meanOriginal += t;
            foreach (var t in timesOptimized)
                meanOptimized += t;
            meanOriginal /= timesOriginal.Count;
            meanOptimized /= timesOptimized.Count;

            // Calculate GFLOPS
            var operations = 2.0 * m * n * k;
            var gflopsOriginal = operations / (meanOriginal * 1e6);
            var gflopsOptimized = operations / (meanOptimized * 1e6);

            var speedup = meanOriginal / meanOptimized;

            _logger.Info($"  Original:  {meanOriginal:F3} ms (median: {medianOriginal:F3} ms), {gflopsOriginal:F2} GFLOPS");
            _logger.Info($"  Optimized: {meanOptimized:F3} ms (median: {medianOptimized:F3} ms), {gflopsOptimized:F2} GFLOPS");
            _logger.Info($"  Speedup:   {speedup:F2}x");
            _logger.Info($"  Allocated (Original): {allocOriginal / 1024.0:F2} KB, Gen0: {gen0Original}");
            _logger.Info($"  Allocated (Optimized): {allocOptimized / 1024.0:F2} KB, Gen0: {gen0Optimized}");
            _logger.Info("");

            // Store results for both
            var resultOriginal = new BenchmarkResult
            {
                Name = $"Q4MatMul_Original_{m}x{k}x{n}",
                Config = _config,
                Metrics = new PerformanceMetrics
                {
                    AllocatedBytesForDecode = allocOriginal,
                    Gen0Collections = gen0Original,
                    Gen1Collections = gen1Original,
                    Gen2Collections = gen2Original,
                    PeakRSS = peakRSSOriginal,
                    ManagedHeapSize = managedHeapOriginal,
                    CustomMetrics = new Dictionary<string, object>
                    {
                        ["TimeMs"] = meanOriginal,
                        ["MedianMs"] = medianOriginal,
                        ["GFLOPS"] = gflopsOriginal,
                        ["M"] = m,
                        ["K"] = k,
                        ["N"] = n,
                        ["Variant"] = "Original"
                    }
                },
                Environment = _environment
            };

            var resultOptimized = new BenchmarkResult
            {
                Name = $"Q4MatMul_Optimized_{m}x{k}x{n}",
                Config = _config,
                Metrics = new PerformanceMetrics
                {
                    AllocatedBytesForDecode = allocOptimized,
                    Gen0Collections = gen0Optimized,
                    Gen1Collections = gen1Optimized,
                    Gen2Collections = gen2Optimized,
                    PeakRSS = peakRSSOptimized,
                    ManagedHeapSize = managedHeapOptimized,
                    CustomMetrics = new Dictionary<string, object>
                    {
                        ["TimeMs"] = meanOptimized,
                        ["MedianMs"] = medianOptimized,
                        ["GFLOPS"] = gflopsOptimized,
                        ["Speedup"] = speedup,
                        ["M"] = m,
                        ["K"] = k,
                        ["N"] = n,
                        ["Variant"] = "Optimized"
                    }
                },
                Environment = _environment
            };

            _results.Add(resultOriginal);
            _results.Add(resultOptimized);
        }

        private void WriteReports()
        {
            _logger.Info("Writing reports...");

            var outputDir = _config.OutputDirectory;
            Directory.CreateDirectory(outputDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");

            var formats = _config.OutputFormats ?? new List<string> { "markdown" };

            foreach (var format in formats)
            {
                if (format.Equals("json", StringComparison.OrdinalIgnoreCase))
                {
                    var jsonPath = Path.Combine(outputDir, $"perf-results-{timestamp}.json");
                    JsonReportWriter.WriteReport(jsonPath, _results);
                    _logger.Info($"  JSON: {jsonPath}");

                    // Also write latest
                    var latestJsonPath = Path.Combine(outputDir, "perf-results-latest.json");
                    JsonReportWriter.WriteReport(latestJsonPath, _results);
                }
                else if (format.Equals("markdown", StringComparison.OrdinalIgnoreCase) ||
                         format.Equals("md", StringComparison.OrdinalIgnoreCase))
                {
                    var mdPath = Path.Combine(outputDir, $"perf-results-{timestamp}.md");

                    // Try to load baseline (look in common locations)
                    var baselinePath = Path.Combine(outputDir, "baseline.json");
                    if (!File.Exists(baselinePath))
                    {
                        baselinePath = "/home/runner/work/SmallMind/SmallMind/perf/baselines/baseline.json";
                    }

                    var baseline = File.Exists(baselinePath) ? JsonReportWriter.ReadBaseline(baselinePath) : null;

                    MarkdownReportWriter.WriteReport(mdPath, _results, baseline);
                    _logger.Info($"  Markdown: {mdPath}");

                    // Also write latest
                    var latestMdPath = Path.Combine(outputDir, "perf-results-latest.md");
                    MarkdownReportWriter.WriteReport(latestMdPath, _results, baseline);

                    // Check for regressions if baseline exists
                    if (baseline != null)
                    {
                        CheckRegressions(baseline);
                    }
                }
            }

            _logger.Info("");
            _logger.Info("Benchmark complete!");
        }

        private void CheckRegressions(List<BenchmarkResult> baseline)
        {
            _logger.Info("");
            _logger.Info("=== Regression Check ===");

            var hasRegressions = false;

            foreach (var result in _results)
            {
                var baselineResult = baseline.Find(r => r.Name == result.Name);
                if (baselineResult == null)
                    continue;

                // Check allocations per iteration
                var currentAlloc = result.Metrics.AllocatedBytesForDecode / (double)_config.MeasuredIterations;
                var baselineAlloc = baselineResult.Metrics.AllocatedBytesForDecode / (double)baselineResult.Config.MeasuredIterations;

                if (currentAlloc > baselineAlloc * 1.1) // 10% threshold
                {
                    _logger.Error($"❌ {result.Name}: Allocation regression!");
                    _logger.Error($"   Current: {currentAlloc:F0} bytes, Baseline: {baselineAlloc:F0} bytes");
                    hasRegressions = true;
                }

                // Check Gen0 collections
                if (result.Metrics.Gen0Collections > baselineResult.Metrics.Gen0Collections)
                {
                    _logger.Error($"❌ {result.Name}: Gen0 collection increase!");
                    _logger.Error($"   Current: {result.Metrics.Gen0Collections}, Baseline: {baselineResult.Metrics.Gen0Collections}");
                    hasRegressions = true;
                }
            }

            if (!hasRegressions)
            {
                _logger.Info("✅ No regressions detected!");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.IO;
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

        public BenchmarkRunner(BenchmarkConfig? config = null)
        {
            _config = config ?? new BenchmarkConfig();
        }

        /// <summary>
        /// Run all benchmarks.
        /// </summary>
        public void RunAll()
        {
            Console.WriteLine("=== SmallMind Performance Benchmarks ===");
            Console.WriteLine();

            CheckConfiguration();
            CollectEnvironmentInfo();

            // Run benchmarks - compare original vs optimized
            RunQ4MatMulComparison(128, 128, 128);
            RunQ4MatMulComparison(256, 256, 256);
            RunQ4MatMulComparison(512, 512, 512);
            // Skip 1024x1024 for now (too slow with current kernel)
            // RunQ4MatMulBenchmark(1024, 1024, 1024);

            // Write reports
            WriteReports();
        }

        private void CheckConfiguration()
        {
#if DEBUG
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("WARNING: Running in Debug mode. Results may not be representative.");
            Console.WriteLine("Please run in Release mode for accurate benchmarks.");
            Console.ResetColor();
            Console.WriteLine();
#endif

            Console.WriteLine($"Configuration:");
            Console.WriteLine($"  Warmup iterations: {_config.WarmupIterations}");
            Console.WriteLine($"  Measured iterations: {_config.MeasuredIterations}");
            Console.WriteLine($"  Seed: {_config.Seed}");
            Console.WriteLine();
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
        /// Benchmark Q4 matrix multiplication - comparing original vs optimized.
        /// </summary>
        private void RunQ4MatMulComparison(int m, int k, int n)
        {
            Console.WriteLine($"Running Q4 MatMul Comparison: {m}x{k} * {k}x{n}");

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
            Console.Write($"  Warmup ({_config.WarmupIterations} iterations)... ");
            for (int i = 0; i < _config.WarmupIterations; i++)
            {
                Array.Clear(c);
                MatMulF32Q4.Multiply(a, b, c, m, k, n);
            }
            Console.WriteLine("Done");

            // Benchmark original
            Console.Write($"  Measuring Original ({_config.MeasuredIterations} iterations)... ");
            
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
            Console.WriteLine("Done");

            // Warmup optimized
            for (int i = 0; i < _config.WarmupIterations; i++)
            {
                Array.Clear(c);
                MatMulF32Q4Optimized.Multiply(a, b, c, m, k, n);
            }

            // Benchmark optimized
            Console.Write($"  Measuring Optimized ({_config.MeasuredIterations} iterations)... ");
            
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
            Console.WriteLine("Done");

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

            Console.WriteLine($"  Original:  {meanOriginal:F3} ms (median: {medianOriginal:F3} ms), {gflopsOriginal:F2} GFLOPS");
            Console.WriteLine($"  Optimized: {meanOptimized:F3} ms (median: {medianOptimized:F3} ms), {gflopsOptimized:F2} GFLOPS");
            Console.WriteLine($"  Speedup:   {speedup:F2}x");
            Console.WriteLine($"  Allocated (Original): {allocOriginal / 1024.0:F2} KB, Gen0: {gen0Original}");
            Console.WriteLine($"  Allocated (Optimized): {allocOptimized / 1024.0:F2} KB, Gen0: {gen0Optimized}");
            Console.WriteLine();

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
            Console.WriteLine("Writing reports...");

            var outputDir = "/home/runner/work/SmallMind/SmallMind/artifacts/perf";
            Directory.CreateDirectory(outputDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var jsonPath = Path.Combine(outputDir, $"perf-results-{timestamp}.json");
            var mdPath = Path.Combine(outputDir, $"perf-results-{timestamp}.md");

            // Try to load baseline
            var baselinePath = "/home/runner/work/SmallMind/SmallMind/perf/baselines/baseline.json";
            var baseline = JsonReportWriter.ReadBaseline(baselinePath);

            // Write JSON
            JsonReportWriter.WriteReport(jsonPath, _results);
            Console.WriteLine($"  JSON: {jsonPath}");

            // Write Markdown
            MarkdownReportWriter.WriteReport(mdPath, _results, baseline);
            Console.WriteLine($"  Markdown: {mdPath}");

            // Also write latest
            var latestJsonPath = Path.Combine(outputDir, "perf-results-latest.json");
            var latestMdPath = Path.Combine(outputDir, "perf-results-latest.md");
            JsonReportWriter.WriteReport(latestJsonPath, _results);
            MarkdownReportWriter.WriteReport(latestMdPath, _results, baseline);

            Console.WriteLine();
            Console.WriteLine("Benchmark complete!");

            // Check for regressions if baseline exists
            if (baseline != null)
            {
                CheckRegressions(baseline);
            }
        }

        private void CheckRegressions(List<BenchmarkResult> baseline)
        {
            Console.WriteLine();
            Console.WriteLine("=== Regression Check ===");

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
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ {result.Name}: Allocation regression!");
                    Console.WriteLine($"   Current: {currentAlloc:F0} bytes, Baseline: {baselineAlloc:F0} bytes");
                    Console.ResetColor();
                    hasRegressions = true;
                }

                // Check Gen0 collections
                if (result.Metrics.Gen0Collections > baselineResult.Metrics.Gen0Collections)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"❌ {result.Name}: Gen0 collection increase!");
                    Console.WriteLine($"   Current: {result.Metrics.Gen0Collections}, Baseline: {baselineResult.Metrics.Gen0Collections}");
                    Console.ResetColor();
                    hasRegressions = true;
                }
            }

            if (!hasRegressions)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ No regressions detected!");
                Console.ResetColor();
            }
        }
    }
}

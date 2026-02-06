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
    public sealed class BenchmarkRunner
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

            // Run benchmarks
            RunQ4MatMulBenchmark(128, 128, 128);
            RunQ4MatMulBenchmark(256, 256, 256);
            RunQ4MatMulBenchmark(512, 512, 512);
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
        /// Benchmark Q4 matrix multiplication.
        /// </summary>
        private void RunQ4MatMulBenchmark(int m, int k, int n)
        {
            Console.WriteLine($"Running Q4 MatMul Benchmark: {m}x{k} * {k}x{n}");

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

            // Measured iterations
            Console.Write($"  Measuring ({_config.MeasuredIterations} iterations)... ");
            
            var collector = new MetricsCollector();
            var times = new List<double>();

            collector.Start();

            for (int i = 0; i < _config.MeasuredIterations; i++)
            {
                Array.Clear(c);

                var sw = Stopwatch.StartNew();
                MatMulF32Q4.Multiply(a, b, c, m, k, n);
                sw.Stop();

                times.Add(sw.Elapsed.TotalMilliseconds);
                
                if (i % 3 == 0)
                    collector.UpdatePeak();
            }

            var (allocatedBytes, gen0, gen1, gen2, peakRSS, managedHeap) = collector.Stop();
            Console.WriteLine("Done");

            // Calculate statistics
            times.Sort();
            var median = times[times.Count / 2];
            var mean = 0.0;
            foreach (var t in times)
                mean += t;
            mean /= times.Count;

            // Calculate GFLOPS: 2*M*N*K operations
            var operations = 2.0 * m * n * k;
            var gflops = operations / (mean * 1e6); // ms to seconds, ops to GFLOPS

            Console.WriteLine($"  Time: {mean:F3} ms (median: {median:F3} ms)");
            Console.WriteLine($"  Performance: {gflops:F2} GFLOPS");
            Console.WriteLine($"  Allocated: {allocatedBytes / 1024.0:F2} KB, Gen0: {gen0}, Gen1: {gen1}, Gen2: {gen2}");
            Console.WriteLine();

            // Store result
            var result = new BenchmarkResult
            {
                Name = $"Q4MatMul_{m}x{k}x{n}",
                Config = _config,
                Metrics = new PerformanceMetrics
                {
                    AllocatedBytesForDecode = allocatedBytes,
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
                        ["M"] = m,
                        ["K"] = k,
                        ["N"] = n
                    }
                },
                Environment = _environment
            };

            _results.Add(result);
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

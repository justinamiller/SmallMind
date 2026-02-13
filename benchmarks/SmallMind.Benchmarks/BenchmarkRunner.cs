using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Benchmarks.Benchmarks;
using SmallMind.Benchmarks.Diagnostics;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Orchestrates the execution of all benchmarks.
    /// </summary>
    internal sealed class BenchmarkRunner
    {
        private readonly BenchmarkConfig _config;
        private readonly SystemInfo _systemInfo;

        public BenchmarkRunner(BenchmarkConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _systemInfo = SystemInfo.Collect();
        }

        public async Task<BenchmarkResults> RunAllAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var metrics = new List<BenchmarkMetric>();
            var errors = new List<string>();
            var warnings = new List<string>();

            Console.WriteLine("Starting benchmarks...");
            Console.WriteLine();
            Console.WriteLine("System Information:");
            Console.WriteLine($"  {_systemInfo}");
            Console.WriteLine($"  SIMD: {_systemInfo.Simd}");
            Console.WriteLine();

#if DEBUG
            warnings.Add("Running in DEBUG mode - results may not be representative. Use Release build for accurate benchmarks.");
            Console.WriteLine("⚠️ WARNING: Running in DEBUG mode!");
            Console.WriteLine();
#endif

            try
            {
                // Benchmark 1: Single Stream Decode Tokens/sec + TTFT
                try
                {
                    var singleStreamBenchmark = new SingleStreamDecodeBenchmark(_config);
                    var metric = await singleStreamBenchmark.RunAsync(cancellationToken);
                    metrics.Add(metric);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    errors.Add($"Single stream benchmark failed: {ex.Message}");
                    Console.WriteLine($"❌ Error: {ex.Message}");
                    Console.WriteLine();
                }

                // Benchmark 2: Concurrent Streams Decode Tokens/sec
                try
                {
                    var concurrentStreamBenchmark = new ConcurrentStreamDecodeBenchmark(_config);
                    var metric = await concurrentStreamBenchmark.RunAsync(cancellationToken);
                    metrics.Add(metric);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    errors.Add($"Concurrent stream benchmark failed: {ex.Message}");
                    Console.WriteLine($"❌ Error: {ex.Message}");
                    Console.WriteLine();
                }

                // Benchmark 3: Memory Growth per Token
                try
                {
                    var memoryGrowthBenchmark = new MemoryGrowthBenchmark(_config);
                    var metric = await memoryGrowthBenchmark.RunAsync(cancellationToken);
                    metrics.Add(metric);
                    Console.WriteLine();
                }
                catch (Exception ex)
                {
                    errors.Add($"Memory growth benchmark failed: {ex.Message}");
                    Console.WriteLine($"❌ Error: {ex.Message}");
                    Console.WriteLine();
                }

                // Additional benchmarks can be added here:
                // - GFLOPS measurement
                // - Quantization format coverage
                // - Soak stability test

                var endTime = DateTime.UtcNow;
                var status = errors.Count == 0 ? "Success" : "Partial";

                return new BenchmarkResults
                {
                    SystemInfo = _systemInfo,
                    Config = _config,
                    StartTime = startTime,
                    EndTime = endTime,
                    TotalDuration = endTime - startTime,
                    Metrics = metrics,
                    Status = status,
                    Errors = errors,
                    Warnings = warnings
                };
            }
            catch (Exception ex)
            {
                errors.Add($"Benchmark execution failed: {ex.Message}");

                return new BenchmarkResults
                {
                    SystemInfo = _systemInfo,
                    Config = _config,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    TotalDuration = DateTime.UtcNow - startTime,
                    Metrics = metrics,
                    Status = "Failed",
                    Errors = errors,
                    Warnings = warnings
                };
            }
        }
    }
}

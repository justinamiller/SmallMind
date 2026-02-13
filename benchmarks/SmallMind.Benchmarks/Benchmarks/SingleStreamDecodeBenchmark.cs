using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Benchmarks.Diagnostics;
using SmallMind.Benchmarks.Utils;

namespace SmallMind.Benchmarks.Benchmarks
{
    /// <summary>
    /// Benchmark for measuring decode tokens per second (single stream).
    /// </summary>
    internal sealed class SingleStreamDecodeBenchmark
    {
        private readonly BenchmarkConfig _config;

        public SingleStreamDecodeBenchmark(BenchmarkConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<BenchmarkMetric> RunAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Running Single Stream Decode Benchmark...");

            var throughputSamples = new List<double>();
            var ttftSamples = new List<double>();
            var memoryTracker = new MemoryTracker();

            memoryTracker.Start();

            try
            {
                // Create SmallMind engine
                var options = new SmallMindOptions
                {
                    ModelPath = _config.ModelPath,
                    MaxContextTokens = _config.ContextSize,
                    EnableKvCache = _config.EnableKVCache,
                    AllowGgufImport = true,
                    ThreadCount = Environment.ProcessorCount
                };

                using var engine = SmallMindFactory.Create(options);

                // Warmup
                Console.WriteLine($"  Warmup ({_config.WarmupIterations} iterations)...");
                for (int i = 0; i < _config.WarmupIterations; i++)
                {
                    await RunSingleInferenceAsync(engine, warmup: true, cancellationToken);
                }

                Console.WriteLine($"  Measuring ({_config.MeasuredIterations} iterations)...");

                // Measured iterations
                for (int i = 0; i < _config.MeasuredIterations; i++)
                {
                    var (tokensPerSec, ttftMs) = await RunSingleInferenceAsync(engine, warmup: false, cancellationToken);
                    throughputSamples.Add(tokensPerSec);
                    ttftSamples.Add(ttftMs);

                    if (i % 5 == 0)
                        memoryTracker.UpdatePeak();

                    if (cancellationToken.IsCancellationRequested)
                        break;
                }

                var memory = memoryTracker.GetSnapshot();

                // Calculate statistics
                var throughputStats = PercentileCalculator.CalculateStatistics(throughputSamples);
                var ttftStats = PercentileCalculator.CalculateStatistics(ttftSamples);

                Console.WriteLine($"  Throughput: {throughputStats.Mean:F2} tok/s (median: {throughputStats.Median:F2}, p95: {throughputStats.P95:F2})");
                Console.WriteLine($"  TTFT: {ttftStats.Mean:F2} ms (p50: {ttftStats.P50:F2}, p95: {ttftStats.P95:F2})");
                Console.WriteLine($"  Memory: {FormatHelper.FormatBytes(memory.PeakWorkingSetBytes)}");

                return new BenchmarkMetric
                {
                    Name = "decode_single_stream",
                    Category = "Throughput",
                    Value = throughputStats.Mean,
                    Unit = "tok/s",
                    Statistics = new Statistics
                    {
                        Count = throughputStats.Count,
                        Min = throughputStats.Min,
                        Max = throughputStats.Max,
                        Mean = throughputStats.Mean,
                        Median = throughputStats.Median,
                        P50 = throughputStats.P50,
                        P95 = throughputStats.P95,
                        P99 = throughputStats.P99,
                        StdDev = throughputStats.StdDev
                    },
                    Metadata = new Dictionary<string, object>
                    {
                        ["ttft_mean_ms"] = ttftStats.Mean,
                        ["ttft_p50_ms"] = ttftStats.P50,
                        ["ttft_p95_ms"] = ttftStats.P95,
                        ["peak_memory_bytes"] = memory.PeakWorkingSetBytes,
                        ["managed_memory_bytes"] = memory.ManagedMemoryBytes,
                        ["gen0_collections"] = memory.Gen0Collections,
                        ["gen1_collections"] = memory.Gen1Collections,
                        ["gen2_collections"] = memory.Gen2Collections
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
                return new BenchmarkMetric
                {
                    Name = "decode_single_stream",
                    Category = "Throughput",
                    Value = 0,
                    Unit = "tok/s",
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["status"] = "failed"
                    }
                };
            }
        }

        private async Task<(double tokensPerSec, double ttftMs)> RunSingleInferenceAsync(
            ISmallMindEngine engine,
            bool warmup,
            CancellationToken cancellationToken)
        {
            var sessionOptions = new TextGenerationOptions
            {
                Temperature = 0.8f,
                TopP = 0.95f,
                MaxOutputTokens = _config.MaxTokensPerRequest
            };

            using var session = engine.CreateTextGenerationSession(sessionOptions);

            var request = new TextGenerationRequest
            {
                Prompt = "The quick brown fox jumps over the lazy dog. Once upon a time in a distant land".AsMemory()
            };

            var timer = new PrecisionTimer();
            timer.Start();

            var result = session.Generate(request);

            timer.Stop();

            if (result.Usage.CompletionTokens == 0)
                return (0, 0);

            double totalMs = timer.ElapsedMilliseconds;
            double tokensPerSec = result.Usage.CompletionTokens / (totalMs / 1000.0);
            double ttftMs = result.Timings?.TimeToFirstTokenMs ?? 0;

            await Task.CompletedTask;
            return (tokensPerSec, ttftMs);
        }
    }
}

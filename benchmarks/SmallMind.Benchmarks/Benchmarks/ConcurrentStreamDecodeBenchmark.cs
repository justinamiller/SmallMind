using SmallMind.Benchmarks.Diagnostics;
using SmallMind.Benchmarks.Utils;

namespace SmallMind.Benchmarks.Benchmarks
{
    /// <summary>
    /// Benchmark for measuring decode tokens per second with N concurrent streams.
    /// </summary>
    internal sealed class ConcurrentStreamDecodeBenchmark
    {
        private readonly BenchmarkConfig _config;

        public ConcurrentStreamDecodeBenchmark(BenchmarkConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<BenchmarkMetric> RunAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"Running Concurrent Stream Decode Benchmark (N={_config.ConcurrentStreams})...");

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
                    await RunConcurrentInferenceAsync(engine, warmup: true, cancellationToken);
                }

                Console.WriteLine($"  Measuring ({_config.MeasuredIterations} iterations)...");

                var throughputSamples = new List<double>();

                // Measured iterations
                for (int i = 0; i < _config.MeasuredIterations; i++)
                {
                    var tokensPerSec = await RunConcurrentInferenceAsync(engine, warmup: false, cancellationToken);
                    throughputSamples.Add(tokensPerSec);

                    if (i % 3 == 0)
                        memoryTracker.UpdatePeak();

                    if (cancellationToken.IsCancellationRequested)
                        break;
                }

                var memory = memoryTracker.GetSnapshot();

                // Calculate statistics
                var stats = PercentileCalculator.CalculateStatistics(throughputSamples);

                Console.WriteLine($"  Aggregate Throughput: {stats.Mean:F2} tok/s (median: {stats.Median:F2}, p95: {stats.P95:F2})");
                Console.WriteLine($"  Per-Stream Throughput: {stats.Mean / _config.ConcurrentStreams:F2} tok/s");
                Console.WriteLine($"  Memory: {FormatHelper.FormatBytes(memory.PeakWorkingSetBytes)}");

                return new BenchmarkMetric
                {
                    Name = $"decode_concurrent_streams_n{_config.ConcurrentStreams}",
                    Category = "Throughput",
                    Value = stats.Mean,
                    Unit = "tok/s",
                    Statistics = new Statistics
                    {
                        Count = stats.Count,
                        Min = stats.Min,
                        Max = stats.Max,
                        Mean = stats.Mean,
                        Median = stats.Median,
                        P50 = stats.P50,
                        P95 = stats.P95,
                        P99 = stats.P99,
                        StdDev = stats.StdDev
                    },
                    Metadata = new Dictionary<string, object>
                    {
                        ["concurrent_streams"] = _config.ConcurrentStreams,
                        ["per_stream_throughput"] = stats.Mean / _config.ConcurrentStreams,
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
                    Name = $"decode_concurrent_streams_n{_config.ConcurrentStreams}",
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

        private async Task<double> RunConcurrentInferenceAsync(
            ISmallMindEngine engine,
            bool warmup,
            CancellationToken cancellationToken)
        {
            var tasks = new List<Task<(int tokens, double ms)>>();
            var timer = new PrecisionTimer();

            timer.Start();

            // Launch concurrent inference tasks
            for (int i = 0; i < _config.ConcurrentStreams; i++)
            {
                tasks.Add(Task.Run(async () =>
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
                        Prompt = "The quick brown fox jumps over the lazy dog.".AsMemory()
                    };

                    var localTimer = new PrecisionTimer();
                    localTimer.Start();

                    var result = session.Generate(request);

                    localTimer.Stop();

                    await Task.CompletedTask;
                    return (result.Usage.CompletionTokens, localTimer.ElapsedMilliseconds);
                }, cancellationToken));
            }

            // Wait for all to complete
            var results = await Task.WhenAll(tasks);

            timer.Stop();

            // Calculate aggregate throughput
            int totalTokens = results.Sum(r => r.tokens);
            double totalTimeSeconds = timer.ElapsedSeconds;

            if (totalTimeSeconds == 0)
                return 0;

            return totalTokens / totalTimeSeconds;
        }
    }
}

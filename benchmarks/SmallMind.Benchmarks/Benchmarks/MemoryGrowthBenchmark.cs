using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Benchmarks.Diagnostics;
using SmallMind.Benchmarks.Utils;

namespace SmallMind.Benchmarks.Benchmarks
{
    /// <summary>
    /// Benchmark for measuring memory growth per generated token.
    /// </summary>
    internal sealed class MemoryGrowthBenchmark
    {
        private readonly BenchmarkConfig _config;

        public MemoryGrowthBenchmark(BenchmarkConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public async Task<BenchmarkMetric> RunAsync(CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Running Memory Growth Benchmark...");

            var memoryTracker = new MemoryTracker();
            memoryTracker.Start();

            try
            {
                var options = new SmallMindOptions
                {
                    ModelPath = _config.ModelPath,
                    MaxContextTokens = _config.ContextSize,
                    EnableKvCache = _config.EnableKVCache,
                    AllowGgufImport = true,
                    ThreadCount = Environment.ProcessorCount
                };

                using var engine = SmallMindFactory.Create(options);

                var initialMemory = memoryTracker.GetSnapshot();
                int totalTokens = 0;

                Console.WriteLine($"  Generating tokens...");

                // Generate tokens and track memory
                for (int i = 0; i < _config.MeasuredIterations; i++)
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
                        Prompt = "Once upon a time in a distant land".AsMemory()
                    };

                    var result = session.Generate(request);
                    totalTokens += result.Usage.CompletionTokens;

                    memoryTracker.UpdatePeak();

                    if (cancellationToken.IsCancellationRequested)
                        break;
                }

                var finalMemory = memoryTracker.GetSnapshot();

                long memoryGrowth = finalMemory.WorkingSetDeltaBytes;
                double bytesPerToken = totalTokens > 0 ? (double)memoryGrowth / totalTokens : 0;

                Console.WriteLine($"  Total Tokens: {totalTokens}");
                Console.WriteLine($"  Memory Growth: {FormatHelper.FormatBytes(memoryGrowth)}");
                Console.WriteLine($"  Bytes per Token: {FormatHelper.FormatBytes((long)bytesPerToken)}");
                Console.WriteLine($"  Peak Working Set: {FormatHelper.FormatBytes(finalMemory.PeakWorkingSetBytes)}");

                await Task.CompletedTask;

                return new BenchmarkMetric
                {
                    Name = "memory_growth_per_token",
                    Category = "Memory",
                    Value = bytesPerToken,
                    Unit = "bytes/token",
                    Metadata = new Dictionary<string, object>
                    {
                        ["total_tokens"] = totalTokens,
                        ["memory_growth_bytes"] = memoryGrowth,
                        ["initial_working_set"] = initialMemory.WorkingSetBytes,
                        ["final_working_set"] = finalMemory.WorkingSetBytes,
                        ["peak_working_set"] = finalMemory.PeakWorkingSetBytes,
                        ["gen0_collections"] = finalMemory.Gen0Collections,
                        ["gen1_collections"] = finalMemory.Gen1Collections,
                        ["gen2_collections"] = finalMemory.Gen2Collections
                    }
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
                return new BenchmarkMetric
                {
                    Name = "memory_growth_per_token",
                    Category = "Memory",
                    Value = 0,
                    Unit = "bytes/token",
                    Metadata = new Dictionary<string, object>
                    {
                        ["error"] = ex.Message,
                        ["status"] = "failed"
                    }
                };
            }
        }
    }
}

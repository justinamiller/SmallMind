using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Benchmarks.Core.Measurement
{
    /// <summary>
    /// Main benchmark harness for running SmallMind performance tests.
    /// Measures TTFT, throughput, memory, and GC statistics.
    /// </summary>
    public sealed class BenchmarkHarness : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Runs a complete benchmark scenario with warmup and measured iterations.
        /// </summary>
        /// <param name="scenario">The benchmark scenario to run</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Benchmark results with statistics</returns>
        /// <exception cref="ArgumentNullException">If scenario is null</exception>
        public async Task<BenchmarkResult> RunScenarioAsync(
            BenchmarkScenario scenario,
            CancellationToken cancellationToken = default)
        {
            if (scenario == null)
            {
                throw new ArgumentNullException(nameof(scenario));
            }

            scenario.Validate();

            // Load model once for all iterations
            using var engine = LoadModel(scenario);

            // Record memory after model load
            long modelLoadRss = MemoryMeasurement.GetCurrentRssBytes();

            // Get baseline GC counts
            var (baseGen0, baseGen1, baseGen2) = MemoryMeasurement.GetGCCollectionCounts();

            // Warmup iterations
            for (int i = 0; i < scenario.WarmupIterations; i++)
            {
                await RunSingleIterationAsync(engine, scenario, measurePerformance: false, cancellationToken);
            }

            // Measured iterations
            var iterationResults = new IterationResult[scenario.MeasuredIterations];
            
            for (int i = 0; i < scenario.MeasuredIterations; i++)
            {
                iterationResults[i] = await RunSingleIterationAsync(
                    engine, 
                    scenario, 
                    measurePerformance: true, 
                    cancellationToken);
            }

            // Collect GC statistics
            var (finalGen0, finalGen1, finalGen2) = MemoryMeasurement.GetGCCollectionCounts();

            // Aggregate results
            return AggregateResults(scenario, iterationResults, modelLoadRss, 
                finalGen0 - baseGen0, finalGen1 - baseGen1, finalGen2 - baseGen2);
        }

        /// <summary>
        /// Loads the model using SmallMind public API.
        /// </summary>
        private ISmallMindEngine LoadModel(BenchmarkScenario scenario)
        {
            try
            {
                var options = new SmallMindOptions
                {
                    ModelPath = scenario.ModelPath,
                    MaxContextTokens = scenario.ContextSize,
                    ThreadCount = scenario.ThreadCount > 0 ? scenario.ThreadCount : null,
                    AllowGgufImport = true,
                    GgufCacheDirectory = scenario.CacheDirectory
                };

                return SmallMindFactory.Create(options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Failed to load model from {scenario.ModelPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Runs a single iteration and measures performance.
        /// </summary>
        private async Task<IterationResult> RunSingleIterationAsync(
            ISmallMindEngine engine,
            BenchmarkScenario scenario,
            bool measurePerformance,
            CancellationToken cancellationToken)
        {
            // Create a text generation session
            var sessionOptions = new TextGenerationOptions
            {
                MaxOutputTokens = scenario.NumTokensToGenerate,
                Temperature = (float)scenario.Temperature,
                TopK = scenario.TopK,
                TopP = (float)scenario.TopP,
                StopSequences = Array.Empty<string>()
            };

            using var session = engine.CreateTextGenerationSession(sessionOptions);

            // Create result structure
            var result = new IterationResult();

            if (!measurePerformance)
            {
                // Warmup: just run without measurements
                var warmupRequest = new TextGenerationRequest
                {
                    Prompt = scenario.PromptText.AsMemory(),
                    Seed = scenario.Seed
                };
                session.Generate(warmupRequest, cancellationToken);
                return result;
            }

            // Measured iteration: track everything
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            long rssBefore = MemoryMeasurement.GetCurrentRssBytes();

            var stopwatch = Stopwatch.StartNew();
            
            int tokenCount = 0;
            bool firstTokenReceived = false;
            long ttftTicks = 0;

            // Stream tokens to measure TTFT
            var request = new TextGenerationRequest
            {
                Prompt = scenario.PromptText.AsMemory(),
                Seed = scenario.Seed
            };

            await foreach (var token in session.GenerateStreaming(request, cancellationToken))
            {
                if (!firstTokenReceived)
                {
                    ttftTicks = stopwatch.ElapsedTicks;
                    firstTokenReceived = true;
                }

                tokenCount++;

                // Stop after reaching the requested number of tokens
                if (tokenCount >= scenario.NumTokensToGenerate)
                {
                    break;
                }
            }

            stopwatch.Stop();

            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
            long rssAfter = MemoryMeasurement.GetCurrentRssBytes();

            // Calculate metrics
            result.TotalTimeMs = stopwatch.Elapsed.TotalMilliseconds;
            result.TTFTMs = TicksToMilliseconds(ttftTicks);
            result.TokensGenerated = tokenCount;
            result.BytesAllocated = allocAfter - allocBefore;
            result.RssBytes = rssAfter;

            if (result.TotalTimeMs > 0)
            {
                result.TokensPerSecond = tokenCount * 1000.0 / result.TotalTimeMs;
            }

            // Steady-state throughput (excluding TTFT)
            double steadyStateTimeMs = result.TotalTimeMs - result.TTFTMs;
            if (steadyStateTimeMs > 0 && tokenCount > 1)
            {
                result.TokensPerSecondSteadyState = (tokenCount - 1) * 1000.0 / steadyStateTimeMs;
            }

            return result;
        }

        /// <summary>
        /// Aggregates iteration results into final benchmark result.
        /// </summary>
        private BenchmarkResult AggregateResults(
            BenchmarkScenario scenario,
            IterationResult[] iterations,
            long modelLoadRss,
            int gen0Collections,
            int gen1Collections,
            int gen2Collections)
        {
            int n = iterations.Length;

            // Extract metrics for statistical analysis
            double[] tokensPerSecValues = new double[n];
            double[] ttftValues = new double[n];
            long totalBytesAllocated = 0;
            long peakRss = 0;

            for (int i = 0; i < n; i++)
            {
                tokensPerSecValues[i] = iterations[i].TokensPerSecond;
                ttftValues[i] = iterations[i].TTFTMs;
                totalBytesAllocated += iterations[i].BytesAllocated;
                
                if (iterations[i].RssBytes > peakRss)
                {
                    peakRss = iterations[i].RssBytes;
                }
            }

            // Calculate statistics
            double medianTPS = Statistics.Median(tokensPerSecValues);
            double p90TPS = Statistics.Percentile(tokensPerSecValues, 90.0);
            double stdDevTPS = Statistics.StandardDeviation(tokensPerSecValues);
            double meanTTFT = Statistics.Mean(ttftValues);
            double meanTotalTime = Statistics.Mean(ExtractTotalTimes(iterations));
            double meanSteadyStateTPS = Statistics.Mean(ExtractSteadyStateValues(iterations));

            int totalTokensGenerated = 0;
            for (int i = 0; i < n; i++)
            {
                totalTokensGenerated += iterations[i].TokensGenerated;
            }

            long bytesAllocatedPerToken = totalTokensGenerated > 0 
                ? totalBytesAllocated / totalTokensGenerated 
                : 0;

            long bytesAllocatedPerSecond = meanTotalTime > 0
                ? (long)(totalBytesAllocated * 1000.0 / (meanTotalTime * n))
                : 0;

            return new BenchmarkResult
            {
                ScenarioName = scenario.ScenarioName,
                ModelName = System.IO.Path.GetFileNameWithoutExtension(scenario.ModelPath),
                ContextSize = scenario.ContextSize,
                ThreadCount = scenario.ThreadCount > 0 ? scenario.ThreadCount : System.Environment.ProcessorCount,
                PromptTokens = 0, // Would need tokenizer to calculate
                GeneratedTokens = totalTokensGenerated / n,
                TTFTMilliseconds = meanTTFT,
                TotalTimeMilliseconds = meanTotalTime,
                TokensPerSecond = medianTPS,
                TokensPerSecondSteadyState = meanSteadyStateTPS,
                PeakRssBytes = peakRss,
                ModelLoadRssBytes = modelLoadRss,
                BytesAllocatedPerToken = bytesAllocatedPerToken,
                BytesAllocatedPerSecond = bytesAllocatedPerSecond,
                Gen0Collections = gen0Collections,
                Gen1Collections = gen1Collections,
                Gen2Collections = gen2Collections,
                MedianTokensPerSecond = medianTPS,
                P90TokensPerSecond = p90TPS,
                StdDevTokensPerSecond = stdDevTPS,
                Timestamp = DateTime.UtcNow
            };
        }

        private static double TicksToMilliseconds(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }

        private static double[] ExtractTotalTimes(IterationResult[] iterations)
        {
            double[] times = new double[iterations.Length];
            for (int i = 0; i < iterations.Length; i++)
            {
                times[i] = iterations[i].TotalTimeMs;
            }
            return times;
        }

        private static double[] ExtractSteadyStateValues(IterationResult[] iterations)
        {
            double[] values = new double[iterations.Length];
            for (int i = 0; i < iterations.Length; i++)
            {
                values[i] = iterations[i].TokensPerSecondSteadyState;
            }
            return values;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
            }
        }

        /// <summary>
        /// Results from a single iteration.
        /// </summary>
        private struct IterationResult
        {
            public double TotalTimeMs;
            public double TTFTMs;
            public int TokensGenerated;
            public double TokensPerSecond;
            public double TokensPerSecondSteadyState;
            public long BytesAllocated;
            public long RssBytes;
        }
    }
}

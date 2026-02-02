using System.Diagnostics;

namespace SmallMind.Benchmarks;

/// <summary>
/// Executes benchmark scenarios and collects measurements.
/// </summary>
public sealed class ScenarioRunner
{
    private readonly BenchmarkConfig _config;
    private readonly EngineAdapter _engine;
    private readonly RuntimeCountersListener _counters;
    private readonly string _prompt;
    
    public ScenarioRunner(BenchmarkConfig config, EngineAdapter engine, RuntimeCountersListener counters)
    {
        _config = config;
        _engine = engine;
        _counters = counters;
        _prompt = PromptProfiles.GetPrompt(config.PromptProfile);
    }
    
    /// <summary>
    /// Run TTFT scenario.
    /// </summary>
    public async Task<ScenarioResult> RunTtftAsync(CancellationToken ct)
    {
        Console.WriteLine("Running TTFT scenario...");
        
        var ttftSamples = new List<double>();
        var latencySamples = new List<double>();
        
        // Warmup
        for (int i = 0; i < _config.Warmup; i++)
        {
            Console.Write($"  Warmup {i + 1}/{_config.Warmup}...\r");
            await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, _config.Temperature, 
                _config.TopK, _config.TopP, _config.Seed, ct);
        }
        Console.WriteLine();
        
        // Reset counters after warmup
        _counters.Reset();
        
        // Measure
        for (int i = 0; i < _config.Iterations; i++)
        {
            Console.Write($"  Iteration {i + 1}/{_config.Iterations}...\r");
            
            var measurement = await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, 
                _config.Temperature, _config.TopK, _config.TopP, _config.Seed, ct);
            
            ttftSamples.Add(measurement.TtftMs);
            latencySamples.Add(measurement.LatencyMs);
        }
        Console.WriteLine();
        
        var result = new ScenarioResult
        {
            Name = "TTFT",
            Aggregates = new Dictionary<string, AggregatedStats>
            {
                ["ttft_ms"] = AggregatedStats.Calculate(ttftSamples.ToArray()),
                ["latency_ms"] = AggregatedStats.Calculate(latencySamples.ToArray())
            },
            RuntimeCounters = RuntimeCounterMetrics.Capture(_counters)
        };
        
        return result;
    }
    
    /// <summary>
    /// Run steady tokens/sec scenario.
    /// </summary>
    public async Task<ScenarioResult> RunTokensPerSecAsync(CancellationToken ct)
    {
        Console.WriteLine("Running Tokens/Sec scenario...");
        
        var steadyTpsSamples = new List<double>();
        var overallTpsSamples = new List<double>();
        
        // Warmup
        for (int i = 0; i < _config.Warmup; i++)
        {
            Console.Write($"  Warmup {i + 1}/{_config.Warmup}...\r");
            await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, _config.Temperature, 
                _config.TopK, _config.TopP, _config.Seed, ct);
        }
        Console.WriteLine();
        
        _counters.Reset();
        
        // Measure
        for (int i = 0; i < _config.Iterations; i++)
        {
            Console.Write($"  Iteration {i + 1}/{_config.Iterations}...\r");
            
            var measurement = await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, 
                _config.Temperature, _config.TopK, _config.TopP, _config.Seed, ct);
            
            steadyTpsSamples.Add(measurement.SteadyTokensPerSec);
            overallTpsSamples.Add(measurement.OverallTokensPerSec);
        }
        Console.WriteLine();
        
        var result = new ScenarioResult
        {
            Name = "STEADY_TOKENS_PER_SEC",
            Aggregates = new Dictionary<string, AggregatedStats>
            {
                ["steady_tokens_per_sec"] = AggregatedStats.Calculate(steadyTpsSamples.ToArray()),
                ["overall_tokens_per_sec"] = AggregatedStats.Calculate(overallTpsSamples.ToArray())
            },
            RuntimeCounters = RuntimeCounterMetrics.Capture(_counters)
        };
        
        return result;
    }
    
    /// <summary>
    /// Run end-to-end latency scenario.
    /// </summary>
    public async Task<ScenarioResult> RunLatencyAsync(CancellationToken ct)
    {
        Console.WriteLine("Running End-to-End Latency scenario...");
        
        var latencySamples = new List<double>();
        
        // Warmup
        for (int i = 0; i < _config.Warmup; i++)
        {
            Console.Write($"  Warmup {i + 1}/{_config.Warmup}...\r");
            await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, _config.Temperature, 
                _config.TopK, _config.TopP, _config.Seed, ct);
        }
        Console.WriteLine();
        
        _counters.Reset();
        
        // Measure
        for (int i = 0; i < _config.Iterations; i++)
        {
            Console.Write($"  Iteration {i + 1}/{_config.Iterations}...\r");
            
            var measurement = await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, 
                _config.Temperature, _config.TopK, _config.TopP, _config.Seed, ct);
            
            latencySamples.Add(measurement.LatencyMs);
        }
        Console.WriteLine();
        
        var result = new ScenarioResult
        {
            Name = "END_TO_END_LATENCY",
            Aggregates = new Dictionary<string, AggregatedStats>
            {
                ["latency_ms"] = AggregatedStats.Calculate(latencySamples.ToArray())
            },
            RuntimeCounters = RuntimeCounterMetrics.Capture(_counters)
        };
        
        return result;
    }
    
    /// <summary>
    /// Run memory footprint scenario.
    /// </summary>
    public async Task<ScenarioResult> RunMemoryAsync(CancellationToken ct)
    {
        Console.WriteLine("Running Memory Footprint scenario...");
        
        var workingSetSamples = new List<long>();
        var privateMemSamples = new List<long>();
        var managedHeapSamples = new List<long>();
        
        // Warmup
        for (int i = 0; i < _config.Warmup; i++)
        {
            Console.Write($"  Warmup {i + 1}/{_config.Warmup}...\r");
            await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, _config.Temperature, 
                _config.TopK, _config.TopP, _config.Seed, ct);
        }
        Console.WriteLine();
        
        _counters.Reset();
        
        // Measure
        for (int i = 0; i < _config.Iterations; i++)
        {
            Console.Write($"  Iteration {i + 1}/{_config.Iterations}...\r");
            
            await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, _config.Temperature, 
                _config.TopK, _config.TopP, _config.Seed, ct);
            
            // Capture memory snapshot
            var snapshot = MemorySnapshot.Capture();
            workingSetSamples.Add(snapshot.WorkingSet);
            privateMemSamples.Add(snapshot.PrivateMemory);
            managedHeapSamples.Add(snapshot.ManagedHeap);
        }
        Console.WriteLine();
        
        var memoryMetrics = new MemoryMetrics
        {
            WorkingSetMinBytes = workingSetSamples.Min(),
            WorkingSetMaxBytes = workingSetSamples.Max(),
            WorkingSetAvgBytes = (long)workingSetSamples.Average(),
            PrivateMemoryMinBytes = privateMemSamples.Min(),
            PrivateMemoryMaxBytes = privateMemSamples.Max(),
            PrivateMemoryAvgBytes = (long)privateMemSamples.Average(),
            ManagedHeapMinBytes = managedHeapSamples.Min(),
            ManagedHeapMaxBytes = managedHeapSamples.Max(),
            ManagedHeapAvgBytes = (long)managedHeapSamples.Average()
        };
        
        var result = new ScenarioResult
        {
            Name = "MEMORY_FOOTPRINT",
            MemoryMetrics = memoryMetrics,
            RuntimeCounters = RuntimeCounterMetrics.Capture(_counters)
        };
        
        return result;
    }
    
    /// <summary>
    /// Run GC and allocations scenario.
    /// </summary>
    public async Task<ScenarioResult> RunGcAsync(CancellationToken ct)
    {
        Console.WriteLine("Running GC and Allocations scenario...");
        
        // Warmup
        for (int i = 0; i < _config.Warmup; i++)
        {
            Console.Write($"  Warmup {i + 1}/{_config.Warmup}...\r");
            await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, _config.Temperature, 
                _config.TopK, _config.TopP, _config.Seed, ct);
        }
        Console.WriteLine();
        
        // Force GC and get baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        long allocatedBefore = GC.GetTotalAllocatedBytes();
        
        _counters.Reset();
        
        // Measure
        for (int i = 0; i < _config.Iterations; i++)
        {
            Console.Write($"  Iteration {i + 1}/{_config.Iterations}...\r");
            
            await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, _config.Temperature, 
                _config.TopK, _config.TopP, _config.Seed, ct);
        }
        Console.WriteLine();
        
        long allocatedAfter = GC.GetTotalAllocatedBytes();
        int gen0After = GC.CollectionCount(0);
        int gen1After = GC.CollectionCount(1);
        int gen2After = GC.CollectionCount(2);
        
        var gcMetrics = new GcMetrics
        {
            Gen0Collections = gen0After - gen0Before,
            Gen1Collections = gen1After - gen1Before,
            Gen2Collections = gen2After - gen2Before,
            TotalAllocatedBytes = allocatedAfter - allocatedBefore,
            AllocationsPerOperation = (double)(allocatedAfter - allocatedBefore) / _config.Iterations
        };
        
        var result = new ScenarioResult
        {
            Name = "GC_AND_ALLOCATIONS",
            GcMetrics = gcMetrics,
            RuntimeCounters = RuntimeCounterMetrics.Capture(_counters)
        };
        
        return result;
    }
    
    /// <summary>
    /// Run concurrency throughput scenario.
    /// </summary>
    public async Task<ScenarioResult> RunConcurrencyAsync(int concurrency, CancellationToken ct)
    {
        Console.WriteLine($"Running Concurrency scenario (concurrency={concurrency})...");
        
        // Warmup
        for (int i = 0; i < _config.Warmup; i++)
        {
            Console.Write($"  Warmup {i + 1}/{_config.Warmup}...\r");
            await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, _config.Temperature, 
                _config.TopK, _config.TopP, _config.Seed, ct);
        }
        Console.WriteLine();
        
        _counters.Reset();
        
        var latencySamples = new List<double>();
        var startTime = TimingUtils.GetTimestamp();
        int totalTokens = 0;
        
        // Run concurrent requests
        var tasks = new Task[concurrency];
        for (int batch = 0; batch < _config.Iterations; batch += concurrency)
        {
            int batchSize = Math.Min(concurrency, _config.Iterations - batch);
            
            for (int i = 0; i < batchSize; i++)
            {
                tasks[i] = Task.Run(async () =>
                {
                    var measurement = await _engine.GenerateAsync(_prompt, _config.MaxNewTokens, 
                        _config.Temperature, _config.TopK, _config.TopP, _config.Seed, ct);
                    
                    lock (latencySamples)
                    {
                        latencySamples.Add(measurement.LatencyMs);
                        totalTokens += measurement.TokenCount;
                    }
                }, ct);
            }
            
            await Task.WhenAll(tasks.Take(batchSize));
            Console.Write($"  Completed {Math.Min(batch + concurrency, _config.Iterations)}/{_config.Iterations}...\r");
        }
        Console.WriteLine();
        
        var endTime = TimingUtils.GetTimestamp();
        double totalSeconds = TimingUtils.TicksToSecondsDouble(endTime - startTime);
        
        double requestsPerSec = _config.Iterations / totalSeconds;
        double tokensPerSec = totalTokens / totalSeconds;
        
        var result = new ScenarioResult
        {
            Name = $"CONCURRENCY_THROUGHPUT_{concurrency}",
            Aggregates = new Dictionary<string, AggregatedStats>
            {
                ["latency_ms"] = AggregatedStats.Calculate(latencySamples.ToArray()),
                ["requests_per_sec"] = new AggregatedStats { Mean = requestsPerSec },
                ["tokens_per_sec"] = new AggregatedStats { Mean = tokensPerSec }
            },
            RuntimeCounters = RuntimeCounterMetrics.Capture(_counters)
        };
        
        return result;
    }
}

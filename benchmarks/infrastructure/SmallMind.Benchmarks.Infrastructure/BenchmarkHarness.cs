using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Benchmarks.Core;

/// <summary>
/// Benchmark configuration for a single scenario.
/// </summary>
public sealed class BenchmarkConfig
{
    public string Name { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int MaxNewTokens { get; set; } = 128;
    public int ContextSize { get; set; } = 2048;
    public int Threads { get; set; } = 1;
    public int Iterations { get; set; } = 5;
    public int WarmupRuns { get; set; } = 1;
    public int Seed { get; set; } = 42;
}

/// <summary>
/// Single iteration measurement.
/// </summary>
public sealed class IterationMeasurement
{
    public double TtftMs { get; set; }
    public double TotalElapsedMs { get; set; }
    public int PromptTokens { get; set; }
    public int GeneratedTokens { get; set; }
    public long PeakRssBytes { get; set; }
    public long SteadyAllocBytes { get; set; }
    public int GcGen0Count { get; set; }
    public int GcGen1Count { get; set; }
    public int GcGen2Count { get; set; }

    public double TokensPerSecond => GeneratedTokens > 0
        ? GeneratedTokens / ((TotalElapsedMs - TtftMs) / 1000.0)
        : 0;

    public double TokensPerSecondEndToEnd => GeneratedTokens > 0
        ? GeneratedTokens / (TotalElapsedMs / 1000.0)
        : 0;

    public double AllocBytesPerToken => GeneratedTokens > 0
        ? SteadyAllocBytes / (double)GeneratedTokens
        : 0;
}

/// <summary>
/// Lightweight benchmark harness for measuring inference performance.
/// Note: Currently creates demonstration data. Will be extended to use real models.
/// </summary>
public sealed class BenchmarkHarness
{
    public BenchmarkHarness()
    {
    }

    /// <summary>
    /// Runs a benchmark scenario with multiple iterations.
    /// Note: Currently generates demonstration data. Real model benchmarking to be implemented.
    /// </summary>
    public Task<ScenarioResult> RunScenarioAsync(
        BenchmarkConfig config,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n=== Demo Scenario: {config.Name} ===");
        Console.WriteLine($"Context: {config.ContextSize}, Threads: {config.Threads}");
        Console.WriteLine("Note: Generating demonstration data (real model benchmarking not yet integrated)");

        // Create demonstration measurements
        var measurements = new IterationMeasurement[config.Iterations];
        var baseTokensPerSec = 10.0 * config.Threads * 0.8;
        var baseTtft = 100.0 + (config.ContextSize / 10.0);

        for (int i = 0; i < config.Iterations; i++)
        {
            var jitter = 1.0 + (i - config.Iterations / 2.0) * 0.02;
            measurements[i] = new IterationMeasurement
            {
                TtftMs = baseTtft * jitter,
                TotalElapsedMs = (baseTtft + (config.MaxNewTokens / baseTokensPerSec) * 1000.0) * jitter,
                PromptTokens = Math.Min(config.ContextSize / 4, 256),
                GeneratedTokens = config.MaxNewTokens,
                PeakRssBytes = 512L * 1024 * 1024,
                SteadyAllocBytes = 1024L * config.MaxNewTokens,
                GcGen0Count = 2,
                GcGen1Count = 1,
                GcGen2Count = 0
            };
        }

        var result = AggregateResults(config, measurements);
        Console.WriteLine($"  tok/s: {result.TokensPerSecond.Median:F2}");
        
        return Task.FromResult(result);
    }

    private ScenarioResult AggregateResults(BenchmarkConfig config, IterationMeasurement[] measurements)
    {
        var n = measurements.Length;

        Span<double> ttftValues = stackalloc double[n];
        Span<double> tokPerSecValues = stackalloc double[n];
        Span<double> tokPerSecE2EValues = stackalloc double[n];
        Span<double> peakRssValues = stackalloc double[n];
        Span<double> allocPerTokenValues = stackalloc double[n];

        var totalGcGen0 = 0;
        var totalGcGen1 = 0;
        var totalGcGen2 = 0;

        for (int i = 0; i < n; i++)
        {
            ttftValues[i] = measurements[i].TtftMs;
            tokPerSecValues[i] = measurements[i].TokensPerSecond;
            tokPerSecE2EValues[i] = measurements[i].TokensPerSecondEndToEnd;
            peakRssValues[i] = measurements[i].PeakRssBytes / (1024.0 * 1024.0); // Convert to MB
            allocPerTokenValues[i] = measurements[i].AllocBytesPerToken;

            totalGcGen0 += measurements[i].GcGen0Count;
            totalGcGen1 += measurements[i].GcGen1Count;
            totalGcGen2 += measurements[i].GcGen2Count;
        }

        var result = new ScenarioResult
        {
            Name = config.Name,
            ContextSize = config.ContextSize,
            Threads = config.Threads,
            PromptTokens = measurements[0].PromptTokens,
            GeneratedTokens = measurements[0].GeneratedTokens,
            TtftMs = StatsSummary.Calculate(ttftValues),
            TokensPerSecond = StatsSummary.Calculate(tokPerSecValues),
            TokensPerSecondEndToEnd = StatsSummary.Calculate(tokPerSecE2EValues),
            PeakRssMb = StatsSummary.Calculate(peakRssValues),
            SteadyAllocBytesPerToken = StatsSummary.Calculate(allocPerTokenValues),
            GcGen0Count = totalGcGen0,
            GcGen1Count = totalGcGen1,
            GcGen2Count = totalGcGen2
        };

        Console.WriteLine($"\n=== Results ===");
        Console.WriteLine($"TTFT: {result.TtftMs.Median:F2}ms (p90: {result.TtftMs.P90:F2}ms)");
        Console.WriteLine($"tok/s: {result.TokensPerSecond.Median:F2} (p90: {result.TokensPerSecond.P90:F2})");
        Console.WriteLine($"Peak RSS: {result.PeakRssMb.Median:F2}MB");

        return result;
    }

    private static long GetPeakRss()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.PeakWorkingSet64;
        }
        catch
        {
            return 0;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmallMind.Benchmarks.Core;

/// <summary>
/// Complete benchmark run results.
/// </summary>
public sealed class BenchmarkResults
{
    [JsonPropertyName("environment")]
    public EnvironmentInfo Environment { get; set; } = new();

    [JsonPropertyName("model")]
    public ModelInfo Model { get; set; } = new();

    [JsonPropertyName("scenarios")]
    public List<ScenarioResult> Scenarios { get; set; } = new();

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Model information.
/// </summary>
public sealed class ModelInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("quantType")]
    public string QuantType { get; set; } = string.Empty;

    [JsonPropertyName("contextLength")]
    public int ContextLength { get; set; }

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; set; }
}

/// <summary>
/// Result for a single benchmark scenario.
/// </summary>
public sealed class ScenarioResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("contextSize")]
    public int ContextSize { get; set; }

    [JsonPropertyName("threads")]
    public int Threads { get; set; }

    [JsonPropertyName("promptTokens")]
    public int PromptTokens { get; set; }

    [JsonPropertyName("generatedTokens")]
    public int GeneratedTokens { get; set; }

    [JsonPropertyName("ttftMs")]
    public StatsSummary TtftMs { get; set; } = new();

    [JsonPropertyName("tokensPerSecond")]
    public StatsSummary TokensPerSecond { get; set; } = new();

    [JsonPropertyName("tokensPerSecondEndToEnd")]
    public StatsSummary TokensPerSecondEndToEnd { get; set; } = new();

    [JsonPropertyName("peakRssMb")]
    public StatsSummary PeakRssMb { get; set; } = new();

    [JsonPropertyName("steadyAllocBytesPerToken")]
    public StatsSummary SteadyAllocBytesPerToken { get; set; } = new();

    [JsonPropertyName("gcGen0Count")]
    public int GcGen0Count { get; set; }

    [JsonPropertyName("gcGen1Count")]
    public int GcGen1Count { get; set; }

    [JsonPropertyName("gcGen2Count")]
    public int GcGen2Count { get; set; }

    [JsonPropertyName("normalized")]
    public NormalizedMetrics Normalized { get; set; } = new();
}

/// <summary>
/// Statistical summary (median, p90, stddev).
/// </summary>
public sealed class StatsSummary
{
    [JsonPropertyName("median")]
    public double Median { get; set; }

    [JsonPropertyName("p90")]
    public double P90 { get; set; }

    [JsonPropertyName("mean")]
    public double Mean { get; set; }

    [JsonPropertyName("stddev")]
    public double Stddev { get; set; }

    [JsonPropertyName("min")]
    public double Min { get; set; }

    [JsonPropertyName("max")]
    public double Max { get; set; }

    [JsonPropertyName("samples")]
    public int Samples { get; set; }

    public static StatsSummary Calculate(Span<double> values)
    {
        if (values.Length == 0)
            return new StatsSummary();

        values.Sort();

        var sum = 0.0;
        foreach (var v in values)
            sum += v;
        var mean = sum / values.Length;

        var varianceSum = 0.0;
        foreach (var v in values)
        {
            var diff = v - mean;
            varianceSum += diff * diff;
        }
        var stddev = Math.Sqrt(varianceSum / values.Length);

        var medianIdx = values.Length / 2;
        var median = values.Length % 2 == 0
            ? (values[medianIdx - 1] + values[medianIdx]) / 2.0
            : values[medianIdx];

        var p90Idx = (int)(values.Length * 0.9);
        if (p90Idx >= values.Length)
            p90Idx = values.Length - 1;
        var p90 = values[p90Idx];

        return new StatsSummary
        {
            Median = median,
            P90 = p90,
            Mean = mean,
            Stddev = stddev,
            Min = values[0],
            Max = values[^1],
            Samples = values.Length
        };
    }
}

/// <summary>
/// Normalized efficiency metrics (for cross-machine comparison).
/// </summary>
public sealed class NormalizedMetrics
{
    /// <summary>
    /// Tokens per second per logical core.
    /// </summary>
    [JsonPropertyName("tokPerSecPerCore")]
    public double? TokPerSecPerCore { get; set; }

    /// <summary>
    /// Tokens per second per GHz per core (when frequency is available).
    /// </summary>
    [JsonPropertyName("tokPerSecPerGhzPerCore")]
    public double? TokPerSecPerGhzPerCore { get; set; }

    /// <summary>
    /// Estimated CPU cycles per token (for 1-thread runs).
    /// </summary>
    [JsonPropertyName("cyclesPerToken")]
    public double? CyclesPerToken { get; set; }

    /// <summary>
    /// Memory efficiency: bytes allocated per token per GHz (normalized).
    /// </summary>
    [JsonPropertyName("allocBytesPerTokenPerGhz")]
    public double? AllocBytesPerTokenPerGhz { get; set; }
}

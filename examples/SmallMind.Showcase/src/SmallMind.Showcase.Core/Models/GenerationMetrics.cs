namespace SmallMind.Showcase.Core.Models;

/// <summary>
/// Real-time metrics for a generation request.
/// </summary>
public sealed class GenerationMetrics
{
    public TimeSpan? TimeToFirstToken { get; set; }
    public double? PrefillTokensPerSecond { get; set; }
    public double? DecodeTokensPerSecond { get; set; }
    public double? PerTokenLatencyMs { get; set; }
    public TimeSpan? EndToEndLatency { get; set; }
    
    public int PromptTokens { get; set; }
    public int GeneratedTokens { get; set; }
    public int TotalTokens => PromptTokens + GeneratedTokens;
    
    public int ContextWindowSize { get; set; }
    public double ContextUtilization => ContextWindowSize > 0 ? (double)TotalTokens / ContextWindowSize : 0;
    
    public long ManagedHeapSizeBytes { get; set; }
    public long TotalAllocatedBytes { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    
    public double? CpuUsagePercent { get; set; }
    
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? LastError { get; set; }
    public string? LastWarning { get; set; }
}

/// <summary>
/// Rolling metrics aggregator for percentile calculations.
/// </summary>
public sealed class MetricsAggregator
{
    private readonly LinkedList<double> _latencies = new();
    private readonly int _windowSize;

    public MetricsAggregator(int windowSize = 50)
    {
        _windowSize = windowSize;
    }

    public void AddLatency(double latencyMs)
    {
        _latencies.AddLast(latencyMs);
        if (_latencies.Count > _windowSize)
        {
            _latencies.RemoveFirst();
        }
    }

    public MetricsPercentiles GetPercentiles()
    {
        if (_latencies.Count == 0)
        {
            return new MetricsPercentiles();
        }

        var sorted = _latencies.OrderBy(x => x).ToList();
        return new MetricsPercentiles
        {
            P50 = GetPercentile(sorted, 0.50),
            P95 = GetPercentile(sorted, 0.95),
            P99 = GetPercentile(sorted, 0.99),
            Count = sorted.Count
        };
    }

    private static double GetPercentile(List<double> sorted, double percentile)
    {
        if (sorted.Count == 0) return 0;
        
        int index = (int)Math.Ceiling(percentile * sorted.Count) - 1;
        index = Math.Max(0, Math.Min(sorted.Count - 1, index));
        return sorted[index];
    }

    public void Clear()
    {
        _latencies.Clear();
    }
}

public sealed class MetricsPercentiles
{
    public double P50 { get; init; }
    public double P95 { get; init; }
    public double P99 { get; init; }
    public int Count { get; init; }
}

using SmallMind.Benchmarks.Diagnostics;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Configuration for benchmark runs.
    /// </summary>
    internal sealed class BenchmarkConfig
    {
        public string ModelPath { get; set; } = string.Empty;
        public int WarmupIterations { get; set; } = 5;
        public int MeasuredIterations { get; set; } = 20;
        public int ConcurrentStreams { get; set; } = 10;
        public int MaxTokensPerRequest { get; set; } = 100;
        public int ContextSize { get; set; } = 2048;
        public bool EnableKVCache { get; set; } = true;
        public TimeSpan SoakDuration { get; set; } = TimeSpan.FromMinutes(1);
        public bool CIMode { get; set; } = false;
        public string OutputPath { get; set; } = "./benchmark-results";
        public BenchmarkOutputFormat OutputFormat { get; set; } = BenchmarkOutputFormat.All;
    }

    /// <summary>
    /// Output format for benchmark results.
    /// </summary>
    [Flags]
    internal enum BenchmarkOutputFormat
    {
        None = 0,
        Json = 1,
        Markdown = 2,
        Csv = 4,
        All = Json | Markdown | Csv
    }

    /// <summary>
    /// Complete benchmark results.
    /// </summary>
    internal sealed class BenchmarkResults
    {
        public SystemInfo SystemInfo { get; init; } = new();
        public BenchmarkConfig Config { get; init; } = new();
        public DateTime StartTime { get; init; }
        public DateTime EndTime { get; init; }
        public TimeSpan TotalDuration { get; init; }
        public List<BenchmarkMetric> Metrics { get; init; } = new();
        public string Status { get; init; } = "Unknown";
        public List<string> Errors { get; init; } = new();
        public List<string> Warnings { get; init; } = new();
    }

    /// <summary>
    /// Individual benchmark metric.
    /// </summary>
    internal sealed class BenchmarkMetric
    {
        public string Name { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public double Value { get; init; }
        public string Unit { get; init; } = string.Empty;
        public Dictionary<string, object> Metadata { get; init; } = new();
        public Statistics? Statistics { get; init; }
    }

    /// <summary>
    /// Statistics for a metric.
    /// </summary>
    internal sealed class Statistics
    {
        public int Count { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public double Mean { get; init; }
        public double Median { get; init; }
        public double P50 { get; init; }
        public double P95 { get; init; }
        public double P99 { get; init; }
        public double StdDev { get; init; }
    }

    /// <summary>
    /// Detailed throughput metrics.
    /// </summary>
    internal sealed class ThroughputMetrics
    {
        public double TokensPerSecond { get; init; }
        public double TimeToFirstTokenMs { get; init; }
        public int TotalTokensGenerated { get; init; }
        public double TotalTimeMs { get; init; }
        public Statistics? TTFTStats { get; init; }
        public Statistics? ThroughputStats { get; init; }
    }

    /// <summary>
    /// Memory growth metrics.
    /// </summary>
    internal sealed class MemoryGrowthMetrics
    {
        public long InitialWorkingSetBytes { get; init; }
        public long FinalWorkingSetBytes { get; init; }
        public long PeakWorkingSetBytes { get; init; }
        public long GrowthBytes { get; init; }
        public double GrowthBytesPerToken { get; init; }
        public int Gen0Collections { get; init; }
        public int Gen1Collections { get; init; }
        public int Gen2Collections { get; init; }
    }

    /// <summary>
    /// GFLOPS measurement for compute-intensive operations.
    /// </summary>
    internal sealed class GFlopsMetrics
    {
        public double GFLOPS { get; init; }
        public double TimeMs { get; init; }
        public long TotalOperations { get; init; }
        public string Operation { get; init; } = string.Empty;
        public Dictionary<string, double> DetailedMetrics { get; init; } = new();
    }

    /// <summary>
    /// Soak test results (stability over time).
    /// </summary>
    internal sealed class SoakTestResults
    {
        public TimeSpan Duration { get; init; }
        public int TotalRequests { get; init; }
        public int SuccessfulRequests { get; init; }
        public int FailedRequests { get; init; }
        public List<string> Errors { get; init; } = new();
        public bool OOMEncountered { get; init; }
        public bool CrashEncountered { get; init; }
        public MemorySnapshot InitialMemory { get; init; } = new();
        public MemorySnapshot FinalMemory { get; init; } = new();
        public double AverageThroughput { get; init; }
    }
}

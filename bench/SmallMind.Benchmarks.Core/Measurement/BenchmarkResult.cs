using System;

namespace SmallMind.Benchmarks.Core.Measurement
{
    /// <summary>
    /// Benchmark result for a single scenario run.
    /// </summary>
    public sealed class BenchmarkResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int ContextSize { get; set; }
        public int ThreadCount { get; set; }
        public int PromptTokens { get; set; }
        public int GeneratedTokens { get; set; }

        // TTFT metrics
        public double TTFTMilliseconds { get; set; }
        
        // Throughput metrics  
        public double TotalTimeMilliseconds { get; set; }
        public double TokensPerSecond { get; set; }
        public double TokensPerSecondSteadyState { get; set; } // Excluding TTFT

        // Memory metrics
        public long PeakRssBytes { get; set; }
        public long ModelLoadRssBytes { get; set; }
        public long BytesAllocatedPerToken { get; set; }
        public long BytesAllocatedPerSecond { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }

        // Statistics across iterations
        public double MedianTokensPerSecond { get; set; }
        public double P90TokensPerSecond { get; set; }
        public double StdDevTokensPerSecond { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Aggregate benchmark run results.
    /// </summary>
    public sealed class BenchmarkRunResults
    {
        public string RunId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime EndTime { get; set; }
        public string GitCommitSha { get; set; } = string.Empty;
        public Environment.EnvironmentSnapshot? Environment { get; set; }
        public BenchmarkResult[] Results { get; set; } = Array.Empty<BenchmarkResult>();
        public BenchmarkNormalized[] NormalizedResults { get; set; } = Array.Empty<BenchmarkNormalized>();
    }

    /// <summary>
    /// Normalized efficiency metrics for cross-platform comparison.
    /// </summary>
    public sealed class BenchmarkNormalized
    {
        public string ModelName { get; set; } = string.Empty;
        public int ContextSize { get; set; }
        public int ThreadCount { get; set; }

        // Normalized metrics
        public double TokensPerSecondPerCore { get; set; }
        public double TokensPerSecondPerGHzPerCore { get; set; }
        public double CyclesPerToken { get; set; }
        
        public string Notes { get; set; } = string.Empty;
    }
}

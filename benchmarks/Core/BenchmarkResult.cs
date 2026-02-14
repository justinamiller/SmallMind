using System;
using System.Collections.Generic;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Represents a single benchmark result.
    /// </summary>
    public sealed class BenchmarkResult
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, object> Parameters { get; set; } = new();
        public Dictionary<string, double> Metrics { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Contains all benchmark results and system metadata.
    /// </summary>
    public sealed class BenchmarkReport
    {
        public SystemInfo SystemInfo { get; set; } = new();
        public List<BenchmarkResult> Results { get; set; } = new();
        public DateTime ReportTimestamp { get; set; }
    }
}

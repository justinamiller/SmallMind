using System;
using System.Collections.Generic;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Performance metrics for a benchmark run.
    /// </summary>
    internal sealed class PerformanceMetrics
    {
        /// <summary>
        /// Time to First Token (ms).
        /// </summary>
        public double TTFT { get; set; }

        /// <summary>
        /// Decode tokens per second.
        /// </summary>
        public double DecodeToksPerSec { get; set; }

        /// <summary>
        /// Prefill tokens per second.
        /// </summary>
        public double PrefillToksPerSec { get; set; }

        /// <summary>
        /// Peak RSS (working set) in bytes.
        /// </summary>
        public long PeakRSS { get; set; }

        /// <summary>
        /// Managed heap size in bytes.
        /// </summary>
        public long ManagedHeapSize { get; set; }

        /// <summary>
        /// Allocated bytes during decode phase.
        /// </summary>
        public long AllocatedBytesForDecode { get; set; }

        /// <summary>
        /// Gen0 collections during decode.
        /// </summary>
        public int Gen0Collections { get; set; }

        /// <summary>
        /// Gen1 collections during decode.
        /// </summary>
        public int Gen1Collections { get; set; }

        /// <summary>
        /// Gen2 collections during decode.
        /// </summary>
        public int Gen2Collections { get; set; }

        /// <summary>
        /// Allocated bytes per generated token (steady-state decode).
        /// </summary>
        public double AllocatedBytesPerToken { get; set; }

        /// <summary>
        /// Number of tokens generated.
        /// </summary>
        public int TokensGenerated { get; set; }

        /// <summary>
        /// Additional custom metrics.
        /// </summary>
        public Dictionary<string, object> CustomMetrics { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Benchmark configuration.
    /// </summary>
    internal sealed class BenchmarkConfig
    {
        /// <summary>
        /// Number of warmup iterations.
        /// </summary>
        public int WarmupIterations { get; set; } = 3;

        /// <summary>
        /// Number of measured iterations.
        /// </summary>
        public int MeasuredIterations { get; set; } = 10;

        /// <summary>
        /// Prompt length for prefill phase.
        /// </summary>
        public int PromptLength { get; set; } = 128;

        /// <summary>
        /// Number of tokens to generate in decode phase.
        /// </summary>
        public int DecodeTokens { get; set; } = 32;

        /// <summary>
        /// Random seed for deterministic runs.
        /// </summary>
        public int Seed { get; set; } = 42;

        /// <summary>
        /// Whether to set CPU affinity (if supported).
        /// </summary>
        public bool UseCpuAffinity { get; set; } = false;

        /// <summary>
        /// CPU core to pin to (if UseCpuAffinity is true).
        /// </summary>
        public int CpuCore { get; set; } = 0;

        /// <summary>
        /// Output directory for benchmark results.
        /// </summary>
        public string OutputDirectory { get; set; } = ".";

        /// <summary>
        /// Output formats (json, markdown).
        /// </summary>
        public List<string> OutputFormats { get; set; } = new List<string> { "markdown" };
    }

    /// <summary>
    /// Benchmark result containing metrics and metadata.
    /// </summary>
    internal sealed class BenchmarkResult
    {
        /// <summary>
        /// Benchmark name.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Benchmark configuration.
        /// </summary>
        public BenchmarkConfig Config { get; set; } = new BenchmarkConfig();

        /// <summary>
        /// Performance metrics (averaged across iterations).
        /// </summary>
        public PerformanceMetrics Metrics { get; set; } = new PerformanceMetrics();

        /// <summary>
        /// Timestamp when benchmark was run.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Environment information.
        /// </summary>
        public Dictionary<string, object> Environment { get; set; } = new Dictionary<string, object>();
    }
}

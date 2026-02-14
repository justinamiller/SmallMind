using SmallMind.Abstractions.Telemetry;

namespace SmallMind.Abstractions
{
    /// <summary>
    /// Options for configuring the SmallMind inference engine behavior.
    /// This configures internal engine settings like threading, batching, and RAG.
    /// For model-specific options, see the public SmallMind.SmallMindOptions class.
    /// </summary>
    public sealed class EngineOptions
    {
        /// <summary>
        /// Gets or sets the default number of threads for inference.
        /// If 0 or negative, uses system default (typically processor count).
        /// Default: 0 (auto).
        /// </summary>
        public int DefaultThreads { get; set; }

        /// <summary>
        /// Gets or sets whether to enable deterministic mode by default.
        /// When true, uses a fixed seed (42) for all generations unless overridden.
        /// Default: false.
        /// </summary>
        public bool EnableDeterministicMode { get; set; }

        /// <summary>
        /// Gets or sets whether to enable KV cache by default.
        /// Default: true.
        /// </summary>
        public bool EnableKvCache { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable batching for concurrent requests.
        /// Default: false.
        /// </summary>
        public bool EnableBatching { get; set; }

        /// <summary>
        /// Gets or sets whether to enable RAG capabilities.
        /// Default: true.
        /// </summary>
        public bool EnableRag { get; set; } = true;

        /// <summary>
        /// Gets or sets the request timeout. If null, no timeout is applied.
        /// Default: null.
        /// </summary>
        public System.TimeSpan? RequestTimeout { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of buffered tokens for streaming backpressure.
        /// If null, no backpressure limit is applied.
        /// Default: null.
        /// </summary>
        public int? MaxBufferedTokens { get; set; }

        /// <summary>
        /// Gets or sets the maximum queue depth for batched/multi-session scenarios.
        /// If null, no queue depth limit is applied.
        /// Default: null.
        /// </summary>
        public int? MaxQueueDepth { get; set; }

        /// <summary>
        /// Gets or sets the maximum tensor memory budget in bytes.
        /// If null, no memory budget enforcement.
        /// Default: null.
        /// </summary>
        public long? MaxTensorBytes { get; set; }

        /// <summary>
        /// Gets or sets the memory budget enforcement mode.
        /// Default: None.
        /// </summary>
        public MemoryBudgetMode MemoryBudgetMode { get; set; } = MemoryBudgetMode.None;

        /// <summary>
        /// Gets or sets the runtime logger for diagnostics and debugging.
        /// If null, uses NullRuntimeLogger.
        /// Default: null.
        /// </summary>
        public IRuntimeLogger? Logger { get; set; }

        /// <summary>
        /// Gets or sets the runtime metrics collector for performance tracking.
        /// If null, uses NullRuntimeMetrics.
        /// Default: null.
        /// </summary>
        public IRuntimeMetrics? Metrics { get; set; }
    }
}

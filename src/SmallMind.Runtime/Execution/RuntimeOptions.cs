namespace SmallMind.Runtime.Execution
{
    /// <summary>
    /// Runtime execution options for threading, determinism, and performance control.
    /// Controls CPU parallelization strategy and execution determinism for inference.
    /// </summary>
    internal sealed class RuntimeOptions
    {
        /// <summary>
        /// Gets or sets the maximum degree of parallelism for CPU operations.
        /// Default: Environment.ProcessorCount (use all cores).
        /// Set to 1 for single-threaded execution.
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Gets or sets whether to enforce deterministic execution.
        /// When true, forces single-threaded execution and fixed scheduling for reproducibility.
        /// Default: false (allows parallelism).
        /// </summary>
        public bool DeterministicMode { get; set; } = false;

        /// <summary>
        /// Gets or sets the minimum work size threshold for parallelization.
        /// Operations smaller than this threshold execute single-threaded to avoid overhead.
        /// Default: 1024 elements.
        /// </summary>
        public int ParallelizationThreshold { get; set; } = 1024;

        /// <summary>
        /// Gets or sets whether KV cache is mandatory for generation.
        /// When true, generation requires KV cache (no full-context recomputation).
        /// When false, allows bypass for debugging/testing (not recommended for production).
        /// Default: true (cache is mandatory).
        /// </summary>
        public bool RequireKvCache { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to enable detailed telemetry collection.
        /// When enabled, tracks prefill/decode timing, cache metrics, and allocations.
        /// Minimal overhead when enabled (~1-2% in most cases).
        /// Default: true.
        /// </summary>
        public bool EnableTelemetry { get; set; } = true;

        /// <summary>
        /// Creates a copy of these runtime options.
        /// </summary>
        public RuntimeOptions Clone()
        {
            return new RuntimeOptions
            {
                MaxDegreeOfParallelism = MaxDegreeOfParallelism,
                DeterministicMode = DeterministicMode,
                ParallelizationThreshold = ParallelizationThreshold,
                RequireKvCache = RequireKvCache,
                EnableTelemetry = EnableTelemetry
            };
        }

        /// <summary>
        /// Validates the runtime options and adjusts for consistency.
        /// </summary>
        public void Validate()
        {
            if (MaxDegreeOfParallelism < 1)
            {
                MaxDegreeOfParallelism = 1;
            }

            if (ParallelizationThreshold < 0)
            {
                ParallelizationThreshold = 0;
            }

            // Deterministic mode forces single-threaded execution
            if (DeterministicMode)
            {
                MaxDegreeOfParallelism = 1;
            }
        }
    }
}

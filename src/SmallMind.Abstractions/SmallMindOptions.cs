namespace SmallMind.Abstractions
{
    /// <summary>
    /// Options for creating the SmallMind engine.
    /// </summary>
    public sealed class SmallMindOptions
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
    }
}

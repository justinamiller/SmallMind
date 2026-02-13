namespace SmallMind
{
    /// <summary>
    /// Engine capabilities descriptor.
    /// Allows consumers to discover what features are available without trial and error.
    /// </summary>
    public sealed class EngineCapabilities
    {
        /// <summary>
        /// Gets whether the engine supports streaming text generation.
        /// </summary>
        public bool SupportsStreaming { get; init; }

        /// <summary>
        /// Gets whether the engine supports embeddings.
        /// </summary>
        public bool SupportsEmbeddings { get; init; }

        /// <summary>
        /// Gets whether the engine supports KV cache optimization.
        /// </summary>
        public bool SupportsKvCache { get; init; }

        /// <summary>
        /// Gets whether the engine supports batching multiple requests.
        /// </summary>
        public bool SupportsBatching { get; init; }

        /// <summary>
        /// Gets the maximum context length in tokens.
        /// </summary>
        public int MaxContextTokens { get; init; }

        /// <summary>
        /// Gets the model format (e.g., "smq", "gguf").
        /// </summary>
        public string ModelFormat { get; init; } = string.Empty;

        /// <summary>
        /// Gets the quantization scheme (e.g., "Q8", "Q4", "FP32").
        /// </summary>
        public string Quantization { get; init; } = string.Empty;

        /// <summary>
        /// Gets the tokenizer identifier or vocabulary information (if available).
        /// </summary>
        public string? TokenizerId { get; init; }
    }
}

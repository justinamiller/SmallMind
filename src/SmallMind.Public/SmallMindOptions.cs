using System;

namespace SmallMind.Public
{
    /// <summary>
    /// Options for creating the SmallMind inference engine.
    /// This is the stable public contract - changes to this class follow semantic versioning.
    /// </summary>
    public sealed class SmallMindOptions
    {
        /// <summary>
        /// Gets or sets the path to the model file (.smq or .gguf).
        /// Required.
        /// </summary>
        public string ModelPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the maximum context length in tokens.
        /// Default: 4096.
        /// </summary>
        public int MaxContextTokens { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the maximum batch size for concurrent requests.
        /// Default: 1 (no batching).
        /// </summary>
        public int MaxBatchSize { get; set; } = 1;

        /// <summary>
        /// Gets or sets whether to enable KV cache optimization.
        /// Default: true.
        /// </summary>
        public bool EnableKvCache { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of threads for inference operations.
        /// If null or 0, uses system default (typically processor count).
        /// Default: null (auto).
        /// </summary>
        public int? ThreadCount { get; set; }

        /// <summary>
        /// Gets or sets the request timeout in milliseconds.
        /// If null or 0, no timeout is enforced.
        /// Default: null (no timeout).
        /// </summary>
        public int? RequestTimeoutMs { get; set; }

        /// <summary>
        /// Gets or sets the diagnostics sink for observability.
        /// If null, no diagnostics events are emitted.
        /// Default: null.
        /// </summary>
        public ISmallMindDiagnosticsSink? DiagnosticsSink { get; set; }

        /// <summary>
        /// Gets or sets whether to allow GGUF import.
        /// When true, GGUF files will be automatically converted to SMQ format.
        /// Default: false.
        /// </summary>
        public bool AllowGgufImport { get; set; }

        /// <summary>
        /// Gets or sets the directory for caching imported GGUF models.
        /// If null, uses a default cache directory.
        /// Default: null (use default).
        /// </summary>
        public string? GgufCacheDirectory { get; set; }
    }

    /// <summary>
    /// Options for creating a text generation session.
    /// These options define the generation behavior for all requests within the session.
    /// </summary>
    public sealed class TextGenerationOptions
    {
        /// <summary>
        /// Gets or sets the temperature for sampling (higher = more random).
        /// Range: 0.0 to 2.0.
        /// Default: 0.8.
        /// </summary>
        public float Temperature { get; set; } = 0.8f;

        /// <summary>
        /// Gets or sets the top-p (nucleus sampling) value.
        /// Range: 0.0 to 1.0.
        /// Default: 0.95.
        /// </summary>
        public float TopP { get; set; } = 0.95f;

        /// <summary>
        /// Gets or sets the min-p (minimum probability) value.
        /// Removes tokens with probability below min_p * max_probability.
        /// More adaptive than top-p as threshold adjusts based on confidence.
        /// Range: 0.0 to 1.0.
        /// Default: 0.0 (disabled).
        /// </summary>
        public float MinP { get; set; } = 0.0f;

        /// <summary>
        /// Gets or sets the top-k value for sampling (0 to disable).
        /// Default: 40.
        /// </summary>
        public int TopK { get; set; } = 40;

        /// <summary>
        /// Gets or sets the maximum number of output tokens to generate.
        /// Default: 100.
        /// </summary>
        public int MaxOutputTokens { get; set; } = 100;

        /// <summary>
        /// Gets or sets stop sequences that end generation.
        /// Default: empty array.
        /// </summary>
        public ReadOnlyMemory<string> StopSequences { get; set; } = ReadOnlyMemory<string>.Empty;
    }

    /// <summary>
    /// Options for creating an embedding session.
    /// </summary>
    public sealed class EmbeddingOptions
    {
        /// <summary>
        /// Gets or sets whether to normalize embedding vectors to unit length.
        /// Default: true.
        /// </summary>
        public bool Normalize { get; set; } = true;
    }
}

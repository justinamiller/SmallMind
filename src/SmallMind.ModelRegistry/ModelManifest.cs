using System.Text.Json.Serialization;

namespace SmallMind.ModelRegistry
{
    /// <summary>
    /// Manifest describing a cached model.
    /// </summary>
    internal sealed class ModelManifest
    {
        /// <summary>
        /// Gets or sets the unique model identifier.
        /// </summary>
        [JsonPropertyName("modelId")]
        public string ModelId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the display name.
        /// </summary>
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the model format (e.g., "gguf", "smq").
        /// </summary>
        [JsonPropertyName("format")]
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantization level (best-effort).
        /// </summary>
        [JsonPropertyName("quantization")]
        public string? Quantization { get; set; }

        /// <summary>
        /// Gets or sets the tokenizer ID (best-effort).
        /// </summary>
        [JsonPropertyName("tokenizerId")]
        public string? TokenizerId { get; set; }

        /// <summary>
        /// Gets or sets the maximum context tokens (best-effort).
        /// </summary>
        [JsonPropertyName("maxContextTokens")]
        public int? MaxContextTokens { get; set; }

        /// <summary>
        /// Gets or sets the file entries.
        /// </summary>
        [JsonPropertyName("files")]
        public List<ModelFileEntry> Files { get; set; } = new List<ModelFileEntry>();

        /// <summary>
        /// Gets or sets the creation timestamp (UTC).
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public string CreatedUtc { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source (path or URL).
        /// </summary>
        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional notes.
        /// </summary>
        [JsonPropertyName("notes")]
        public string? Notes { get; set; }
    }

    /// <summary>
    /// Represents a file entry in the model manifest.
    /// </summary>
    internal sealed class ModelFileEntry
    {
        /// <summary>
        /// Gets or sets the relative file path within the model directory.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        [JsonPropertyName("sizeBytes")]
        public long SizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the SHA256 hash.
        /// </summary>
        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = string.Empty;
    }
}

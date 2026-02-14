using System.Text.Json.Serialization;

namespace SmallMind.Quantization.IO.Smq
{
    /// <summary>
    /// SMQ manifest (sidecar JSON file) for human-readable metadata.
    /// Saved as "model.smq.manifest.json" alongside "model.smq".
    /// </summary>
    internal class SmqManifest
    {
        /// <summary>
        /// SMQ format version.
        /// </summary>
        [JsonPropertyName("format_version")]
        public uint FormatVersion { get; set; }

        /// <summary>
        /// Model name (if known).
        /// </summary>
        [JsonPropertyName("model_name")]
        public string? ModelName { get; set; }

        /// <summary>
        /// Creation timestamp (UTC).
        /// </summary>
        [JsonPropertyName("created_utc")]
        public DateTime CreatedUtc { get; set; }

        /// <summary>
        /// Number of tensors in the model.
        /// </summary>
        [JsonPropertyName("tensor_count")]
        public int TensorCount { get; set; }

        /// <summary>
        /// Primary quantization schemes used.
        /// </summary>
        [JsonPropertyName("quant_schemes")]
        public List<string> QuantSchemes { get; set; } = new();

        /// <summary>
        /// Model dimensions summary.
        /// </summary>
        [JsonPropertyName("model_dims")]
        public ModelDimensions? ModelDims { get; set; }

        /// <summary>
        /// SHA256 hash of the SMQ file (hex string).
        /// </summary>
        [JsonPropertyName("smq_sha256")]
        public string? SmqSha256 { get; set; }

        /// <summary>
        /// Additional metadata (vocab size, context length, etc.).
        /// </summary>
        [JsonPropertyName("metadata")]
        public Dictionary<string, object>? Metadata { get; set; }
    }
}

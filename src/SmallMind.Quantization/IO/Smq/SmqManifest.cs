using System;
using System.Collections.Generic;
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

    /// <summary>
    /// Model architecture dimensions.
    /// </summary>
    internal class ModelDimensions
    {
        /// <summary>
        /// Number of layers.
        /// </summary>
        [JsonPropertyName("n_layers")]
        public int? NLayers { get; set; }

        /// <summary>
        /// Number of attention heads.
        /// </summary>
        [JsonPropertyName("n_heads")]
        public int? NHeads { get; set; }

        /// <summary>
        /// Hidden dimension size.
        /// </summary>
        [JsonPropertyName("hidden_dim")]
        public int? HiddenDim { get; set; }

        /// <summary>
        /// Vocabulary size.
        /// </summary>
        [JsonPropertyName("vocab_size")]
        public int? VocabSize { get; set; }

        /// <summary>
        /// Context window length.
        /// </summary>
        [JsonPropertyName("context_length")]
        public int? ContextLength { get; set; }
    }
}

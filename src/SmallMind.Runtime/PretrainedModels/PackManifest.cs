using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Represents metadata for a pretrained data pack.
    /// </summary>
    internal class PackManifest
    {
        /// <summary>
        /// Unique identifier for this pack (e.g., "sm.pretrained.sentiment.v1").
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Pack type (e.g., "task-pack").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Domain of the pack (e.g., "sentiment", "finance").
        /// </summary>
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Intended use cases for this pack.
        /// </summary>
        [JsonPropertyName("intended_use")]
        public List<string> IntendedUse { get; set; } = new();

        /// <summary>
        /// Supported languages.
        /// </summary>
        [JsonPropertyName("language")]
        public List<string> Language { get; set; } = new();

        /// <summary>
        /// Source information.
        /// </summary>
        [JsonPropertyName("source")]
        public PackSource Source { get; set; } = new();

        /// <summary>
        /// Recommended settings for using this pack.
        /// </summary>
        [JsonPropertyName("recommended_settings")]
        public RecommendedSettings RecommendedSettings { get; set; } = new();

        /// <summary>
        /// Task-specific metadata.
        /// </summary>
        [JsonPropertyName("task")]
        public TaskMetadata? Task { get; set; }

        /// <summary>
        /// Statistical information about the pack.
        /// </summary>
        [JsonPropertyName("statistics")]
        public PackStatistics? Statistics { get; set; }

        /// <summary>
        /// RAG-specific metadata (if applicable).
        /// </summary>
        [JsonPropertyName("rag")]
        public RagMetadata? Rag { get; set; }
    }
}

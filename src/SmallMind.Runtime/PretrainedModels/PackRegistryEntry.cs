using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Registry entry for a pretrained pack.
    /// </summary>
    internal class PackRegistryEntry
    {
        /// <summary>
        /// Pack identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable pack name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Relative path to pack directory.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Pack type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Pack domain.
        /// </summary>
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Supported tasks.
        /// </summary>
        [JsonPropertyName("tasks")]
        public List<string> Tasks { get; set; } = new();

        /// <summary>
        /// Whether RAG is enabled.
        /// </summary>
        [JsonPropertyName("rag_enabled")]
        public bool RagEnabled { get; set; }

        /// <summary>
        /// Pack status (e.g., "stable", "experimental").
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }
}

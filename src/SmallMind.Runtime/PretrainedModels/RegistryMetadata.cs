using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Metadata for the pack registry.
    /// </summary>
    internal class RegistryMetadata
    {
        /// <summary>
        /// Total number of packs.
        /// </summary>
        [JsonPropertyName("total_packs")]
        public int TotalPacks { get; set; }

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        [JsonPropertyName("last_updated")]
        public string LastUpdated { get; set; } = string.Empty;

        /// <summary>
        /// Schema version.
        /// </summary>
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = string.Empty;
    }
}

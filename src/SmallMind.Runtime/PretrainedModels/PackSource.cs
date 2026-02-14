using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Source information for a pack.
    /// </summary>
    internal class PackSource
    {
        /// <summary>
        /// Origin of the data (e.g., "synthetic", "public-domain").
        /// </summary>
        [JsonPropertyName("origin")]
        public string Origin { get; set; } = string.Empty;

        /// <summary>
        /// License type (e.g., "MIT").
        /// </summary>
        [JsonPropertyName("license")]
        public string License { get; set; } = string.Empty;

        /// <summary>
        /// Additional notes about the source.
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }
}

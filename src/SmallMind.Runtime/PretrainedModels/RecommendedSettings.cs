using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Recommended settings for using a pack.
    /// </summary>
    internal class RecommendedSettings
    {
        /// <summary>
        /// Recommended context token length.
        /// </summary>
        [JsonPropertyName("context_tokens")]
        public int ContextTokens { get; set; }

        /// <summary>
        /// Whether deterministic mode is recommended.
        /// </summary>
        [JsonPropertyName("deterministic")]
        public bool Deterministic { get; set; }
    }
}

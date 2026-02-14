using System.Text.Json.Serialization;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Special tokens configuration.
    /// </summary>
    internal class SpecialTokensConfig
    {
        /// <summary>
        /// Beginning-of-sequence token.
        /// </summary>
        [JsonPropertyName("bos")]
        public string? Bos { get; set; }

        /// <summary>
        /// End-of-sequence token.
        /// </summary>
        [JsonPropertyName("eos")]
        public string? Eos { get; set; }

        /// <summary>
        /// Padding token.
        /// </summary>
        [JsonPropertyName("pad")]
        public string? Pad { get; set; }

        /// <summary>
        /// Unknown token.
        /// </summary>
        [JsonPropertyName("unk")]
        public string? Unk { get; set; }
    }
}

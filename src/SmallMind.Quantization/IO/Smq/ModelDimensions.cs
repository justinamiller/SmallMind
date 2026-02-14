using System.Text.Json.Serialization;

namespace SmallMind.Quantization.IO.Smq
{
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

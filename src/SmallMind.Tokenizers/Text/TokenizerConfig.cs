using System.Text.Json.Serialization;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Configuration for creating and initializing tokenizers.
    /// </summary>
    internal class TokenizerConfig
    {
        /// <summary>
        /// The type of tokenizer to create.
        /// </summary>
        [JsonPropertyName("kind")]
        public TokenizerKind Kind { get; set; } = TokenizerKind.Char;

        /// <summary>
        /// Path to the vocabulary file (for BPE, WordPiece).
        /// </summary>
        [JsonPropertyName("vocabPath")]
        public string? VocabPath { get; set; }

        /// <summary>
        /// Path to the merges file (for BPE).
        /// </summary>
        [JsonPropertyName("mergesPath")]
        public string? MergesPath { get; set; }

        /// <summary>
        /// Path to the model file (for Unigram).
        /// </summary>
        [JsonPropertyName("modelPath")]
        public string? ModelPath { get; set; }

        /// <summary>
        /// Optional name for the tokenizer (used for logging and identification).
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        /// <summary>
        /// Special tokens configuration.
        /// </summary>
        [JsonPropertyName("specialTokens")]
        public SpecialTokensConfig? SpecialTokens { get; set; }

        /// <summary>
        /// Additional options for specific tokenizer types.
        /// </summary>
        [JsonPropertyName("options")]
        public Dictionary<string, object>? Options { get; set; }

        /// <summary>
        /// For ByteFallback tokenizer: the inner tokenizer configuration.
        /// </summary>
        [JsonPropertyName("innerTokenizer")]
        public TokenizerConfig? InnerTokenizer { get; set; }

        /// <summary>
        /// Training text for CharTokenizer (required for Char mode).
        /// </summary>
        [JsonPropertyName("trainingText")]
        public string? TrainingText { get; set; }
    }

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

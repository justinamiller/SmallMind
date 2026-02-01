namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Configuration options for tokenizer selection and behavior.
    /// </summary>
    public class TokenizerOptions
    {
        /// <summary>
        /// Tokenizer mode to use.
        /// Default is Auto (tries BPE if assets exist, otherwise falls back to Char).
        /// </summary>
        public TokenizerMode Mode { get; set; } = TokenizerMode.Auto;

        /// <summary>
        /// Name of the tokenizer (used for asset discovery).
        /// Default is "default".
        /// </summary>
        public string TokenizerName { get; set; } = "default";

        /// <summary>
        /// Optional explicit path to tokenizer assets directory.
        /// If specified, this path is checked first before standard locations.
        /// </summary>
        public string? TokenizerPath { get; set; }

        /// <summary>
        /// Strict mode behavior for BPE tokenizer.
        /// If true, BPE load failures will throw TokenizationException.
        /// If false, BPE load failures will fallback to CharTokenizer.
        /// Default is false (allow fallback).
        /// </summary>
        public bool Strict { get; set; } = false;
    }
}

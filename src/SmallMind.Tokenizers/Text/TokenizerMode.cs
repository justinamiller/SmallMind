namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Specifies which tokenizer implementation to use.
    /// </summary>
    public enum TokenizerMode
    {
        /// <summary>
        /// Automatically select tokenizer based on available assets.
        /// Uses BPE if vocab.json and merges.txt are found, otherwise falls back to CharTokenizer.
        /// </summary>
        Auto,

        /// <summary>
        /// Use character-level tokenizer (default, simple, works with any text).
        /// </summary>
        Char,

        /// <summary>
        /// Use Byte Pair Encoding (BPE) tokenizer.
        /// Requires vocab.json and merges.txt assets.
        /// </summary>
        Bpe
    }
}

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Specifies the type of tokenizer to use.
    /// </summary>
    internal enum TokenizerKind
    {
        /// <summary>
        /// Character-level tokenizer (simple, works with any text).
        /// </summary>
        Char,

        /// <summary>
        /// Byte-level BPE tokenizer (GPT-2 style).
        /// Operates on UTF-8 bytes with reversible byte-to-token mapping.
        /// </summary>
        ByteBpe,

        /// <summary>
        /// Classic BPE tokenizer over Unicode characters.
        /// </summary>
        Bpe,

        /// <summary>
        /// Unigram Language Model tokenizer (SentencePiece-style).
        /// Uses Viterbi decoding for best segmentation.
        /// </summary>
        Unigram,

        /// <summary>
        /// WordPiece tokenizer (BERT-style with ## continuation).
        /// Greedy longest-match-first algorithm.
        /// </summary>
        WordPiece,

        /// <summary>
        /// Byte fallback wrapper around another tokenizer.
        /// Falls back to byte tokens for unknown sequences.
        /// </summary>
        ByteFallback
    }
}

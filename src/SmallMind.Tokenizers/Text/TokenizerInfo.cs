namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Metadata about a tokenizer instance.
    /// </summary>
    public readonly struct TokenizerInfo
    {
        /// <summary>
        /// Name of the tokenizer.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Vocabulary size (number of unique tokens).
        /// </summary>
        public int VocabSize { get; }

        /// <summary>
        /// ID of the beginning-of-sequence token, or -1 if not supported.
        /// </summary>
        public int BosTokenId { get; }

        /// <summary>
        /// ID of the end-of-sequence token, or -1 if not supported.
        /// </summary>
        public int EosTokenId { get; }

        /// <summary>
        /// ID of the padding token, or -1 if not supported.
        /// </summary>
        public int PadTokenId { get; }

        /// <summary>
        /// ID of the unknown token, or -1 if not supported.
        /// </summary>
        public int UnkTokenId { get; }

        /// <summary>
        /// Whether this tokenizer supports byte-level fallback for unknown sequences.
        /// </summary>
        public bool SupportsByteFallback { get; }

        /// <summary>
        /// Whether to automatically prepend BOS token during encoding (Llama-family models).
        /// </summary>
        public bool AddBos { get; }

        /// <summary>
        /// Creates a new TokenizerInfo instance.
        /// </summary>
        public TokenizerInfo(
            string name,
            int vocabSize,
            int bosTokenId = -1,
            int eosTokenId = -1,
            int padTokenId = -1,
            int unkTokenId = -1,
            bool supportsByteFallback = false,
            bool addBos = true)
        {
            Name = name;
            VocabSize = vocabSize;
            BosTokenId = bosTokenId;
            EosTokenId = eosTokenId;
            PadTokenId = padTokenId;
            UnkTokenId = unkTokenId;
            SupportsByteFallback = supportsByteFallback;
            // Only add BOS if explicitly requested and BOS token is available
            AddBos = addBos && bosTokenId >= 0;
        }
    }
}

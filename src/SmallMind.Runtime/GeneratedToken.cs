namespace SmallMind.Runtime
{
    /// <summary>
    /// Represents a single generated token with metadata.
    /// Used in streaming generation APIs.
    /// </summary>
    public readonly struct GeneratedToken
    {
        /// <summary>
        /// The token ID from the vocabulary.
        /// </summary>
        public int TokenId { get; }

        /// <summary>
        /// The decoded text chunk for this token.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// The position index of this token in the generated sequence (0-based).
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Optional: Log probability of this token (if available).
        /// Returns null when not computed.
        /// </summary>
        public float? LogProb { get; }

        /// <summary>
        /// Creates a new GeneratedToken.
        /// </summary>
        /// <param name="tokenId">Token ID</param>
        /// <param name="text">Decoded text</param>
        /// <param name="index">Position in sequence</param>
        /// <param name="logProb">Optional log probability</param>
        public GeneratedToken(int tokenId, string text, int index, float? logProb = null)
        {
            TokenId = tokenId;
            Text = text;
            Index = index;
            LogProb = logProb;
        }

        public override string ToString()
        {
            return LogProb.HasValue 
                ? $"Token[{Index}]={TokenId} \"{Text}\" (logprob={LogProb.Value:F4})"
                : $"Token[{Index}]={TokenId} \"{Text}\"";
        }
    }
}

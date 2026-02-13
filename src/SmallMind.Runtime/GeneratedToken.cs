namespace SmallMind.Runtime
{
    /// <summary>
    /// Reason why text generation finished.
    /// </summary>
    internal enum FinishReason
    {
        /// <summary>
        /// Generation is not yet complete (used during streaming).
        /// </summary>
        None = 0,

        /// <summary>
        /// Maximum number of new tokens was reached.
        /// </summary>
        MaxTokens = 1,

        /// <summary>
        /// End-of-sequence token was generated.
        /// </summary>
        EndOfSequence = 2,

        /// <summary>
        /// A configured stop token ID was generated.
        /// </summary>
        StopToken = 3,

        /// <summary>
        /// A configured stop sequence (text pattern) was detected.
        /// </summary>
        StopSequence = 4,

        /// <summary>
        /// Maximum time limit was exceeded.
        /// </summary>
        Timeout = 5,

        /// <summary>
        /// Maximum context length was reached.
        /// </summary>
        MaxContext = 6
    }

    /// <summary>
    /// Represents a single generated token with metadata.
    /// Used in streaming generation APIs.
    /// </summary>
    internal readonly struct GeneratedToken
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
        /// The reason generation finished, or None if still in progress.
        /// </summary>
        public FinishReason FinishReason { get; }

        /// <summary>
        /// Creates a new GeneratedToken.
        /// </summary>
        /// <param name="tokenId">Token ID</param>
        /// <param name="text">Decoded text</param>
        /// <param name="index">Position in sequence</param>
        /// <param name="logProb">Optional log probability</param>
        /// <param name="finishReason">Reason generation finished</param>
        public GeneratedToken(int tokenId, string text, int index, float? logProb = null, FinishReason finishReason = FinishReason.None)
        {
            TokenId = tokenId;
            Text = text;
            Index = index;
            LogProb = logProb;
            FinishReason = finishReason;
        }

        public override string ToString()
        {
            return LogProb.HasValue
                ? $"Token[{Index}]={TokenId} \"{Text}\" (logprob={LogProb.Value:F4})"
                : $"Token[{Index}]={TokenId} \"{Text}\"";
        }
    }
}

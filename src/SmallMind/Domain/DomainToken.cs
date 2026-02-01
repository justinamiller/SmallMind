namespace SmallMind.Domain
{
    /// <summary>
    /// Represents a single token generated during streaming domain-bounded reasoning.
    /// </summary>
    public class DomainToken
    {
        /// <summary>
        /// Gets or sets the token text.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the token ID.
        /// </summary>
        public int TokenId { get; set; }

        /// <summary>
        /// Gets or sets the index of this token in the generation sequence.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the elapsed time since generation started.
        /// </summary>
        public System.TimeSpan ElapsedTime { get; set; }

        /// <summary>
        /// Gets or sets the probability of this token (if available).
        /// </summary>
        public float? Probability { get; set; }

        /// <summary>
        /// Creates a new domain token.
        /// </summary>
        /// <param name="text">The token text.</param>
        /// <param name="tokenId">The token ID.</param>
        /// <param name="index">The token index.</param>
        /// <param name="elapsedTime">The elapsed time.</param>
        /// <param name="probability">Optional token probability.</param>
        /// <returns>A new domain token.</returns>
        public static DomainToken Create(string text, int tokenId, int index, System.TimeSpan elapsedTime, float? probability = null)
        {
            return new DomainToken
            {
                Text = text,
                TokenId = tokenId,
                Index = index,
                ElapsedTime = elapsedTime,
                Probability = probability
            };
        }
    }
}

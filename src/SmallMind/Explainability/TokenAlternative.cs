namespace SmallMind.Explainability
{
    /// <summary>
    /// Represents an alternative token that was considered but not selected.
    /// </summary>
    public class TokenAlternative
    {
        /// <summary>
        /// Gets the token ID.
        /// </summary>
        public int TokenId { get; }

        /// <summary>
        /// Gets the decoded token text.
        /// </summary>
        public string TokenText { get; }

        /// <summary>
        /// Gets the probability of this token (0.0 to 1.0).
        /// </summary>
        public double Prob { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenAlternative"/> class.
        /// </summary>
        public TokenAlternative(int tokenId, string tokenText, double prob)
        {
            TokenId = tokenId;
            TokenText = tokenText;
            Prob = prob;
        }
    }
}

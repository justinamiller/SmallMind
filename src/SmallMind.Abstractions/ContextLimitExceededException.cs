namespace SmallMind.Abstractions
{
    /// <summary>
    /// Thrown when the context window limit is exceeded.
    /// Remediation: Reduce input length or increase MaxContextTokens in options.
    /// </summary>
    public class ContextLimitExceededException : SmallMindException
    {
        /// <summary>
        /// Gets the total tokens in the conversation.
        /// </summary>
        public int TotalTokens { get; }

        /// <summary>
        /// Gets the context limit.
        /// </summary>
        public int ContextLimit { get; }

        /// <summary>
        /// Gets the system message tokens.
        /// </summary>
        public int SystemTokens { get; }

        /// <summary>
        /// Gets the current message tokens.
        /// </summary>
        public int MessageTokens { get; }

        /// <summary>
        /// Creates a new ContextLimitExceededException.
        /// </summary>
        public ContextLimitExceededException(string message, int totalTokens, int contextLimit, int systemTokens = 0, int messageTokens = 0)
            : base(message, "CONTEXT_LIMIT_EXCEEDED")
        {
            TotalTokens = totalTokens;
            ContextLimit = contextLimit;
            SystemTokens = systemTokens;
            MessageTokens = messageTokens;
        }
    }
}

namespace SmallMind.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a timeout occurs during inference.
    /// </summary>
    public class InferenceTimeoutException : InferenceException
    {
        /// <summary>
        /// Gets the timeout duration in milliseconds.
        /// </summary>
        public int TimeoutMs { get; }

        public InferenceTimeoutException(int timeoutMs)
            : base($"Inference timeout after {timeoutMs}ms")
        {
            TimeoutMs = timeoutMs;
        }

        public InferenceTimeoutException(int timeoutMs, string additionalMessage)
            : base($"Inference timeout after {timeoutMs}ms. {additionalMessage}")
        {
            TimeoutMs = timeoutMs;
        }
    }
}

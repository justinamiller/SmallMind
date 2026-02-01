using System;

namespace SmallMind.Core.Exceptions
{
    /// <summary>
    /// Base exception for inference-related errors.
    /// </summary>
    public class InferenceException : SmallMindException
    {
        public InferenceException(string message) : base(message) { }
        public InferenceException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Exception thrown when resource limits are exceeded during inference.
    /// </summary>
    public class ResourceLimitException : InferenceException
    {
        /// <summary>
        /// Gets the type of resource that was exceeded.
        /// </summary>
        public string ResourceType { get; }

        /// <summary>
        /// Gets the limit that was exceeded.
        /// </summary>
        public long Limit { get; }

        /// <summary>
        /// Gets the actual value that exceeded the limit.
        /// </summary>
        public long Actual { get; }

        public ResourceLimitException(string resourceType, long limit, long actual)
            : base($"{resourceType} limit exceeded: {actual} > {limit}")
        {
            ResourceType = resourceType;
            Limit = limit;
            Actual = actual;
        }

        public ResourceLimitException(string resourceType, long limit, long actual, string additionalMessage)
            : base($"{resourceType} limit exceeded: {actual} > {limit}. {additionalMessage}")
        {
            ResourceType = resourceType;
            Limit = limit;
            Actual = actual;
        }
    }

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

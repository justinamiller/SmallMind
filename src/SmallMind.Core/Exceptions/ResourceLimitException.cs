namespace SmallMind.Core.Exceptions
{
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
}

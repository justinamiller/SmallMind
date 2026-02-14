namespace SmallMind.Abstractions
{
    /// <summary>
    /// Thrown when insufficient memory is available for an operation.
    /// Remediation: Reduce batch size, lower model size, or increase available memory.
    /// </summary>
    public class InsufficientMemoryException : SmallMindException
    {
        /// <summary>
        /// Gets the estimated memory required in bytes.
        /// </summary>
        public long RequiredBytes { get; }

        /// <summary>
        /// Gets the available memory in bytes.
        /// </summary>
        public long AvailableBytes { get; }

        /// <summary>
        /// Creates a new InsufficientMemoryException.
        /// </summary>
        public InsufficientMemoryException(long requiredBytes, long availableBytes)
            : base($"Insufficient memory: operation requires ~{requiredBytes / 1024 / 1024}MB, " +
                   $"but only {availableBytes / 1024 / 1024}MB available (>90% would be used). " +
                   $"Remediation: reduce batch size or lower model size.", "INSUFFICIENT_MEMORY")
        {
            RequiredBytes = requiredBytes;
            AvailableBytes = availableBytes;
        }
    }
}

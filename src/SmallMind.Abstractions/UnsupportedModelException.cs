namespace SmallMind.Abstractions
{
    /// <summary>
    /// Thrown when attempting to load an unsupported model file format.
    /// Remediation: Ensure model file is .smq format, or enable GGUF import if supported.
    /// </summary>
    public class UnsupportedModelException : SmallMindException
    {
        /// <summary>
        /// Gets the file path that failed to load.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the file extension that was not supported.
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// Creates a new UnsupportedModelException.
        /// </summary>
        public UnsupportedModelException(string filePath, string extension, string message)
            : base($"Unsupported model format: {message}", "UNSUPPORTED_MODEL_FORMAT")
        {
            FilePath = filePath;
            Extension = extension;
        }
    }
}

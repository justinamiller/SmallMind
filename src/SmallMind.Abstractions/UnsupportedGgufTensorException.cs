namespace SmallMind.Abstractions
{
    /// <summary>
    /// Thrown when a GGUF tensor type is not supported by the engine.
    /// Remediation: Use a different quantization format, or file an issue if this is a common GGUF format.
    /// </summary>
    public class UnsupportedGgufTensorException : SmallMindException
    {
        /// <summary>
        /// Gets the unsupported tensor type value.
        /// </summary>
        public int TensorType { get; }

        /// <summary>
        /// Gets the tensor name that failed.
        /// </summary>
        public string TensorName { get; }

        /// <summary>
        /// Creates a new UnsupportedGgufTensorException.
        /// </summary>
        public UnsupportedGgufTensorException(string tensorName, int tensorType, string message)
            : base($"Unsupported GGUF tensor type: {message}", "UNSUPPORTED_GGUF_TENSOR")
        {
            TensorName = tensorName;
            TensorType = tensorType;
        }
    }
}

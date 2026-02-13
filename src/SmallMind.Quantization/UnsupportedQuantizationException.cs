namespace SmallMind.Quantization
{
    /// <summary>
    /// Exception thrown when an unsupported quantization type or operation is encountered.
    /// </summary>
    public class UnsupportedQuantizationException : NotSupportedException
    {
        /// <summary>
        /// Initializes a new instance of the UnsupportedQuantizationException class.
        /// </summary>
        public UnsupportedQuantizationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the UnsupportedQuantizationException class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public UnsupportedQuantizationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the UnsupportedQuantizationException class with a specified error message and a reference to the inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public UnsupportedQuantizationException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}

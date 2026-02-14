namespace SmallMind.Abstractions
{
    /// <summary>
    /// Base exception for all SmallMind engine errors.
    /// All public exception types inherit from this.
    /// </summary>
    public class SmallMindException : Exception
    {
        /// <summary>
        /// Gets the error code for programmatic handling.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Creates a new SmallMindException.
        /// </summary>
        public SmallMindException(string message, string errorCode = "SMALLMIND_ERROR")
            : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Creates a new SmallMindException with an inner exception.
        /// </summary>
        public SmallMindException(string message, Exception innerException, string errorCode = "SMALLMIND_ERROR")
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}

using System;

namespace SmallMind.Exceptions
{
    /// <summary>
    /// Base exception class for all SmallMind-specific exceptions.
    /// Provides structured metadata for error diagnosis and handling.
    /// </summary>
    public class SmallMindException : Exception
    {
        /// <summary>
        /// Gets the error code that categorizes this exception.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmallMindException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="errorCode">The error code that categorizes this exception.</param>
        public SmallMindException(string message, string errorCode = "SMALLMIND_ERROR")
            : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SmallMindException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="errorCode">The error code that categorizes this exception.</param>
        public SmallMindException(string message, Exception innerException, string errorCode = "SMALLMIND_ERROR")
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}

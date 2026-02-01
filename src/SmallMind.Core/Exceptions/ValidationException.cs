using System;

namespace SmallMind.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when input validation fails.
    /// </summary>
    public class ValidationException : SmallMindException
    {
        /// <summary>
        /// Gets the name of the parameter that failed validation.
        /// </summary>
        public string? ParameterName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="parameterName">The name of the parameter that failed validation.</param>
        public ValidationException(string message, string? parameterName = null)
            : base(message, "VALIDATION_ERROR")
        {
            ParameterName = parameterName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationException"/> class with a specified error message and inner exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        /// <param name="parameterName">The name of the parameter that failed validation.</param>
        public ValidationException(string message, Exception innerException, string? parameterName = null)
            : base(message, innerException, "VALIDATION_ERROR")
        {
            ParameterName = parameterName;
        }
    }
}

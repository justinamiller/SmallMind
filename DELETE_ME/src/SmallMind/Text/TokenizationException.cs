using System;

namespace SmallMind.Text
{
    /// <summary>
    /// Exception thrown when tokenization operations fail.
    /// </summary>
    public class TokenizationException : Exception
    {
        /// <summary>
        /// Creates a new TokenizationException with the specified message.
        /// </summary>
        public TokenizationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Creates a new TokenizationException with the specified message and inner exception.
        /// </summary>
        public TokenizationException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}

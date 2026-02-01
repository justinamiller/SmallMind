using System;
using SmallMind.Exceptions;

namespace SmallMind.Domain
{
    /// <summary>
    /// Exception thrown when content is rejected as out of domain.
    /// </summary>
    public class OutOfDomainException : SmallMindException
    {
        /// <summary>
        /// Gets the content that was rejected.
        /// </summary>
        public string RejectedContent { get; }

        /// <summary>
        /// Gets the reason for rejection.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutOfDomainException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="rejectedContent">The content that was rejected.</param>
        /// <param name="reason">The reason for rejection.</param>
        public OutOfDomainException(string message, string rejectedContent, string reason)
            : base(message, "OUT_OF_DOMAIN")
        {
            RejectedContent = rejectedContent;
            Reason = reason;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutOfDomainException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="rejectedContent">The content that was rejected.</param>
        /// <param name="reason">The reason for rejection.</param>
        /// <param name="innerException">The inner exception.</param>
        public OutOfDomainException(string message, string rejectedContent, string reason, Exception innerException)
            : base(message, innerException, "OUT_OF_DOMAIN")
        {
            RejectedContent = rejectedContent;
            Reason = reason;
        }
    }
}

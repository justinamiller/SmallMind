using System;
using SmallMind.Core.Exceptions;

namespace SmallMind.Domain
{
    /// <summary>
    /// Exception thrown when a domain policy is violated.
    /// </summary>
    public class DomainPolicyViolationException : SmallMindException
    {
        /// <summary>
        /// Gets the name of the policy that was violated.
        /// </summary>
        public string PolicyName { get; }

        /// <summary>
        /// Gets the value that violated the policy.
        /// </summary>
        public object? ViolatingValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainPolicyViolationException"/> class.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="policyName">The name of the policy that was violated.</param>
        /// <param name="violatingValue">The value that violated the policy.</param>
        public DomainPolicyViolationException(string message, string policyName, object? violatingValue = null)
            : base(message, "DOMAIN_POLICY_VIOLATION")
        {
            PolicyName = policyName;
            ViolatingValue = violatingValue;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DomainPolicyViolationException"/> class with an inner exception.
        /// </summary>
        /// <param name="message">The error message.</param>
        /// <param name="policyName">The name of the policy that was violated.</param>
        /// <param name="violatingValue">The value that violated the policy.</param>
        /// <param name="innerException">The inner exception.</param>
        public DomainPolicyViolationException(string message, string policyName, object? violatingValue, Exception innerException)
            : base(message, innerException, "DOMAIN_POLICY_VIOLATION")
        {
            PolicyName = policyName;
            ViolatingValue = violatingValue;
        }
    }
}

namespace SmallMind.Abstractions
{
    /// <summary>
    /// Thrown when a security violation is detected (malicious input, unauthorized access, etc).
    /// Remediation: Review input for injections, check authorization policies.
    /// </summary>
    public class SecurityViolationException : SmallMindException
    {
        /// <summary>
        /// Gets the violation type.
        /// </summary>
        public string ViolationType { get; }

        /// <summary>
        /// Creates a new SecurityViolationException.
        /// </summary>
        public SecurityViolationException(string violationType, string message)
            : base($"Security violation: {message}", "SECURITY_VIOLATION")
        {
            ViolationType = violationType;
        }
    }
}

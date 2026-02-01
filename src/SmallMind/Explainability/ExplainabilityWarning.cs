namespace SmallMind.Explainability
{
    /// <summary>
    /// Represents a warning or issue encountered during explainability capture.
    /// </summary>
    public class ExplainabilityWarning
    {
        /// <summary>
        /// Gets the warning code or identifier.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the human-readable warning message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplainabilityWarning"/> class.
        /// </summary>
        public ExplainabilityWarning(string code, string message)
        {
            Code = code;
            Message = message;
        }

        /// <summary>
        /// Returns a string representation of the warning.
        /// </summary>
        public override string ToString() => $"[{Code}] {Message}";
    }
}

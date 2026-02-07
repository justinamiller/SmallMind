namespace SmallMind.Workflows
{
    /// <summary>
    /// Defines the expected output format for a workflow step.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// Plain text output (discouraged - allows free-form responses).
        /// </summary>
        PlainText,

        /// <summary>
        /// JSON-only output (default - structured data).
        /// </summary>
        JsonOnly,

        /// <summary>
        /// Enum-only output (single value from allowed list).
        /// </summary>
        EnumOnly,

        /// <summary>
        /// Regex-constrained output.
        /// </summary>
        RegexConstrained
    }
}

namespace SmallMind.Domain
{
    /// <summary>
    /// Specifies the output format for domain-bound reasoning responses.
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// Plain text output with no format constraints.
        /// </summary>
        PlainText,

        /// <summary>
        /// Output must be valid JSON.
        /// </summary>
        JsonOnly,

        /// <summary>
        /// Enum-only output (single value from allowed list).
        /// </summary>
        EnumOnly,

        /// <summary>
        /// Output must match a specified regular expression pattern.
        /// </summary>
        RegexConstrained
    }
}

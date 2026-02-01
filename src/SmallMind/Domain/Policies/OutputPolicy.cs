using SmallMind.Validation;

namespace SmallMind.Domain.Policies
{
    /// <summary>
    /// Defines output format and validation constraints for domain-bounded generation.
    /// </summary>
    public class OutputPolicy
    {
        /// <summary>
        /// Gets or sets the required output format.
        /// Default is PlainText.
        /// </summary>
        public OutputFormat Format { get; set; } = OutputFormat.PlainText;

        /// <summary>
        /// Gets or sets the regular expression pattern for RegexConstrained format.
        /// Only used when Format is RegexConstrained.
        /// </summary>
        public string? Regex { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of characters in the output.
        /// Default is 10,000 characters.
        /// </summary>
        public int MaxCharacters { get; set; } = 10_000;

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for format validation.
        /// Default is 2 retries.
        /// </summary>
        public int MaxRetries { get; set; } = 2;

        /// <summary>
        /// Validates the output policy configuration.
        /// </summary>
        public void Validate()
        {
            Guard.GreaterThan(MaxCharacters, 0, nameof(MaxCharacters));
            Guard.GreaterThanOrEqualTo(MaxRetries, 0, nameof(MaxRetries));

            if (Format == OutputFormat.RegexConstrained)
            {
                Guard.NotNullOrWhiteSpace(Regex, nameof(Regex));
            }
        }

        /// <summary>
        /// Creates a default OutputPolicy for plain text output.
        /// </summary>
        /// <returns>A default output policy.</returns>
        public static OutputPolicy Default() => new OutputPolicy();

        /// <summary>
        /// Creates an OutputPolicy for JSON-only output.
        /// </summary>
        /// <returns>A policy requiring JSON output.</returns>
        public static OutputPolicy JsonOnly()
        {
            return new OutputPolicy
            {
                Format = OutputFormat.JsonOnly
            };
        }

        /// <summary>
        /// Creates an OutputPolicy constrained by a regular expression.
        /// </summary>
        /// <param name="regex">The regex pattern to match.</param>
        /// <returns>A policy requiring output to match the regex.</returns>
        public static OutputPolicy MatchRegex(string regex)
        {
            Guard.NotNullOrWhiteSpace(regex, nameof(regex));

            return new OutputPolicy
            {
                Format = OutputFormat.RegexConstrained,
                Regex = regex
            };
        }
    }
}

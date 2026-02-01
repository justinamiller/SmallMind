using System.Collections.Generic;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Specifies the expected output format and constraints for a workflow step.
    /// </summary>
    public class StepOutputSpec
    {
        /// <summary>
        /// The format of the output.
        /// </summary>
        public OutputFormat Format { get; set; } = OutputFormat.JsonOnly;

        /// <summary>
        /// JSON template (example output shape) for JsonOnly format.
        /// </summary>
        public string? JsonTemplate { get; set; }

        /// <summary>
        /// Required JSON field names for JsonOnly format.
        /// </summary>
        public IReadOnlyList<string>? RequiredJsonFields { get; set; }

        /// <summary>
        /// Allowed values for EnumOnly format.
        /// </summary>
        public IReadOnlyList<string>? AllowedValues { get; set; }

        /// <summary>
        /// Regex pattern for RegexConstrained format.
        /// </summary>
        public string? Regex { get; set; }

        /// <summary>
        /// Maximum output characters allowed.
        /// </summary>
        public int MaxOutputChars { get; set; } = 2000;

        /// <summary>
        /// If true, reject when output is invalid; if false, allow best-effort with warnings.
        /// </summary>
        public bool Strict { get; set; } = true;
    }
}

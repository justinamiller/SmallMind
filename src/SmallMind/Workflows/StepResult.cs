using System;
using System.Collections.Generic;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Result of executing a single workflow step.
    /// </summary>
    public class StepResult
    {
        /// <summary>
        /// Step identifier.
        /// </summary>
        public string StepId { get; set; } = string.Empty;

        /// <summary>
        /// Execution status.
        /// </summary>
        public StepStatus Status { get; set; }

        /// <summary>
        /// Raw output text from the step.
        /// </summary>
        public string OutputText { get; set; } = string.Empty;

        /// <summary>
        /// Parsed output object (for JSON output).
        /// </summary>
        public object? OutputObject { get; set; }

        /// <summary>
        /// Validation errors (if any).
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new List<string>();

        /// <summary>
        /// Number of input tokens.
        /// </summary>
        public int TokensIn { get; set; }

        /// <summary>
        /// Number of output tokens.
        /// </summary>
        public int TokensOut { get; set; }

        /// <summary>
        /// Step execution duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Failure reason (if status is Failed).
        /// </summary>
        public string? FailureReason { get; set; }

        /// <summary>
        /// Number of attempts made (including retries).
        /// </summary>
        public int Attempts { get; set; } = 1;
    }
}

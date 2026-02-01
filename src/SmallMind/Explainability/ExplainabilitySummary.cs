using System;

namespace SmallMind.Explainability
{
    /// <summary>
    /// Summary data provided when generation completes.
    /// </summary>
    public class ExplainabilitySummary
    {
        /// <summary>
        /// Gets the total elapsed time for generation.
        /// </summary>
        public TimeSpan TotalDuration { get; }

        /// <summary>
        /// Gets a value indicating whether generation completed successfully.
        /// </summary>
        public bool Success { get; }

        /// <summary>
        /// Gets an optional error message if generation failed.
        /// </summary>
        public string? ErrorMessage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplainabilitySummary"/> class.
        /// </summary>
        public ExplainabilitySummary(TimeSpan totalDuration, bool success, string? errorMessage = null)
        {
            TotalDuration = totalDuration;
            Success = success;
            ErrorMessage = errorMessage;
        }
    }
}

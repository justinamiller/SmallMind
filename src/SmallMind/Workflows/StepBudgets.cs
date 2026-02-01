using System;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Token and duration budgets for a workflow step.
    /// </summary>
    public class StepBudgets
    {
        /// <summary>
        /// Maximum input tokens for this step.
        /// </summary>
        public int? MaxInputTokens { get; set; }

        /// <summary>
        /// Maximum output tokens for this step.
        /// </summary>
        public int? MaxOutputTokens { get; set; }

        /// <summary>
        /// Maximum duration for this step.
        /// </summary>
        public TimeSpan? MaxStepDuration { get; set; }
    }
}

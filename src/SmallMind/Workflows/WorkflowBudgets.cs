using System;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Token and duration budgets for an entire workflow.
    /// </summary>
    public class WorkflowBudgets
    {
        /// <summary>
        /// Maximum total tokens across all steps.
        /// </summary>
        public int? MaxTotalTokens { get; set; }

        /// <summary>
        /// Default maximum tokens per step.
        /// </summary>
        public int MaxStepTokens { get; set; } = 500;

        /// <summary>
        /// Maximum total duration for the workflow.
        /// </summary>
        public TimeSpan? MaxDuration { get; set; }
    }
}

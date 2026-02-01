using System;
using System.Collections.Generic;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Result of executing a complete workflow.
    /// </summary>
    public class WorkflowRunResult
    {
        /// <summary>
        /// Workflow run identifier.
        /// </summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>
        /// Overall workflow status.
        /// </summary>
        public WorkflowRunStatus Status { get; set; }

        /// <summary>
        /// Final workflow state.
        /// </summary>
        public WorkflowState FinalState { get; set; } = new WorkflowState();

        /// <summary>
        /// Results for each step.
        /// </summary>
        public IReadOnlyList<StepResult> Steps { get; set; } = new List<StepResult>();

        /// <summary>
        /// Total workflow duration.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Total input tokens across all steps.
        /// </summary>
        public int TotalInputTokens { get; set; }

        /// <summary>
        /// Total output tokens across all steps.
        /// </summary>
        public int TotalOutputTokens { get; set; }

        /// <summary>
        /// Overall failure reason (if status is Failed or RejectedPolicy).
        /// </summary>
        public string? FailureReason { get; set; }
    }
}

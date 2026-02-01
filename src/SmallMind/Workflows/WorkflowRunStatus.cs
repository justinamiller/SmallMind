namespace SmallMind.Workflows
{
    /// <summary>
    /// Status of a workflow run.
    /// </summary>
    public enum WorkflowRunStatus
    {
        /// <summary>
        /// Workflow completed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// Workflow failed during execution.
        /// </summary>
        Failed,

        /// <summary>
        /// Workflow was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Workflow was rejected due to policy violation.
        /// </summary>
        RejectedPolicy
    }
}

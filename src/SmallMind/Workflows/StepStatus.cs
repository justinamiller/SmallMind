namespace SmallMind.Workflows
{
    /// <summary>
    /// Status of a workflow step.
    /// </summary>
    public enum StepStatus
    {
        /// <summary>
        /// Step completed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// Step failed.
        /// </summary>
        Failed,

        /// <summary>
        /// Step was skipped.
        /// </summary>
        Skipped,

        /// <summary>
        /// Step requires human approval.
        /// </summary>
        HumanApprovalRequired
    }
}

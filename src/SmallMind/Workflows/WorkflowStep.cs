namespace SmallMind.Workflows
{
    /// <summary>
    /// Defines a single step in a workflow.
    /// </summary>
    public class WorkflowStep
    {
        /// <summary>
        /// Stable identifier for this step.
        /// </summary>
        public string StepId { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable title for this step.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// System guidance/instruction for the model at this step.
        /// </summary>
        public string Instruction { get; set; } = string.Empty;

        /// <summary>
        /// Input specification for this step.
        /// </summary>
        public StepInputSpec InputSpec { get; set; } = new StepInputSpec();

        /// <summary>
        /// Output specification for this step.
        /// </summary>
        public StepOutputSpec OutputSpec { get; set; } = new StepOutputSpec();

        /// <summary>
        /// Budget overrides for this step (optional).
        /// </summary>
        public StepBudgets? Budgets { get; set; }

        /// <summary>
        /// Retry policy for this step.
        /// </summary>
        public StepRetryPolicy Retry { get; set; } = new StepRetryPolicy();

        /// <summary>
        /// Flag indicating if this step requires human approval (not implemented, flag only).
        /// </summary>
        public bool RequiresHumanApproval { get; set; } = false;
    }
}

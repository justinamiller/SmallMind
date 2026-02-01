namespace SmallMind.Workflows
{
    /// <summary>
    /// Request to execute a workflow.
    /// </summary>
    public class WorkflowRunRequest
    {
        /// <summary>
        /// Workflow definition to execute.
        /// </summary>
        public WorkflowDefinition Workflow { get; set; } = new WorkflowDefinition();

        /// <summary>
        /// Initial state for the workflow.
        /// </summary>
        public WorkflowState InitialState { get; set; } = new WorkflowState();

        /// <summary>
        /// Optional run identifier (generated if not provided).
        /// </summary>
        public string? RunId { get; set; }

        /// <summary>
        /// Optional user input (only used if step allows notes).
        /// </summary>
        public string? UserInput { get; set; }
    }
}

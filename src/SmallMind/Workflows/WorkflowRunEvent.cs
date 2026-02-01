namespace SmallMind.Workflows
{
    /// <summary>
    /// Type of workflow run event.
    /// </summary>
    public enum WorkflowRunEventType
    {
        /// <summary>
        /// Workflow run started.
        /// </summary>
        RunStarted,

        /// <summary>
        /// Workflow step started.
        /// </summary>
        StepStarted,

        /// <summary>
        /// Token generated during step execution.
        /// </summary>
        TokenGenerated,

        /// <summary>
        /// Workflow step completed.
        /// </summary>
        StepCompleted,

        /// <summary>
        /// Workflow run completed.
        /// </summary>
        RunCompleted
    }

    /// <summary>
    /// Event emitted during workflow execution (for streaming).
    /// </summary>
    public class WorkflowRunEvent
    {
        /// <summary>
        /// Event type.
        /// </summary>
        public WorkflowRunEventType Type { get; set; }

        /// <summary>
        /// Run identifier.
        /// </summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>
        /// Step identifier (for step-related events).
        /// </summary>
        public string? StepId { get; set; }

        /// <summary>
        /// Generated token (for TokenGenerated events).
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// Elapsed time since run/step start.
        /// </summary>
        public long ElapsedMilliseconds { get; set; }

        /// <summary>
        /// Additional event data.
        /// </summary>
        public string? Message { get; set; }
    }
}

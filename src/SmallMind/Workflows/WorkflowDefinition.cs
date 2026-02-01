using System.Collections.Generic;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Defines a complete workflow with steps, budgets, and options.
    /// </summary>
    public class WorkflowDefinition
    {
        /// <summary>
        /// Workflow name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Workflow version.
        /// </summary>
        public string Version { get; set; } = "1.0";

        /// <summary>
        /// Ordered list of workflow steps.
        /// </summary>
        public IReadOnlyList<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();

        /// <summary>
        /// Default budgets for the workflow.
        /// </summary>
        public WorkflowBudgets Budgets { get; set; } = new WorkflowBudgets();

        /// <summary>
        /// Default runner options.
        /// </summary>
        public WorkflowRunnerOptions RunnerOptions { get; set; } = new WorkflowRunnerOptions();
    }
}

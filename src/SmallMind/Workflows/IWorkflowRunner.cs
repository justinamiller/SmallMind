using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Interface for executing workflows.
    /// </summary>
    public interface IWorkflowRunner
    {
        /// <summary>
        /// Execute a workflow and return the final result.
        /// </summary>
        Task<WorkflowRunResult> RunAsync(WorkflowRunRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Execute a workflow with streaming events.
        /// </summary>
        IAsyncEnumerable<WorkflowRunEvent> RunStreamAsync(WorkflowRunRequest request, CancellationToken cancellationToken = default);
    }
}

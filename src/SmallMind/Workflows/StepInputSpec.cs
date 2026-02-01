using System.Collections.Generic;

namespace SmallMind.Workflows
{
    /// <summary>
    /// Specifies the input requirements for a workflow step.
    /// </summary>
    public class StepInputSpec
    {
        /// <summary>
        /// State keys that must be present before the step can execute.
        /// </summary>
        public IReadOnlyList<string> RequiredStateKeys { get; set; } = new List<string>();

        /// <summary>
        /// State keys that may be used if present.
        /// </summary>
        public IReadOnlyList<string> OptionalStateKeys { get; set; } = new List<string>();

        /// <summary>
        /// Maximum context characters to include.
        /// </summary>
        public int MaxContextChars { get; set; } = 4000;

        /// <summary>
        /// Include outputs from prior steps in the context.
        /// </summary>
        public bool IncludePriorStepOutputs { get; set; } = true;

        /// <summary>
        /// Allow user-provided notes/input.
        /// </summary>
        public bool AllowUserNotes { get; set; } = false;
    }
}

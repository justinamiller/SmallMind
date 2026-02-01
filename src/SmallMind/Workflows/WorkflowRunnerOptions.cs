namespace SmallMind.Workflows
{
    /// <summary>
    /// Options for the workflow runner.
    /// </summary>
    public class WorkflowRunnerOptions
    {
        /// <summary>
        /// Enable deterministic execution (requires seed).
        /// </summary>
        public bool Deterministic { get; set; } = false;

        /// <summary>
        /// Random seed for deterministic execution.
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Default temperature for generation.
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Default top-k for generation.
        /// </summary>
        public int TopK { get; set; } = 40;

        /// <summary>
        /// Default top-p for generation (not yet implemented in base generator).
        /// </summary>
        public double TopP { get; set; } = 0.9;

        /// <summary>
        /// Stop workflow execution on first step failure.
        /// </summary>
        public bool StopOnFailure { get; set; } = true;

        /// <summary>
        /// Emit diagnostic information during execution.
        /// </summary>
        public bool EmitDiagnostics { get; set; } = true;
    }
}

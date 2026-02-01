namespace SmallMind.Workflows
{
    /// <summary>
    /// Retry policy for a workflow step.
    /// </summary>
    public class StepRetryPolicy
    {
        /// <summary>
        /// Maximum number of attempts (including original).
        /// Default is 2 (original + 1 repair attempt).
        /// </summary>
        public int MaxAttempts { get; set; } = 2;

        /// <summary>
        /// Whether retries should use the same seed (for determinism).
        /// </summary>
        public bool UseSameSeed { get; set; } = true;
    }
}

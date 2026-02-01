namespace SmallMind.Explainability
{
    /// <summary>
    /// Defines the level of explainability data to capture during text generation.
    /// Higher levels provide more detail but may incur performance overhead.
    /// </summary>
    public enum ExplainabilityLevel
    {
        /// <summary>
        /// No explainability data captured. Zero overhead.
        /// </summary>
        None = 0,

        /// <summary>
        /// Basic explainability: token probabilities and top-k alternatives.
        /// Minimal overhead.
        /// </summary>
        Basic = 1,

        /// <summary>
        /// Standard explainability: includes confidence summaries and input saliency (best-effort).
        /// Moderate overhead.
        /// </summary>
        Standard = 2,

        /// <summary>
        /// Detailed explainability: step-by-step attribution with full context.
        /// Higher overhead, best-effort basis.
        /// </summary>
        Detailed = 3
    }
}

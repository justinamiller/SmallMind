namespace SmallMind.Explainability
{
    /// <summary>
    /// Represents the influence of a portion of the input on the generated output.
    /// Computed via ablation-based saliency analysis (best-effort).
    /// </summary>
    public class InputAttribution
    {
        /// <summary>
        /// Gets the starting token index in the input sequence.
        /// </summary>
        public int InputTokenIndexStart { get; }

        /// <summary>
        /// Gets the ending token index (exclusive) in the input sequence.
        /// </summary>
        public int InputTokenIndexEnd { get; }

        /// <summary>
        /// Gets the influence score (higher = more influential).
        /// Computed as probability delta when this portion is ablated.
        /// </summary>
        public double InfluenceScore { get; }

        /// <summary>
        /// Gets an optional text snippet for this input region.
        /// </summary>
        public string? Snippet { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="InputAttribution"/> class.
        /// </summary>
        public InputAttribution(int inputTokenIndexStart, int inputTokenIndexEnd, double influenceScore, string? snippet = null)
        {
            InputTokenIndexStart = inputTokenIndexStart;
            InputTokenIndexEnd = inputTokenIndexEnd;
            InfluenceScore = influenceScore;
            Snippet = snippet;
        }
    }
}

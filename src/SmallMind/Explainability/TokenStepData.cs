using System;

namespace SmallMind.Explainability
{
    /// <summary>
    /// Data captured for a single token generation step.
    /// </summary>
    public class TokenStepData
    {
        /// <summary>
        /// Gets the zero-based step index.
        /// </summary>
        public int StepIndex { get; }

        /// <summary>
        /// Gets the ID of the selected token.
        /// </summary>
        public int SelectedTokenId { get; }

        /// <summary>
        /// Gets the decoded text of the selected token.
        /// </summary>
        public string SelectedTokenText { get; }

        /// <summary>
        /// Gets the probability of the selected token.
        /// </summary>
        public double SelectedTokenProb { get; }

        /// <summary>
        /// Gets the top-k alternative token IDs (sorted by descending probability).
        /// </summary>
        public int[] AlternativeTokenIds { get; }

        /// <summary>
        /// Gets the top-k alternative token texts.
        /// </summary>
        public string[] AlternativeTokenTexts { get; }

        /// <summary>
        /// Gets the top-k alternative probabilities.
        /// </summary>
        public double[] AlternativeProbs { get; }

        /// <summary>
        /// Gets the entropy of the distribution (optional).
        /// </summary>
        public double? Entropy { get; }

        /// <summary>
        /// Gets the elapsed time for this step (optional).
        /// </summary>
        public TimeSpan? Elapsed { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenStepData"/> class.
        /// </summary>
        public TokenStepData(
            int stepIndex,
            int selectedTokenId,
            string selectedTokenText,
            double selectedTokenProb,
            int[] alternativeTokenIds,
            string[] alternativeTokenTexts,
            double[] alternativeProbs,
            double? entropy = null,
            TimeSpan? elapsed = null)
        {
            StepIndex = stepIndex;
            SelectedTokenId = selectedTokenId;
            SelectedTokenText = selectedTokenText;
            SelectedTokenProb = selectedTokenProb;
            AlternativeTokenIds = alternativeTokenIds;
            AlternativeTokenTexts = alternativeTokenTexts;
            AlternativeProbs = alternativeProbs;
            Entropy = entropy;
            Elapsed = elapsed;
        }
    }
}

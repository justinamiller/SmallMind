using System;
using System.Collections.Generic;

namespace SmallMind.Explainability
{
    /// <summary>
    /// Captures explainability data for a single token generation step.
    /// </summary>
    public class TokenStepExplanation
    {
        /// <summary>
        /// Gets the zero-based step index in the generation sequence.
        /// </summary>
        public int StepIndex { get; }

        /// <summary>
        /// Gets the ID of the selected token.
        /// </summary>
        public int TokenId { get; }

        /// <summary>
        /// Gets the decoded text of the selected token.
        /// </summary>
        public string TokenText { get; }

        /// <summary>
        /// Gets the probability of the selected token (0.0 to 1.0).
        /// </summary>
        public double TokenProb { get; }

        /// <summary>
        /// Gets the list of alternative tokens considered.
        /// Sorted by descending probability.
        /// </summary>
        public IReadOnlyList<TokenAlternative> Alternatives { get; }

        /// <summary>
        /// Gets the entropy of the probability distribution for this step (optional).
        /// Higher entropy indicates less certainty.
        /// </summary>
        public double? StepEntropy { get; }

        /// <summary>
        /// Gets the elapsed time for this generation step (optional).
        /// </summary>
        public TimeSpan? Elapsed { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenStepExplanation"/> class.
        /// </summary>
        public TokenStepExplanation(
            int stepIndex,
            int tokenId,
            string tokenText,
            double tokenProb,
            IReadOnlyList<TokenAlternative> alternatives,
            double? stepEntropy = null,
            TimeSpan? elapsed = null)
        {
            StepIndex = stepIndex;
            TokenId = tokenId;
            TokenText = tokenText;
            TokenProb = tokenProb;
            Alternatives = alternatives;
            StepEntropy = stepEntropy;
            Elapsed = elapsed;
        }
    }
}

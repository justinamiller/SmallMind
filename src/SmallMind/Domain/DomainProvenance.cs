using System.Collections.Generic;

namespace SmallMind.Domain
{
    /// <summary>
    /// Provides provenance and explainability information for domain-bounded reasoning.
    /// </summary>
    public class DomainProvenance
    {
        /// <summary>
        /// Gets or sets the overall confidence score (0.0 to 1.0).
        /// Calculated as the average of top token probabilities.
        /// </summary>
        public double Confidence { get; set; }

        /// <summary>
        /// Gets or sets the list of evidence items supporting the answer.
        /// </summary>
        public IReadOnlyList<DomainEvidenceItem> Evidence { get; set; } = new List<DomainEvidenceItem>();

        /// <summary>
        /// Creates a new domain provenance with the specified confidence and evidence.
        /// </summary>
        /// <param name="confidence">The confidence score.</param>
        /// <param name="evidence">The evidence items.</param>
        /// <returns>A new domain provenance.</returns>
        public static DomainProvenance Create(double confidence, IReadOnlyList<DomainEvidenceItem> evidence)
        {
            return new DomainProvenance
            {
                Confidence = confidence,
                Evidence = evidence ?? new List<DomainEvidenceItem>()
            };
        }
    }

    /// <summary>
    /// Represents a single piece of evidence in the provenance trail.
    /// </summary>
    public class DomainEvidenceItem
    {
        /// <summary>
        /// Gets or sets the token ID.
        /// </summary>
        public int TokenId { get; set; }

        /// <summary>
        /// Gets or sets the token text.
        /// </summary>
        public string TokenText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the probability of this token.
        /// </summary>
        public float Probability { get; set; }

        /// <summary>
        /// Gets or sets the step index where this token was generated.
        /// </summary>
        public int StepIndex { get; set; }

        /// <summary>
        /// Gets or sets optional training source IDs (if available).
        /// </summary>
        public IReadOnlyList<string>? SourceIds { get; set; }

        /// <summary>
        /// Creates a new domain evidence item.
        /// </summary>
        /// <param name="tokenId">The token ID.</param>
        /// <param name="tokenText">The token text.</param>
        /// <param name="probability">The token probability.</param>
        /// <param name="stepIndex">The step index.</param>
        /// <returns>A new evidence item.</returns>
        public static DomainEvidenceItem Create(int tokenId, string tokenText, float probability, int stepIndex)
        {
            return new DomainEvidenceItem
            {
                TokenId = tokenId,
                TokenText = tokenText,
                Probability = probability,
                StepIndex = stepIndex
            };
        }
    }
}

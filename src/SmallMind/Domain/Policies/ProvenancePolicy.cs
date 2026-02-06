using SmallMind.Core.Validation;

namespace SmallMind.Domain.Policies
{
    /// <summary>
    /// Defines provenance and explainability settings for domain-bounded generation.
    /// </summary>
    public class ProvenancePolicy
    {
        /// <summary>
        /// Gets or sets whether provenance tracking is enabled.
        /// Default is false.
        /// </summary>
        public bool EnableProvenance { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of evidence items to include.
        /// Default is 10.
        /// </summary>
        public int MaxEvidenceItems { get; set; } = 10;

        /// <summary>
        /// Gets or sets whether to include training source IDs in provenance.
        /// Note: This feature requires training data to support source tracking.
        /// Default is false.
        /// </summary>
        public bool IncludeTrainingSourceIds { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to track token probabilities at each step.
        /// Default is true when provenance is enabled.
        /// </summary>
        public bool TrackTokenProbabilities { get; set; } = true;

        /// <summary>
        /// Validates the provenance policy configuration.
        /// </summary>
        public void Validate()
        {
            Guard.GreaterThan(MaxEvidenceItems, 0, nameof(MaxEvidenceItems));
        }

        /// <summary>
        /// Creates a default ProvenancePolicy with provenance disabled.
        /// </summary>
        /// <returns>A default provenance policy.</returns>
        public static ProvenancePolicy Default() => new ProvenancePolicy();

        /// <summary>
        /// Creates a ProvenancePolicy with provenance enabled.
        /// </summary>
        /// <param name="maxEvidenceItems">Maximum evidence items to track.</param>
        /// <returns>A provenance policy with tracking enabled.</returns>
        public static ProvenancePolicy Enabled(int maxEvidenceItems = 10)
        {
            Guard.GreaterThan(maxEvidenceItems, 0, nameof(maxEvidenceItems));

            return new ProvenancePolicy
            {
                EnableProvenance = true,
                MaxEvidenceItems = maxEvidenceItems,
                TrackTokenProbabilities = true
            };
        }
    }
}

using SmallMind.Validation;

namespace SmallMind.Domain.Policies
{
    /// <summary>
    /// Defines safety constraints for domain-bounded generation.
    /// </summary>
    public class SafetyPolicy
    {
        /// <summary>
        /// Gets or sets whether to reject requests that appear out of domain.
        /// Default is true.
        /// </summary>
        public bool RejectOutOfDomain { get; set; } = true;

        /// <summary>
        /// Gets or sets the minimum confidence threshold for accepting output.
        /// Output below this threshold will be rejected.
        /// Range: 0.0 to 1.0
        /// Default is 0.0 (no minimum).
        /// </summary>
        public float MinConfidence { get; set; } = 0.0f;

        /// <summary>
        /// Gets or sets whether to disallow unknown tokens.
        /// When true, any unknown/out-of-vocabulary token will cause rejection.
        /// Default is false.
        /// </summary>
        public bool DisallowUnknownTokens { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to require citations when provenance is enabled.
        /// Only applicable when provenance tracking is enabled.
        /// Default is false.
        /// </summary>
        public bool RequireCitations { get; set; } = false;

        /// <summary>
        /// Validates the safety policy configuration.
        /// </summary>
        public void Validate()
        {
            Guard.InRange(MinConfidence, 0.0f, 1.0f, nameof(MinConfidence));
        }

        /// <summary>
        /// Creates a default SafetyPolicy with standard safety settings.
        /// </summary>
        /// <returns>A default safety policy.</returns>
        public static SafetyPolicy Default() => new SafetyPolicy();

        /// <summary>
        /// Creates a SafetyPolicy with strict safety settings.
        /// </summary>
        /// <returns>A strict safety policy.</returns>
        public static SafetyPolicy Strict()
        {
            return new SafetyPolicy
            {
                RejectOutOfDomain = true,
                MinConfidence = 0.5f,
                DisallowUnknownTokens = true,
                RequireCitations = true
            };
        }

        /// <summary>
        /// Creates a SafetyPolicy with permissive safety settings.
        /// </summary>
        /// <returns>A permissive safety policy.</returns>
        public static SafetyPolicy Permissive()
        {
            return new SafetyPolicy
            {
                RejectOutOfDomain = false,
                MinConfidence = 0.0f,
                DisallowUnknownTokens = false,
                RequireCitations = false
            };
        }
    }
}

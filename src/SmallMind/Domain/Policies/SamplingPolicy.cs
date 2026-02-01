using SmallMind.Validation;

namespace SmallMind.Domain.Policies
{
    /// <summary>
    /// Defines sampling behavior for domain-bounded generation.
    /// Controls determinism, temperature, and top-k/top-p parameters.
    /// </summary>
    public class SamplingPolicy
    {
        /// <summary>
        /// Gets or sets whether generation should be deterministic.
        /// When true, the same input always produces the same output.
        /// Default is false.
        /// </summary>
        public bool Deterministic { get; set; } = false;

        /// <summary>
        /// Gets or sets the random seed for deterministic generation.
        /// Only used when Deterministic is true.
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Gets or sets the temperature for sampling.
        /// Higher values (e.g., 1.0) make output more random, lower values (e.g., 0.1) make it more deterministic.
        /// Default is 0.7.
        /// </summary>
        public float Temperature { get; set; } = 0.7f;

        /// <summary>
        /// Gets or sets the top-k value for filtering.
        /// Only the top k most likely tokens are considered.
        /// 0 means no top-k filtering.
        /// Default is 0 (disabled).
        /// </summary>
        public int TopK { get; set; } = 0;

        /// <summary>
        /// Gets or sets the top-p (nucleus) value for filtering.
        /// Only tokens with cumulative probability up to p are considered.
        /// 0 means no top-p filtering.
        /// Default is 0 (disabled).
        /// </summary>
        public float TopP { get; set; } = 0f;

        /// <summary>
        /// Validates the sampling policy configuration.
        /// </summary>
        public void Validate()
        {
            Guard.InRange(Temperature, 0.0f, 2.0f, nameof(Temperature));
            Guard.GreaterThanOrEqualTo(TopK, 0, nameof(TopK));
            Guard.InRange(TopP, 0.0f, 1.0f, nameof(TopP));

            if (Deterministic && !Seed.HasValue)
            {
                // Auto-assign a default seed for deterministic mode
                Seed = 42;
            }
        }

        /// <summary>
        /// Gets the effective temperature for sampling.
        /// In deterministic mode, returns a very low temperature (near 0).
        /// </summary>
        public float GetEffectiveTemperature()
        {
            return Deterministic ? 0.01f : Temperature;
        }

        /// <summary>
        /// Gets the effective top-k value for sampling.
        /// In deterministic mode, returns 1 to force greedy decoding.
        /// </summary>
        public int GetEffectiveTopK()
        {
            return Deterministic ? 1 : TopK;
        }

        /// <summary>
        /// Creates a default SamplingPolicy with balanced randomness.
        /// </summary>
        /// <returns>A default sampling policy.</returns>
        public static SamplingPolicy Default() => new SamplingPolicy();

        /// <summary>
        /// Creates a SamplingPolicy for deterministic generation.
        /// </summary>
        /// <param name="seed">Optional seed for reproducibility.</param>
        /// <returns>A deterministic sampling policy.</returns>
        public static SamplingPolicy CreateDeterministic(int? seed = null)
        {
            return new SamplingPolicy
            {
                Deterministic = true,
                Seed = seed ?? 42
            };
        }

        /// <summary>
        /// Creates a SamplingPolicy for creative generation.
        /// </summary>
        /// <returns>A creative sampling policy with higher temperature.</returns>
        public static SamplingPolicy Creative()
        {
            return new SamplingPolicy
            {
                Temperature = 1.2f,
                TopK = 50
            };
        }
    }
}

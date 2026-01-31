using System;

namespace SmallMind.Configuration
{
    /// <summary>
    /// Configuration options for SmallMind inference operations.
    /// Controls sampling parameters for text generation.
    /// </summary>
    public sealed class InferenceOptions
    {
        /// <summary>
        /// Gets or sets the temperature for sampling (higher = more random).
        /// </summary>
        public double Temperature { get; set; } = 0.8;
        
        /// <summary>
        /// Gets or sets the top-k value for sampling (0 to disable).
        /// </summary>
        public int TopK { get; set; } = 40;
        
        /// <summary>
        /// Gets or sets the top-p (nucleus sampling) value (1.0 to disable).
        /// </summary>
        public double TopP { get; set; } = 0.95;
        
        /// <summary>
        /// Gets or sets the maximum number of tokens to generate.
        /// </summary>
        public int MaxTokens { get; set; } = 100;
        
        /// <summary>
        /// Validates the inference options.
        /// </summary>
        /// <exception cref="Exceptions.ValidationException">Thrown when options are invalid.</exception>
        public void Validate()
        {
            Validation.Guard.GreaterThan(Temperature, 0.0);
            Validation.Guard.GreaterThan(MaxTokens, 0);
        }
    }
}

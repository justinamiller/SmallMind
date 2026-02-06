using System;
using SmallMind.Core.Validation;

namespace SmallMind.Configuration
{
    /// <summary>
    /// Configuration options for SmallMind inference operations.
    /// Controls sampling parameters, resource limits, and governance for production use.
    /// </summary>
    public sealed class InferenceOptions
    {
        // Sampling Parameters
        
        /// <summary>
        /// Gets or sets the temperature for sampling (higher = more random).
        /// Default: 0.8. Valid range: > 0.0
        /// </summary>
        public double Temperature { get; set; } = 0.8;
        
        /// <summary>
        /// Gets or sets the top-k value for sampling (0 to disable).
        /// Default: 40. Set to 0 to disable top-k filtering.
        /// </summary>
        public int TopK { get; set; } = 40;
        
        /// <summary>
        /// Gets or sets the top-p (nucleus sampling) value (1.0 to disable).
        /// Default: 0.95. Valid range: 0.0 to 1.0
        /// </summary>
        public double TopP { get; set; } = 0.95;
        
        /// <summary>
        /// Gets or sets the random seed for deterministic generation.
        /// When set, the same seed with same prompt and options produces identical output.
        /// Default: null (non-deterministic).
        /// </summary>
        public int? Seed { get; set; }
        
        // Resource Governance
        
        /// <summary>
        /// Gets or sets the maximum number of input tokens allowed.
        /// Input exceeding this limit will be rejected or truncated.
        /// Default: 2048. Set to 0 for unlimited (not recommended for production).
        /// </summary>
        public int MaxInputTokens { get; set; } = 2048;
        
        /// <summary>
        /// Gets or sets the maximum total context tokens (input + generated).
        /// Generation stops when this limit is reached.
        /// Default: 4096. Set to 0 for unlimited (not recommended for production).
        /// </summary>
        public int MaxContextTokens { get; set; } = 4096;
        
        /// <summary>
        /// Gets or sets the maximum number of new tokens to generate.
        /// Generation stops after this many tokens are produced.
        /// Default: 100.
        /// </summary>
        public int MaxNewTokens { get; set; } = 100;
        
        /// <summary>
        /// Alias for MaxNewTokens for backward compatibility.
        /// </summary>
        public int MaxTokens
        {
            get => MaxNewTokens;
            set => MaxNewTokens = value;
        }
        
        /// <summary>
        /// Gets or sets the maximum time allowed for generation in milliseconds.
        /// Generation is cancelled if this timeout is exceeded.
        /// Default: 0 (no timeout). For production, set to a reasonable value (e.g., 30000 for 30s).
        /// </summary>
        public int MaxTimeMs { get; set; } = 0;
        
        /// <summary>
        /// Gets or sets the maximum number of concurrent inference sessions allowed.
        /// Default: 0 (unlimited). For production, set based on available resources.
        /// </summary>
        public int MaxConcurrentSessions { get; set; } = 0;
        
        /// <summary>
        /// Gets or sets whether to truncate input exceeding MaxInputTokens.
        /// If false, input exceeding the limit throws ResourceLimitException.
        /// If true, input is truncated to MaxInputTokens (taking the last N tokens).
        /// Default: false (reject oversized inputs).
        /// </summary>
        public bool TruncateInput { get; set; } = false;
        
        /// <summary>
        /// Validates the inference options.
        /// </summary>
        /// <exception cref="Core.Exceptions.ValidationException">Thrown when options are invalid.</exception>
        public void Validate()
        {
            Guard.GreaterThan(Temperature, 0.0);
            Guard.GreaterThan(MaxNewTokens, 0);
            Guard.GreaterThanOrEqualTo(MaxInputTokens, 0);
            Guard.GreaterThanOrEqualTo(MaxContextTokens, 0);
            Guard.GreaterThanOrEqualTo(MaxTimeMs, 0);
            Guard.GreaterThanOrEqualTo(MaxConcurrentSessions, 0);
            Guard.InRange(TopP, 0.0, 1.0);
            
            // Validate that context limit is reasonable
            if (MaxContextTokens > 0 && MaxInputTokens > MaxContextTokens)
            {
                throw new Core.Exceptions.ValidationException(
                    $"MaxInputTokens ({MaxInputTokens}) cannot exceed MaxContextTokens ({MaxContextTokens})",
                    nameof(MaxInputTokens));
            }
        }
    }
}

using System;
using SmallMind.Core.Exceptions;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Production-grade inference options with resource governance and safety controls.
    /// Use this for commercial deployments requiring bounded resources and predictable behavior.
    /// </summary>
    public sealed class ProductionInferenceOptions
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
        /// Input exceeding this limit will be rejected or truncated based on TruncateInput.
        /// Default: 2048. Set to 0 for unlimited (not recommended for production).
        /// </summary>
        public int MaxInputTokens { get; set; } = 2048;
        
        /// <summary>
        /// Gets or sets the maximum total context tokens (input + generated).
        /// This bounds KV cache size. Generation stops when this limit is reached.
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
        /// Gets or sets the maximum time allowed for generation in milliseconds.
        /// Generation is cancelled if this timeout is exceeded.
        /// Default: 0 (no timeout). For production, set to a reasonable value (e.g., 30000 for 30s).
        /// </summary>
        public int MaxTimeMs { get; set; } = 0;
        
        /// <summary>
        /// Gets or sets whether to truncate input exceeding MaxInputTokens.
        /// If false, input exceeding the limit throws ResourceLimitException.
        /// If true, input is truncated to MaxInputTokens (taking the last N tokens).
        /// Default: false (reject oversized inputs).
        /// </summary>
        public bool TruncateInput { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether to include log probabilities in streaming output.
        /// Enabling this adds computational overhead.
        /// Default: false.
        /// </summary>
        public bool IncludeLogProbs { get; set; } = false;
        
        /// <summary>
        /// Validates the inference options and throws if invalid.
        /// </summary>
        /// <exception cref="ValidationException">Thrown when options are invalid.</exception>
        public void Validate()
        {
            if (Temperature <= 0.0)
            {
                throw new ValidationException("Temperature must be greater than 0", nameof(Temperature));
            }
            
            if (MaxNewTokens <= 0)
            {
                throw new ValidationException("MaxNewTokens must be greater than 0", nameof(MaxNewTokens));
            }
            
            if (MaxInputTokens < 0)
            {
                throw new ValidationException("MaxInputTokens cannot be negative", nameof(MaxInputTokens));
            }
            
            if (MaxContextTokens < 0)
            {
                throw new ValidationException("MaxContextTokens cannot be negative", nameof(MaxContextTokens));
            }
            
            if (MaxTimeMs < 0)
            {
                throw new ValidationException("MaxTimeMs cannot be negative", nameof(MaxTimeMs));
            }
            
            if (TopP < 0.0 || TopP > 1.0)
            {
                throw new ValidationException("TopP must be between 0.0 and 1.0", nameof(TopP));
            }
            
            // Validate that context limit is reasonable
            if (MaxContextTokens > 0 && MaxInputTokens > MaxContextTokens)
            {
                throw new ValidationException(
                    $"MaxInputTokens ({MaxInputTokens}) cannot exceed MaxContextTokens ({MaxContextTokens})",
                    nameof(MaxInputTokens));
            }
        }
        
        /// <summary>
        /// Creates a copy of these options.
        /// </summary>
        public ProductionInferenceOptions Clone()
        {
            return new ProductionInferenceOptions
            {
                Temperature = Temperature,
                TopK = TopK,
                TopP = TopP,
                Seed = Seed,
                MaxInputTokens = MaxInputTokens,
                MaxContextTokens = MaxContextTokens,
                MaxNewTokens = MaxNewTokens,
                MaxTimeMs = MaxTimeMs,
                TruncateInput = TruncateInput,
                IncludeLogProbs = IncludeLogProbs
            };
        }
    }
}

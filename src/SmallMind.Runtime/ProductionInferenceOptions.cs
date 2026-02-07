using System;
using SmallMind.Core.Exceptions;
using SmallMind.Runtime.Scheduling;

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
        /// Gets or sets the min-p sampling threshold (0.0 to disable).
        /// Filters tokens with probability less than MinP * max_probability.
        /// Default: 0.0 (disabled). Valid range: 0.0 to 1.0
        /// </summary>
        public double MinP { get; set; } = 0.0;
        
        /// <summary>
        /// Gets or sets additional stop token IDs that end generation.
        /// Generation stops if any of these token IDs are produced.
        /// Default: empty array.
        /// </summary>
        public int[] StopTokenIds { get; set; } = Array.Empty<int>();
        
        /// <summary>
        /// Gets or sets stop sequences (text patterns) that end generation.
        /// Generation stops when any of these sequences appear in output.
        /// Default: empty array.
        /// </summary>
        public string[] StopSequences { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Gets or sets whether to remove stop sequences from final output.
        /// Default: true (stop sequences are removed).
        /// </summary>
        public bool RemoveStopSequenceFromOutput { get; set; } = true;
        
        /// <summary>
        /// Gets or sets the repetition penalty (1.0 to disable).
        /// Penalizes tokens that have already appeared in recent context.
        /// Default: 1.0 (disabled). Typical values: 1.05-1.2
        /// Valid range: greater than 0, not equal to 0.
        /// </summary>
        public float RepetitionPenalty { get; set; } = 1.0f;
        
        /// <summary>
        /// Gets or sets the presence penalty (0.0 to disable).
        /// Subtracts a fixed penalty from logits of tokens present in recent context.
        /// Default: 0.0 (disabled). Typical values: 0.0-1.0
        /// </summary>
        public float PresencePenalty { get; set; } = 0.0f;
        
        /// <summary>
        /// Gets or sets the frequency penalty (0.0 to disable).
        /// Penalizes tokens proportionally to their frequency in recent context.
        /// Default: 0.0 (disabled). Typical values: 0.0-1.0
        /// </summary>
        public float FrequencyPenalty { get; set; } = 0.0f;
        
        /// <summary>
        /// Gets or sets the size of the context window for repetition penalties (0 = full context).
        /// Only tokens within the last N tokens are considered for penalties.
        /// Default: 0 (use full context, limited to reasonable default internally).
        /// </summary>
        public int RepetitionWindow { get; set; } = 0;
        
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
        /// NOTE: Log probability calculation not yet implemented. This option is reserved for future use.
        /// </summary>
        public bool IncludeLogProbs { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to enable deterministic token scheduling.
        /// When true, token generation order is tracked and made reproducible.
        /// Default: false.
        /// </summary>
        public bool EnableScheduleTracking { get; set; } = false;

        /// <summary>
        /// Gets or sets the scheduling policy when schedule tracking is enabled.
        /// Default: FIFO (First-In-First-Out).
        /// </summary>
        public SchedulingPolicy SchedulingPolicy { get; set; } = SchedulingPolicy.FIFO;
        
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
            
            if (MinP < 0.0 || MinP > 1.0)
            {
                throw new ValidationException("MinP must be between 0.0 and 1.0", nameof(MinP));
            }
            
            if (RepetitionPenalty <= 0.0f)
            {
                throw new ValidationException("RepetitionPenalty must be greater than 0", nameof(RepetitionPenalty));
            }
            
            if (RepetitionWindow < 0)
            {
                throw new ValidationException("RepetitionWindow cannot be negative", nameof(RepetitionWindow));
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
                MinP = MinP,
                StopTokenIds = StopTokenIds.Length > 0 ? (int[])StopTokenIds.Clone() : Array.Empty<int>(),
                StopSequences = StopSequences.Length > 0 ? (string[])StopSequences.Clone() : Array.Empty<string>(),
                RemoveStopSequenceFromOutput = RemoveStopSequenceFromOutput,
                RepetitionPenalty = RepetitionPenalty,
                PresencePenalty = PresencePenalty,
                FrequencyPenalty = FrequencyPenalty,
                RepetitionWindow = RepetitionWindow,
                Seed = Seed,
                MaxInputTokens = MaxInputTokens,
                MaxContextTokens = MaxContextTokens,
                MaxNewTokens = MaxNewTokens,
                MaxTimeMs = MaxTimeMs,
                TruncateInput = TruncateInput,
                IncludeLogProbs = IncludeLogProbs,
                EnableScheduleTracking = EnableScheduleTracking,
                SchedulingPolicy = SchedulingPolicy
            };
        }
    }
}

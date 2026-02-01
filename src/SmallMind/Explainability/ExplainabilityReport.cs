using System;
using System.Collections.Generic;

namespace SmallMind.Explainability
{
    /// <summary>
    /// Contains the complete explainability report for a text generation request.
    /// </summary>
    public class ExplainabilityReport
    {
        /// <summary>
        /// Gets an optional request identifier for correlation.
        /// </summary>
        public string? RequestId { get; }

        /// <summary>
        /// Gets the level of explainability that was captured.
        /// </summary>
        public ExplainabilityLevel Level { get; }

        /// <summary>
        /// Gets the total duration of the generation request (optional).
        /// </summary>
        public TimeSpan? TotalDuration { get; }

        /// <summary>
        /// Gets the number of tokens in the prompt.
        /// </summary>
        public int PromptTokens { get; }

        /// <summary>
        /// Gets the number of tokens generated.
        /// </summary>
        public int GeneratedTokens { get; }

        /// <summary>
        /// Gets the average of the maximum token probability across all steps.
        /// Higher values indicate higher overall confidence.
        /// </summary>
        public double AvgMaxTokenProb { get; }

        /// <summary>
        /// Gets the minimum of the maximum token probabilities across all steps.
        /// Low values may indicate uncertain generation steps.
        /// </summary>
        public double MinMaxTokenProb { get; }

        /// <summary>
        /// Gets an estimate of perplexity based on mean negative log probability (optional).
        /// Lower perplexity suggests more confident generation.
        /// </summary>
        public double? PerplexityEstimate { get; }

        /// <summary>
        /// Gets the per-token generation steps with explainability data.
        /// </summary>
        public IReadOnlyList<TokenStepExplanation> Steps { get; }

        /// <summary>
        /// Gets the input attribution data (saliency) if captured.
        /// </summary>
        public IReadOnlyList<InputAttribution>? InputAttributions { get; }

        /// <summary>
        /// Gets any warnings or issues encountered during capture.
        /// </summary>
        public IReadOnlyList<ExplainabilityWarning> Warnings { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplainabilityReport"/> class.
        /// </summary>
        public ExplainabilityReport(
            ExplainabilityLevel level,
            int promptTokens,
            int generatedTokens,
            double avgMaxTokenProb,
            double minMaxTokenProb,
            IReadOnlyList<TokenStepExplanation> steps,
            IReadOnlyList<ExplainabilityWarning> warnings,
            string? requestId = null,
            TimeSpan? totalDuration = null,
            double? perplexityEstimate = null,
            IReadOnlyList<InputAttribution>? inputAttributions = null)
        {
            RequestId = requestId;
            Level = level;
            TotalDuration = totalDuration;
            PromptTokens = promptTokens;
            GeneratedTokens = generatedTokens;
            AvgMaxTokenProb = avgMaxTokenProb;
            MinMaxTokenProb = minMaxTokenProb;
            PerplexityEstimate = perplexityEstimate;
            Steps = steps;
            InputAttributions = inputAttributions;
            Warnings = warnings;
        }
    }
}

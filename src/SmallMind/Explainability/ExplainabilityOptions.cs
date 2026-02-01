using System;

namespace SmallMind.Explainability
{
    /// <summary>
    /// Configuration options for explainability data capture.
    /// </summary>
    public class ExplainabilityOptions
    {
        /// <summary>
        /// Gets or sets the level of explainability to capture.
        /// Default is None (no explainability).
        /// </summary>
        public ExplainabilityLevel Level { get; set; } = ExplainabilityLevel.None;

        /// <summary>
        /// Gets or sets the number of alternative tokens to capture per step.
        /// Default is 5, maximum is 50.
        /// </summary>
        public int TopKAlternatives { get; set; } = 5;

        /// <summary>
        /// Gets or sets the maximum number of generation steps to capture.
        /// Default is 256.
        /// </summary>
        public int MaxCapturedSteps { get; set; } = 256;

        /// <summary>
        /// Gets or sets whether to capture raw logits for each step.
        /// Default is false (saves memory).
        /// </summary>
        public bool CaptureLogits { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to capture attention weights.
        /// Default is false (not yet implemented).
        /// </summary>
        public bool CaptureAttention { get; set; } = false;

        /// <summary>
        /// Gets or sets whether to capture input saliency via ablation.
        /// Default is true for Standard and Detailed levels.
        /// </summary>
        public bool CaptureInputSaliency { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of recent tokens to consider for saliency analysis.
        /// Default is 64.
        /// </summary>
        public int SaliencyWindowTokens { get; set; } = 64;

        /// <summary>
        /// Gets or sets whether to include prompt tokens in the report.
        /// Default is true.
        /// </summary>
        public bool IncludePromptTokens { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to include timing information per token.
        /// Default is true.
        /// </summary>
        public bool IncludeTiming { get; set; } = true;

        /// <summary>
        /// Gets or sets whether to redact the prompt text from the output.
        /// Default is false.
        /// </summary>
        public bool RedactPromptText { get; set; } = false;

        /// <summary>
        /// Gets or sets an optional redaction function to sanitize token text.
        /// Applied to all token strings if provided.
        /// </summary>
        public Func<string, string>? Redactor { get; set; } = null;

        /// <summary>
        /// Validates and normalizes the options.
        /// </summary>
        internal void Validate()
        {
            if (TopKAlternatives < 0)
                TopKAlternatives = 0;
            if (TopKAlternatives > 50)
                TopKAlternatives = 50;

            if (MaxCapturedSteps < 0)
                MaxCapturedSteps = 0;

            if (SaliencyWindowTokens < 0)
                SaliencyWindowTokens = 0;
        }
    }
}

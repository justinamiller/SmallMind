using System.Collections.Generic;

namespace SmallMind.Explainability
{
    /// <summary>
    /// Context provided at the start of a generation session.
    /// </summary>
    public class ExplainabilityContext
    {
        /// <summary>
        /// Gets the prompt tokens (token IDs).
        /// </summary>
        public IReadOnlyList<int> PromptTokens { get; }

        /// <summary>
        /// Gets the prompt text (may be redacted if requested).
        /// </summary>
        public string? PromptText { get; }

        /// <summary>
        /// Gets the explainability options for this session.
        /// </summary>
        public ExplainabilityOptions Options { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplainabilityContext"/> class.
        /// </summary>
        public ExplainabilityContext(IReadOnlyList<int> promptTokens, string? promptText, ExplainabilityOptions options)
        {
            PromptTokens = promptTokens;
            PromptText = promptText;
            Options = options;
        }
    }
}

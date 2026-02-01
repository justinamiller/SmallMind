using System;
using System.Collections.Generic;

namespace SmallMind.Explainability
{
    /// <summary>
    /// Default implementation of <see cref="IExplainabilitySink"/> that collects
    /// explainability data and produces an <see cref="ExplainabilityReport"/>.
    /// Uses pooled buffers and preallocated collections to minimize allocations.
    /// </summary>
    public class ExplainabilityCollector : IExplainabilitySink
    {
        private readonly ExplainabilityOptions _options;
        private readonly List<TokenStepExplanation> _steps;
        private readonly List<ExplainabilityWarning> _warnings;
        
        private ExplainabilityContext? _context;
        private ExplainabilitySummary? _summary;
        private int _generatedTokenCount;

        /// <summary>
        /// Gets a value indicating whether this collector is enabled.
        /// </summary>
        public bool IsEnabled => _options.Level != ExplainabilityLevel.None;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExplainabilityCollector"/> class.
        /// </summary>
        /// <param name="options">The explainability options.</param>
        public ExplainabilityCollector(ExplainabilityOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();

            // Preallocate collections to reduce allocations during generation
            int capacity = Math.Min(_options.MaxCapturedSteps, 256);
            _steps = new List<TokenStepExplanation>(capacity);
            _warnings = new List<ExplainabilityWarning>();
        }

        /// <summary>
        /// Called when generation starts.
        /// </summary>
        public void OnGenerationStart(ExplainabilityContext ctx)
        {
            _context = ctx ?? throw new ArgumentNullException(nameof(ctx));
            _generatedTokenCount = 0;
        }

        /// <summary>
        /// Called for each token generation step.
        /// </summary>
        public void OnTokenStep(TokenStepData step)
        {
            if (step == null)
                return;

            // Respect max captured steps
            if (_steps.Count >= _options.MaxCapturedSteps)
            {
                // Only warn once
                if (_steps.Count == _options.MaxCapturedSteps)
                {
                    _warnings.Add(new ExplainabilityWarning(
                        "MAX_STEPS_EXCEEDED",
                        $"Maximum captured steps ({_options.MaxCapturedSteps}) exceeded. Subsequent steps not recorded."));
                }
                return;
            }

            // Apply redactor if configured
            string tokenText = step.SelectedTokenText;
            if (_options.Redactor != null)
            {
                try
                {
                    tokenText = _options.Redactor(tokenText);
                }
                catch
                {
                    // Redactor failure should not break generation
                    tokenText = "[REDACTED]";
                }
            }

            // Build alternatives list
            var alternatives = new List<TokenAlternative>(step.AlternativeTokenIds.Length);
            for (int i = 0; i < step.AlternativeTokenIds.Length; i++)
            {
                string altText = step.AlternativeTokenTexts[i];
                if (_options.Redactor != null)
                {
                    try
                    {
                        altText = _options.Redactor(altText);
                    }
                    catch
                    {
                        altText = "[REDACTED]";
                    }
                }

                alternatives.Add(new TokenAlternative(
                    step.AlternativeTokenIds[i],
                    altText,
                    step.AlternativeProbs[i]));
            }

            // Create step explanation
            var explanation = new TokenStepExplanation(
                step.StepIndex,
                step.SelectedTokenId,
                tokenText,
                step.SelectedTokenProb,
                alternatives,
                step.Entropy,
                _options.IncludeTiming ? step.Elapsed : null);

            _steps.Add(explanation);
            _generatedTokenCount++;
        }

        /// <summary>
        /// Called when generation completes.
        /// </summary>
        public void OnGenerationEnd(ExplainabilitySummary summary)
        {
            _summary = summary ?? throw new ArgumentNullException(nameof(summary));
        }

        /// <summary>
        /// Builds and returns the final explainability report.
        /// </summary>
        /// <param name="requestId">Optional request identifier.</param>
        /// <returns>The explainability report.</returns>
        public ExplainabilityReport GetReport(string? requestId = null)
        {
            if (_context == null || _summary == null)
            {
                throw new InvalidOperationException("Cannot get report before generation completes.");
            }

            // Compute confidence metrics
            double avgMaxTokenProb = 0.0;
            double minMaxTokenProb = 1.0;
            double sumNegLogProb = 0.0;

            foreach (var step in _steps)
            {
                avgMaxTokenProb += step.TokenProb;
                if (step.TokenProb < minMaxTokenProb)
                    minMaxTokenProb = step.TokenProb;
                
                if (step.TokenProb > 0)
                    sumNegLogProb += -Math.Log(step.TokenProb);
            }

            if (_steps.Count > 0)
            {
                avgMaxTokenProb /= _steps.Count;
            }

            // Compute perplexity estimate
            double? perplexity = null;
            if (_steps.Count > 0 && _options.Level >= ExplainabilityLevel.Standard)
            {
                double meanNegLogProb = sumNegLogProb / _steps.Count;
                perplexity = Math.Exp(meanNegLogProb);
            }

            // Add warning for low confidence
            if (minMaxTokenProb < 0.15 && _steps.Count > 0)
            {
                _warnings.Add(new ExplainabilityWarning(
                    "LOW_CONFIDENCE",
                    $"Low confidence during generation (min prob = {minMaxTokenProb:F4})"));
            }

            return new ExplainabilityReport(
                level: _options.Level,
                promptTokens: _context.PromptTokens.Count,
                generatedTokens: _generatedTokenCount,
                avgMaxTokenProb: avgMaxTokenProb,
                minMaxTokenProb: minMaxTokenProb,
                steps: _steps,
                warnings: _warnings,
                requestId: requestId,
                totalDuration: _summary.TotalDuration,
                perplexityEstimate: perplexity,
                inputAttributions: null  // TODO: implement saliency
            );
        }
    }
}

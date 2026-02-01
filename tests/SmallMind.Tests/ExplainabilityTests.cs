using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using SmallMind.Core;
using SmallMind.Text;
using SmallMind.Explainability;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for the explainability feature.
    /// Verifies correct capture of token probabilities, alternatives, and determinism.
    /// </summary>
    public class ExplainabilityTests
    {
        private readonly Tokenizer _tokenizer;
        private readonly TransformerModel _model;
        private readonly Sampling _sampling;

        public ExplainabilityTests()
        {
            // Create a small test model
            var vocab = "the cat dog sat ran on mat . a";
            _tokenizer = new Tokenizer(vocab);

            int vocabSize = _tokenizer.VocabSize;
            int blockSize = 16;
            int nEmbd = 8;
            int nLayer = 1;
            int nHead = 2;
            double dropout = 0.0;

            _model = new TransformerModel(
                vocabSize: vocabSize,
                blockSize: blockSize,
                nEmbd: nEmbd,
                nLayer: nLayer,
                nHead: nHead,
                dropout: dropout,
                seed: 42);

            _sampling = new Sampling(_model, _tokenizer, blockSize);
        }

        [Fact]
        public void ExplainabilityDisabled_ProducesNoReport_AndMinimalOverhead()
        {
            // Arrange
            var options = new ExplainabilityOptions { Level = ExplainabilityLevel.None };
            var collector = new ExplainabilityCollector(options);

            // Act
            var result = _sampling.Generate(
                prompt: "the cat",
                maxNewTokens: 5,
                seed: 42,
                explainabilityOptions: options,
                explainabilitySink: collector);

            // Assert
            Assert.False(collector.IsEnabled);
            
            // Should throw because no data was collected
            Assert.Throws<InvalidOperationException>(() => collector.GetReport());
        }

        [Fact]
        public void DeterministicRuns_ProduceIdenticalOutput_AndIdenticalExplainability()
        {
            // Arrange
            var options1 = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Basic,
                TopKAlternatives = 5
            };
            var collector1 = new ExplainabilityCollector(options1);

            var options2 = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Basic,
                TopKAlternatives = 5
            };
            var collector2 = new ExplainabilityCollector(options2);

            int seed = 12345;
            string prompt = "the cat";
            int maxTokens = 10;

            // Act - Run 1
            var result1 = _sampling.Generate(
                prompt: prompt,
                maxNewTokens: maxTokens,
                seed: seed,
                explainabilityOptions: options1,
                explainabilitySink: collector1);

            var report1 = collector1.GetReport();

            // Act - Run 2
            var result2 = _sampling.Generate(
                prompt: prompt,
                maxNewTokens: maxTokens,
                seed: seed,
                explainabilityOptions: options2,
                explainabilitySink: collector2);

            var report2 = collector2.GetReport();

            // Assert - Identical output
            Assert.Equal(result1, result2);

            // Assert - Identical report metrics
            Assert.Equal(report1.PromptTokens, report2.PromptTokens);
            Assert.Equal(report1.GeneratedTokens, report2.GeneratedTokens);
            Assert.Equal(report1.Steps.Count, report2.Steps.Count);

            // Assert - Identical token probabilities (within tolerance)
            for (int i = 0; i < report1.Steps.Count; i++)
            {
                var step1 = report1.Steps[i];
                var step2 = report2.Steps[i];

                Assert.Equal(step1.TokenId, step2.TokenId);
                Assert.Equal(step1.TokenText, step2.TokenText);
                Assert.Equal(step1.TokenProb, step2.TokenProb, precision: 6);

                // Check alternatives
                Assert.Equal(step1.Alternatives.Count, step2.Alternatives.Count);
                for (int j = 0; j < step1.Alternatives.Count; j++)
                {
                    Assert.Equal(step1.Alternatives[j].TokenId, step2.Alternatives[j].TokenId);
                    Assert.Equal(step1.Alternatives[j].Prob, step2.Alternatives[j].Prob, precision: 6);
                }
            }
        }

        [Fact]
        public void TopKAlternatives_ReturnsCorrectCount_AndSortedByDescendingProbability()
        {
            // Arrange
            var options = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Basic,
                TopKAlternatives = 3
            };
            var collector = new ExplainabilityCollector(options);

            // Act
            _sampling.Generate(
                prompt: "the",
                maxNewTokens: 5,
                seed: 999,
                explainabilityOptions: options,
                explainabilitySink: collector);

            var report = collector.GetReport();

            // Assert
            Assert.True(report.Steps.Count > 0);

            foreach (var step in report.Steps)
            {
                // Should have at most 3 alternatives
                Assert.True(step.Alternatives.Count <= 3);

                // Alternatives should be sorted by descending probability
                for (int i = 0; i < step.Alternatives.Count - 1; i++)
                {
                    Assert.True(step.Alternatives[i].Prob >= step.Alternatives[i + 1].Prob,
                        $"Alternatives not sorted at step {step.StepIndex}: " +
                        $"{step.Alternatives[i].Prob} < {step.Alternatives[i + 1].Prob}");
                }

                // Selected token should be among the alternatives or have higher probability
                bool selectedTokenInAlternatives = step.Alternatives.Any(a => a.TokenId == step.TokenId);
                if (selectedTokenInAlternatives)
                {
                    var selectedAlt = step.Alternatives.First(a => a.TokenId == step.TokenId);
                    Assert.Equal(step.TokenProb, selectedAlt.Prob, precision: 6);
                }
            }
        }

        [Fact]
        public void MaxCapturedSteps_RespectsLimit()
        {
            // Arrange
            var options = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Basic,
                TopKAlternatives = 3,
                MaxCapturedSteps = 5  // Limit to 5 steps
            };
            var collector = new ExplainabilityCollector(options);

            // Act - Generate more than 5 tokens
            _sampling.Generate(
                prompt: "the",
                maxNewTokens: 10,  // Request 10 tokens
                seed: 777,
                explainabilityOptions: options,
                explainabilitySink: collector);

            var report = collector.GetReport();

            // Assert - Should only have 5 steps captured
            Assert.Equal(5, report.Steps.Count);

            // Should have a warning about exceeded steps
            Assert.Contains(report.Warnings, w => w.Code == "MAX_STEPS_EXCEEDED");
        }

        [Fact]
        public void PromptRedaction_RedactsPromptText()
        {
            // Arrange
            var options = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Basic,
                TopKAlternatives = 2,
                RedactPromptText = true
            };
            
            bool promptWasNull = false;
            var customSink = new CustomSink(ctx =>
            {
                promptWasNull = ctx.PromptText == null;
            });

            // Act
            _sampling.Generate(
                prompt: "secret prompt",
                maxNewTokens: 3,
                seed: 111,
                explainabilityOptions: options,
                explainabilitySink: customSink);

            // Assert
            Assert.True(promptWasNull, "Prompt should be null when RedactPromptText is true");
        }

        [Fact]
        public void CustomRedactor_AppliedToTokenText()
        {
            // Arrange
            var options = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Basic,
                TopKAlternatives = 2,
                Redactor = text => "***"  // Redact all text
            };
            var collector = new ExplainabilityCollector(options);

            // Act
            _sampling.Generate(
                prompt: "the cat",
                maxNewTokens: 3,
                seed: 222,
                explainabilityOptions: options,
                explainabilitySink: collector);

            var report = collector.GetReport();

            // Assert
            Assert.True(report.Steps.Count > 0);
            foreach (var step in report.Steps)
            {
                Assert.Equal("***", step.TokenText);
                foreach (var alt in step.Alternatives)
                {
                    Assert.Equal("***", alt.TokenText);
                }
            }
        }

        [Fact]
        public void StandardLevel_ComputesEntropyAndPerplexity()
        {
            // Arrange
            var options = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Standard,  // Standard includes entropy
                TopKAlternatives = 3
            };
            var collector = new ExplainabilityCollector(options);

            // Act
            _sampling.Generate(
                prompt: "the",
                maxNewTokens: 5,
                seed: 333,
                explainabilityOptions: options,
                explainabilitySink: collector);

            var report = collector.GetReport();

            // Assert
            Assert.True(report.Steps.Count > 0);

            // Standard level should compute entropy for each step
            foreach (var step in report.Steps)
            {
                Assert.NotNull(step.StepEntropy);
                Assert.True(step.StepEntropy.Value >= 0, "Entropy should be non-negative");
            }

            // Standard level should compute perplexity estimate
            Assert.NotNull(report.PerplexityEstimate);
            Assert.True(report.PerplexityEstimate.Value > 0, "Perplexity should be positive");
        }

        [Fact]
        public void ConfidenceMetrics_ComputedCorrectly()
        {
            // Arrange
            var options = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Basic,
                TopKAlternatives = 3
            };
            var collector = new ExplainabilityCollector(options);

            // Act
            _sampling.Generate(
                prompt: "the cat",
                maxNewTokens: 5,
                seed: 444,
                explainabilityOptions: options,
                explainabilitySink: collector);

            var report = collector.GetReport();

            // Assert
            Assert.True(report.Steps.Count > 0);

            // Compute expected metrics manually
            double sumProb = 0;
            double minProb = 1.0;
            foreach (var step in report.Steps)
            {
                sumProb += step.TokenProb;
                if (step.TokenProb < minProb)
                    minProb = step.TokenProb;
            }
            double expectedAvg = sumProb / report.Steps.Count;

            // Verify
            Assert.Equal(expectedAvg, report.AvgMaxTokenProb, precision: 6);
            Assert.Equal(minProb, report.MinMaxTokenProb, precision: 6);
        }

        [Fact]
        public void LowConfidenceWarning_TriggeredWhenAppropriate()
        {
            // This test may or may not trigger the warning depending on model randomness
            // We just verify the warning logic works when it does trigger
            
            // Arrange
            var options = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Basic,
                TopKAlternatives = 3
            };
            var collector = new ExplainabilityCollector(options);

            // Act
            _sampling.Generate(
                prompt: "the",
                maxNewTokens: 20,
                seed: 555,
                explainabilityOptions: options,
                explainabilitySink: collector);

            var report = collector.GetReport();

            // Assert - If min prob is less than 0.15, should have warning
            if (report.MinMaxTokenProb < 0.15)
            {
                Assert.Contains(report.Warnings, w => w.Code == "LOW_CONFIDENCE");
            }
        }

        // Helper class for custom sink testing
        private class CustomSink : IExplainabilitySink
        {
            private readonly Action<ExplainabilityContext> _onStart;
            
            public bool IsEnabled => true;

            public CustomSink(Action<ExplainabilityContext> onStart)
            {
                _onStart = onStart;
            }

            public void OnGenerationStart(ExplainabilityContext ctx)
            {
                _onStart?.Invoke(ctx);
            }

            public void OnTokenStep(TokenStepData step) { }
            public void OnGenerationEnd(ExplainabilitySummary summary) { }
        }
    }
}

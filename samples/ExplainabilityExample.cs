using System;
using SmallMind.Core;
using SmallMind.Text;
using SmallMind.Explainability;

namespace SmallMind.Samples
{
    /// <summary>
    /// Demonstrates the explainability feature for understanding token generation decisions.
    /// Shows how to capture confidence metrics, alternative tokens, and perplexity estimates.
    /// </summary>
    public static class ExplainabilityExample
    {
        public static void Run()
        {
            Console.WriteLine("=== Explainability Example ===\n");
            Console.WriteLine("This example demonstrates how to capture explainability data during text generation.");
            Console.WriteLine("It shows token probabilities, alternative tokens, and confidence metrics.\n");

            // Create a small vocabulary for demonstration
            var vocab = "the cat dog sat ran on mat . a";
            var tokenizer = new Tokenizer(vocab);

            // Create a tiny model for demonstration
            int vocabSize = tokenizer.VocabSize;
            int blockSize = 16;
            int nEmbd = 8;
            int nHead = 2;
            int nLayer = 2;
            double dropout = 0.0;

            var model = new TransformerModel(
                vocabSize: vocabSize,
                blockSize: blockSize,
                nEmbd: nEmbd,
                nLayer: nLayer,
                nHead: nHead,
                dropout: dropout,
                seed: 42);

            var sampling = new Sampling(model, tokenizer, blockSize);

            // Configure explainability options
            var explainOptions = new ExplainabilityOptions
            {
                Level = ExplainabilityLevel.Standard,  // Capture detailed metrics
                TopKAlternatives = 5,                   // Show top 5 alternatives per token
                MaxCapturedSteps = 20,                  // Capture up to 20 tokens
                IncludeTiming = true,                   // Include per-token timing
                CaptureInputSaliency = false,           // Skip saliency for this demo (not yet implemented)
                RedactPromptText = false                // Don't redact the prompt
            };

            // Create the collector to receive explainability data
            var collector = new ExplainabilityCollector(explainOptions);

            // Generate text with explainability
            Console.WriteLine("Generating text with explainability enabled...\n");
            
            string prompt = "the cat";
            int maxTokens = 10;
            
            string generated = sampling.Generate(
                prompt: prompt,
                maxNewTokens: maxTokens,
                temperature: 1.0,
                topK: 0,
                seed: 42,  // Deterministic for reproducibility
                showPerf: false,
                isPerfJsonMode: false,
                metrics: null,
                explainabilityOptions: explainOptions,
                explainabilitySink: collector);

            Console.WriteLine($"\nGenerated text: \"{generated}\"");
            Console.WriteLine(new string('=', 60));

            // Get the explainability report
            var report = collector.GetReport(requestId: "demo-001");

            // Display summary metrics
            Console.WriteLine("\n### Confidence Summary ###");
            Console.WriteLine($"Prompt tokens: {report.PromptTokens}");
            Console.WriteLine($"Generated tokens: {report.GeneratedTokens}");
            Console.WriteLine($"Average max token probability: {report.AvgMaxTokenProb:F4}");
            Console.WriteLine($"Minimum max token probability: {report.MinMaxTokenProb:F4}");
            
            if (report.PerplexityEstimate.HasValue)
            {
                Console.WriteLine($"Perplexity estimate: {report.PerplexityEstimate.Value:F2}");
            }
            
            if (report.TotalDuration.HasValue)
            {
                Console.WriteLine($"Total duration: {report.TotalDuration.Value.TotalMilliseconds:F2}ms");
            }

            // Display warnings
            if (report.Warnings.Count > 0)
            {
                Console.WriteLine("\n### Warnings ###");
                foreach (var warning in report.Warnings)
                {
                    Console.WriteLine($"  {warning}");
                }
            }

            // Display first 10 steps with alternatives
            Console.WriteLine("\n### Token Generation Steps (first 10) ###");
            int displayCount = Math.Min(10, report.Steps.Count);
            
            for (int i = 0; i < displayCount; i++)
            {
                var step = report.Steps[i];
                Console.WriteLine($"\nStep {step.StepIndex}:");
                Console.WriteLine($"  Selected: \"{step.TokenText}\" (ID: {step.TokenId}, prob: {step.TokenProb:F4})");
                
                if (step.StepEntropy.HasValue)
                {
                    Console.WriteLine($"  Entropy: {step.StepEntropy.Value:F2} bits");
                }
                
                if (step.Elapsed.HasValue)
                {
                    Console.WriteLine($"  Time: {step.Elapsed.Value.TotalMilliseconds:F2}ms");
                }
                
                if (step.Alternatives.Count > 0)
                {
                    Console.WriteLine("  Alternatives:");
                    foreach (var alt in step.Alternatives)
                    {
                        Console.WriteLine($"    - \"{alt.TokenText}\" (ID: {alt.TokenId}, prob: {alt.Prob:F4})");
                    }
                }
            }

            Console.WriteLine("\n" + new string('=', 60));
            Console.WriteLine("Example complete!\n");
        }
    }
}

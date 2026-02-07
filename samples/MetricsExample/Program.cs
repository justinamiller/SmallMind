using System;
using SmallMind.Transformers;
using SmallMind.Runtime;
using SmallMind.Tokenizers;

namespace SmallMind.Examples
{
    /// <summary>
    /// Demonstrates the new training metrics system for monitoring and improving prediction quality.
    /// Shows how to track perplexity, token accuracy, and gradient health during training.
    /// </summary>
    class MetricsExample
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Training Metrics Example ===\n");
            Console.WriteLine("This example demonstrates the comprehensive metrics tracking system.");
            Console.WriteLine("Metrics help you understand prediction quality and training progress.\n");

            // Sample training data - Shakespeare-style text
            string trainingText = @"
To be, or not to be, that is the question:
Whether 'tis nobler in the mind to suffer
The slings and arrows of outrageous fortune,
Or to take arms against a sea of troubles
And by opposing end them. To die—to sleep,
No more; and by a sleep to say we end
The heart-ache and the thousand natural shocks
That flesh is heir to: 'tis a consummation
Devoutly to be wish'd. To die, to sleep;
To sleep, perchance to dream—ay, there's the rub:
For in that sleep of death what dreams may come,
When we have shuffled off this mortal coil,
Must give us pause—there's the respect
That makes calamity of so long life.
";

            // Create tokenizer
            Console.WriteLine("Creating tokenizer...");
            var tokenizer = new CharTokenizer(trainingText);
            Console.WriteLine($"Vocabulary size: {tokenizer.VocabSize}");

            // Create a small model for demonstration
            Console.WriteLine("\nCreating model...");
            var model = new SmallMind.Transformers.TransformerModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 32,      // Context window
                nEmbd: 64,          // Embedding dimension
                nLayer: 2,          // Number of transformer layers
                nHead: 4,           // Number of attention heads
                dropout: 0.1,       // Dropout rate
                seed: 42
            );

            Console.WriteLine($"Model created with {CountParameters(model)} parameters");

            // Create training instance (metrics tracking is automatic)
            Console.WriteLine("\nInitializing training...");
            var training = new SmallMind.Runtime.Training(
                model: model,
                tokenizer: tokenizer,
                trainingText: trainingText,
                blockSize: 32,
                batchSize: 4,
                seed: 42
            );

            // Train with enhanced metrics
            Console.WriteLine("\nStarting training with metrics tracking...");
            Console.WriteLine("Watch for: Loss, Perplexity, and Token Accuracy\n");

            training.TrainEnhanced(
                steps: 200,                  // Training steps
                learningRate: 0.001,         // Learning rate
                logEvery: 20,                // Log every 20 steps
                saveEvery: 500,              // Save checkpoints
                checkpointDir: "./checkpoints/metrics_example",
                showPerf: true,              // Show performance stats
                gradAccumSteps: 2,           // Gradient accumulation
                warmupSteps: 20,             // Learning rate warmup
                valEvery: 40,                // Validate every 40 steps
                valBatches: 5,               // Use 5 batches for validation
                minLr: 0.0001f               // Minimum learning rate
            );

            // Display comprehensive metrics report
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine(training.Metrics.GetReport());

            // Demonstrate programmatic access to metrics
            Console.WriteLine(new string('=', 70));
            Console.WriteLine("METRICS ANALYSIS");
            Console.WriteLine(new string('=', 70));

            var summary = training.Metrics.GetSummary();

            // Training progress
            if (training.Metrics.IsTrainingProgressing(lookbackSteps: 10))
            {
                Console.WriteLine("✓ Training is progressing (loss decreasing)");
            }
            else
            {
                Console.WriteLine("⚠ Training may have stalled (loss not decreasing)");
            }

            // Best results
            var bestValLoss = training.Metrics.GetBestValidationLoss();
            var bestPerplexity = training.Metrics.GetBestPerplexity();
            
            if (bestValLoss.HasValue && bestPerplexity.HasValue)
            {
                Console.WriteLine($"\nBest Validation Results:");
                Console.WriteLine($"  Loss:       {bestValLoss.Value:F4}");
                Console.WriteLine($"  Perplexity: {bestPerplexity.Value:F2}");
                
                // Provide interpretation
                Console.WriteLine("\nInterpretation:");
                if (bestPerplexity.Value < 50)
                {
                    Console.WriteLine("  ✓ Excellent - Model has good prediction quality for this data size");
                }
                else if (bestPerplexity.Value < 100)
                {
                    Console.WriteLine("  → Decent - Model is learning but could improve with more training");
                }
                else
                {
                    Console.WriteLine("  ⚠ Poor - Consider training longer or increasing model capacity");
                }
            }

            // Gradient health
            if (summary.GradientHealthSummary != null)
            {
                var gh = summary.GradientHealthSummary;
                Console.WriteLine("\nGradient Health Check:");
                
                if (gh.TotalNanCount == 0 && gh.TotalInfCount == 0)
                {
                    Console.WriteLine("  ✓ No NaN or Infinity values detected");
                }
                else
                {
                    Console.WriteLine($"  ⚠ Issues detected: {gh.TotalNanCount} NaN, {gh.TotalInfCount} Inf");
                }

                if (gh.AverageMeanNorm > 1e-8f && gh.MaxNormSeen < 100f)
                {
                    Console.WriteLine("  ✓ Gradient magnitudes in healthy range");
                }
                else if (gh.AverageMeanNorm < 1e-8f)
                {
                    Console.WriteLine("  ⚠ Vanishing gradients detected - consider increasing learning rate");
                }
                else if (gh.MaxNormSeen > 100f)
                {
                    Console.WriteLine("  ⚠ Exploding gradients detected - consider decreasing learning rate");
                }
            }

            // Token accuracy
            var currentAccuracy = training.Metrics.GetCurrentTokenAccuracy();
            if (currentAccuracy.HasValue)
            {
                Console.WriteLine($"\nToken Prediction Accuracy: {currentAccuracy.Value * 100:F2}%");
                Console.WriteLine($"  (Model correctly predicts {currentAccuracy.Value * 100:F1}% of tokens)");
            }

            // Recommendations
            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("RECOMMENDATIONS FOR IMPROVEMENT");
            Console.WriteLine(new string('=', 70));

            // Check for overfitting
            if (summary.ValidationLossStats != null && summary.TrainingLossStats != null)
            {
                float trainLoss = summary.TrainingLossStats.Mean;
                float valLoss = summary.ValidationLossStats.Mean;
                
                if (valLoss > trainLoss * 1.2f)
                {
                    Console.WriteLine("→ Model may be overfitting:");
                    Console.WriteLine("  - Consider adding more dropout");
                    Console.WriteLine("  - Use early stopping based on validation loss");
                    Console.WriteLine("  - Collect more training data");
                }
                else if (valLoss < trainLoss)
                {
                    Console.WriteLine("→ Model may be underfitting:");
                    Console.WriteLine("  - Consider increasing model size (more layers/embedding dimension)");
                    Console.WriteLine("  - Train for more steps");
                    Console.WriteLine("  - Decrease dropout rate");
                }
                else
                {
                    Console.WriteLine("✓ Good balance between training and validation loss");
                }
            }

            // Perplexity-based recommendations
            if (bestPerplexity.HasValue)
            {
                if (bestPerplexity.Value > 100)
                {
                    Console.WriteLine("\n→ High perplexity suggests:");
                    Console.WriteLine("  - Model needs more capacity (increase nEmbd, nLayer, or nHead)");
                    Console.WriteLine("  - More training data would help");
                    Console.WriteLine("  - Train for more steps");
                }
            }

            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("Example complete! Check ./checkpoints/metrics_example/ for saved models.");
            Console.WriteLine("\nKey Takeaways:");
            Console.WriteLine("  • Perplexity shows how 'surprised' the model is (lower = better)");
            Console.WriteLine("  • Token accuracy shows exact prediction correctness");
            Console.WriteLine("  • Gradient health prevents training failures");
            Console.WriteLine("  • These metrics help you measure improvements objectively");
            Console.WriteLine("\nSee docs/TRAINING_METRICS_GUIDE.md for detailed explanations.");
        }

        private static int CountParameters(TransformerModel model)
        {
            int total = 0;
            foreach (var param in model.Parameters)
            {
                total += param.Size;
            }
            return total;
        }
    }
}

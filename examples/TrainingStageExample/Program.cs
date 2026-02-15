using SmallMind.Core;
using SmallMind.Tokenizers;
using SmallMind.Training;
using SmallMind.Transformers;
using System;

namespace TrainingStageExample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SmallMind Training Stage Verification Example");
            Console.WriteLine("==============================================\n");

            // Create a simple tokenizer
            const string vocab = "abcdefghijklmnopqrstuvwxyz ";
            var tokenizer = new CharTokenizer(vocab);

            // Create a small transformer model
            var model = new TransformerModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 16,
                nEmbd: 32,
                nLayer: 2,
                nHead: 2,
                dropout: 0.0,
                seed: 42
            );

            // Create training instance
            var training = new Training(
                model: model,
                tokenizer: tokenizer,
                trainingText: "hello world this is a test of the training system",
                blockSize: 16,
                batchSize: 2,
                seed: 42
            );

            // Example 1: Check initial stage
            Console.WriteLine("=== Example 1: Initial Stage ===");
            Console.WriteLine(training.VerifyCurrentStage());
            Console.WriteLine();

            // Example 2: Set a training stage and update progress
            Console.WriteLine("=== Example 2: Set Pretraining Stage ===");
            training.SetTrainingStage(TrainingStage.Pretraining, totalSteps: 100);
            Console.WriteLine(training.VerifyCurrentStage());
            Console.WriteLine();

            // Example 3: Update progress
            Console.WriteLine("=== Example 3: Update Progress (50/100 steps) ===");
            training.UpdateStageProgress(50);
            Console.WriteLine(training.VerifyCurrentStage());
            Console.WriteLine();

            // Example 4: Complete the stage
            Console.WriteLine("=== Example 4: Complete Current Stage ===");
            training.UpdateStageProgress(100);
            Console.WriteLine(training.VerifyCurrentStage());
            Console.WriteLine();

            // Example 5: Check if there's a next stage
            Console.WriteLine("=== Example 5: Check Next Stage ===");
            if (training.HasNextStage())
            {
                var nextStage = training.GetNextStage();
                Console.WriteLine($"Next stage available: {nextStage}");
                
                // Advance to next stage
                if (training.AdvanceToNextStage(nextStageTotalSteps: 50))
                {
                    Console.WriteLine($"\nAdvanced to: {training.StageInfo.CurrentStage}");
                    Console.WriteLine(training.VerifyCurrentStage());
                }
            }
            else
            {
                Console.WriteLine("No next stage available - training is complete!");
            }

            Console.WriteLine("\n=== Example 6: Save Checkpoint with Stage Info ===");
            string checkpointPath = "training_checkpoint.smnd";
            training.SaveCheckpointWithStage(checkpointPath);
            Console.WriteLine($"Checkpoint saved to: {checkpointPath}");

            // Load checkpoint to verify stage info is preserved
            Console.WriteLine("\n=== Example 7: Load Checkpoint and Verify Stage ===");
            var training2 = new Training(
                model: model,
                tokenizer: tokenizer,
                trainingText: "hello world this is a test of the training system",
                blockSize: 16,
                batchSize: 2,
                seed: 42
            );
            training2.LoadCheckpointWithStage(checkpointPath);
            Console.WriteLine(training2.VerifyCurrentStage());

            Console.WriteLine("\n=== All Examples Completed Successfully! ===");
        }
    }
}

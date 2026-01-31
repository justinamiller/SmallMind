using System;
using System.IO;

namespace TinyLLM
{
    /// <summary>
    /// CLI entry point for the Tiny LLM - Pure C# implementation.
    /// Supports training and generation modes with command-line arguments.
    /// No 3rd party dependencies - everything implemented in pure C#.
    /// </summary>
    class Program
    {
        // Model hyperparameters (defaults)
        private const int BLOCK_SIZE = 512;
        private const int N_EMBD = 128;
        private const int N_LAYER = 4;
        private const int N_HEAD = 4;
        private const double DROPOUT = 0.1;
        private const int BATCH_SIZE = 16;
        private const double LEARNING_RATE = 3e-4;
        private const int TRAIN_STEPS = 2000;
        private const int LOG_EVERY = 50;
        private const int SAVE_EVERY = 500;
        private const int SEED = 42;
        private const string CHECKPOINT_DIR = "checkpoints";
        private const string DATA_FILE = "data.txt";

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("=== Tiny LLM - Educational Transformer in Pure C# ===");
                Console.WriteLine("No 3rd party dependencies - everything from scratch!\n");

                // Parse command-line arguments
                bool shouldTrain = !HasArg(args, "--no-train");
                bool shouldLoad = HasArg(args, "--load");
                bool showPerf = HasArg(args, "--perf");
                string prompt = GetArgValue(args, "--prompt", "Once upon a time");
                int generateSteps = int.Parse(GetArgValue(args, "--steps", "200"));
                double temperature = double.Parse(GetArgValue(args, "--temperature", "1.0"));
                int topK = int.Parse(GetArgValue(args, "--top-k", "0"));

                // Ensure data file exists
                EnsureDataFile();

                // Load training data
                string trainingText = File.ReadAllText(DATA_FILE);
                Console.WriteLine($"Loaded {trainingText.Length} characters from {DATA_FILE}");

                // Build tokenizer
                var tokenizer = new Tokenizer(trainingText);

                // Create model
                var model = new TransformerModel(
                    vocabSize: tokenizer.VocabSize,
                    blockSize: BLOCK_SIZE,
                    nEmbd: N_EMBD,
                    nLayer: N_LAYER,
                    nHead: N_HEAD,
                    dropout: DROPOUT,
                    seed: SEED
                );

                // Training
                var trainer = new Training(
                    model: model,
                    tokenizer: tokenizer,
                    trainingText: trainingText,
                    blockSize: BLOCK_SIZE,
                    batchSize: BATCH_SIZE,
                    seed: SEED
                );

                var checkpointPath = Path.Combine(CHECKPOINT_DIR, "model.json");

                // Load checkpoint if requested or if it exists
                if (shouldLoad || (!shouldTrain && File.Exists(checkpointPath)))
                {
                    if (File.Exists(checkpointPath))
                    {
                        trainer.LoadCheckpoint(checkpointPath);
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Checkpoint {checkpointPath} not found.");
                        if (!shouldTrain)
                        {
                            Console.WriteLine("Training is disabled and no checkpoint exists. Enabling training.");
                            shouldTrain = true;
                        }
                    }
                }

                // Train if requested
                if (shouldTrain)
                {
                    trainer.Train(
                        steps: TRAIN_STEPS,
                        learningRate: LEARNING_RATE,
                        logEvery: LOG_EVERY,
                        saveEvery: SAVE_EVERY,
                        checkpointDir: CHECKPOINT_DIR,
                        showPerf: showPerf
                    );
                }

                // Generation
                var sampler = new Sampling(model, tokenizer, BLOCK_SIZE);
                var generated = sampler.Generate(
                    prompt: prompt,
                    maxNewTokens: generateSteps,
                    temperature: temperature,
                    topK: topK,
                    seed: SEED,
                    showPerf: showPerf
                );

                Console.WriteLine("\n=== Generated Text ===");
                Console.WriteLine(generated);
                Console.WriteLine("\n=== End ===");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Create default data.txt if it doesn't exist.
        /// </summary>
        private static void EnsureDataFile()
        {
            if (!File.Exists(DATA_FILE))
            {
                Console.WriteLine($"{DATA_FILE} not found. Creating default dataset...");
                var defaultData = @"The quick brown fox jumps over the lazy dog. A journey of a thousand miles begins with a single step.
To be or not to be, that is the question. All that glitters is not gold. Where there is a will, there is a way.
Actions speak louder than words. The early bird catches the worm. Knowledge is power. Practice makes perfect.
Time is money. Better late than never. A picture is worth a thousand words. When in Rome, do as the Romans do.
The pen is mightier than the sword. Fortune favors the bold. Necessity is the mother of invention.
Two heads are better than one. The grass is always greener on the other side. You can't judge a book by its cover.
Easy come, easy go. When the going gets tough, the tough get going. No pain, no gain. The best things in life are free.
Honesty is the best policy. Patience is a virtue. There is no place like home. Laughter is the best medicine.
Once upon a time in a land far away, there lived a wise old owl who knew many secrets of the forest.
The owl would share stories with the young animals, teaching them valuable lessons about life and survival.
In the depths of winter, when snow covered the ground, the animals would gather around to hear tales of bravery.
Spring brought new life and hope, as flowers bloomed and birds sang their cheerful songs throughout the day.
Summer was a time of abundance, with fruits ripening on trees and plenty of food for all creatures great and small.
Autumn arrived with golden leaves falling gently to the earth, reminding everyone that change is constant.
";
                File.WriteAllText(DATA_FILE, defaultData);
                Console.WriteLine($"Created {DATA_FILE} with default content.");
            }
        }

        /// <summary>
        /// Check if a command-line argument is present.
        /// </summary>
        private static bool HasArg(string[] args, string name)
        {
            return Array.IndexOf(args, name) >= 0;
        }

        /// <summary>
        /// Get the value of a command-line argument.
        /// </summary>
        private static string GetArgValue(string[] args, string name, string defaultValue)
        {
            var index = Array.IndexOf(args, name);
            if (index >= 0 && index + 1 < args.Length)
            {
                return args[index + 1];
            }
            return defaultValue;
        }
    }
}

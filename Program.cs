using System;
using System.IO;
using System.Diagnostics;

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
        private const int DEFAULT_BLOCK_SIZE = 512;
        private const int N_EMBD = 128;
        private const int N_LAYER = 4;
        private const int N_HEAD = 4;
        private const double DROPOUT = 0.1;
        private const int DEFAULT_BATCH_SIZE = 16;
        private const double LEARNING_RATE = 3e-4;
        private const int TRAIN_STEPS = 2000;
        private const int LOG_EVERY = 50;
        private const int SAVE_EVERY = 500;
        private const int SEED = 42;
        private const string CHECKPOINT_DIR = "checkpoints";
        private const string DATA_FILE = "data.txt";
        
        // Maximum safe block size for this architecture (increased for larger context windows)
        // Can be overridden with --max-block-size for even larger contexts
        // Supports up to 128GB RAM with 32768 tokens
        private const int MAX_BLOCK_SIZE = 32768;

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
                bool autoConfig = HasArg(args, "--auto-config");
                string prompt = GetArgValue(args, "--prompt", "Once upon a time");
                int generateSteps = int.Parse(GetArgValue(args, "--steps", "200"));
                double temperature = double.Parse(GetArgValue(args, "--temperature", "1.0"));
                int topK = int.Parse(GetArgValue(args, "--top-k", "0"));
                
                // Get max block size override if provided
                string maxBlockSizeArg = GetArgValue(args, "--max-block-size", "");
                int maxBlockSize = MAX_BLOCK_SIZE;
                if (!string.IsNullOrEmpty(maxBlockSizeArg))
                {
                    maxBlockSize = int.Parse(maxBlockSizeArg);
                    Console.WriteLine($"Maximum block size override: {maxBlockSize}");
                }
                
                // Determine block size (priority: command-line > auto-config > default)
                int blockSize = DEFAULT_BLOCK_SIZE;
                string blockSizeArg = GetArgValue(args, "--block-size", "");
                
                if (!string.IsNullOrEmpty(blockSizeArg))
                {
                    // User specified block size
                    blockSize = int.Parse(blockSizeArg);
                    if (blockSize > maxBlockSize)
                    {
                        Console.WriteLine($"Warning: Requested block size {blockSize} exceeds maximum {maxBlockSize}. Using {maxBlockSize}.");
                        blockSize = maxBlockSize;
                    }
                    Console.WriteLine($"Using user-specified block size: {blockSize}");
                }
                else if (autoConfig)
                {
                    // Auto-configure based on system resources
                    blockSize = DetermineOptimalBlockSize(maxBlockSize);
                    Console.WriteLine($"Auto-configured block size based on system resources: {blockSize}");
                }
                else
                {
                    Console.WriteLine($"Using default block size: {blockSize}");
                }
                
                // Determine batch size (auto-config or default)
                int batchSize = DEFAULT_BATCH_SIZE;
                string batchSizeArg = GetArgValue(args, "--batch-size", "");
                
                if (!string.IsNullOrEmpty(batchSizeArg))
                {
                    batchSize = int.Parse(batchSizeArg);
                    Console.WriteLine($"Using user-specified batch size: {batchSize}");
                }
                else if (autoConfig)
                {
                    batchSize = DetermineOptimalBatchSize(blockSize);
                    Console.WriteLine($"Auto-configured batch size: {batchSize}");
                }
                else
                {
                    Console.WriteLine($"Using default batch size: {batchSize}");
                }

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
                    blockSize: blockSize,
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
                    blockSize: blockSize,
                    batchSize: batchSize,
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
                var sampler = new Sampling(model, tokenizer, blockSize);
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
        /// Determine optimal block size based on available system resources.
        /// </summary>
        private static int DetermineOptimalBlockSize(int maxBlockSize = MAX_BLOCK_SIZE)
        {
            var (totalMemoryGB, availableMemoryGB, cpuCores) = GetSystemInfo();
            
            Console.WriteLine($"System resources: {availableMemoryGB:F1}GB available RAM, {cpuCores} CPU cores");
            
            // Algorithm to determine block size based on resources:
            // - Base calculation on available memory (primary constraint)
            // - Each token in the model uses approximately memory proportional to:
            //   - Position embeddings: blockSize * nEmbd * 4 bytes
            //   - Attention masks: blockSize * blockSize * 4 bytes (most significant)
            //   - Intermediate tensors during forward/backward pass
            // - We want to stay well under available memory to avoid swapping
            
            int recommendedBlockSize;
            
            // Empirically determined thresholds based on testing with the current architecture
            // (batch_size=16, nEmbd=128, nLayer=4, nHead=4)
            // Memory usage scales primarily with blockSize^2 due to attention mechanism
            // Updated values to support larger context windows up to 128GB RAM
            
            if (availableMemoryGB >= 128.0)
            {
                // Extreme memory (128GB+) - use maximum (32768)
                recommendedBlockSize = Math.Min(32768, maxBlockSize);
            }
            else if (availableMemoryGB >= 64.0)
            {
                // Very high memory (64GB+) - use 16384
                recommendedBlockSize = Math.Min(16384, maxBlockSize);
            }
            else if (availableMemoryGB >= 32.0)
            {
                // High memory (32GB+) - use 8192
                recommendedBlockSize = Math.Min(8192, maxBlockSize);
            }
            else if (availableMemoryGB >= 16.0)
            {
                // Good memory (16GB+) - use 6144
                recommendedBlockSize = 6144;
            }
            else if (availableMemoryGB >= 8.0)
            {
                // Moderate memory (8GB+) - use 4096
                recommendedBlockSize = 4096;
            }
            else if (availableMemoryGB >= 4.0)
            {
                // Limited memory (4GB+) - use 2048
                recommendedBlockSize = 2048;
            }
            else if (availableMemoryGB >= 2.0)
            {
                // Low memory (2GB+) - use 1024
                recommendedBlockSize = 1024;
            }
            else if (availableMemoryGB >= 1.0)
            {
                // Very low memory (1GB+) - use 512
                recommendedBlockSize = 512;
            }
            else
            {
                // Extremely limited memory (<1GB) - use 256
                recommendedBlockSize = 256;
            }
            
            // Ensure we don't exceed maximum
            recommendedBlockSize = Math.Min(recommendedBlockSize, maxBlockSize);
            
            return recommendedBlockSize;
        }
        
        /// <summary>
        /// Determine optimal batch size based on block size and available memory.
        /// Larger batches improve throughput but require more memory.
        /// </summary>
        private static int DetermineOptimalBatchSize(int blockSize)
        {
            var (totalMemoryGB, availableMemoryGB, cpuCores) = GetSystemInfo();
            
            // Scale batch size inversely with block size to maintain memory usage
            // Larger block sizes need smaller batches to fit in memory
            int recommendedBatchSize;
            
            if (blockSize >= 16384)
            {
                // Extreme context (16K+ tokens) - use very small batches
                recommendedBatchSize = availableMemoryGB >= 64.0 ? 4 : 2;
            }
            else if (blockSize >= 8192)
            {
                // Very large context (8K+ tokens) - use small batches
                recommendedBatchSize = availableMemoryGB >= 32.0 ? 8 : 4;
            }
            else if (blockSize >= 4096)
            {
                // Large context (4K+ tokens) - use smaller batches
                recommendedBatchSize = availableMemoryGB >= 16.0 ? 8 : 4;
            }
            else if (blockSize >= 2048)
            {
                // Medium-large context (2K+ tokens) - moderate batches
                recommendedBatchSize = availableMemoryGB >= 8.0 ? 16 : 8;
            }
            else if (blockSize >= 1024)
            {
                // Medium context (1K+ tokens) - good batches
                recommendedBatchSize = availableMemoryGB >= 4.0 ? 24 : 16;
            }
            else
            {
                // Smaller context (<1K tokens) - can use larger batches for better throughput
                recommendedBatchSize = availableMemoryGB >= 4.0 ? 32 : 24;
            }
            
            return recommendedBatchSize;
        }
        
        /// <summary>
        /// Get system information (RAM and CPU cores) using pure C#.
        /// </summary>
        private static (double totalMemoryGB, double availableMemoryGB, int cpuCores) GetSystemInfo()
        {
            // Get CPU cores
            int cpuCores = Environment.ProcessorCount;
            
            // Get memory information
            // On Linux, we can read from /proc/meminfo
            // On Windows, we use GC.GetGCMemoryInfo (available in .NET Core 3.0+)
            double totalMemoryGB = 0;
            double availableMemoryGB = 0;
            
            try
            {
                if (OperatingSystem.IsLinux() && File.Exists("/proc/meminfo"))
                {
                    // Parse /proc/meminfo on Linux
                    var lines = File.ReadAllLines("/proc/meminfo");
                    long totalKB = 0;
                    long availableKB = 0;
                    
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("MemTotal:"))
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                long.TryParse(parts[1], out totalKB);
                            }
                        }
                        else if (line.StartsWith("MemAvailable:"))
                        {
                            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                long.TryParse(parts[1], out availableKB);
                            }
                        }
                    }
                    
                    totalMemoryGB = totalKB / 1024.0 / 1024.0; // Convert KB to GB
                    availableMemoryGB = availableKB / 1024.0 / 1024.0;
                }
                else
                {
                    // Fallback: Use GC memory info (available on all platforms)
                    var gcMemoryInfo = GC.GetGCMemoryInfo();
                    totalMemoryGB = gcMemoryInfo.TotalAvailableMemoryBytes / 1024.0 / 1024.0 / 1024.0;
                    
                    // Estimate available memory as total minus current process memory
                    var currentProcess = Process.GetCurrentProcess();
                    long processMemoryBytes = currentProcess.WorkingSet64;
                    availableMemoryGB = totalMemoryGB - (processMemoryBytes / 1024.0 / 1024.0 / 1024.0);
                    
                    // On non-Linux platforms, GC info may not be accurate for available memory.
                    // Use a conservative estimate based on total memory.
                    if (availableMemoryGB <= 0 || availableMemoryGB > totalMemoryGB)
                    {
                        // Assume 60% of total memory is available as a conservative estimate
                        availableMemoryGB = totalMemoryGB * 0.6;
                    }
                }
            }
            catch
            {
                // Fallback to conservative defaults if we can't read system info
                totalMemoryGB = 4.0;
                availableMemoryGB = 2.0;
            }
            
            return (totalMemoryGB, availableMemoryGB, cpuCores);
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

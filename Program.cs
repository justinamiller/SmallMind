using System;
using System.IO;
using System.Diagnostics;
using TinyLLM.Core;
using TinyLLM.Text;
using TinyLLM.RAG;
using TinyLLM.Embeddings;
using TinyLLM.Indexing;

namespace TinyLLM
{
    /// <summary>
    /// CLI entry point for the Tiny LLM - Pure C# implementation.
    /// Supports training and generation modes with command-line arguments.
    /// No 3rd party dependencies - everything implemented in pure C#.
    /// </summary>
    class Program
    {
        /// <summary>
        /// Model configuration preset inspired by popular LLM architectures.
        /// Allows users to choose different model sizes and configurations.
        /// </summary>
        public class ModelConfig
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public int BlockSize { get; set; }
            public int NEmbedding { get; set; }
            public int NLayers { get; set; }
            public int NHeads { get; set; }
            public double Dropout { get; set; }
            public int BatchSize { get; set; }
            public double LearningRate { get; set; }

            public ModelConfig(string name, string description, int blockSize, int nEmbedding, int nLayers, int nHeads, double dropout, int batchSize, double learningRate)
            {
                Name = name;
                Description = description;
                BlockSize = blockSize;
                NEmbedding = nEmbedding;
                NLayers = nLayers;
                NHeads = nHeads;
                Dropout = dropout;
                BatchSize = batchSize;
                LearningRate = learningRate;
            }
        }

        // Predefined model configurations inspired by popular LLM architectures
        private static readonly Dictionary<string, ModelConfig> MODEL_PRESETS = new Dictionary<string, ModelConfig>
        {
            ["default"] = new ModelConfig(
                name: "Default (Tiny Educational)",
                description: "Original tiny model for educational purposes - fast training on CPU",
                blockSize: 512,
                nEmbedding: 128,
                nLayers: 4,
                nHeads: 4,
                dropout: 0.1,
                batchSize: 16,
                learningRate: 3e-4
            ),
            ["mistral-7b"] = new ModelConfig(
                name: "Mistral 7B (Scaled)",
                description: "Configuration inspired by Mistral 7B architecture - larger model with more layers",
                blockSize: 2048,
                nEmbedding: 256,
                nLayers: 8,
                nHeads: 8,
                dropout: 0.1,
                batchSize: 8,
                learningRate: 2e-4
            ),
            ["mistral-medium"] = new ModelConfig(
                name: "Mistral Medium",
                description: "Medium-sized configuration with balanced performance and training time",
                blockSize: 1024,
                nEmbedding: 192,
                nLayers: 6,
                nHeads: 6,
                dropout: 0.1,
                batchSize: 12,
                learningRate: 2.5e-4
            ),
            ["deepseek"] = new ModelConfig(
                name: "DeepSeek (Scaled)",
                description: "Configuration inspired by DeepSeek architecture - optimized for reasoning tasks",
                blockSize: 4096,
                nEmbedding: 320,
                nLayers: 10,
                nHeads: 8,
                dropout: 0.05,
                batchSize: 4,
                learningRate: 1.5e-4
            ),
            ["tiny"] = new ModelConfig(
                name: "Tiny (Fastest)",
                description: "Very small model for quick testing and prototyping",
                blockSize: 256,
                nEmbedding: 64,
                nLayers: 2,
                nHeads: 2,
                dropout: 0.1,
                batchSize: 32,
                learningRate: 4e-4
            )
        };

        // Training parameters (shared across all presets)
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
                bool isPerfJsonMode = HasArg(args, "--perf-json");
                bool benchMode = HasArg(args, "--bench");
                bool autoConfig = HasArg(args, "--auto-config");
                bool enhancedTraining = HasArg(args, "--enhanced-training");
                bool qaMode = HasArg(args, "--qa");
                bool interactiveMode = HasArg(args, "--interactive");
                bool listPresets = HasArg(args, "--list-presets");
                string prompt = GetArgValue(args, "--prompt", "Once upon a time");
                int generateSteps = int.Parse(GetArgValue(args, "--steps", "200"));
                double temperature = double.Parse(GetArgValue(args, "--temperature", "1.0"));
                int topK = int.Parse(GetArgValue(args, "--top-k", "0"));
                
                // List available model presets and exit if requested
                if (listPresets)
                {
                    Console.WriteLine("\n=== Available Model Presets ===\n");
                    foreach (var preset in MODEL_PRESETS)
                    {
                        var config = preset.Value;
                        Console.WriteLine($"Preset: {preset.Key}");
                        Console.WriteLine($"  Name: {config.Name}");
                        Console.WriteLine($"  Description: {config.Description}");
                        Console.WriteLine($"  Parameters: {config.NEmbedding} embedding dim, {config.NLayers} layers, {config.NHeads} heads");
                        Console.WriteLine($"  Context: {config.BlockSize} tokens, Batch: {config.BatchSize}");
                        Console.WriteLine($"  Learning Rate: {config.LearningRate}, Dropout: {config.Dropout}");
                        Console.WriteLine();
                    }
                    return;
                }
                
                // Select model preset (default to "default")
                string presetName = GetArgValue(args, "--model-preset", "default").ToLower();
                if (!MODEL_PRESETS.ContainsKey(presetName))
                {
                    Console.WriteLine($"Error: Unknown model preset '{presetName}'");
                    Console.WriteLine("Available presets: " + string.Join(", ", MODEL_PRESETS.Keys));
                    Console.WriteLine("Use --list-presets to see detailed information about each preset");
                    Environment.Exit(1);
                }
                
                ModelConfig selectedPreset = MODEL_PRESETS[presetName];
                Console.WriteLine($"\n=== Using Model Preset: {selectedPreset.Name} ===");
                Console.WriteLine($"Description: {selectedPreset.Description}");
                Console.WriteLine($"Configuration: {selectedPreset.NEmbedding} embedding dim, {selectedPreset.NLayers} layers, {selectedPreset.NHeads} heads\n");
                
                // Enhanced training parameters
                int gradAccumSteps = int.Parse(GetArgValue(args, "--grad-accum", "1"));
                int warmupSteps = int.Parse(GetArgValue(args, "--warmup", "100"));
                
                // Get max block size override if provided
                string maxBlockSizeArg = GetArgValue(args, "--max-block-size", "");
                int maxBlockSize = MAX_BLOCK_SIZE;
                if (!string.IsNullOrEmpty(maxBlockSizeArg))
                {
                    maxBlockSize = int.Parse(maxBlockSizeArg);
                    Console.WriteLine($"Maximum block size override: {maxBlockSize}");
                }
                
                // Determine block size (priority: command-line > auto-config > preset default)
                int blockSize = selectedPreset.BlockSize;
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
                    Console.WriteLine($"Using preset block size: {blockSize}");
                }
                
                // Determine batch size (priority: command-line > auto-config > preset default)
                int batchSize = selectedPreset.BatchSize;
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
                    Console.WriteLine($"Using preset batch size: {batchSize}");
                }

                // Ensure data file exists
                EnsureDataFile();

                // Load training data
                string trainingText = File.ReadAllText(DATA_FILE);
                Console.WriteLine($"Loaded {trainingText.Length} characters from {DATA_FILE}");

                // Build tokenizer
                var tokenizer = new Tokenizer(trainingText);

                // Create model with selected preset configuration
                var model = new TransformerModel(
                    vocabSize: tokenizer.VocabSize,
                    blockSize: blockSize,
                    nEmbd: selectedPreset.NEmbedding,
                    nLayer: selectedPreset.NLayers,
                    nHead: selectedPreset.NHeads,
                    dropout: selectedPreset.Dropout,
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
                    if (enhancedTraining)
                    {
                        Console.WriteLine("\nUsing enhanced training with gradient accumulation and learning rate scheduling");
                        trainer.TrainEnhanced(
                            steps: TRAIN_STEPS,
                            learningRate: selectedPreset.LearningRate,
                            logEvery: LOG_EVERY,
                            saveEvery: SAVE_EVERY,
                            checkpointDir: CHECKPOINT_DIR,
                            showPerf: showPerf,
                            gradAccumSteps: gradAccumSteps,
                            warmupSteps: warmupSteps,
                            valEvery: 500,
                            valBatches: 10
                        );
                    }
                    else
                    {
                        trainer.Train(
                            steps: TRAIN_STEPS,
                            learningRate: selectedPreset.LearningRate,
                            logEvery: LOG_EVERY,
                            saveEvery: SAVE_EVERY,
                            checkpointDir: CHECKPOINT_DIR,
                            showPerf: showPerf
                        );
                    }
                }

                // Benchmark mode - run sweeps over concurrency and max_tokens
                if (benchMode)
                {
                    RunBenchmarkMode(model, tokenizer, blockSize, prompt, temperature, topK);
                }
                // Interactive mode - conversation session
                else if (interactiveMode)
                {
                    RunInteractiveMode(model, tokenizer, blockSize, trainingText);
                }
                // Q&A mode
                else if (qaMode)
                {
                    RunQAMode(model, tokenizer, blockSize, trainingText, prompt, generateSteps, temperature, topK);
                }
                // Standard generation mode
                else
                {
                    var sampler = new Sampling(model, tokenizer, blockSize);
                    var generated = sampler.Generate(
                        prompt: prompt,
                        maxNewTokens: generateSteps,
                        temperature: temperature,
                        topK: topK,
                        seed: SEED,
                        showPerf: showPerf,
                        isPerfJsonMode: isPerfJsonMode
                    );

                    if (!isPerfJsonMode)
                    {
                        Console.WriteLine("\n=== Generated Text ===");
                        Console.WriteLine(generated);
                        Console.WriteLine("\n=== End ===");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Run interactive conversation mode with session context
        /// </summary>
        private static void RunInteractiveMode(TransformerModel model, Tokenizer tokenizer, int blockSize, string trainingText)
        {
            Console.WriteLine("\n=== Interactive Conversation Mode ===");
            Console.WriteLine("Type your questions or messages. Type 'exit' to quit, 'clear' to clear history, 'save' to save session.");
            Console.WriteLine("The model will maintain conversation context across turns.\n");

            var session = new ConversationSession("interactive-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), tokenizer, blockSize);
            var qaEngine = new QuestionAnsweringEngine(model, tokenizer, blockSize, trainingText);

            while (true)
            {
                Console.Write("You: ");
                string? input = Console.ReadLine();
                
                if (string.IsNullOrWhiteSpace(input))
                {
                    continue;
                }

                if (input.Trim().ToLower() == "exit")
                {
                    Console.WriteLine("Goodbye!");
                    break;
                }

                if (input.Trim().ToLower() == "clear")
                {
                    session.Clear();
                    Console.WriteLine("Conversation history cleared.");
                    continue;
                }

                if (input.Trim().ToLower() == "save")
                {
                    string sessionPath = $"sessions/session_{session.SessionId}.json";
                    Directory.CreateDirectory("sessions");
                    session.SaveToFile(sessionPath);
                    Console.WriteLine($"Session saved to {sessionPath}");
                    continue;
                }

                if (input.Trim().ToLower() == "history")
                {
                    Console.WriteLine("\nConversation History:");
                    var history = session.GetHistory();
                    foreach (var turn in history)
                    {
                        string prefix = turn.Role == "user" ? "You" : "Assistant";
                        Console.WriteLine($"{prefix}: {turn.Content}");
                    }
                    Console.WriteLine();
                    continue;
                }

                // Add user input to session
                session.AddUserInput(input);

                // Get conversation context
                string context = session.GetContextString();

                // Generate response using Q&A engine with context
                Console.Write("Assistant: ");
                string response = qaEngine.AnswerQuestionWithContext(
                    question: input,
                    conversationContext: context,
                    maxTokens: 150,
                    temperature: 0.7,
                    topK: 40
                );
                Console.WriteLine(response);

                // Add assistant response to session
                session.AddAssistantResponse(response);
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Run Q&A mode for answering single questions
        /// </summary>
        private static void RunQAMode(TransformerModel model, Tokenizer tokenizer, int blockSize, string trainingText, 
                                      string question, int maxTokens, double temperature, int topK)
        {
            Console.WriteLine("\n=== Question-Answering Mode ===");
            
            var qaEngine = new QuestionAnsweringEngine(model, tokenizer, blockSize, trainingText);
            
            Console.WriteLine($"\nQuestion: {question}");
            Console.Write("Thinking...");
            
            string answer = qaEngine.AnswerQuestion(
                question: question,
                maxTokens: maxTokens,
                temperature: temperature,
                topK: topK,
                seed: SEED,
                useContext: true
            );
            
            Console.WriteLine($"\n\nAnswer: {answer}");
            Console.WriteLine("\n=== End ===");
        }

        /// <summary>
        /// Run benchmark mode - sweep over concurrency and max_tokens configurations.
        /// </summary>
        private static void RunBenchmarkMode(TransformerModel model, Tokenizer tokenizer, int blockSize, string prompt, double temperature, int topK)
        {
            Console.WriteLine("\n=== Benchmark Mode ===");
            Console.WriteLine("Running performance sweeps over different configurations...\n");

            // Benchmark configurations (simplified for single-threaded CPU execution)
            // Note: True concurrency would require async/parallel execution which isn't in scope for this educational LLM
            var concurrencyLevels = new[] { 1 }; // Single request at a time
            var maxTokensValues = new[] { 64, 128, 256 };
            var results = new List<BenchmarkResult>();

            foreach (var concurrency in concurrencyLevels)
            {
                foreach (var maxTokens in maxTokensValues)
                {
                    Console.WriteLine($"Running: concurrency={concurrency}, max_tokens={maxTokens}");

                    var metrics = new PerformanceMetrics();
                    metrics.Start();

                    var sampler = new Sampling(model, tokenizer, blockSize);
                    
                    // Run a single request (or multiple in true concurrent scenario)
                    for (int i = 0; i < concurrency; i++)
                    {
                        var generated = sampler.Generate(
                            prompt: prompt,
                            maxNewTokens: maxTokens,
                            temperature: temperature,
                            topK: topK,
                            seed: SEED + i,
                            showPerf: false,
                            isPerfJsonMode: false,
                            metrics: metrics
                        );
                    }

                    metrics.Stop();
                    var summary = metrics.GetSummary(maxTokensRequested: maxTokens, concurrencyLevel: concurrency);
                    results.Add(BenchmarkResult.FromSummary(summary));

                    Console.WriteLine($"  Completed: {summary.TokensPerSecond:F2} tok/s\n");
                }
            }

            // Sort by throughput (descending)
            var sortedResults = results.OrderByDescending(r => r.TokensPerSecond).ToArray();

            // Print best throughput
            if (sortedResults.Length > 0)
            {
                Console.WriteLine(MetricsFormatter.FormatBestThroughput(sortedResults[0]));
            }

            // Print detailed results table
            Console.WriteLine(MetricsFormatter.FormatBenchmarkTable(sortedResults));
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

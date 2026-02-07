using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SmallMind.Abstractions;
using SmallMind.Core.Core;
using SmallMind.Engine;

namespace SmallMind.Examples.GgufInference
{
    /// <summary>
    /// Demo: Load a GGUF model and run inference with performance metrics.
    /// Usage: dotnet run -- path/to/model.gguf
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("=== SmallMind GGUF Inference Demo ===\n");

            // Parse command line arguments
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: dotnet run -- <path-to-gguf-file>");
                Console.WriteLine();
                Console.WriteLine("Example:");
                Console.WriteLine("  dotnet run -- SmolLM2-135M-Instruct-Q8_0.gguf");
                Console.WriteLine();
                Console.WriteLine("To download SmolLM2-135M-Instruct:");
                Console.WriteLine("  wget https://huggingface.co/second-state/SmolLM2-135M-Instruct-GGUF/resolve/main/SmolLM2-135M-Instruct-Q8_0.gguf");
                return 1;
            }

            string ggufPath = args[0];

            if (!File.Exists(ggufPath))
            {
                Console.WriteLine($"Error: File not found: {ggufPath}");
                return 1;
            }

            Console.WriteLine($"Loading model from: {ggufPath}");
            Console.WriteLine();

            try
            {
                // Create engine with default options
                var engineOptions = new SmallMindOptions
                {
                    EnableRag = false,
                    EnableKvCache = true,
                    EnableBatching = false
                };

                using var engine = new SmallMindEngine(engineOptions);

                // Load model
                Console.WriteLine("Importing GGUF to SMQ format (cached)...");
                var loadRequest = new ModelLoadRequest
                {
                    Path = ggufPath,
                    AllowGgufImport = true
                };

                var modelHandle = await engine.LoadModelAsync(loadRequest);
                Console.WriteLine("✓ Model loaded successfully");
                Console.WriteLine();

                // Display model info
                DisplayModelInfo(modelHandle);

                // Run inference with metrics
                await RunInferenceDemo(engine, modelHandle);

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
                Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
                return 1;
            }
        }

        static void DisplayModelInfo(IModelHandle modelHandle)
        {
            Console.WriteLine("=== Model Information ===");
            
            // Cast to get internal details
            if (modelHandle is ModelHandle internalHandle)
            {
                Console.WriteLine($"Tokenizer: {internalHandle.Tokenizer.Info.Name}");
                Console.WriteLine($"Vocabulary size: {internalHandle.Tokenizer.Info.VocabSize:N0}");
                Console.WriteLine($"BOS token ID: {internalHandle.Tokenizer.Info.BosTokenId}");
                Console.WriteLine($"EOS token ID: {internalHandle.Tokenizer.Info.EosTokenId}");
            }
            Console.WriteLine();
        }

        static async Task RunInferenceDemo(ISmallMindEngine engine, IModelHandle modelHandle)
        {
            Console.WriteLine("=== Running Inference Demo ===");
            Console.WriteLine();

            // Test prompts
            string[] prompts = new[]
            {
                "The capital of France is",
                "Once upon a time",
                "Q: What is 2+2?\nA:"
            };

            // Generation options
            var options = new GenerationOptions
            {
                MaxNewTokens = 30,
                Temperature = 0.8,
                TopP = 0.95,
                TopK = 40
            };

            var metrics = new PerformanceMetrics();

            foreach (var prompt in prompts)
            {
                Console.WriteLine($"Prompt: \"{prompt}\"");
                Console.Write("Output: \"");

                metrics.Start();
                int requestId = metrics.RecordRequestStart();
                metrics.RecordInferenceStart(requestId);

                var stopwatch = Stopwatch.StartNew();
                bool firstToken = true;
                int tokenCount = 0;
                long ttftMs = 0;

                var request = new GenerationRequest
                {
                    Prompt = prompt,
                    Options = options
                };

                try
                {
                    // Stream tokens
                    await foreach (var tokenEvent in engine.GenerateStreamingAsync(modelHandle, request))
                    {
                        if (tokenEvent.Kind == TokenEventKind.Token)
                        {
                            Console.Write(tokenEvent.Text.ToString());
                            tokenCount++;

                            if (firstToken)
                            {
                                ttftMs = stopwatch.ElapsedMilliseconds;
                                metrics.RecordFirstToken(requestId);
                                firstToken = false;
                            }
                        }
                        else if (tokenEvent.Kind == TokenEventKind.Completed)
                        {
                            break;
                        }
                        else if (tokenEvent.Kind == TokenEventKind.Error)
                        {
                            Console.WriteLine($"\nError during generation");
                            break;
                        }
                    }

                    stopwatch.Stop();

                    // Record completion
                    if (modelHandle is ModelHandle internalHandle)
                    {
                        var promptTokens = internalHandle.Tokenizer.Encode(prompt);
                        metrics.RecordRequestComplete(requestId, promptTokens.Count, tokenCount);
                    }

                    Console.WriteLine("\"");
                    Console.WriteLine();

                    // Display metrics for this generation
                    DisplayGenerationMetrics(ttftMs, tokenCount, stopwatch.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nGeneration error: {ex.Message}");
                }

                Console.WriteLine();
            }

            metrics.Stop();

            // Display overall metrics
            DisplayOverallMetrics(metrics);
        }

        static void DisplayGenerationMetrics(long ttftMs, int tokens, long totalMs)
        {
            Console.WriteLine($"  TTFT: {ttftMs}ms");
            Console.WriteLine($"  Tokens: {tokens}");
            Console.WriteLine($"  Total time: {totalMs}ms");
            
            if (totalMs > 0 && tokens > 0)
            {
                double tokensPerSec = (tokens * 1000.0) / totalMs;
                Console.WriteLine($"  Throughput: {tokensPerSec:F2} tok/s");
            }
        }

        static void DisplayOverallMetrics(PerformanceMetrics metrics)
        {
            var summary = metrics.GetSummary();

            Console.WriteLine("=== Overall Performance ===");
            Console.WriteLine($"Completed requests: {summary.CompletedRequests}");
            Console.WriteLine($"Total input tokens: {summary.TotalInputTokens:N0}");
            Console.WriteLine($"Total output tokens: {summary.TotalOutputTokens:N0}");
            Console.WriteLine($"Average throughput: {summary.TokensPerSecond:F2} tok/s");
            Console.WriteLine($"TTFT p50: {summary.TtftStats.P50:F1}ms");
            Console.WriteLine($"TTFT p95: {summary.TtftStats.P95:F1}ms");
            Console.WriteLine($"TTFT p99: {summary.TtftStats.P99:F1}ms");
            Console.WriteLine();

            // Display memory usage
            var process = Process.GetCurrentProcess();
            var workingSetMB = process.WorkingSet64 / 1024.0 / 1024.0;
            var privateMemMB = process.PrivateMemorySize64 / 1024.0 / 1024.0;

            Console.WriteLine("=== Memory Usage ===");
            Console.WriteLine($"Working set: {workingSetMB:F1} MB");
            Console.WriteLine($"Private memory: {privateMemMB:F1} MB");
            Console.WriteLine();
        }
    }
}

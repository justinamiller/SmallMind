using System;
using System.Threading;
using System.Threading.Tasks;
using SmallMind;

namespace GoldenPath
{
    /// <summary>
    /// Golden Path example demonstrating the stable SmallMind API.
    /// Shows: engine creation, capabilities check, text generation, streaming, and cancellation.
    /// </summary>
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Public API - Golden Path Example ===\n");

            // Get model path from arguments or use default
            string modelPath = args.Length > 0 ? args[0] : "model.smq";

            if (!File.Exists(modelPath))
            {
                Console.WriteLine($"Error: Model file not found: {modelPath}");
                Console.WriteLine("\nUsage: dotnet run <model-path>");
                Console.WriteLine("\nNote: This example requires a trained model (.smq or .gguf file).");
                Console.WriteLine("You can train a model using the SmallMind.Console tool or use a pretrained model.");
                return;
            }

            // Example 1: Create engine and check capabilities
            Console.WriteLine("1. Creating engine and checking capabilities...");
            await DemonstrateEngineCreation(modelPath);

            // Example 2: Non-streaming generation
            Console.WriteLine("\n2. Demonstrating non-streaming generation...");
            await DemonstrateNonStreamingGeneration(modelPath);

            // Example 3: Streaming generation
            Console.WriteLine("\n3. Demonstrating streaming generation...");
            await DemonstrateStreamingGeneration(modelPath);

            // Example 4: Cancellation support
            Console.WriteLine("\n4. Demonstrating cancellation support...");
            await DemonstrateCancellation(modelPath);

            // Example 5: Error handling
            Console.WriteLine("\n5. Demonstrating error handling...");
            await DemonstrateErrorHandling();

            // Example 6: Diagnostics
            Console.WriteLine("\n6. Demonstrating diagnostics...");
            await DemonstrateDiagnostics(modelPath);

            Console.WriteLine("\n=== All examples completed successfully! ===");
        }

        static async Task DemonstrateEngineCreation(string modelPath)
        {
            // Create engine with options
            var options = new SmallMindOptions
            {
                ModelPath = modelPath,
                MaxContextTokens = 2048,
                EnableKvCache = true,
                AllowGgufImport = true,
                ThreadCount = Environment.ProcessorCount,
                RequestTimeoutMs = 30000 // 30 second timeout
            };

            try
            {
                using var engine = SmallMindFactory.Create(options);

                // Get capabilities
                var caps = engine.GetCapabilities();

                Console.WriteLine($"✓ Engine created successfully!");
                Console.WriteLine($"  - Max Context: {caps.MaxContextTokens} tokens");
                Console.WriteLine($"  - Model Format: {caps.ModelFormat}");
                Console.WriteLine($"  - Quantization: {caps.Quantization}");
                Console.WriteLine($"  - Supports Streaming: {caps.SupportsStreaming}");
                Console.WriteLine($"  - Supports KV Cache: {caps.SupportsKvCache}");
                Console.WriteLine($"  - Supports Embeddings: {caps.SupportsEmbeddings}");
            }
            catch (SmallMindException ex)
            {
                Console.WriteLine($"✗ Failed to create engine: {ex.Message}");
                Console.WriteLine($"  Error Code: {ex.Code}");
                throw;
            }
        }

        static async Task DemonstrateNonStreamingGeneration(string modelPath)
        {
            var options = new SmallMindOptions
            {
                ModelPath = modelPath,
                MaxContextTokens = 2048,
                EnableKvCache = true
            };

            using var engine = SmallMindFactory.Create(options);

            // Create a text generation session
            var sessionOptions = new TextGenerationOptions
            {
                Temperature = 0.8f,
                TopP = 0.95f,
                TopK = 40,
                MaxOutputTokens = 50
            };

            using var session = engine.CreateTextGenerationSession(sessionOptions);

            // Generate text
            var request = new TextGenerationRequest
            {
                Prompt = "Once upon a time".AsMemory()
            };

            try
            {
                var result = session.Generate(request);

                Console.WriteLine($"✓ Generated text:");
                Console.WriteLine($"  Prompt: \"Once upon a time\"");
                Console.WriteLine($"  Output: \"{result.Text}\"");
                Console.WriteLine($"  Tokens: {result.Usage.CompletionTokens}");
                Console.WriteLine($"  Duration: {result.Timings.TotalMs:F2}ms");
                Console.WriteLine($"  Speed: {result.Timings.TokensPerSecond:F2} tokens/sec");
                Console.WriteLine($"  Finish Reason: {result.FinishReason}");
            }
            catch (SmallMindException ex)
            {
                Console.WriteLine($"✗ Generation failed: {ex.Message}");
                Console.WriteLine($"  Error Code: {ex.Code}");
            }

            await Task.CompletedTask;
        }

        static async Task DemonstrateStreamingGeneration(string modelPath)
        {
            var options = new SmallMindOptions
            {
                ModelPath = modelPath,
                MaxContextTokens = 2048,
                EnableKvCache = true
            };

            using var engine = SmallMindFactory.Create(options);

            var sessionOptions = new TextGenerationOptions
            {
                Temperature = 1.0f,
                MaxOutputTokens = 30
            };

            using var session = engine.CreateTextGenerationSession(sessionOptions);

            var request = new TextGenerationRequest
            {
                Prompt = "The quick brown fox".AsMemory()
            };

            try
            {
                Console.Write($"✓ Streaming generation:\n  Prompt: \"The quick brown fox\"\n  Output: \"");

                int tokenCount = 0;
                await foreach (var token in session.GenerateStreaming(request))
                {
                    Console.Write(token.TokenText);
                    tokenCount++;
                }

                Console.WriteLine($"\"\n  Total tokens: {tokenCount}");
            }
            catch (SmallMindException ex)
            {
                Console.WriteLine($"\n✗ Streaming failed: {ex.Message}");
                Console.WriteLine($"  Error Code: {ex.Code}");
            }
        }

        static async Task DemonstrateCancellation(string modelPath)
        {
            var options = new SmallMindOptions
            {
                ModelPath = modelPath,
                MaxContextTokens = 2048
            };

            using var engine = SmallMindFactory.Create(options);

            var sessionOptions = new TextGenerationOptions
            {
                MaxOutputTokens = 100
            };

            using var session = engine.CreateTextGenerationSession(sessionOptions);

            var request = new TextGenerationRequest
            {
                Prompt = "This is a test prompt".AsMemory()
            };

            // Cancel after 100ms
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(100);

            try
            {
                Console.Write($"✓ Testing cancellation (will cancel after 100ms)...\n  ");

                int tokenCount = 0;
                await foreach (var token in session.GenerateStreaming(request, cts.Token))
                {
                    Console.Write(".");
                    tokenCount++;
                }

                Console.WriteLine($"\n  Completed without cancellation ({tokenCount} tokens)");
            }
            catch (RequestCancelledException ex)
            {
                Console.WriteLine($"\n  ✓ Cancellation handled correctly: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Unexpected error: {ex.Message}");
            }
        }

        static async Task DemonstrateErrorHandling()
        {
            // Demonstrate various error scenarios

            // 1. Invalid model path
            try
            {
                var options = new SmallMindOptions
                {
                    ModelPath = "nonexistent.smq",
                    MaxContextTokens = 2048
                };

                using var engine = SmallMindFactory.Create(options);
                Console.WriteLine("✗ Should have thrown InvalidOptionsException");
            }
            catch (InvalidOptionsException ex)
            {
                Console.WriteLine($"✓ Caught InvalidOptionsException: {ex.OptionName}");
            }

            // 2. Invalid temperature
            var validOptions = new SmallMindOptions
            {
                ModelPath = "dummy.smq", // Won't be created due to earlier validation
                MaxContextTokens = 2048
            };

            try
            {
                // This will fail on model path validation first
                using var engine = SmallMindFactory.Create(validOptions);

                var sessionOptions = new TextGenerationOptions
                {
                    Temperature = 5.0f, // Invalid!
                    MaxOutputTokens = 100
                };

                var session = engine.CreateTextGenerationSession(sessionOptions);
                Console.WriteLine("✗ Should have thrown InvalidOptionsException");
            }
            catch (InvalidOptionsException ex)
            {
                Console.WriteLine($"✓ Caught invalid option error: {ex.OptionName}");
            }

            await Task.CompletedTask;
        }

        static async Task DemonstrateDiagnostics(string modelPath)
        {
            // Create a simple diagnostics sink
            var diagnosticsSink = new ConsoleDiagnosticsSink();

            var options = new SmallMindOptions
            {
                ModelPath = modelPath,
                MaxContextTokens = 2048,
                DiagnosticsSink = diagnosticsSink
            };

            Console.WriteLine("✓ Creating engine with diagnostics enabled...");

            try
            {
                using var engine = SmallMindFactory.Create(options);

                var sessionOptions = new TextGenerationOptions
                {
                    MaxOutputTokens = 10
                };

                using var session = engine.CreateTextGenerationSession(sessionOptions);

                var request = new TextGenerationRequest
                {
                    Prompt = "Hello".AsMemory()
                };

                _ = session.Generate(request);

                Console.WriteLine($"  Total diagnostic events: {diagnosticsSink.EventCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error during diagnostics demo: {ex.Message}");
            }

            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Simple diagnostics sink that writes to console.
    /// </summary>
    class ConsoleDiagnosticsSink : ISmallMindDiagnosticsSink
    {
        private int _eventCount;

        public int EventCount => _eventCount;

        public void OnEvent(in SmallMindDiagnosticEvent e)
        {
            Interlocked.Increment(ref _eventCount);

            var message = e.EventType switch
            {
                DiagnosticEventType.EngineCreated => "  [Diag] Engine created",
                DiagnosticEventType.ModelLoaded => "  [Diag] Model loaded",
                DiagnosticEventType.SessionCreated => "  [Diag] Session created",
                DiagnosticEventType.RequestStarted => "  [Diag] Request started",
                DiagnosticEventType.RequestCompleted => $"  [Diag] Request completed ({e.DurationMs:F2}ms, {e.CompletionTokens} tokens)",
                DiagnosticEventType.TokenEmitted => null, // Too noisy
                _ => $"  [Diag] {e.EventType}"
            };

            if (message != null)
            {
                Console.WriteLine(message);
            }
        }
    }
}

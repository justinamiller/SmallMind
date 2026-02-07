using System;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using SmallMind.Core.Core;

namespace ProductionInference
{
    /// <summary>
    /// Production-grade inference example demonstrating:
    /// - Resource governance (timeouts, limits, cancellation)
    /// - Deterministic generation for testing/debugging
    /// - Streaming token generation
    /// - Concurrent request handling
    /// - Memory estimation
    /// - Performance metrics
    /// </summary>
    class Program
    {
        private const string Vocabulary = "abcdefghijklmnopqrstuvwxyz ,.-!?'";

        static async Task Main(string[] args)
        {
            Console.WriteLine("SmallMind Production Inference Example");
            Console.WriteLine("======================================\n");

            // Create a small model for demonstration
            var (model, tokenizer) = CreateDemoModel();

            // Example 1: Basic generation with resource limits
            await Example1_BasicGenerationWithLimits(model, tokenizer);

            // Example 2: Deterministic generation (same seed = same output)
            await Example2_DeterministicGeneration(model, tokenizer);

            // Example 3: Streaming token generation
            await Example3_StreamingGeneration(model, tokenizer);

            // Example 4: Concurrent requests with bounded resources
            await Example4_ConcurrentRequests(model, tokenizer);

            // Example 5: Memory estimation
            Example5_MemoryEstimation(model);

            Console.WriteLine("\n✓ All examples completed successfully!");
        }

        private static (TransformerModel model, ITokenizer tokenizer) CreateDemoModel()
        {
            Console.WriteLine("Creating demo model...");

            int vocabSize = Vocabulary.Length;
            int blockSize = 64;
            int nEmbd = 32;
            int nLayer = 3;
            int nHead = 4;
            double dropout = 0.0;
            int seed = 42;

            var model = new TransformerModel(vocabSize, blockSize, nEmbd, nLayer, nHead, dropout, seed);
            var tokenizer = new CharTokenizer(Vocabulary);

            Console.WriteLine($"✓ Model created: {vocabSize} vocab, {blockSize} block size, {nEmbd} embd\n");

            return (model, tokenizer);
        }

        private static async Task Example1_BasicGenerationWithLimits(TransformerModel model, ITokenizer tokenizer)
        {
            Console.WriteLine("Example 1: Basic Generation with Resource Limits");
            Console.WriteLine("------------------------------------------------");

            var options = new ProductionInferenceOptions
            {
                MaxInputTokens = 100,
                MaxNewTokens = 20,
                MaxTimeMs = 5000, // 5 second timeout
                Temperature = 0.8,
                TopK = 10,
                Seed = 123 // Deterministic for this example
            };

            using var session = new InferenceSession(model, tokenizer, options, model.BlockSize);

            var prompt = "hello world";
            Console.WriteLine($"Prompt: \"{prompt}\"");

            var result = await session.GenerateAsync(prompt);

            Console.WriteLine($"Result: \"{result}\"");
            Console.WriteLine($"Session ID: {session.SessionId}\n");
        }

        private static async Task Example2_DeterministicGeneration(TransformerModel model, ITokenizer tokenizer)
        {
            Console.WriteLine("Example 2: Deterministic Generation (Reproducibility)");
            Console.WriteLine("----------------------------------------------------");

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 15,
                Temperature = 1.0,
                Seed = 999 // Fixed seed
            };

            var prompt = "the quick brown";
            Console.WriteLine($"Prompt: \"{prompt}\"");
            Console.WriteLine("Running generation twice with same seed...\n");

            // First generation
            using (var session1 = new InferenceSession(model, tokenizer, options, model.BlockSize))
            {
                var result1 = await session1.GenerateAsync(prompt);
                Console.WriteLine($"Run 1: \"{result1}\"");
            }

            // Second generation with same seed - should be identical
            using (var session2 = new InferenceSession(model, tokenizer, options, model.BlockSize))
            {
                var result2 = await session2.GenerateAsync(prompt);
                Console.WriteLine($"Run 2: \"{result2}\"");
            }

            Console.WriteLine("✓ Both runs should produce identical output\n");
        }

        private static async Task Example3_StreamingGeneration(TransformerModel model, ITokenizer tokenizer)
        {
            Console.WriteLine("Example 3: Streaming Token Generation");
            Console.WriteLine("-------------------------------------");

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 25,
                Temperature = 0.7,
                Seed = 456
            };

            using var session = new InferenceSession(model, tokenizer, options, model.BlockSize);

            var prompt = "once upon a time";
            Console.WriteLine($"Prompt: \"{prompt}\"");
            Console.Write("Streaming: \"");

            await foreach (var token in session.GenerateStreamAsync(prompt))
            {
                Console.Write(token.Text);
                
                // Simulate real-time display
                await Task.Delay(50);
            }

            Console.WriteLine("\"\n");
        }

        private static async Task Example4_ConcurrentRequests(TransformerModel model, ITokenizer tokenizer)
        {
            Console.WriteLine("Example 4: Concurrent Request Handling");
            Console.WriteLine("--------------------------------------");

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 0.8,
                Seed = null // Non-deterministic for variety
            };

            // Create engine with concurrency limit
            using var engine = new InferenceEngine(model, tokenizer, model.BlockSize, maxConcurrentSessions: 3);

            Console.WriteLine($"Engine max concurrent sessions: {engine.MaxConcurrentSessions}");

            // Create performance metrics
            var metrics = new PerformanceMetrics();
            metrics.Start();

            // Launch 5 concurrent requests (will queue beyond limit of 3)
            var prompts = new[] { "hello", "world", "test", "example", "demo" };
            var tasks = new Task<string>[prompts.Length];

            for (int i = 0; i < prompts.Length; i++)
            {
                int index = i; // Capture for closure
                tasks[i] = Task.Run(async () =>
                {
                    var result = await engine.GenerateAsync(prompts[index], options, metrics);
                    Console.WriteLine($"  Request {index + 1} completed: \"{prompts[index]}\" -> \"{result.Substring(0, Math.Min(30, result.Length))}...\"");
                    return result;
                });
            }

            // Wait for all to complete
            await Task.WhenAll(tasks);

            metrics.Stop();

            // Show statistics
            var stats = engine.GetStatistics();
            var summary = metrics.GetSummary();

            Console.WriteLine($"\n✓ Completed {summary.CompletedRequests} requests");
            Console.WriteLine($"  Total tokens generated: {summary.TotalOutputTokens}");
            Console.WriteLine($"  Tokens/sec: {summary.TokensPerSecond:F2}");
            Console.WriteLine($"  Active sessions: {stats.ActiveSessions}\n");
        }

        private static void Example5_MemoryEstimation(TransformerModel model)
        {
            Console.WriteLine("Example 5: Memory Estimation");
            Console.WriteLine("---------------------------");

            // Model configuration
            int nEmbd = 32;
            int nLayer = 3;
            int nHead = 4;
            long modelParams = model.Parameters.Count * 100; // Rough estimate

            var options = new ProductionInferenceOptions
            {
                MaxInputTokens = 512,
                MaxContextTokens = 512,
                MaxNewTokens = 50
            };

            // Estimate memory for single session
            long sessionBytes = MemoryEstimator.EstimateSessionBytes(modelParams, options, nEmbd, nLayer, nHead);
            Console.WriteLine($"Single session estimate: {MemoryEstimator.FormatBytes(sessionBytes)}");

            // Estimate KV cache
            long kvCacheBytes = MemoryEstimator.EstimateKvCacheBytes(options.MaxContextTokens, nEmbd, nLayer, nHead);
            Console.WriteLine($"KV cache per session: {MemoryEstimator.FormatBytes(kvCacheBytes)}");

            // Estimate for 10 concurrent sessions
            long engineBytes = MemoryEstimator.EstimateEngineBytes(modelParams, options, nEmbd, nLayer, nHead, 10);
            Console.WriteLine($"10 concurrent sessions: {MemoryEstimator.FormatBytes(engineBytes)}");

            Console.WriteLine();
        }
    }
}

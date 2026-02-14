using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using SmallMind.Core.Core;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Benchmark for new inference features: Top-P, Min-P, repetition penalties, stop conditions.
    /// Measures tokens/sec and allocations/token with and without these features enabled.
    /// Pure BCL - no BenchmarkDotNet dependency.
    /// </summary>
    class InferenceFeaturesBenchmark
    {
        // Simple mock tokenizer for benchmarking
        private class BenchmarkTokenizer : ITokenizer
        {
            private readonly int _vocabSize;

            public BenchmarkTokenizer(int vocabSize)
            {
                _vocabSize = vocabSize;
            }

            public int VocabSize => _vocabSize;

            public TokenizerInfo Info => new TokenizerInfo(
                name: "BenchmarkTokenizer",
                vocabSize: _vocabSize,
                bosTokenId: -1,
                eosTokenId: 0,
                padTokenId: -1,
                unkTokenId: -1,
                supportsByteFallback: false
            );

            public List<int> Encode(string text)
            {
                // Simple encoding: each word as a token
                var tokens = new List<int>();
                var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                foreach (var word in words)
                {
                    int tokenId = Math.Abs(word.GetHashCode()) % _vocabSize;
                    tokens.Add(tokenId);
                }
                return tokens.Count > 0 ? tokens : new List<int> { 1 };
            }

            public int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
            {
                throw new NotImplementedException();
            }

            public int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out)
            {
                throw new NotImplementedException();
            }

            public string Decode(List<int> tokens)
            {
                // Simple decode: token ID to character
                var chars = new char[tokens.Count];
                for (int i = 0; i < tokens.Count; i++)
                {
                    chars[i] = (char)('a' + (tokens[i] % 26));
                }
                return new string(chars);
            }

            public string DecodeToString(ReadOnlySpan<int> tokens)
            {
                var list = new List<int>(tokens.ToArray());
                return Decode(list);
            }
        }

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Inference Features Benchmark ===");
            Console.WriteLine($"Runtime: .NET {Environment.Version}");
            Console.WriteLine($"OS: {Environment.OSVersion}");
            Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
            Console.WriteLine();

            // Create a small model for benchmarking
            const int vocabSize = 1000;
            const int blockSize = 128;
            const int nEmbd = 128;
            const int nLayer = 2;
            const int nHead = 4;

            Console.WriteLine($"Model Configuration:");
            Console.WriteLine($"  Vocab Size: {vocabSize}");
            Console.WriteLine($"  Block Size: {blockSize}");
            Console.WriteLine($"  Embedding Dim: {nEmbd}");
            Console.WriteLine($"  Layers: {nLayer}");
            Console.WriteLine($"  Heads: {nHead}");
            Console.WriteLine();

            var model = new TransformerModel(
                vocabSize: vocabSize,
                blockSize: blockSize,
                nEmbd: nEmbd,
                nLayer: nLayer,
                nHead: nHead,
                dropout: 0.0,
                seed: 42
            );

            var tokenizer = new BenchmarkTokenizer(vocabSize);

            // Run benchmarks
            await BenchmarkBaseline(model, tokenizer);
            await BenchmarkTopP(model, tokenizer);
            await BenchmarkMinP(model, tokenizer);
            await BenchmarkRepetitionPenalties(model, tokenizer);
            await BenchmarkAllFeatures(model, tokenizer);

            Console.WriteLine("\n=== Benchmark Complete ===");
        }

        static async Task BenchmarkBaseline(TransformerModel model, ITokenizer tokenizer)
        {
            Console.WriteLine("--- BASELINE: No Additional Features ---");
            
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 50,
                Temperature = 1.0,
                TopK = 0,
                TopP = 1.0, // Disabled
                MinP = 0.0, // Disabled
                RepetitionPenalty = 1.0f, // Disabled
                Seed = 42
            };

            await RunBenchmark("Baseline", model, tokenizer, options, iterations: 10);
        }

        static async Task BenchmarkTopP(TransformerModel model, ITokenizer tokenizer)
        {
            Console.WriteLine("--- WITH TOP-P (nucleus sampling = 0.9) ---");
            
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 50,
                Temperature = 1.0,
                TopK = 0,
                TopP = 0.9, // Enabled
                MinP = 0.0,
                RepetitionPenalty = 1.0f,
                Seed = 42
            };

            await RunBenchmark("Top-P", model, tokenizer, options, iterations: 10);
        }

        static async Task BenchmarkMinP(TransformerModel model, ITokenizer tokenizer)
        {
            Console.WriteLine("--- WITH MIN-P (threshold = 0.05) ---");
            
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 50,
                Temperature = 1.0,
                TopK = 0,
                TopP = 1.0,
                MinP = 0.05, // Enabled
                RepetitionPenalty = 1.0f,
                Seed = 42
            };

            await RunBenchmark("Min-P", model, tokenizer, options, iterations: 10);
        }

        static async Task BenchmarkRepetitionPenalties(TransformerModel model, ITokenizer tokenizer)
        {
            Console.WriteLine("--- WITH REPETITION PENALTIES ---");
            
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 50,
                Temperature = 1.0,
                TopK = 0,
                TopP = 1.0,
                MinP = 0.0,
                RepetitionPenalty = 1.1f, // Enabled
                PresencePenalty = 0.5f, // Enabled
                FrequencyPenalty = 0.3f, // Enabled
                RepetitionWindow = 64,
                Seed = 42
            };

            await RunBenchmark("Repetition Penalties", model, tokenizer, options, iterations: 10);
        }

        static async Task BenchmarkAllFeatures(TransformerModel model, ITokenizer tokenizer)
        {
            Console.WriteLine("--- WITH ALL FEATURES ENABLED ---");
            
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 50,
                Temperature = 1.0,
                TopK = 40,
                TopP = 0.9,
                MinP = 0.05,
                RepetitionPenalty = 1.1f,
                PresencePenalty = 0.5f,
                FrequencyPenalty = 0.3f,
                RepetitionWindow = 64,
                Seed = 42
            };

            await RunBenchmark("All Features", model, tokenizer, options, iterations: 10);
        }

        static async Task RunBenchmark(string name, TransformerModel model, ITokenizer tokenizer, ProductionInferenceOptions options, int iterations)
        {
            const string prompt = "hello world this is a test";
            
            // Warmup
            {
                using var warmupSession = new InferenceSession(model, tokenizer, options, model.BlockSize);
                await warmupSession.GenerateAsync(prompt);
            }

            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);

            var sw = Stopwatch.StartNew();

            // Run benchmark
            for (int i = 0; i < iterations; i++)
            {
                using var session = new InferenceSession(model, tokenizer, options, model.BlockSize);
                await session.GenerateAsync(prompt);
            }

            sw.Stop();

            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocations = endAllocations - startAllocations;
            int gen0Collections = GC.CollectionCount(0) - startGen0;
            int gen1Collections = GC.CollectionCount(1) - startGen1;
            int gen2Collections = GC.CollectionCount(2) - startGen2;

            int totalTokens = iterations * options.MaxNewTokens;
            double avgTimePerToken = sw.Elapsed.TotalMilliseconds / totalTokens;
            double tokensPerSecond = totalTokens / sw.Elapsed.TotalSeconds;
            long allocationsPerToken = totalAllocations / totalTokens;

            Console.WriteLine($"Iterations: {iterations}, Tokens: {totalTokens}");
            Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Tokens/sec: {tokensPerSecond:F2}");
            Console.WriteLine($"Avg time per token: {avgTimePerToken:F3}ms");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total allocations: {totalAllocations / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"  Allocations per token: {allocationsPerToken / 1024.0:F2} KB");
            Console.WriteLine($"  Gen0 Collections: {gen0Collections}");
            Console.WriteLine($"  Gen1 Collections: {gen1Collections}");
            Console.WriteLine($"  Gen2 Collections: {gen2Collections}");
            Console.WriteLine();
        }
    }
}

using System;
using System.Diagnostics;
using SmallMind.Core.Core;
using SmallMind.Transformers;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Benchmark to measure allocation reduction in inference hot paths.
    /// Specifically measures GC pressure from MultiHeadAttention, MLP, and Transformer forward passes.
    /// </summary>
    class InferenceAllocationBenchmark
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Inference Allocation Benchmark ===");
            Console.WriteLine($"Runtime: .NET {Environment.Version}");
            Console.WriteLine($"OS: {Environment.OSVersion}");
            Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
            Console.WriteLine();
            
            // Run benchmarks
            BenchmarkMultiHeadAttentionForward();
            BenchmarkMLPForward();
            BenchmarkTransformerForward();
            // Note: KV cache decode path has a pre-existing bug - skipping for now
            // BenchmarkTokenGeneration();
            
            Console.WriteLine("\n=== Benchmark Complete ===");
        }
        
        static void BenchmarkMultiHeadAttentionForward()
        {
            Console.WriteLine("--- MultiHeadAttention Forward Pass Allocation Profile ---");
            Console.WriteLine("Measures allocations in attention mechanism (hottest path).\n");
            
            const int iterations = 100;
            const int B = 4;      // Batch size
            const int T = 64;     // Sequence length
            const int nEmbd = 256;
            const int nHead = 8;
            const int blockSize = 512;
            
            var random = new Random(42);
            var attention = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout: 0.0f, random);
            attention.Eval(); // Disable dropout for consistent results
            
            // Input: (B, T, n_embd)
            var input = new Tensor(new int[] { B, T, nEmbd }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)random.NextDouble();
            
            // Warmup
            for (int i = 0; i < 10; i++)
            {
                var _ = attention.Forward(input);
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
                var output = attention.Forward(input);
            }
            
            sw.Stop();
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocations = endAllocations - startAllocations;
            int gen0Collections = GC.CollectionCount(0) - startGen0;
            int gen1Collections = GC.CollectionCount(1) - startGen1;
            int gen2Collections = GC.CollectionCount(2) - startGen2;
            
            Console.WriteLine($"Configuration: B={B}, T={T}, nEmbd={nEmbd}, nHead={nHead}");
            Console.WriteLine($"Iterations: {iterations}");
            Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Avg time per forward: {sw.Elapsed.TotalMilliseconds / iterations:F3}ms");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total allocations: {totalAllocations / 1024.0:F2} KB ({totalAllocations / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"  Allocations per forward: {totalAllocations / iterations / 1024.0:F2} KB");
            Console.WriteLine($"  Gen0 Collections: {gen0Collections}");
            Console.WriteLine($"  Gen1 Collections: {gen1Collections}");
            Console.WriteLine($"  Gen2 Collections: {gen2Collections}");
            
            // Expected allocations before optimization:
            // - qShape, kShape, vShape: 3 * 4 * sizeof(int) = 48 bytes
            // - scoresShape: 4 * sizeof(int) = 16 bytes
            // - reshapedShape: 3 * sizeof(int) = 12 bytes
            // Total per forward: ~76 bytes (minimum, not counting hidden allocations)
            // With 100 iterations: ~7.6 KB
            
            Console.WriteLine("\nAnalysis:");
            if (totalAllocations < 1024) // Less than 1 KB
            {
                Console.WriteLine($"  ✓ Excellent! Near-zero allocations ({totalAllocations} bytes total)");
            }
            else if (totalAllocations < 10 * 1024) // Less than 10 KB
            {
                Console.WriteLine($"  ✓ Good! Minimal allocations");
            }
            else
            {
                Console.WriteLine($"  ⚠️  Higher than expected allocations");
            }
            
            if (gen0Collections == 0)
            {
                Console.WriteLine("  ✓ Zero Gen0 collections - excellent!");
            }
            
            Console.WriteLine();
        }
        
        static void BenchmarkMLPForward()
        {
            Console.WriteLine("--- MLP Forward Pass Allocation Profile ---");
            Console.WriteLine("Measures allocations in feed-forward network.\n");
            
            const int iterations = 100;
            const int B = 4;
            const int T = 64;
            const int nEmbd = 256;
            
            var random = new Random(42);
            var mlp = new MLP(nEmbd, dropout: 0.0f, random);
            mlp.Eval();
            
            var input = new Tensor(new int[] { B, T, nEmbd }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)random.NextDouble();
            
            // Warmup
            for (int i = 0; i < 10; i++)
            {
                var _ = mlp.Forward(input);
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var output = mlp.Forward(input);
            }
            
            sw.Stop();
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocations = endAllocations - startAllocations;
            int gen0Collections = GC.CollectionCount(0) - startGen0;
            
            Console.WriteLine($"Configuration: B={B}, T={T}, nEmbd={nEmbd}");
            Console.WriteLine($"Iterations: {iterations}");
            Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Avg time per forward: {sw.Elapsed.TotalMilliseconds / iterations:F3}ms");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total allocations: {totalAllocations / 1024.0:F2} KB");
            Console.WriteLine($"  Allocations per forward: {totalAllocations / iterations / 1024.0:F2} KB");
            Console.WriteLine($"  Gen0 Collections: {gen0Collections}");
            
            Console.WriteLine("\nAnalysis:");
            if (totalAllocations < 1024)
            {
                Console.WriteLine($"  ✓ Excellent! Near-zero allocations ({totalAllocations} bytes)");
            }
            else if (totalAllocations < 10 * 1024)
            {
                Console.WriteLine($"  ✓ Good! Minimal allocations");
            }
            
            if (gen0Collections == 0)
            {
                Console.WriteLine("  ✓ Zero Gen0 collections!");
            }
            
            Console.WriteLine();
        }
        
        static void BenchmarkTransformerForward()
        {
            Console.WriteLine("--- Transformer Forward Pass Allocation Profile ---");
            Console.WriteLine("Measures allocations in full transformer forward pass.\n");
            
            const int iterations = 50;
            const int B = 2;
            const int T = 32;
            const int vocabSize = 512;
            const int nEmbd = 128;
            const int nHead = 4;
            const int nLayer = 2;
            const int blockSize = 256;
            
            var random = new Random(42);
            var transformer = new TransformerModel(
                vocabSize: vocabSize,
                nEmbd: nEmbd,
                nHead: nHead,
                nLayer: nLayer,
                blockSize: blockSize,
                dropout: 0.0f,
                seed: 42
            );
            transformer.Eval();
            
            var input = new Tensor(new int[] { B, T }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = random.Next(0, vocabSize);
            
            // Warmup
            for (int i = 0; i < 5; i++)
            {
                var _ = transformer.Forward(input);
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            
            var sw = Stopwatch.StartNew();
            
            for (int i = 0; i < iterations; i++)
            {
                var output = transformer.Forward(input);
            }
            
            sw.Stop();
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocations = endAllocations - startAllocations;
            int gen0Collections = GC.CollectionCount(0) - startGen0;
            int gen1Collections = GC.CollectionCount(1) - startGen1;
            
            Console.WriteLine($"Configuration: B={B}, T={T}, nEmbd={nEmbd}, nHead={nHead}, nLayer={nLayer}");
            Console.WriteLine($"Iterations: {iterations}");
            Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Avg time per forward: {sw.Elapsed.TotalMilliseconds / iterations:F3}ms");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total allocations: {totalAllocations / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"  Allocations per forward: {totalAllocations / iterations / 1024.0:F2} KB");
            Console.WriteLine($"  Gen0 Collections: {gen0Collections}");
            Console.WriteLine($"  Gen1 Collections: {gen1Collections}");
            
            Console.WriteLine("\nAnalysis:");
            double allocPerForwardKB = totalAllocations / (double)iterations / 1024.0;
            if (allocPerForwardKB < 10)
            {
                Console.WriteLine($"  ✓ Excellent! Minimal per-forward allocations ({allocPerForwardKB:F2} KB)");
            }
            else if (allocPerForwardKB < 50)
            {
                Console.WriteLine($"  ✓ Good! Low allocations per forward");
            }
            else
            {
                Console.WriteLine($"  ⚠️  Consider further optimization");
            }
            
            if (gen0Collections <= 1)
            {
                Console.WriteLine("  ✓ Minimal GC pressure!");
            }
            
            Console.WriteLine();
        }
        
        static void BenchmarkTokenGeneration()
        {
            Console.WriteLine("--- Token Generation (Decode) Allocation Profile ---");
            Console.WriteLine("Measures allocations during autoregressive generation (T=1 per step).\n");
            
            const int numTokens = 100; // Generate 100 tokens
            const int B = 1;
            const int vocabSize = 512;
            const int nEmbd = 128;
            const int nHead = 4;
            const int nLayer = 2;
            const int blockSize = 256;
            
            var random = new Random(42);
            var transformer = new TransformerModel(
                vocabSize: vocabSize,
                nEmbd: nEmbd,
                nHead: nHead,
                nLayer: nLayer,
                blockSize: blockSize,
                dropout: 0.0f,
                seed: 42
            );
            transformer.Eval();
            
            // Enable KV cache for efficient generation
            transformer.EnableKVCache();
            
            // Start with a single token
            var currentToken = new Tensor(new int[] { B, 1 }, requiresGrad: false);
            currentToken.Data[0] = random.Next(0, vocabSize);
            
            // Warmup
            for (int i = 0; i < 10; i++)
            {
                var _ = transformer.Forward(currentToken);
            }
            transformer.ResetKVCache();
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            
            var sw = Stopwatch.StartNew();
            
            // Generate tokens one by one (decode path)
            for (int i = 0; i < numTokens; i++)
            {
                var logits = transformer.Forward(currentToken);
                
                // Simple argmax sampling (no softmax needed for benchmark)
                int nextToken = 0;
                float maxLogit = float.NegativeInfinity;
                for (int j = 0; j < vocabSize; j++)
                {
                    if (logits.Data[j] > maxLogit)
                    {
                        maxLogit = logits.Data[j];
                        nextToken = j;
                    }
                }
                
                currentToken.Data[0] = nextToken;
            }
            
            sw.Stop();
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocations = endAllocations - startAllocations;
            int gen0Collections = GC.CollectionCount(0) - startGen0;
            
            double tokensPerSecond = numTokens / sw.Elapsed.TotalSeconds;
            
            Console.WriteLine($"Configuration: nEmbd={nEmbd}, nHead={nHead}, nLayer={nLayer}");
            Console.WriteLine($"Tokens generated: {numTokens}");
            Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Tokens/sec: {tokensPerSecond:F2}");
            Console.WriteLine($"Avg time per token: {sw.Elapsed.TotalMilliseconds / numTokens:F3}ms");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total allocations: {totalAllocations / 1024.0:F2} KB");
            Console.WriteLine($"  Allocations per token: {totalAllocations / numTokens:F2} bytes");
            Console.WriteLine($"  Gen0 Collections: {gen0Collections}");
            
            Console.WriteLine("\nAnalysis:");
            double allocPerToken = totalAllocations / (double)numTokens;
            if (allocPerToken < 100)
            {
                Console.WriteLine($"  ✓ Excellent! Minimal per-token allocations ({allocPerToken:F2} bytes)");
            }
            else if (allocPerToken < 500)
            {
                Console.WriteLine($"  ✓ Good! Low allocations per token");
            }
            else
            {
                Console.WriteLine($"  ⚠️  Consider optimization for decode path");
            }
            
            if (gen0Collections == 0)
            {
                Console.WriteLine("  ✓ Zero Gen0 collections during generation!");
            }
            
            Console.WriteLine($"  Throughput: {tokensPerSecond:F2} tokens/sec");
            
            Console.WriteLine();
        }
    }
}

using SmallMind.Core;
using SmallMind.Text;
using System;

namespace SmallMind.Examples
{
    /// <summary>
    /// Demonstrates KV cache inference for efficient token generation.
    /// Shows performance comparison between standard and cached generation.
    /// </summary>
    class KVCacheExample
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind KV Cache Example ===\n");

            // Create a small model for demonstration
            Console.WriteLine("Creating model...");
            var model = new TransformerModel(
                vocabSize: 50,
                blockSize: 128,
                nEmbd: 128,
                nLayer: 4,
                nHead: 8,
                dropout: 0.0,
                seed: 42);
            
            model.Eval(); // Set to evaluation mode

            // Create simple character tokenizer
            const string vocab = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?;:'\"-\n";
            var tokenizer = new CharTokenizer(vocab);
            var sampling = new Sampling(model, tokenizer, model.BlockSize);

            string prompt = "The quick brown fox";
            int maxTokens = 50;
            double temperature = 0.8;
            int topK = 40;
            int seed = 123;

            Console.WriteLine($"\nPrompt: \"{prompt}\"");
            Console.WriteLine($"Generating {maxTokens} tokens...\n");

            // Generate WITHOUT KV cache
            Console.WriteLine("--- Standard Generation (No Cache) ---");
            var sw1 = System.Diagnostics.Stopwatch.StartNew();
            string output1 = sampling.Generate(
                prompt, maxTokens, temperature, topK, seed, showPerf: false);
            sw1.Stop();
            
            Console.WriteLine($"Output: {output1.Substring(0, Math.Min(100, output1.Length))}...");
            Console.WriteLine($"Time: {sw1.ElapsedMilliseconds}ms");
            Console.WriteLine($"Throughput: {maxTokens / sw1.Elapsed.TotalSeconds:F2} tokens/sec\n");

            // Generate WITH KV cache
            Console.WriteLine("--- KV Cache Generation ---");
            string output2 = sampling.GenerateWithCache(
                prompt, maxTokens, temperature, topK, seed, showPerf: true);

            // Verify outputs are identical
            Console.WriteLine($"\n--- Verification ---");
            if (output1 == output2)
            {
                Console.WriteLine("✓ Output matches non-cached version (deterministic)");
            }
            else
            {
                Console.WriteLine("✗ WARNING: Outputs differ!");
                Console.WriteLine($"  Non-cached: {output1.Substring(0, Math.Min(50, output1.Length))}...");
                Console.WriteLine($"  Cached:     {output2.Substring(0, Math.Min(50, output2.Length))}...");
            }

            Console.WriteLine("\n=== Summary ===");
            Console.WriteLine("KV cache eliminates redundant computation during auto-regressive generation.");
            Console.WriteLine("Benefits:");
            Console.WriteLine("  - Prefill phase: Process entire prompt once");
            Console.WriteLine("  - Decode phase: Only compute new token, reuse cached K/V");
            Console.WriteLine("  - Throughput: Faster token generation (especially for long prompts)");
            Console.WriteLine("  - Deterministic: Same outputs as standard generation");
            Console.WriteLine("  - Memory efficient: Pre-allocated buffers, no dynamic allocation");
        }
    }
}

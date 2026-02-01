using System;
using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Text;
using SmallMind.Tokenizers;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Performance harness for tokenizers.
    /// Measures tokens/sec, allocations, and time-to-first-token.
    /// </summary>
    public class TokenizerPerf
    {
        private const int WarmupIterations = 100;
        private const int BenchmarkIterations = 1000;

        public static void Main(string[] args)
        {
            Console.WriteLine("SmallMind Tokenizer Performance Harness");
            Console.WriteLine("========================================");
            Console.WriteLine($"Warmup iterations: {WarmupIterations}");
            Console.WriteLine($"Benchmark iterations: {BenchmarkIterations}");
            Console.WriteLine();

            // Sample texts for benchmarking
            string shortText = "Hello, World!";
            string mediumText = "The quick brown fox jumps over the lazy dog. " +
                               "Pack my box with five dozen liquor jugs. " +
                               "How vexingly quick daft zebras jump!";
            string longText = string.Join(" ", Enumerable.Repeat(mediumText, 10));

            // Create tokenizers
            var charTokenizer = new CharTokenizer(
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 !.,?;:'\"-()[]{}");
            
            var byteFallbackCharTokenizer = new ByteFallbackTokenizer(charTokenizer);

            Console.WriteLine("=== CharTokenizer ===");
            BenchmarkTokenizer(charTokenizer, shortText, "Short text");
            BenchmarkTokenizer(charTokenizer, mediumText, "Medium text");
            BenchmarkTokenizer(charTokenizer, longText, "Long text");
            Console.WriteLine();

            Console.WriteLine("=== ByteFallbackTokenizer(Char) ===");
            BenchmarkTokenizer(byteFallbackCharTokenizer, shortText, "Short text");
            BenchmarkTokenizer(byteFallbackCharTokenizer, mediumText, "Medium text");
            BenchmarkTokenizer(byteFallbackCharTokenizer, longText, "Long text");
            Console.WriteLine();

            Console.WriteLine("=== Span-based API Performance ===");
            BenchmarkSpanAPI(charTokenizer, mediumText);
            Console.WriteLine();

            Console.WriteLine("=== UTF-8 and Emoji Performance ===");
            string utf8Text = "Hello 世界! 🌍 Café résumé naïve";
            BenchmarkTokenizer(charTokenizer, utf8Text, "UTF-8 text");
            BenchmarkTokenizer(byteFallbackCharTokenizer, utf8Text, "UTF-8 text with fallback");
            Console.WriteLine();

            Console.WriteLine("Performance testing complete!");
        }

        static void BenchmarkTokenizer(ITokenizer tokenizer, string text, string label)
        {
            Console.WriteLine($"  {label} ({text.Length} chars):");

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                var _ = tokenizer.Encode(text);
            }

            // Measure encoding
            long startAlloc = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            
            int totalTokens = 0;
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                var tokens = tokenizer.Encode(text);
                totalTokens += tokens.Count;
            }
            
            sw.Stop();
            long endAlloc = GC.GetAllocatedBytesForCurrentThread();
            long allocatedBytes = endAlloc - startAlloc;

            double tokensPerSecond = (double)totalTokens / sw.Elapsed.TotalSeconds;
            double avgTimeMs = sw.Elapsed.TotalMilliseconds / BenchmarkIterations;
            double allocPerIteration = (double)allocatedBytes / BenchmarkIterations / 1024.0; // KB

            Console.WriteLine($"    Tokens/sec: {tokensPerSecond:N0}");
            Console.WriteLine($"    Avg time: {avgTimeMs:F3} ms");
            Console.WriteLine($"    Allocations: {allocPerIteration:F2} KB per iteration");
            Console.WriteLine($"    Total tokens: {totalTokens / BenchmarkIterations}");
            
            // Measure decoding
            var sampleTokens = tokenizer.Encode(text);
            sw.Restart();
            
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                var _ = tokenizer.Decode(sampleTokens);
            }
            
            sw.Stop();
            double decodeTimeMs = sw.Elapsed.TotalMilliseconds / BenchmarkIterations;
            Console.WriteLine($"    Decode time: {decodeTimeMs:F3} ms");
            Console.WriteLine();
        }

        static void BenchmarkSpanAPI(ITokenizer tokenizer, string text)
        {
            Console.WriteLine("  Span-based encoding (minimal allocations):");

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
            int[] tokenBuffer = ArrayPool<int>.Shared.Rent(utf8Bytes.Length * 2);
            byte[] decodeBuffer = ArrayPool<byte>.Shared.Rent(utf8Bytes.Length * 2);

            try
            {
                // Warmup
                for (int i = 0; i < WarmupIterations; i++)
                {
                    int count = tokenizer.Encode(utf8Bytes, tokenBuffer);
                }

                // Benchmark
                long startAlloc = GC.GetAllocatedBytesForCurrentThread();
                var sw = Stopwatch.StartNew();
                
                int totalTokens = 0;
                for (int i = 0; i < BenchmarkIterations; i++)
                {
                    int count = tokenizer.Encode(utf8Bytes, tokenBuffer);
                    totalTokens += count;
                }
                
                sw.Stop();
                long endAlloc = GC.GetAllocatedBytesForCurrentThread();
                long allocatedBytes = endAlloc - startAlloc;

                double tokensPerSecond = (double)totalTokens / sw.Elapsed.TotalSeconds;
                double avgTimeMs = sw.Elapsed.TotalMilliseconds / BenchmarkIterations;
                double allocPerIteration = (double)allocatedBytes / BenchmarkIterations / 1024.0;

                Console.WriteLine($"    Tokens/sec: {tokensPerSecond:N0}");
                Console.WriteLine($"    Avg time: {avgTimeMs:F3} ms");
                Console.WriteLine($"    Allocations: {allocPerIteration:F2} KB per iteration");
                Console.WriteLine($"    Speedup vs List<int> API: {CalculateSpeedup(tokenizer, text, avgTimeMs):F2}x");

                // Measure decode
                int tokenCount = tokenizer.Encode(utf8Bytes, tokenBuffer);
                sw.Restart();
                
                for (int i = 0; i < BenchmarkIterations; i++)
                {
                    int byteCount = tokenizer.Decode(tokenBuffer.AsSpan(0, tokenCount), decodeBuffer);
                }
                
                sw.Stop();
                double decodeTimeMs = sw.Elapsed.TotalMilliseconds / BenchmarkIterations;
                Console.WriteLine($"    Span decode time: {decodeTimeMs:F3} ms");
            }
            finally
            {
                ArrayPool<int>.Shared.Return(tokenBuffer);
                ArrayPool<byte>.Shared.Return(decodeBuffer);
            }
        }

        static double CalculateSpeedup(ITokenizer tokenizer, string text, double spanTimeMs)
        {
            // Measure List<int> API time
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < BenchmarkIterations; i++)
            {
                var _ = tokenizer.Encode(text);
            }
            sw.Stop();
            
            double listTimeMs = sw.Elapsed.TotalMilliseconds / BenchmarkIterations;
            return listTimeMs / spanTimeMs;
        }
    }
}

using System;
using SmallMind.Text;

namespace SmallMind.Examples
{
    /// <summary>
    /// Demonstrates the use of CharTokenizer, BpeTokenizer, and TokenizerFactory.
    /// </summary>
    class TokenizerExample
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Tokenizer Examples ===\n");

            string sampleText = "the quick brown fox jumps over the lazy dog.";

            // Example 1: CharTokenizer (default, simple)
            Console.WriteLine("1. CharTokenizer (Character-level):");
            var charTokenizer = new CharTokenizer(sampleText);
            Console.WriteLine($"   Vocabulary size: {charTokenizer.VocabSize}");
            
            var charTokens = charTokenizer.Encode(sampleText);
            Console.WriteLine($"   Encoded tokens: {charTokens.Count} tokens");
            Console.WriteLine($"   First 10 tokens: [{string.Join(", ", charTokens.GetRange(0, Math.Min(10, charTokens.Count)))}]");
            
            var charDecoded = charTokenizer.Decode(charTokens);
            Console.WriteLine($"   Decoded: \"{charDecoded}\"");
            Console.WriteLine($"   Round-trip successful: {charDecoded == sampleText}\n");

            // Example 2: BpeTokenizer (production, if assets exist)
            Console.WriteLine("2. BpeTokenizer (Byte Pair Encoding):");
            try
            {
                var bpeTokenizer = new BpeTokenizer("assets/tokenizers/default");
                Console.WriteLine($"   Vocabulary size: {bpeTokenizer.VocabSize}");
                
                var bpeTokens = bpeTokenizer.Encode(sampleText);
                Console.WriteLine($"   Encoded tokens: {bpeTokens.Count} tokens");
                Console.WriteLine($"   First 10 tokens: [{string.Join(", ", bpeTokens.GetRange(0, Math.Min(10, bpeTokens.Count)))}]");
                
                var bpeDecoded = bpeTokenizer.Decode(bpeTokens);
                Console.WriteLine($"   Decoded: \"{bpeDecoded}\"");
                Console.WriteLine($"   Round-trip successful: {bpeDecoded == sampleText}");
                Console.WriteLine($"   Compression ratio: {(double)charTokens.Count / bpeTokens.Count:F2}x\n");
            }
            catch (TokenizationException ex)
            {
                Console.WriteLine($"   BPE tokenizer not available: {ex.Message.Split('\n')[0]}\n");
            }

            // Example 3: TokenizerFactory with Auto mode
            Console.WriteLine("3. TokenizerFactory (Auto mode):");
            var autoOptions = new TokenizerOptions
            {
                Mode = TokenizerMode.Auto,
                TokenizerName = "default",
                Strict = false
            };
            
            var autoTokenizer = TokenizerFactory.Create(autoOptions, sampleText);
            Console.WriteLine($"   Selected tokenizer: {autoTokenizer.GetType().Name}");
            Console.WriteLine($"   Vocabulary size: {autoTokenizer.VocabSize}");
            
            var autoTokens = autoTokenizer.Encode(sampleText);
            Console.WriteLine($"   Encoded tokens: {autoTokens.Count} tokens\n");

            // Example 4: TokenizerFactory with explicit Char mode
            Console.WriteLine("4. TokenizerFactory (Explicit Char mode):");
            var charOptions = new TokenizerOptions
            {
                Mode = TokenizerMode.Char
            };
            
            var factoryCharTokenizer = TokenizerFactory.Create(charOptions, sampleText);
            Console.WriteLine($"   Selected tokenizer: {factoryCharTokenizer.GetType().Name}");
            Console.WriteLine($"   Vocabulary size: {factoryCharTokenizer.VocabSize}\n");

            // Example 5: TokenizerFactory with explicit BPE mode and fallback
            Console.WriteLine("5. TokenizerFactory (BPE mode with fallback):");
            var bpeOptions = new TokenizerOptions
            {
                Mode = TokenizerMode.Bpe,
                TokenizerName = "default",
                Strict = false  // Allow fallback to CharTokenizer
            };
            
            var factoryBpeTokenizer = TokenizerFactory.Create(bpeOptions, sampleText);
            Console.WriteLine($"   Selected tokenizer: {factoryBpeTokenizer.GetType().Name}");
            Console.WriteLine($"   Vocabulary size: {factoryBpeTokenizer.VocabSize}\n");

            // Example 6: TokenizerFactory with strict mode
            Console.WriteLine("6. TokenizerFactory (Strict mode):");
            var strictOptions = new TokenizerOptions
            {
                Mode = TokenizerMode.Bpe,
                TokenizerName = "nonexistent",
                Strict = true  // Throw exception if BPE assets not found
            };
            
            try
            {
                var strictTokenizer = TokenizerFactory.Create(strictOptions);
                Console.WriteLine($"   Selected tokenizer: {strictTokenizer.GetType().Name}");
            }
            catch (TokenizationException ex)
            {
                Console.WriteLine($"   Expected exception caught:");
                Console.WriteLine($"   {ex.Message.Split('\n')[0]}\n");
            }

            // Example 7: Backwards compatibility with Tokenizer class
            Console.WriteLine("7. Backwards compatibility (Tokenizer class):");
            var oldTokenizer = new Tokenizer(sampleText);  // Still works!
            Console.WriteLine($"   Tokenizer type: {oldTokenizer.GetType().Name}");
            Console.WriteLine($"   Is CharTokenizer: {oldTokenizer is CharTokenizer}");
            Console.WriteLine($"   Vocabulary size: {oldTokenizer.VocabSize}\n");

            Console.WriteLine("=== All examples completed successfully! ===");
        }
    }
}

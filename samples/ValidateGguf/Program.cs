using System;
using System.Diagnostics;
using System.Linq;
using SmallMind.Runtime;
using SmallMind.Transformers;

namespace SmallMind.ValidateGguf
{
    /// <summary>
    /// Validates GGUF model loading and text generation.
    /// Usage: dotnet run --model path/to/model.gguf --prompt "The capital of France is" --max-tokens 50
    /// </summary>
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                // Parse arguments
                string? modelPath = null;
                string prompt = "The capital of France is";
                int maxTokens = 50;
                int seed = 42;

                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] == "--model" && i + 1 < args.Length)
                        modelPath = args[i + 1];
                    else if (args[i] == "--prompt" && i + 1 < args.Length)
                        prompt = args[i + 1];
                    else if (args[i] == "--max-tokens" && i + 1 < args.Length)
                        maxTokens = int.Parse(args[i + 1]);
                    else if (args[i] == "--seed" && i + 1 < args.Length)
                        seed = int.Parse(args[i + 1]);
                }

                if (string.IsNullOrEmpty(modelPath))
                {
                    Console.WriteLine("GGUF Model Validation Tool");
                    Console.WriteLine("Usage: ValidateGguf --model <path> [--prompt <text>] [--max-tokens <n>] [--seed <n>]");
                    Console.WriteLine();
                    Console.WriteLine("Arguments:");
                    Console.WriteLine("  --model       Path to GGUF model file (required)");
                    Console.WriteLine("  --prompt      Prompt text (default: 'The capital of France is')");
                    Console.WriteLine("  --max-tokens  Maximum tokens to generate (default: 50)");
                    Console.WriteLine("  --seed        Random seed (default: 42)");
                    Console.WriteLine();
                    Console.WriteLine("Acceptance criteria:");
                    Console.WriteLine("  - Generate >= 20 tokens");
                    Console.WriteLine("  - Output must be coherent English (not NaNs/repeated junk)");
                    return 1;
                }

                Console.WriteLine("=== GGUF Model Validation ===");
                Console.WriteLine($"Model: {modelPath}");
                Console.WriteLine($"Prompt: \"{prompt}\"");
                Console.WriteLine($"Max tokens: {maxTokens}");
                Console.WriteLine($"Seed: {seed}");
                Console.WriteLine();

                // Load model
                Console.WriteLine("Loading model...");
                var sw = Stopwatch.StartNew();
                var (model, tokenizer, config) = GgufModelLoader.LoadFromGguf(modelPath, seed);
                sw.Stop();
                Console.WriteLine($"Model loaded in {sw.ElapsedMilliseconds}ms");
                Console.WriteLine();

                // Set model to eval mode
                model.Eval();
                model.EnableKVCache();

                // Encode prompt
                Console.WriteLine("Encoding prompt...");
                var tokens = tokenizer.Encode(prompt);
                
                // Optionally prepend BOS token if model expects it
                if (config.BosTokenId >= 0 && !tokens.Contains(config.BosTokenId))
                {
                    tokens.Insert(0, config.BosTokenId);
                    Console.WriteLine($"Prepended BOS token ({config.BosTokenId})");
                }

                Console.WriteLine($"Prompt tokens: {tokens.Count}");
                Console.WriteLine();

                // Generate
                Console.WriteLine("Generating tokens...");
                Console.WriteLine("---");
                Console.Write(prompt);

                var generatedTokens = new System.Collections.Generic.List<int>();
                var firstTokenTime = TimeSpan.Zero;
                var genSw = Stopwatch.StartNew();

                for (int i = 0; i < maxTokens; i++)
                {
                    // Create input tensor
                    var inputTensor = new SmallMind.Core.Core.Tensor(
                        tokens.Select(t => (float)t).ToArray(),
                        new int[] { 1, tokens.Count });

                    // Forward pass
                    var logits = model.Forward(inputTensor, positionOffset: generatedTokens.Count);

                    // Get last token logits
                    int vocabSize = config.VocabSize;
                    var lastLogits = new float[vocabSize];
                    int lastTokenOffset = (tokens.Count - 1) * vocabSize;
                    Array.Copy(logits.Data, lastTokenOffset, lastLogits, 0, vocabSize);

                    // Sample (greedy for deterministic output)
                    int nextToken = ArgMax(lastLogits);

                    // Check for NaN
                    if (float.IsNaN(lastLogits[nextToken]))
                    {
                        Console.WriteLine();
                        Console.WriteLine();
                        Console.WriteLine("FAIL: NaN detected in logits!");
                        return 1;
                    }

                    // Record first token time
                    if (i == 0)
                    {
                        firstTokenTime = genSw.Elapsed;
                    }

                    // Decode and print
                    var tokenText = tokenizer.Decode(new System.Collections.Generic.List<int> { nextToken });
                    Console.Write(tokenText);

                    generatedTokens.Add(nextToken);
                    tokens.Clear();
                    tokens.Add(nextToken);

                    // Check for EOS
                    if (nextToken == config.EosTokenId)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"(EOS after {generatedTokens.Count} tokens)");
                        break;
                    }
                }

                genSw.Stop();
                Console.WriteLine();
                Console.WriteLine("---");
                Console.WriteLine();

                // Report metrics
                Console.WriteLine("=== Generation Metrics ===");
                Console.WriteLine($"Generated tokens: {generatedTokens.Count}");
                Console.WriteLine($"Time to first token (TTFT): {firstTokenTime.TotalMilliseconds:F2}ms");
                Console.WriteLine($"Total generation time: {genSw.ElapsedMilliseconds}ms");
                if (generatedTokens.Count > 1)
                {
                    double tokensPerSec = (generatedTokens.Count - 1) / (genSw.Elapsed.TotalSeconds - firstTokenTime.TotalSeconds);
                    Console.WriteLine($"Tokens per second: {tokensPerSec:F2}");
                }
                Console.WriteLine();

                // Validate output
                Console.WriteLine("=== Validation ===");
                
                if (generatedTokens.Count < 20)
                {
                    Console.WriteLine($"FAIL: Generated only {generatedTokens.Count} tokens (expected >= 20)");
                    return 1;
                }

                var fullOutput = tokenizer.Decode(generatedTokens);
                
                // Check for NaN strings
                if (fullOutput.Contains("NaN") || fullOutput.Contains("nan"))
                {
                    Console.WriteLine("FAIL: Output contains NaN strings");
                    return 1;
                }

                // Check for excessive repetition (same character repeated > 50 times)
                if (HasExcessiveRepetition(fullOutput))
                {
                    Console.WriteLine("FAIL: Excessive character repetition detected");
                    return 1;
                }

                // Check for reasonable word count (at least 3 multi-letter words)
                var words = fullOutput.Split(new[] { ' ', '\n', '\t', '.', ',', '!', '?' }, 
                    StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length > 1)
                    .ToList();

                if (words.Count < 3)
                {
                    Console.WriteLine($"FAIL: Only {words.Count} multi-letter words (expected >= 3)");
                    return 1;
                }

                Console.WriteLine($"✓ Generated {generatedTokens.Count} tokens");
                Console.WriteLine($"✓ Contains {words.Count} multi-letter words");
                Console.WriteLine("✓ No NaN values detected");
                Console.WriteLine("✓ No excessive repetition");
                Console.WriteLine();
                Console.WriteLine("SUCCESS: Model validation passed!");
                
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"ERROR: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static int ArgMax(float[] array)
        {
            int maxIndex = 0;
            float maxValue = array[0];
            for (int i = 1; i < array.Length; i++)
            {
                if (array[i] > maxValue)
                {
                    maxValue = array[i];
                    maxIndex = i;
                }
            }
            return maxIndex;
        }

        static bool HasExcessiveRepetition(string text)
        {
            if (text.Length < 50) return false;

            int maxRepeat = 1;
            int currentRepeat = 1;
            char lastChar = text[0];

            for (int i = 1; i < text.Length; i++)
            {
                if (text[i] == lastChar)
                {
                    currentRepeat++;
                    maxRepeat = Math.Max(maxRepeat, currentRepeat);
                }
                else
                {
                    currentRepeat = 1;
                    lastChar = text[i];
                }
            }

            return maxRepeat > 50;
        }
    }
}

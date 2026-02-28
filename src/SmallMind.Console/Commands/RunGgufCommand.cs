using System.Diagnostics;
using SmallMind.Runtime;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// End-to-end GGUF validation command.
    /// Loads a GGUF model directly, runs inference, and validates output coherence.
    /// </summary>
    internal sealed class RunGgufCommand : ICommand
    {
        public string Name => "run-gguf";
        public string Description => "Load GGUF model and run inference validation";

        public async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 2)
            {
                ShowUsage();
                return 1;
            }

            string ggufPath = args[0];
            string prompt = args[1];

            // Parse optional arguments
            int maxTokens = 50;
            double temperature = 0.7;
            int seed = 42;

            for (int i = 2; i < args.Length; i++)
            {
                if (args[i] == "--max-tokens" && i + 1 < args.Length)
                {
                    i++;
                    if (int.TryParse(args[i], out int tokens))
                        maxTokens = tokens;
                }
                else if (args[i] == "--temperature" && i + 1 < args.Length)
                {
                    i++;
                    if (double.TryParse(args[i], out double temp))
                        temperature = temp;
                }
                else if (args[i] == "--seed" && i + 1 < args.Length)
                {
                    i++;
                    if (int.TryParse(args[i], out int s))
                        seed = s;
                }
            }

            // Print banner to stdout FIRST so there is always visible output
            System.Console.WriteLine($"=== GGUF Validation Test ===");
            System.Console.WriteLine($"Model: {Path.GetFileName(ggufPath)}");
            System.Console.WriteLine($"Path:  {ggufPath}");
            System.Console.WriteLine($"Prompt: \"{prompt}\"");
            System.Console.WriteLine($"Max tokens: {maxTokens}");
            System.Console.WriteLine($"Temperature: {temperature}");
            System.Console.WriteLine($"Seed: {seed}");
            System.Console.WriteLine();

            if (!File.Exists(ggufPath))
            {
                string msg = $"Error: GGUF file not found: {ggufPath}";
                System.Console.WriteLine(msg);
                System.Console.Error.WriteLine(msg);
                return 1;
            }

            try
            {
                // Load model from GGUF
                var loadStopwatch = Stopwatch.StartNew();
                System.Console.WriteLine("Loading GGUF model...");

                var (model, tokenizer, config) = GgufModelLoader.LoadFromGguf(ggufPath, seed);

                loadStopwatch.Stop();
                System.Console.WriteLine($"✓ Model loaded in {loadStopwatch.ElapsedMilliseconds}ms");
                System.Console.WriteLine();

                // Create inference session
                var options = new ProductionInferenceOptions
                {
                    MaxNewTokens = maxTokens,
                    Temperature = temperature,
                    TopK = 40,
                    TopP = 0.95,
                    RepetitionPenalty = 1.1f,
                    PresencePenalty = 0.1f,
                    RepetitionWindow = 64,
                    MaxRepeatedTokenStreak = 5,
                    Seed = seed,
                    MaxContextTokens = config.ContextLength
                };

                using var session = new InferenceSession(
                    model,
                    tokenizer,
                    options,
                    config.ContextLength);

                // Run generation
                var genStopwatch = Stopwatch.StartNew();
                System.Console.WriteLine("Generating...");
                System.Console.WriteLine("─".PadRight(60, '─'));

                string output = await session.GenerateAsync(prompt);

                genStopwatch.Stop();

                // Encode prompt and full output once; reuse for both token counting and display.
                // Decoding only the generated-token slice avoids the fragile string-length
                // comparison and is robust to Unicode normalization differences.
                var promptTokenIds = tokenizer.Encode(prompt);
                var outputTokenIds = tokenizer.Encode(output);
                int outputTokens = Math.Max(0, outputTokenIds.Count - promptTokenIds.Count);

                // Decode only the newly generated tokens for display.
                string generated = outputTokens > 0
                    ? tokenizer.Decode(outputTokenIds.GetRange(promptTokenIds.Count, outputTokens)).TrimStart()
                    : string.Empty;

                System.Console.WriteLine($"Prompt: {prompt}");
                System.Console.WriteLine($"Output: {generated}");
                System.Console.WriteLine("─".PadRight(60, '─'));
                System.Console.WriteLine();

                double tokensPerSec = outputTokens / (genStopwatch.ElapsedMilliseconds / 1000.0);

                System.Console.WriteLine($"Generation time: {genStopwatch.ElapsedMilliseconds}ms");
                System.Console.WriteLine($"Tokens generated: {outputTokens}");
                System.Console.WriteLine($"Speed: {tokensPerSec:F2} tok/s");
                System.Console.WriteLine();

                // Coherence check
                bool isCoherent = ValidateCoherence(output, prompt);

                if (isCoherent)
                {
                    System.Console.WriteLine("✓ PASS - Output is coherent English");
                    return 0;
                }
                else
                {
                    System.Console.WriteLine("✗ FAIL - Output appears to be garbage (likely non-English or random tokens)");
                    return 2;
                }
            }
            catch (Exception ex)
            {
                string msg = $"Error: {ex.Message}";
                System.Console.WriteLine(msg);
                System.Console.Error.WriteLine(msg);
                if (ex.InnerException != null)
                {
                    string inner = $"Inner: {ex.InnerException.Message}";
                    System.Console.WriteLine(inner);
                    System.Console.Error.WriteLine(inner);
                }
                System.Console.WriteLine();
                System.Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
                System.Console.Error.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        public void ShowUsage()
        {
            System.Console.WriteLine("Usage: smallmind run-gguf <gguf-file> <prompt> [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Arguments:");
            System.Console.WriteLine("  <gguf-file>    Path to GGUF model file");
            System.Console.WriteLine("  <prompt>       Text prompt for generation");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("  --max-tokens <n>      Maximum tokens to generate (default: 50)");
            System.Console.WriteLine("  --temperature <t>     Sampling temperature (default: 0.7)");
            System.Console.WriteLine("  --seed <s>            Random seed (default: 42)");
            System.Console.WriteLine();
            System.Console.WriteLine("Exit codes:");
            System.Console.WriteLine("  0 - Success: output is coherent");
            System.Console.WriteLine("  1 - Error: exception or usage error");
            System.Console.WriteLine("  2 - Failure: output appears to be garbage");
            System.Console.WriteLine();
            System.Console.WriteLine("Example:");
            System.Console.WriteLine("  smallmind run-gguf model.gguf \"The capital of France is\" --max-tokens 100");
        }

        /// <summary>
        /// Coherence check: validates that output contains reasonable English text.
        /// Checks for:
        /// - Sufficient length
        /// - Primarily ASCII printable characters
        /// - Contains alphabetic characters
        /// - Not mostly repeated characters or garbage
        /// - No repeated word streaks (e.g., "the the the")
        /// - No repeated n-gram phrases (bigram/trigram loops)
        /// - Sufficient lexical diversity (unique words / total words)
        /// </summary>
        internal bool ValidateCoherence(string output, string prompt)
        {
            // Extract generated portion (after prompt)
            string generated = output.Length > prompt.Length
                ? output.Substring(prompt.Length).TrimStart()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(generated))
            {
                System.Console.WriteLine("Coherence check: No output generated");
                return false;
            }

            // Check length
            if (generated.Length < 10)
            {
                System.Console.WriteLine("Coherence check: Output too short");
                return false;
            }

            // Count character types
            int alphaCount = 0;
            int printableCount = 0;
            int spaceCount = 0;

            foreach (char c in generated)
            {
                if (char.IsLetter(c))
                    alphaCount++;
                if (c >= 32 && c <= 126) // Printable ASCII
                    printableCount++;
                if (char.IsWhiteSpace(c))
                    spaceCount++;
            }

            // Check for reasonable English text
            double alphaPct = (double)alphaCount / generated.Length;
            double printablePct = (double)printableCount / generated.Length;
            double spacePct = (double)spaceCount / generated.Length;

            // English text should have:
            // - At least 40% alphabetic characters
            // - At least 80% printable ASCII
            // - 5-25% whitespace
            bool hasEnoughAlpha = alphaPct >= 0.4;
            bool mostlyPrintable = printablePct >= 0.8;
            bool reasonableSpacing = spacePct >= 0.05 && spacePct <= 0.25;

            if (!hasEnoughAlpha)
            {
                System.Console.WriteLine($"Coherence check: Too few alphabetic characters ({alphaPct:P0})");
                return false;
            }

            if (!mostlyPrintable)
            {
                System.Console.WriteLine($"Coherence check: Contains non-printable characters ({printablePct:P0} printable)");
                return false;
            }

            if (!reasonableSpacing)
            {
                System.Console.WriteLine($"Coherence check: Unusual spacing ({spacePct:P0})");
                return false;
            }

            // Check for excessive character repetition (same character repeated many times)
            int maxCharRepeat = 0;
            int currentRepeat = 1;
            char lastChar = '\0';

            foreach (char c in generated)
            {
                if (c == lastChar)
                {
                    currentRepeat++;
                    maxCharRepeat = Math.Max(maxCharRepeat, currentRepeat);
                }
                else
                {
                    currentRepeat = 1;
                    lastChar = c;
                }
            }

            if (maxCharRepeat > 20)
            {
                System.Console.WriteLine($"Coherence check: Excessive character repetition (max {maxCharRepeat})");
                return false;
            }

            // Tokenize into words for word-level checks
            var words = generated
                .Split(new char[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?', ';', ':' },
                       StringSplitOptions.RemoveEmptyEntries);

            if (words.Length >= 3)
            {
                // Check for repeated word streak (e.g., "the the the the the")
                int maxWordStreak = 0;
                int wordStreak = 1;
                string lastWord = "";

                foreach (string word in words)
                {
                    string lower = word.ToLowerInvariant();
                    if (lower == lastWord && lower.Length > 1)
                    {
                        wordStreak++;
                        maxWordStreak = Math.Max(maxWordStreak, wordStreak);
                    }
                    else
                    {
                        wordStreak = 1;
                        lastWord = lower;
                    }
                }

                if (maxWordStreak >= 4)
                {
                    System.Console.WriteLine($"Coherence check: Repeated word streak detected (max {maxWordStreak})");
                    return false;
                }

                // Check for repeated bigrams (e.g., "hello world hello world hello world")
                if (words.Length >= 6)
                {
                    int maxBigramRepeat = CountMaxNgramRepetitions(words, 2);
                    int bigramThreshold = Math.Max(3, words.Length / 4);
                    if (maxBigramRepeat >= bigramThreshold)
                    {
                        System.Console.WriteLine($"Coherence check: Repeated bigram detected ({maxBigramRepeat} repetitions)");
                        return false;
                    }
                }

                // Check lexical diversity: unique words / total words
                var uniqueWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string word in words)
                    uniqueWords.Add(word);

                double diversity = (double)uniqueWords.Count / words.Length;
                if (words.Length >= 10 && diversity < 0.3)
                {
                    System.Console.WriteLine($"Coherence check: Low lexical diversity ({diversity:P0}, {uniqueWords.Count} unique / {words.Length} total words)");
                    return false;
                }
            }

            System.Console.WriteLine($"Coherence check: alpha={alphaPct:P0}, printable={printablePct:P0}, spacing={spacePct:P0}, maxCharRepeat={maxCharRepeat}");
            return true;
        }

        /// <summary>
        /// Counts the maximum number of times any n-gram of <paramref name="n"/> words
        /// is consecutively repeated in <paramref name="words"/>.
        /// </summary>
        internal static int CountMaxNgramRepetitions(string[] words, int n)
        {
            int maxRepeat = 0;
            int outerBound = words.Length - n * 2;
            int endPos = words.Length - n;
            for (int i = 0; i <= outerBound; i++)
            {
                // Build n-gram starting at i
                int consecutive = 1;
                int pos = i + n;
                while (pos <= endPos)
                {
                    bool match = true;
                    for (int k = 0; k < n; k++)
                    {
                        if (!string.Equals(words[i + k], words[pos + k], StringComparison.OrdinalIgnoreCase))
                        {
                            match = false;
                            break;
                        }
                    }
                    if (match)
                    {
                        consecutive++;
                        pos += n;
                    }
                    else
                    {
                        break;
                    }
                }
                maxRepeat = Math.Max(maxRepeat, consecutive);
            }
            return maxRepeat;
        }
    }
}

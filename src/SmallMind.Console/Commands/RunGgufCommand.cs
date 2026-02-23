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

        /// <summary>
        /// Minimum token ID used to distinguish chat-template added tokens (e.g.
        /// &lt;|user|&gt; at ID 32001) from base-vocabulary tokens.  All Zephyr/TinyLlama
        /// added-token IDs exceed this value.
        /// </summary>
        private const int MinimumChatTokenId = 30000;

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

            if (!File.Exists(ggufPath))
            {
                System.Console.Error.WriteLine($"Error: GGUF file not found: {ggufPath}");
                return 1;
            }

            try
            {
                System.Console.WriteLine($"=== GGUF Validation Test ===");
                System.Console.WriteLine($"Model: {Path.GetFileName(ggufPath)}");
                System.Console.WriteLine($"Prompt: \"{prompt}\"");
                System.Console.WriteLine($"Max tokens: {maxTokens}");
                System.Console.WriteLine($"Temperature: {temperature}");
                System.Console.WriteLine($"Seed: {seed}");
                System.Console.WriteLine();

                // Load model from GGUF
                var loadStopwatch = Stopwatch.StartNew();
                System.Console.WriteLine("Loading GGUF model...");

                var (model, tokenizer, config) = GgufModelLoader.LoadFromGguf(ggufPath, seed);

                loadStopwatch.Stop();
                System.Console.WriteLine($"✓ Model loaded in {loadStopwatch.ElapsedMilliseconds}ms");

                // Detect Zephyr/TinyLlama-style chat model: <|user|> must encode as a single
                // high-ID vocabulary token.  If so, wrap the prompt in the chat template so the
                // model generates a coherent assistant response rather than raw text continuation.
                string effectivePrompt = prompt;
                var userTokens = tokenizer.Encode("<|user|>");
                bool isChatModel = userTokens.Count == 1 && userTokens[0] > MinimumChatTokenId;
                if (isChatModel)
                {
                    effectivePrompt = $"<|user|>\n{prompt}</s>\n<|assistant|>\n";
                    System.Console.WriteLine("Chat template: Zephyr format applied");
                }

                System.Console.WriteLine();

                // Create inference session
                var options = new ProductionInferenceOptions
                {
                    MaxNewTokens = maxTokens,
                    Temperature = temperature,
                    TopK = 40,
                    TopP = 0.95,
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

                string output = await session.GenerateAsync(effectivePrompt);

                genStopwatch.Stop();
                System.Console.WriteLine(output);
                System.Console.WriteLine("─".PadRight(60, '─'));
                System.Console.WriteLine();

                // Calculate tokens/sec
                int outputTokens = tokenizer.Encode(output).Count - tokenizer.Encode(effectivePrompt).Count;
                double tokensPerSec = outputTokens / (genStopwatch.ElapsedMilliseconds / 1000.0);

                System.Console.WriteLine($"Generation time: {genStopwatch.ElapsedMilliseconds}ms");
                System.Console.WriteLine($"Tokens generated: {outputTokens}");
                System.Console.WriteLine($"Speed: {tokensPerSec:F2} tok/s");
                System.Console.WriteLine();

                // Coherence check — always evaluate against the original user prompt so
                // the extractor can locate the generated portion inside the output.
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
                System.Console.Error.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                {
                    System.Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
                }
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
        /// Minimal coherence check: validates that output contains reasonable English text.
        /// Checks for:
        /// - Sufficient length
        /// - Primarily ASCII printable characters
        /// - Contains alphabetic characters
        /// - Not mostly repeated characters or garbage
        /// </summary>
        private bool ValidateCoherence(string output, string rawUserPrompt)
        {
            // Extract the generated portion.
            // The decoded output may differ from rawUserPrompt in whitespace representation
            // (▁ prefix → leading space) and may be prefixed by chat-template tokens such
            // as <|user|> and <|assistant|>.  We try three strategies in order:
            //   1. TrimStart + StartsWith to handle the common case where the decoded output
            //      is just the decoded prompt (possibly with a leading ▁-space) followed by
            //      the generated text.
            //   2. IndexOf to locate rawUserPrompt inside the output when a chat template
            //      prepends extra tokens (<|user|> etc.).
            //   3. Length-based fallback (original behaviour).
            string generated;
            string outputTrimmed = output.TrimStart();

            if (outputTrimmed.StartsWith(rawUserPrompt, StringComparison.Ordinal))
            {
                // Common case: prompt decoded cleanly, generated text follows.
                generated = outputTrimmed.Substring(rawUserPrompt.Length).TrimStart();
            }
            else
            {
                int idx = output.IndexOf(rawUserPrompt, StringComparison.Ordinal);
                if (idx >= 0)
                {
                    // Chat-template case: rawUserPrompt is embedded in the decoded output.
                    generated = output.Substring(idx + rawUserPrompt.Length).TrimStart();
                }
                else
                {
                    // Last-resort length-based strip.
                    generated = output.Length > rawUserPrompt.Length
                        ? output.Substring(rawUserPrompt.Length).TrimStart()
                        : outputTrimmed;
                }
            }

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
                if (char.IsWhiteSpace(c) || c == '▁')
                    spaceCount++;
            }

            // Check for reasonable English text
            double alphaPct = (double)alphaCount / generated.Length;
            double printablePct = (double)printableCount / generated.Length;
            double spacePct = (double)spaceCount / generated.Length;

            // English text should have:
            // - At least 40% alphabetic characters
            // - At least 80% printable ASCII
            // - 3-30% whitespace (slightly wider than the naive 5-25% to tolerate chat
            //   template prefix tokens that have no spaces, e.g. "<|assistant|>")
            bool hasEnoughAlpha = alphaPct >= 0.4;
            bool mostlyPrintable = printablePct >= 0.8;
            bool reasonableSpacing = spacePct >= 0.03 && spacePct <= 0.30;

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

            // Check for excessive repetition (same character repeated many times)
            int maxRepeat = 0;
            int currentRepeat = 1;
            char lastChar = '\0';

            foreach (char c in generated)
            {
                if (c == lastChar)
                {
                    currentRepeat++;
                    maxRepeat = Math.Max(maxRepeat, currentRepeat);
                }
                else
                {
                    currentRepeat = 1;
                    lastChar = c;
                }
            }

            if (maxRepeat > 20)
            {
                System.Console.WriteLine($"Coherence check: Excessive character repetition (max {maxRepeat})");
                return false;
            }

            System.Console.WriteLine($"Coherence check: alpha={alphaPct:P0}, printable={printablePct:P0}, spacing={spacePct:P0}, maxRepeat={maxRepeat}");
            return true;
        }
    }
}

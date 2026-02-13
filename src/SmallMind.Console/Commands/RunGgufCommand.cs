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
                    maxTokens = int.Parse(args[++i]);
                }
                else if (args[i] == "--temperature" && i + 1 < args.Length)
                {
                    temperature = double.Parse(args[++i]);
                }
                else if (args[i] == "--seed" && i + 1 < args.Length)
                {
                    seed = int.Parse(args[++i]);
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

                string output = await session.GenerateAsync(prompt);

                genStopwatch.Stop();
                System.Console.WriteLine(output);
                System.Console.WriteLine("─".PadRight(60, '─'));
                System.Console.WriteLine();

                // Calculate tokens/sec
                int outputTokens = tokenizer.Encode(output).Count - tokenizer.Encode(prompt).Count;
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
        private bool ValidateCoherence(string output, string prompt)
        {
            // Extract generated portion (after prompt)
            string generated = output.Length > prompt.Length
                ? output.Substring(prompt.Length).TrimStart()
                : output;

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

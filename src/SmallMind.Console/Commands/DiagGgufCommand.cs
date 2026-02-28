using SmallMind.Abstractions.Telemetry;
using SmallMind.Core.Core;
using SmallMind.Runtime;
using SmallMind.Runtime.Telemetry;

namespace SmallMind.ConsoleApp.Commands
{
    /// <summary>
    /// GGUF diagnostic command.
    /// Loads a GGUF model and prints a diagnostic report covering:
    ///   - Model metadata and architecture (vocab, layers, heads, RoPE, BOS/EOS)
    ///   - Tensor load coverage summary (loaded vs expected)
    ///   - NaN/Inf check on first-step logits
    ///   - Top-10 next-token predictions after one forward pass
    ///
    /// Use this to quickly determine whether a coherence failure originates in
    /// weight loading/dequantisation, the transformer forward pass, or sampling.
    /// </summary>
    internal sealed class DiagGgufCommand : ICommand
    {
        public string Name => "diag-gguf";
        public string Description => "GGUF diagnostic: tensor coverage, first-step top-10 logits, NaN/Inf";

        public async Task<int> ExecuteAsync(string[] args)
        {
            if (args.Length < 1 || args[0] is "--help" or "-h")
            {
                ShowUsage();
                return args.Length < 1 ? 1 : 0;
            }

            string ggufPath = args[0];
            string prompt = "The capital of France is";
            int seed = 42;

            for (int i = 1; i < args.Length; i++)
            {
                if ((args[i] == "--prompt" || args[i] == "-p") && i + 1 < args.Length)
                {
                    prompt = args[++i];
                }
                else if (args[i] == "--seed" && i + 1 < args.Length)
                {
                    if (int.TryParse(args[++i], out int s))
                        seed = s;
                }
            }

            if (!File.Exists(ggufPath))
            {
                System.Console.Error.WriteLine($"Error: GGUF file not found: {ggufPath}");
                return 1;
            }

            System.Console.WriteLine("=== GGUF Diagnostics ===");
            System.Console.WriteLine($"Model:  {Path.GetFileName(ggufPath)}");
            System.Console.WriteLine($"Prompt: \"{prompt}\"");
            System.Console.WriteLine($"Seed:   {seed}");
            System.Console.WriteLine();

            try
            {
                // ── 1. Load model with INFO-level console logging ──────────────
                // RuntimeLoggerAdapter bridges IInternalRuntimeLogger → public IRuntimeLogger.
                // ConsoleRuntimeLogger(Info) captures coverage summary, architecture info, and
                // any warnings about missing tensors that GgufModelLoader emits.
                System.Console.WriteLine("--- Loading Model ---");
                var publicLogger = new ConsoleRuntimeLogger(LogLevel.Info);
                var internalLogger = new RuntimeLoggerAdapter(publicLogger);

                var (model, tokenizer, config) =
                    GgufModelLoader.LoadFromGguf(ggufPath, seed, logger: internalLogger);

                // ── 2. Model metadata summary ──────────────────────────────────
                System.Console.WriteLine();
                System.Console.WriteLine("--- Model Metadata ---");
                System.Console.WriteLine($"Architecture : {config.Architecture}");
                System.Console.WriteLine($"Vocab size   : {config.VocabSize:N0}  (tokenizer: {tokenizer.VocabSize:N0})");
                System.Console.WriteLine($"Context      : {config.ContextLength:N0} tokens");
                System.Console.WriteLine($"Layers       : {config.BlockCount}");
                System.Console.WriteLine(
                    $"Heads        : {config.HeadCount} Q, {config.HeadCountKv} KV" +
                    (config.HeadCountKv < config.HeadCount ? " [GQA]" : ""));
                System.Console.WriteLine($"RoPE freq    : {config.RopeFreqBase}");
                System.Console.WriteLine($"BOS token    : {tokenizer.BosTokenId}");
                System.Console.WriteLine($"EOS token    : {tokenizer.EosTokenId}");
                System.Console.WriteLine($"Tokenizer    : {tokenizer.GetType().Name}");

                // ── 3. Tokenize prompt ──────────────────────────────────────────
                System.Console.WriteLine();
                System.Console.WriteLine("--- Tokenization ---");
                var tokens = tokenizer.Encode(prompt);
                System.Console.WriteLine($"Input tokens ({tokens.Count}): [{string.Join(", ", tokens)}]");

                if (tokens.Count == 0)
                {
                    System.Console.Error.WriteLine("ERROR: Tokenizer returned empty token list for the prompt.");
                    return 2;
                }

                // ── 4. One forward pass ────────────────────────────────────────
                System.Console.WriteLine();
                System.Console.WriteLine("--- First-Step Logits ---");

                var inputData = new float[tokens.Count];
                for (int i = 0; i < tokens.Count; i++)
                    inputData[i] = tokens[i];

                var inputTensor = new Tensor(inputData, new int[] { 1, tokens.Count });
                var outputTensor = model.Forward(inputTensor);

                // Extract last-position logits: shape is [1, seqLen, vocabSize] or [seqLen, vocabSize]
                var shape = outputTensor.Shape;
                int vocabSize = shape[shape.Length - 1];

                float[] lastLogits;
                if (shape.Length >= 2 && outputTensor.Size > vocabSize)
                {
                    // Take the last token's logit row
                    int offset = outputTensor.Size - vocabSize;
                    lastLogits = new float[vocabSize];
                    Array.Copy(outputTensor.Data, offset, lastLogits, 0, vocabSize);
                }
                else
                {
                    lastLogits = outputTensor.Data;
                }

                // ── 5. NaN / Inf checks ────────────────────────────────────────
                bool hasNaN = false;
                bool hasInf = false;
                for (int i = 0; i < lastLogits.Length; i++)
                {
                    if (float.IsNaN(lastLogits[i])) { hasNaN = true; break; }
                }
                for (int i = 0; i < lastLogits.Length; i++)
                {
                    if (float.IsInfinity(lastLogits[i])) { hasInf = true; break; }
                }

                System.Console.WriteLine($"NaN in logits : {(hasNaN ? "YES ✗  ← check weight loading / dequant" : "NO ✓")}");
                System.Console.WriteLine($"Inf in logits : {(hasInf ? "YES ✗  ← check weight loading / dequant" : "NO ✓")}");

                if (!hasNaN && !hasInf)
                {
                    float minLogit = float.MaxValue;
                    float maxLogit = float.MinValue;
                    for (int i = 0; i < lastLogits.Length; i++)
                    {
                        if (lastLogits[i] < minLogit) minLogit = lastLogits[i];
                        if (lastLogits[i] > maxLogit) maxLogit = lastLogits[i];
                    }

                    System.Console.WriteLine($"Logit range   : [{minLogit:F4}, {maxLogit:F4}]");

                    // ── 6. Top-10 predictions ──────────────────────────────────
                    System.Console.WriteLine();
                    System.Console.WriteLine("Top-10 next-token predictions:");
                    System.Console.WriteLine($"  {"#",-3} {"Token",-24} {"ID",7}  {"Logit",9}");
                    System.Console.WriteLine($"  {"─",-3} {"─",-24} {"─",7}  {"─",9}");

                    // Partial sort: find top 10 without full sort for efficiency
                    var top10 = lastLogits
                        .Select((val, idx) => (val, idx))
                        .OrderByDescending(x => x.val)
                        .Take(10)
                        .ToArray();

                    for (int i = 0; i < top10.Length; i++)
                    {
                        var (val, idx) = top10[i];
                        string tokenText;
                        try
                        {
                            tokenText = tokenizer.Decode(new List<int> { idx });
                            // Escape control characters for clean display
                            tokenText = tokenText.Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
                            if (tokenText.Length > 22) tokenText = tokenText[..22] + "…";
                        }
                        catch
                        {
                            tokenText = $"<id:{idx}>";
                        }

                        System.Console.WriteLine($"  {i + 1,-3} {idx,7}  \"{tokenText,-22}\" {val,9:F4}");
                    }

                    // One-line quality summary: classify top tokens as English-like or garbage
                    int printableEnglishCount = 0;
                    foreach (var (_, idx) in top10)
                    {
                        string tokenText;
                        try { tokenText = tokenizer.Decode(new List<int> { idx }); }
                        catch { tokenText = string.Empty; }

                        // Count as English-like if it contains an ASCII letter or digit (no LINQ allocation)
                        bool isEnglishLike = false;
                        for (int ci = 0; ci < tokenText.Length; ci++)
                        {
                            char c = tokenText[ci];
                            if (c < 128 && char.IsLetterOrDigit(c)) { isEnglishLike = true; break; }
                        }
                        if (isEnglishLike) printableEnglishCount++;
                    }

                    string qualitySummary = printableEnglishCount >= 6
                        ? "Top-k mostly printable English-like"
                        : "Top-k dominated by mixed-script/symbol tokens";
                    System.Console.WriteLine();
                    System.Console.WriteLine($"Quality summary: {qualitySummary} ({printableEnglishCount}/{top10.Length} English-like tokens)");

                    System.Console.WriteLine();
                    System.Console.WriteLine("Interpretation guide:");
                    System.Console.WriteLine("  • Top tokens look like plausible English  → forward pass is likely correct");
                    System.Console.WriteLine("  • Top tokens are all punctuation/garbage  → check weight loading / Q4_0 dequant");
                    System.Console.WriteLine("  • Logit range is extremely narrow (<1.0)  → check RoPE / layer-norm params");
                    System.Console.WriteLine("  • NaN or Inf present                      → numerical instability in forward pass");
                }

                return (hasNaN || hasInf) ? 2 : 0;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("required tensor"))
            {
                // Hard-fail from GgufModelLoader — missing critical weights
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.Error.WriteLine($"\nFATAL: {ex.Message}");
                System.Console.ResetColor();
                return 2;
            }
            catch (Exception ex)
            {
                System.Console.Error.WriteLine($"Error: {ex.Message}");
                if (ex.InnerException != null)
                    System.Console.Error.WriteLine($"Inner: {ex.InnerException.Message}");
                return 1;
            }
        }

        public void ShowUsage()
        {
            System.Console.WriteLine("Usage: smallmind diag-gguf <gguf-file> [options]");
            System.Console.WriteLine();
            System.Console.WriteLine("Arguments:");
            System.Console.WriteLine("  <gguf-file>         Path to GGUF model file");
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("  --prompt <text>     Prompt for first-step analysis (default: \"The capital of France is\")");
            System.Console.WriteLine("  --seed <n>          Random seed (default: 42)");
            System.Console.WriteLine("  --help, -h          Show this help");
            System.Console.WriteLine();
            System.Console.WriteLine("Output includes:");
            System.Console.WriteLine("  - Model metadata (arch, vocab, layers, heads, RoPE, BOS/EOS, tokenizer)");
            System.Console.WriteLine("  - Tensor load coverage summary (from loader INFO log)");
            System.Console.WriteLine("  - NaN/Inf check on first-step logits");
            System.Console.WriteLine("  - Top-10 next-token predictions with decoded text");
            System.Console.WriteLine();
            System.Console.WriteLine("Exit codes:");
            System.Console.WriteLine("  0 - Diagnostics completed, no NaN/Inf detected");
            System.Console.WriteLine("  1 - Error loading or processing the model");
            System.Console.WriteLine("  2 - NaN/Inf in logits or critical tensors missing");
        }
    }
}

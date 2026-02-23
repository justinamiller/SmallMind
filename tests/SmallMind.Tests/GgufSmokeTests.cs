using SmallMind.Runtime;

namespace SmallMind.Tests;

/// <summary>
/// Deterministic GGUF smoke test for tinyllama (or another GGUF model).
/// Validates that model output is coherent (non-gibberish) using printable/whitespace
/// ratio heuristics and max repeated-token burst detection.
///
/// Skip this test unless SMALLMIND_GGUF_SMOKE_MODEL is set to a valid GGUF file path.
/// </summary>
public class GgufSmokeTests
{
    private const string ModelPathEnvVar = "SMALLMIND_GGUF_SMOKE_MODEL";
    private const string SkipReason = $"Set {ModelPathEnvVar}=/path/to/tinyllama.gguf to run GGUF smoke tests";

    private const string TestPrompt =
        "You are a helpful assistant. Answer in one short English sentence: What is the capital of France?";

    /// <summary>
    /// Runs a deterministic inference with tinyllama (or configured GGUF model) and validates
    /// that the output is not gibberish by checking:
    /// - printable character ratio above threshold
    /// - whitespace ratio within expected bounds
    /// - no excessively long repeated token bursts
    /// </summary>
    [Fact]
    public async Task GgufModel_DeterministicInference_ProducesCoherentOutput()
    {
        string? modelPath = Environment.GetEnvironmentVariable(ModelPathEnvVar);
        if (string.IsNullOrEmpty(modelPath))
        {
            // Skip cleanly when model path not configured
            return;
        }

        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException(
                $"GGUF model file not found at path specified by {ModelPathEnvVar}: {modelPath}");
        }

        // Load model
        var (model, tokenizer, config) = GgufModelLoader.LoadFromGguf(
            modelPath,
            seed: 42,
            useMmap: false);

        // Configure deterministic low-temperature inference
        var options = new ProductionInferenceOptions
        {
            MaxNewTokens = 60,
            Temperature = 0.1,
            TopK = 40,
            TopP = 0.95,
            Seed = 12345,
            MaxInputTokens = 512,
            TruncateInput = true
        };

        using var session = new InferenceSession(model, tokenizer, options, config.ContextLength);

        string output = await session.GenerateAsync(TestPrompt);

        // Strip the prompt prefix from output for analysis
        string generated = output.StartsWith(TestPrompt, StringComparison.Ordinal)
            ? output[TestPrompt.Length..]
            : output;

        // Validate output quality with heuristics
        AssertCoherentOutput(generated, modelPath);
    }

    /// <summary>
    /// Validates that generated text is non-pathological using robust heuristics.
    /// </summary>
    private static void AssertCoherentOutput(string generated, string modelPath)
    {
        // Must have some output
        Assert.True(generated.Length > 0,
            $"Model [{modelPath}] produced empty output for prompt.");

        // Heuristic 1: Printable character ratio (gibberish has many non-printable chars)
        const double printableThreshold = 0.90;
        int printableCount = 0;
        foreach (char c in generated)
        {
            if (c >= 0x20 && c < 0x7F || c == '\n' || c == '\r' || c == '\t')
                printableCount++;
        }
        double printableRatio = (double)printableCount / generated.Length;
        Assert.True(printableRatio >= printableThreshold,
            $"Model [{modelPath}] output has low printable ratio ({printableRatio:P1} < {printableThreshold:P0}). " +
            $"Possible gibberish. Output: {Truncate(generated, 200)}");

        // Heuristic 2: Whitespace ratio (very low whitespace = run-together words; too high = mostly spaces)
        int wsCount = 0;
        foreach (char c in generated)
            if (char.IsWhiteSpace(c)) wsCount++;
        double wsRatio = (double)wsCount / generated.Length;
        Assert.True(wsRatio is >= 0.05 and <= 0.60,
            $"Model [{modelPath}] has unusual whitespace ratio ({wsRatio:P1}). " +
            $"Output: {Truncate(generated, 200)}");

        // Heuristic 3: Max repeated token burst (e.g. "aaaaaa...") indicates degenerate distribution
        const int maxRepeatedBurst = 6;
        int maxBurst = MaxConsecutiveCharRun(generated);
        Assert.True(maxBurst <= maxRepeatedBurst,
            $"Model [{modelPath}] has a repeated character burst of {maxBurst} chars (threshold={maxRepeatedBurst}). " +
            $"Possible stuck/degenerate distribution. Output: {Truncate(generated, 200)}");
    }

    private static int MaxConsecutiveCharRun(string s)
    {
        if (s.Length == 0) return 0;
        int max = 1, cur = 1;
        for (int i = 1; i < s.Length; i++)
        {
            if (s[i] == s[i - 1]) { cur++; if (cur > max) max = cur; }
            else cur = 1;
        }
        return max;
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";
}

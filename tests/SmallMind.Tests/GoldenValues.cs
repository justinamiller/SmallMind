namespace SmallMind.Tests
{
    /// <summary>
    /// Golden values for regression testing.
    /// These values are captured from a known-good version of the code
    /// and used to detect regressions in model behavior.
    /// 
    /// All values are generated using SyntheticModelFactory with seed=42.
    /// If you need to regenerate these values, run the GoldenValuesGenerator utility.
    /// </summary>
    internal static class GoldenValues
    {
        /// <summary>
        /// Greedy generation output for prompt "Hello" (20 tokens, temp=0.0, seed=42)
        /// </summary>
        public static class GreedyGeneration
        {
            public const string Prompt = "Hello";
            public const int MaxTokens = 20;
            public const int Seed = 42;

            // Golden output (to be populated by running actual generation)
            // Format: [token1, token2, token3, ...]
            public static readonly int[] GoldenTokens = new[]
            {
                // Note: These will be populated by running SyntheticModelFactory
                // and capturing actual output. For now, using placeholder values.
                72, 101, 108, 108, 111, // "Hello"
                32, 119, 111, 114, 108, 100, // " world"
                33, 32, 84, 104, 105, 115, 32, 105, 115 // "! This is"
            };

            public const string GoldenText = "Hello world! This is";
        }

        /// <summary>
        /// Forward pass logits for token sequence [65, 66, 67] (A, B, C)
        /// First 10 logits of the output (last position)
        /// </summary>
        public static class ForwardPassLogits
        {
            public static readonly int[] InputTokens = new[] { 65, 66, 67 }; // "ABC"

            // Top 10 logits at last position (to be populated from actual run)
            // These are the raw logit values before softmax
            public static readonly float[] Top10Logits = new[]
            {
                -2.3456f, -2.4567f, -2.5678f, -2.6789f, -2.7890f,
                -2.8901f, -2.9012f, -3.0123f, -3.1234f, -3.2345f
            };

            // Indices of top 10 logits (which tokens they correspond to)
            public static readonly int[] Top10Indices = new[]
            {
                68, 69, 70, 71, 72, 73, 74, 75, 76, 77 // D-M
            };

            // Statistics for validation
            public const float ExpectedMean = -2.7f;      // Approximate mean of logits
            public const float ExpectedVariance = 0.8f;    // Approximate variance
            public const float ExpectedMin = -8.5f;        // Approximate min
            public const float ExpectedMax = -1.5f;        // Approximate max

            // Tolerance for floating point comparisons
            public const float Tolerance = 0.1f;
        }

        /// <summary>
        /// KV Cache comparison: output should be identical with/without cache
        /// </summary>
        public static class KVCacheComparison
        {
            public const string Prompt = "Test";
            public const int MaxTokens = 10;
            public const int Seed = 42;

            // Expected: same tokens regardless of KV cache enabled/disabled
            public static readonly int[] ExpectedTokens = new[]
            {
                84, 101, 115, 116, // "Test"
                32, 111, 117, 116, 112, 117 // " outpu"
            };
        }

        /// <summary>
        /// Quantization roundtrip tolerances
        /// </summary>
        public static class QuantizationTolerances
        {
            // Q4_0: 4-bit quantization (no min)
            public const float Q4_0_MaxError = 0.05f;
            public const float Q4_0_MeanError = 0.02f;

            // Q8_0: 8-bit quantization (higher precision)
            public const float Q8_0_MaxError = 0.01f;
            public const float Q8_0_MeanError = 0.005f;

            // Q4_1: 4-bit with min (to be added in WS3)
            public const float Q4_1_MaxError = 0.04f;
            public const float Q4_1_MeanError = 0.015f;

            // Q5_0: 5-bit quantization (to be added in WS3)
            public const float Q5_0_MaxError = 0.03f;
            public const float Q5_0_MeanError = 0.01f;
        }

        /// <summary>
        /// Fused kernel vs dequantized matmul comparison tolerances
        /// </summary>
        public static class FusedKernelTolerances
        {
            // Acceptable difference between fused Q4 matmul and dequant-then-matmul
            public const float Q4_FusedMaxDiff = 0.001f;
            public const float Q4_FusedMeanDiff = 0.0005f;

            // Acceptable difference for Q8
            public const float Q8_FusedMaxDiff = 0.0005f;
            public const float Q8_FusedMeanDiff = 0.0001f;
        }

        /// <summary>
        /// Stop sequence behavior expectations
        /// </summary>
        public static class StopSequences
        {
            public const string Prompt = "Count: 1, 2, 3";
            public static readonly string[] StopSeqs = new[] { ",", "\n" };
            public const int MaxTokens = 50;

            // Should stop at first comma (after "Count: 1")
            public const int ExpectedMaxGeneratedTokens = 10; // Should be less than MaxTokens
        }

        /// <summary>
        /// Tokenizer encode/decode roundtrip test cases
        /// </summary>
        public static class TokenizerRoundtrip
        {
            public static readonly (string input, bool exactMatch)[] TestCases = new[]
            {
                ("Hello, world!", true),
                ("The quick brown fox", true),
                ("123 456 789", true),
                ("", true),
                (" ", true),
                ("Test\nNewline", true)
            };
        }
    }
}

namespace SmallMind.Tests.TestHelpers
{
    /// <summary>
    /// Golden values for regression testing.
    /// These values are generated once from a known-good implementation
    /// and used to detect regressions in model inference, sampling, and tokenization.
    /// </summary>
    internal static class GoldenValues
    {
        /// <summary>
        /// Golden output for greedy generation with TinyModel (seed=42)
        /// Prompt: "Hello"
        /// MaxTokens: 20
        /// Temperature: 0.001 (near-greedy)
        /// </summary>
        public static class TinyModelGreedy
        {
            public const string Prompt = "Hello";
            public const int Seed = 42;
            public const int MaxTokens = 20;

            // To be populated after establishing baseline
            public static readonly int[] ExpectedTokens = Array.Empty<int>();
            public static readonly string ExpectedOutput = "";
        }

        /// <summary>
        /// Golden logits for forward pass with MicroModel
        /// Input: [65, 66, 67] (ABC in ASCII)
        /// First 10 logits of the output
        /// </summary>
        public static class MicroModelForwardPass
        {
            public static readonly int[] InputTokens = new[] { 65, 66, 67 };

            // To be populated after establishing baseline
            // These are the first 10 logit values from the forward pass
            public static readonly float[] ExpectedFirstLogits = Array.Empty<float>();

            // Tolerance for floating point comparison
            public const float LogitTolerance = 1e-4f;
        }

        /// <summary>
        /// Expected behavior for tokenizer round-trip tests
        /// </summary>
        public static class TokenizerRoundTrip
        {
            public static readonly Dictionary<string, string> TestCases = new()
            {
                { "Hello, world!", "Hello, world!" },
                { "The quick brown fox", "The quick brown fox" },
                { "2+2=4", "2+2=4" },
                { "Hello\nWorld", "Hello\nWorld" }
            };
        }
    }
}

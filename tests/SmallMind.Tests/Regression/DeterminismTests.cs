using SmallMind.Runtime;
using SmallMind.Tests.Fixtures;

namespace SmallMind.Tests.Regression
{
    /// <summary>
    /// Determinism regression tests for SmallMind.
    /// Validates that same seed + config + prompt produces identical outputs.
    /// </summary>
    [Trait("Category", "Regression")]
    [Trait("Subcategory", "Determinism")]
    public class DeterminismTests
    {
        private readonly TinyModelFixture _fixture = new();

        [Fact]
        public async Task Generation_SameSeed_ProducesIdenticalOutputs()
        {
            // Arrange
            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 1.0,
                Seed = 42,
                TopK = 0 // Disable top-k for pure determinism
            };

            var prompt = "hello";

            // Act - Run generation twice with same configuration
            string result1, result2;
            using (var session1 = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen))
            {
                result1 = await session1.GenerateAsync(prompt);
            }

            using (var session2 = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen))
            {
                result2 = await session2.GenerateAsync(prompt);
            }

            // Assert
            Assert.Equal(result1, result2);
        }

        [Fact]
        public async Task Generation_MultipleRuns_SameSeed_AllIdentical()
        {
            // Arrange
            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 5,
                Temperature = 1.0,
                Seed = 12345,
                TopK = 0
            };

            var prompt = "test";
            const int numRuns = 5;
            var results = new string[numRuns];

            // Act - Run generation multiple times
            for (int i = 0; i < numRuns; i++)
            {
                using var session = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                results[i] = await session.GenerateAsync(prompt);
            }

            // Assert - All results should be identical
            for (int i = 1; i < numRuns; i++)
            {
                Assert.Equal(results[0], results[i]);
            }
        }

        [Fact]
        public async Task Generation_DifferentSeeds_ProduceDifferentOutputs()
        {
            // Arrange
            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            var prompt = "test";

            var options1 = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 1.0,
                Seed = 111
            };

            var options2 = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 1.0,
                Seed = 222
            };

            // Act
            string result1, result2;
            using (var session1 = new InferenceSession(model, tokenizer, options1, TinyModelFixture.MaxSeqLen))
            {
                result1 = await session1.GenerateAsync(prompt);
            }

            using (var session2 = new InferenceSession(model, tokenizer, options2, TinyModelFixture.MaxSeqLen))
            {
                result2 = await session2.GenerateAsync(prompt);
            }

            // Assert - Different seeds should produce different outputs
            Assert.NotEqual(result1, result2);
        }

        [Fact]
        public async Task Generation_GreedySampling_Deterministic()
        {
            // Arrange - Greedy sampling (temp ~0, always pick argmax) should be deterministic even without seed
            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 5,
                Temperature = 0.001, // Near-zero for greedy
                TopK = 0
            };

            var prompt = "hello";

            // Act - Two runs without explicit seed
            string result1, result2;
            using (var session1 = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen))
            {
                result1 = await session1.GenerateAsync(prompt);
            }

            using (var session2 = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen))
            {
                result2 = await session2.GenerateAsync(prompt);
            }

            // Assert - Greedy should be deterministic
            Assert.Equal(result1, result2);
        }
    }
}

using System;
using System.Linq;
using SmallMind.Tests.TestHelpers;
using SmallMind.Tokenizers;
using SmallMind.Tokenizers.Text;
using Xunit;

namespace SmallMind.Tests.Regression
{
    /// <summary>
    /// Golden output regression tests to catch breaking changes.
    /// These tests use synthetic models with deterministic weights
    /// and verify that outputs match pre-computed golden values.
    /// </summary>
    [Trait("Category", "GoldenOutput")]
    public class GoldenOutputTests
    {
        [Fact]
        public void SyntheticModel_CreatesSuccessfully()
        {
            // Arrange & Act
            var model = SyntheticModelFactory.CreateTinyModel(seed: 42);
            
            // Assert
            Assert.NotNull(model);
            var namedParams = model.GetNamedParameters();
            Assert.NotEmpty(namedParams);
        }
        
        [Fact]
        public void SyntheticModel_IsDeterministic()
        {
            // Arrange & Act
            var model1 = SyntheticModelFactory.CreateTinyModel(seed: 42);
            var model2 = SyntheticModelFactory.CreateTinyModel(seed: 42);
            
            // Assert - both models should have identical weights
            var params1 = model1.GetNamedParameters();
            var params2 = model2.GetNamedParameters();
            
            Assert.Equal(params1.Count, params2.Count);
            
            foreach (var kvp in params1)
            {
                Assert.True(params2.ContainsKey(kvp.Key), $"Model 2 missing parameter: {kvp.Key}");
                var data1 = kvp.Value.Data;
                var data2 = params2[kvp.Key].Data;
                
                Assert.Equal(data1.Length, data2.Length);
                for (int i = 0; i < Math.Min(100, data1.Length); i++) // Check first 100 values
                {
                    Assert.Equal(data1[i], data2[i], precision: 6);
                }
            }
        }
        
        [Fact]
        public void TokenizerRoundTrip_MatchesGoldenValues()
        {
            // Arrange
            var vocab = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?\n=+-";
            var tokenizer = TokenizerFactory.CreateCharLevel(vocab);
            
            // Act & Assert - only test cases that use characters in our vocab
            var simpleTests = new Dictionary<string, string>
            {
                { "Hello world", "Hello world" },
                { "The quick brown fox", "The quick brown fox" }
            };
            
            foreach (var (input, expected) in simpleTests)
            {
                var tokens = tokenizer.Encode(input);
                var decoded = tokenizer.Decode(tokens);
                
                // Normalize whitespace for comparison
                var normalizedExpected = expected.Trim();
                var normalizedDecoded = decoded.Trim();
                
                Assert.Equal(normalizedExpected, normalizedDecoded);
            }
        }
        
        /// <summary>
        /// Placeholder test for greedy generation golden output.
        /// To be implemented after baseline is established.
        /// </summary>
        [Fact(Skip = "Golden values not yet established - run baseline first")]
        public void GreedyGeneration_MatchesGoldenOutput()
        {
            // This test would verify that greedy generation produces
            // the exact same tokens as the golden values
            // TODO: Implement after establishing baseline
        }
        
        /// <summary>
        /// Placeholder test for forward pass logits.
        /// To be implemented after baseline is established.
        /// </summary>
        [Fact(Skip = "Golden values not yet established - run baseline first")]
        public void ForwardPass_MatchesGoldenLogits()
        {
            // This test would verify that forward pass produces
            // logits within tolerance of golden values
            // TODO: Implement after establishing baseline
        }
    }
}

using System;
using Xunit;
using SmallMind.Tests.Fixtures;
using SmallMind.Tests.Utilities;
using SmallMind.Core.Core;

namespace SmallMind.Tests.Regression
{
    /// <summary>
    /// Correctness regression tests for SmallMind.
    /// Validates that model produces expected outputs for known inputs.
    /// </summary>
    [Trait("Category", "Regression")]
    [Trait("Subcategory", "Correctness")]
    public class CorrectnessTests
    {
        private readonly TinyModelFixture _fixture = new();

        [Fact]
        public void Model_Forward_ProducesFiniteLogits()
        {
            // Arrange
            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            var prompts = _fixture.GetKnownPrompts();

            model.Eval(); // Disable dropout for determinism

            foreach (var (key, promptInfo) in prompts)
            {
                if (string.IsNullOrEmpty(promptInfo.Prompt))
                    continue; // Skip empty prompt for this test

                // Act
                var tokens = tokenizer.Encode(promptInfo.Prompt).ToArray();
                var inputTensor = CreateInputTensor(tokens);
                var logits = model.Forward(inputTensor, 0);

                // Assert
                Assert.NotNull(logits);
                Assert.True(logits.Data.Length > 0, $"Logits should not be empty for prompt: {key}");

                // Check all logits are finite (no NaN/Inf)
                for (int i = 0; i < logits.Data.Length; i++)
                {
                    var value = logits.Data[i];
                    Assert.False(float.IsNaN(value), 
                        $"Logit at position {i} is NaN for prompt: {key}");
                    Assert.False(float.IsInfinity(value), 
                        $"Logit at position {i} is Infinity for prompt: {key}");
                }

                // Logits should typically be in a reasonable range before softmax
                var maxLogit = MaxValue(logits.Data);
                var minLogit = MinValue(logits.Data);
                Assert.True(maxLogit < 100f, 
                    $"Max logit {maxLogit:F2} seems unreasonably high for prompt: {key}");
                Assert.True(minLogit > -100f, 
                    $"Min logit {minLogit:F2} seems unreasonably low for prompt: {key}");
            }
        }

        [Fact]
        public void Model_Forward_ProducesCorrectShape()
        {
            // Arrange
            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var prompt = "test";
            var tokens = tokenizer.Encode(prompt).ToArray();
            var inputTensor = CreateInputTensor(tokens);

            // Act
            var logits = model.Forward(inputTensor, 0);

            // Assert
            Assert.NotNull(logits);
            Assert.Equal(3, logits.Shape.Length); // [batch_size, seq_len, vocab_size]
            
            // Last dimension should be vocab size
            var lastDim = logits.Shape[2];
            Assert.Equal(TinyModelFixture.VocabSize, lastDim);
        }

        [Fact]
        public void TokenGeneration_SingleStep_ProducesValidTokenID()
        {
            // Arrange
            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var prompt = "hello";
            var tokens = tokenizer.Encode(prompt).ToArray();
            var inputTensor = CreateInputTensor(tokens);

            // Act
            var logits = model.Forward(inputTensor, 0);
            var nextTokenId = ArgMax(GetLastTokenLogits(logits));

            // Assert
            Assert.True(nextTokenId >= 0, "Token ID should be non-negative");
            Assert.True(nextTokenId < TinyModelFixture.VocabSize, 
                $"Token ID {nextTokenId} should be < vocab size {TinyModelFixture.VocabSize}");
        }

        [Fact]
        public void Tokenizer_Encoding_ProducesExpectedTokenCounts()
        {
            // Arrange
            var tokenizer = _fixture.CreateTokenizer();
            var prompts = _fixture.GetKnownPrompts();

            foreach (var (key, promptInfo) in prompts)
            {
                // Act
                var tokens = tokenizer.Encode(promptInfo.Prompt);

                // Assert
                Assert.Equal(promptInfo.ExpectedTokenCount, tokens.Count);
            }
        }

        [Fact]
        public void Tokenizer_RoundTrip_PreservesText()
        {
            // Arrange
            var tokenizer = _fixture.CreateTokenizer();
            var testStrings = new[] { "hello", "test", "123", "The quick brown fox", " " };

            foreach (var original in testStrings)
            {
                // Act
                var tokens = tokenizer.Encode(original);
                var decoded = tokenizer.Decode(tokens);

                // Assert
                Assert.Equal(original, decoded);
            }
        }

        #region Helper Methods

        private Tensor CreateInputTensor(int[] tokens)
        {
            // Create tensor with shape [1, seq_len] for batch size 1
            var data = new float[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                data[i] = tokens[i];
            }
            return new Tensor(data, new int[] { 1, tokens.Length }, requiresGrad: false);
        }

        private float[] GetLastTokenLogits(Tensor logits)
        {
            // Assuming logits shape is [batch, seq_len, vocab_size]
            int batchSize = logits.Shape[0];
            int seqLen = logits.Shape[1];
            int vocabSize = logits.Shape[2];
            
            var lastLogits = new float[vocabSize];
            int offset = (seqLen - 1) * vocabSize;
            Array.Copy(logits.Data, offset, lastLogits, 0, vocabSize);
            
            return lastLogits;
        }

        private int ArgMax(float[] values)
        {
            int maxIndex = 0;
            float maxValue = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > maxValue)
                {
                    maxValue = values[i];
                    maxIndex = i;
                }
            }
            return maxIndex;
        }

        private float MaxValue(float[] values)
        {
            float max = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] > max) max = values[i];
            }
            return max;
        }

        private float MinValue(float[] values)
        {
            float min = values[0];
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] < min) min = values[i];
            }
            return min;
        }

        #endregion
    }
}

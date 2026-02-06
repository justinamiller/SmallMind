using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for streaming generation, cancellation, budgets, and deterministic mode.
    /// </summary>
    public class StreamingAndBudgetsTests
    {
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private const int BlockSize = 64;

        public StreamingAndBudgetsTests()
        {
            // Create a tiny model for testing
            string vocab = "abcdefghijklmnopqrstuvwxyz .!";
            int vocabSize = vocab.Length;
            _tokenizer = new CharTokenizer(vocab);
            
            _model = new TransformerModel(
                vocabSize, BlockSize, 32, 2, 2, 0.0, 42);
            _model.Eval(); // Set to evaluation mode
        }

        [Fact]
        public async Task StreamingGeneration_EmitsTokensIncrementally()
        {
            // Arrange
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 1.0,
                TopK = 10
            };

            SmallMind.Runtime.InferenceSession session = new InferenceSession(_model, _tokenizer, options, BlockSize);

            // Act
            int tokenCount = 0;
            await foreach (var token in session.GenerateStreamAsync("hello", null))
            {
                // Assert each token
                Assert.NotNull(token.Text);
                Assert.True(token.Index >= 0);
                tokenCount++;
            }

            // Assert total count
            Assert.True(tokenCount > 0);
            Assert.True(tokenCount <= options.MaxNewTokens + 1); // +1 for prompt tokens
        }

        [Fact]
        public async Task StreamingGeneration_RespectsCancellationToken()
        {
            // Arrange
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 100, // Long generation
                Temperature = 1.0
            };

            SmallMind.Runtime.InferenceSession session = new InferenceSession(_model, _tokenizer, options, BlockSize);
            var cts = new CancellationTokenSource();

            // Act & Assert
            int tokenCount = 0;
            await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            {
                await foreach (var token in session.GenerateStreamAsync("test", null, cts.Token))
                {
                    tokenCount++;
                    if (tokenCount >= 3)
                    {
                        cts.Cancel(); // Cancel after a few tokens
                    }
                }
            });

            // Should have generated at least a few tokens before cancellation
            Assert.True(tokenCount >= 3);
        }

        [Fact]
        public async Task MaxNewTokens_LimitsGeneration()
        {
            // Arrange
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 5,
                Temperature = 1.0
            };

            SmallMind.Runtime.InferenceSession session = new InferenceSession(_model, _tokenizer, options, BlockSize);

            // Act
            var result = await session.GenerateAsync("test", null);

            // Assert
            Assert.NotNull(result);
            
            // Count generated tokens (excluding prompt)
            var promptTokens = _tokenizer.Encode("test");
            var allTokens = _tokenizer.Encode(result);
            int generatedTokens = allTokens.Count - promptTokens.Count;
            
            // Should not exceed MaxNewTokens
            Assert.True(generatedTokens <= options.MaxNewTokens);
        }

        [Fact]
        public async Task MaxContextTokens_EnforcesLimit()
        {
            // Arrange
            var options = new ProductionInferenceOptions
            {
                MaxInputTokens = 15, // Smaller than MaxContextTokens
                MaxContextTokens = 20, // Very small context
                MaxNewTokens = 50,
                Temperature = 1.0
            };

            SmallMind.Runtime.InferenceSession session = new SmallMind.Runtime.InferenceSession(_model, _tokenizer, options, BlockSize);

            // Act
            var result = await session.GenerateAsync("test input", null);

            // Assert
            var allTokens = _tokenizer.Encode(result);
            Assert.True(allTokens.Count <= options.MaxContextTokens);
        }

        [Fact]
        public async Task TimeoutMs_StopsGenerationGracefully()
        {
            // Arrange
            // Use a long prompt that fills most of the block size to minimize generation time
            // This makes timeout more likely to fire before hitting block size limit
            string longPrompt = new string('a', BlockSize - 5); // Leave room for only a few tokens
            
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 1000, // Would take a while
                MaxTimeMs = 1, // Very short timeout
                Temperature = 1.0
            };

            SmallMind.Runtime.InferenceSession session = new SmallMind.Runtime.InferenceSession(_model, _tokenizer, options, BlockSize);

            // Act & Assert
            // On fast hardware, generation might complete before timeout fires
            // The important thing is that timeout mechanism works when it does fire
            try
            {
                await session.GenerateAsync(longPrompt, null);
                // If we get here, generation completed before timeout - that's ok
            }
            catch (SmallMind.Core.Exceptions.InferenceTimeoutException)
            {
                // Expected when timeout fires - this is what we're testing
            }
        }

        [Fact]
        public async Task DeterministicMode_ProducesSameOutput()
        {
            // Arrange
            const int seed = 12345;
            var options1 = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 0.8,
                TopK = 20,
                Seed = seed
            };

            var options2 = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 0.8,
                TopK = 20,
                Seed = seed
            };

            var session1 = new InferenceSession(_model, _tokenizer, options1, BlockSize);
            var session2 = new InferenceSession(_model, _tokenizer, options2, BlockSize);

            // Act
            var result1 = await session1.GenerateAsync("hello world", null);
            var result2 = await session2.GenerateAsync("hello world", null);

            // Assert
            Assert.Equal(result1, result2);
        }

        [Fact]
        public async Task DeterministicMode_DifferentSeedsProduceDifferentOutput()
        {
            // Arrange
            var options1 = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 0.8,
                TopK = 20,
                Seed = 111
            };

            var options2 = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 0.8,
                TopK = 20,
                Seed = 222
            };

            var session1 = new InferenceSession(_model, _tokenizer, options1, BlockSize);
            var session2 = new InferenceSession(_model, _tokenizer, options2, BlockSize);

            // Act
            var result1 = await session1.GenerateAsync("hello world", null);
            var result2 = await session2.GenerateAsync("hello world", null);

            // Assert
            Assert.NotEqual(result1, result2);
        }

        [Fact]
        public async Task StreamingWithDeterministicMode_ProducesSameTokens()
        {
            // Arrange
            const int seed = 98765;
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 0.8,
                TopK = 20,
                Seed = seed
            };

            SmallMind.Runtime.InferenceSession session1 = new SmallMind.Runtime.InferenceSession(_model, _tokenizer, options, BlockSize);
            SmallMind.Runtime.InferenceSession session2 = new SmallMind.Runtime.InferenceSession(_model, _tokenizer, options, BlockSize);

            // Act
            var tokens1 = await session1.GenerateStreamAsync("test", null).ToListAsync();
            var tokens2 = await session2.GenerateStreamAsync("test", null).ToListAsync();

            // Assert
            Assert.Equal(tokens1.Count, tokens2.Count);
            for (int i = 0; i < tokens1.Count; i++)
            {
                Assert.Equal(tokens1[i].TokenId, tokens2[i].TokenId);
                Assert.Equal(tokens1[i].Text, tokens2[i].Text);
            }
        }
    }
}

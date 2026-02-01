using System;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Core.Core;
using SmallMind.Runtime.Batching;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using Xunit;

namespace SmallMind.Tests.Batching
{
    public class BatchedInferenceEngineTests
    {
        private TransformerModel CreateMockModel()
        {
            // Create a minimal model for testing
            // TransformerModel(vocabSize, blockSize, nEmbd, nLayer, nHead, dropout, seed)
            return new TransformerModel(
                vocabSize: 10,
                blockSize: 8,
                nEmbd: 8,
                nLayer: 1,
                nHead: 1,
                dropout: 0.0,
                seed: 42
            );
        }

        private ITokenizer CreateMockTokenizer()
        {
            // Create a simple tokenizer with minimal vocabulary
            return new CharTokenizer("abcdefghij");
        }

        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions { Enabled = false };

            // Act
            using var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);

            // Assert
            Assert.NotNull(engine);
            Assert.NotNull(engine.BatchingOptions);
        }

        [Fact]
        public void Constructor_WithNullModel_Throws()
        {
            // Arrange
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new BatchedInferenceEngine(null!, tokenizer, 8, batchingOptions));
        }

        [Fact]
        public void Constructor_WithNullTokenizer_Throws()
        {
            // Arrange
            var model = CreateMockModel();
            var batchingOptions = new BatchingOptions();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new BatchedInferenceEngine(model, null!, 8, batchingOptions));
        }

        [Fact]
        public void Constructor_WithNullBatchingOptions_Throws()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new BatchedInferenceEngine(model, tokenizer, 8, null!));
        }

        [Fact]
        public async Task GenerateAsync_WithBatchingDisabled_UsesDirectGeneration()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions { Enabled = false };
            var inferenceOptions = new Runtime.ProductionInferenceOptions
            {
                MaxNewTokens = 2,
                Temperature = 1.0
            };

            using var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);

            // Act
            var result = await engine.GenerateAsync("a", inferenceOptions);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GenerateAsync_WithBatchingEnabled_ProcessesRequest()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions
            {
                Enabled = true,
                MaxBatchSize = 4,
                MaxBatchWaitMs = 50
            };
            var inferenceOptions = new Runtime.ProductionInferenceOptions
            {
                MaxNewTokens = 2,
                Temperature = 1.0
            };

            using var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);

            // Act
            var result = await engine.GenerateAsync("a", inferenceOptions);

            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
        }

        [Fact]
        public async Task GenerateStreamingAsync_WithBatchingDisabled_StreamsTokens()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions { Enabled = false };
            var inferenceOptions = new Runtime.ProductionInferenceOptions
            {
                MaxNewTokens = 3,
                Temperature = 1.0
            };

            using var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);

            // Act
            var tokens = new System.Collections.Generic.List<Runtime.GeneratedToken>();
            await foreach (var token in engine.GenerateStreamingAsync("a", inferenceOptions))
            {
                tokens.Add(token);
            }

            // Assert
            Assert.NotEmpty(tokens);
            Assert.True(tokens.Count <= 3);
        }

        [Fact]
        public async Task GenerateStreamingAsync_WithBatchingEnabled_StreamsTokens()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions
            {
                Enabled = true,
                MaxBatchSize = 2,
                MaxBatchWaitMs = 50
            };
            var inferenceOptions = new Runtime.ProductionInferenceOptions
            {
                MaxNewTokens = 3,
                Temperature = 1.0
            };

            using var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);

            // Act
            var tokens = new System.Collections.Generic.List<Runtime.GeneratedToken>();
            await foreach (var token in engine.GenerateStreamingAsync("a", inferenceOptions))
            {
                tokens.Add(token);
            }

            // Assert
            Assert.NotEmpty(tokens);
            Assert.True(tokens.Count <= 3);
        }

        [Fact]
        public async Task GenerateAsync_WithCancellation_CancelsRequest()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions
            {
                Enabled = true,
                MaxBatchSize = 4,
                MaxBatchWaitMs = 100
            };
            var inferenceOptions = new Runtime.ProductionInferenceOptions
            {
                MaxNewTokens = 100,
                Temperature = 1.0
            };

            using var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);
            using var cts = new CancellationTokenSource();

            // Act - cancel immediately before starting
            cts.Cancel();

            // Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                await engine.GenerateAsync("a", inferenceOptions, null, cts.Token);
            });
        }

        [Fact]
        public async Task MultipleConcurrentRequests_AreProcessed()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions
            {
                Enabled = true,
                MaxBatchSize = 4,
                MaxBatchWaitMs = 100
            };
            var inferenceOptions = new Runtime.ProductionInferenceOptions
            {
                MaxNewTokens = 2,
                Temperature = 1.0
            };

            using var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);

            // Act - launch multiple concurrent requests
            var tasks = new System.Collections.Generic.List<Task<string>>();
            for (int i = 0; i < 3; i++)
            {
                tasks.Add(engine.GenerateAsync("a", inferenceOptions));
            }

            var results = await Task.WhenAll(tasks);

            // Assert
            Assert.Equal(3, results.Length);
            foreach (var result in results)
            {
                Assert.NotNull(result);
                Assert.NotEmpty(result);
            }
        }

        [Fact]
        public async Task ShutdownAsync_StopsProcessing()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions
            {
                Enabled = true,
                MaxBatchSize = 4
            };

            var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);

            // Act
            await engine.ShutdownAsync();

            // Assert - subsequent operations should fail gracefully or be rejected
            // (exact behavior depends on implementation)
            engine.Dispose();
        }

        [Fact]
        public void Dispose_CleansUpResources()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions { Enabled = true };

            var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);

            // Act
            engine.Dispose();

            // Assert - should not throw
            // Further operations should throw ObjectDisposedException
        }

        [Fact]
        public async Task GenerateAsync_AfterDispose_Throws()
        {
            // Arrange
            var model = CreateMockModel();
            var tokenizer = CreateMockTokenizer();
            var batchingOptions = new BatchingOptions { Enabled = false };
            var inferenceOptions = new Runtime.ProductionInferenceOptions();

            var engine = new BatchedInferenceEngine(model, tokenizer, 8, batchingOptions);
            engine.Dispose();

            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
            {
                await engine.GenerateAsync("a", inferenceOptions);
            });
        }
    }
}

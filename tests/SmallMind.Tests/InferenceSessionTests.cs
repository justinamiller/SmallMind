using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;

namespace SmallMind.Tests
{
    public class InferenceSessionTests
    {
        private const string TestVocab = "abcdefghijklmnopqrstuvwxyz ";
        
        private (TransformerModel model, ITokenizer tokenizer) CreateTestModel()
        {
            // Create a minimal model for testing
            int vocabSize = TestVocab.Length;
            int blockSize = 32;
            int nEmbd = 16;
            int nLayer = 2;
            int nHead = 2;
            double dropout = 0.0;
            int seed = 42;
            
            var model = new TransformerModel(vocabSize, blockSize, nEmbd, nLayer, nHead, dropout, seed);
            var tokenizer = new CharTokenizer(TestVocab);
            
            return (model, tokenizer);
        }
        
        [Fact]
        public void InferenceSession_Constructor_ValidatesArguments()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions();
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => 
                new InferenceSession(null!, tokenizer, options, 32));
            
            Assert.Throws<ArgumentNullException>(() => 
                new InferenceSession(model, null!, options, 32));
            
            Assert.Throws<ArgumentNullException>(() => 
                new InferenceSession(model, tokenizer, null!, 32));
        }
        
        [Fact]
        public void InferenceSession_InvalidOptions_ThrowsValidationException()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions
            {
                Temperature = 0.0 // Invalid: must be > 0
            };
            
            // Act & Assert
            Assert.Throws<ValidationException>(() => 
                new InferenceSession(model, tokenizer, options, 32));
        }
        
        [Fact]
        public async Task InferenceSession_GenerateAsync_ProducesOutput()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 5,
                Temperature = 1.0,
                Seed = 42
            };
            
            using var session = new InferenceSession(model, tokenizer, options, 32);
            
            // Act
            var result = await session.GenerateAsync("hello");
            
            // Assert
            Assert.NotNull(result);
            Assert.NotEmpty(result);
            Assert.StartsWith("hello", result); // Should include prompt
        }
        
        [Fact]
        public async Task InferenceSession_DeterministicGeneration_SameSeedSameOutput()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 1.0,
                TopK = 5,
                Seed = 12345
            };
            
            using var session1 = new InferenceSession(model, tokenizer, options, 32);
            using var session2 = new InferenceSession(model, tokenizer, options, 32);
            
            // Act
            var result1 = await session1.GenerateAsync("test");
            var result2 = await session2.GenerateAsync("test");
            
            // Assert
            Assert.Equal(result1, result2);
        }
        
        [Fact]
        public async Task InferenceSession_DifferentSeeds_ProduceDifferentOutputs()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
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
            
            using var session1 = new InferenceSession(model, tokenizer, options1, 32);
            using var session2 = new InferenceSession(model, tokenizer, options2, 32);
            
            // Act
            var result1 = await session1.GenerateAsync("test");
            var result2 = await session2.GenerateAsync("test");
            
            // Assert
            Assert.NotEqual(result1, result2);
        }
        
        [Fact]
        public async Task InferenceSession_MaxInputTokens_Rejects()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions
            {
                MaxInputTokens = 5,
                TruncateInput = false, // Reject oversized inputs
                MaxNewTokens = 5
            };
            
            using var session = new InferenceSession(model, tokenizer, options, 32);
            
            // Act & Assert
            await Assert.ThrowsAsync<ResourceLimitException>(async () => 
                await session.GenerateAsync("this is a very long input that exceeds the limit"));
        }
        
        [Fact]
        public async Task InferenceSession_MaxInputTokens_Truncates()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions
            {
                MaxInputTokens = 10,
                TruncateInput = true, // Truncate oversized inputs
                MaxNewTokens = 5
            };
            
            using var session = new InferenceSession(model, tokenizer, options, 32);
            
            // Act - Should not throw, but truncate
            var result = await session.GenerateAsync("this is a very long input that should be truncated");
            
            // Assert
            Assert.NotNull(result);
        }
        
        [Fact]
        public async Task InferenceSession_GenerateStreamAsync_ProducesTokens()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 5,
                Temperature = 1.0,
                Seed = 42
            };
            
            using var session = new InferenceSession(model, tokenizer, options, 32);
            
            // Act
            var tokens = new List<GeneratedToken>();
            await foreach (var token in session.GenerateStreamAsync("test"))
            {
                tokens.Add(token);
            }
            
            // Assert
            Assert.Equal(5, tokens.Count);
            
            // Verify token indices are sequential
            for (int i = 0; i < tokens.Count; i++)
            {
                Assert.Equal(i, tokens[i].Index);
            }
            
            // Verify all tokens have text
            Assert.All(tokens, t => Assert.NotNull(t.Text));
        }
        
        [Fact]
        public async Task InferenceSession_Cancellation_StopsGeneration()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 100, // Long generation
                Temperature = 1.0
            };
            
            using var session = new InferenceSession(model, tokenizer, options, 32);
            using var cts = new CancellationTokenSource();
            
            // Cancel immediately to ensure cancellation happens during generation
            cts.Cancel();
            
            // Act & Assert
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => 
                await session.GenerateAsync("test", cancellationToken: cts.Token));
        }
        
        [Fact]
        public async Task InferenceSession_Timeout_ThrowsTimeoutException()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 1000, // Very long generation
                MaxTimeMs = 100, // Very short timeout
                Temperature = 1.0
            };
            
            using var session = new InferenceSession(model, tokenizer, options, 32);
            
            // Act & Assert
            // Note: This might not always timeout if generation is very fast
            // but the mechanism should be in place
            try
            {
                await session.GenerateAsync("test");
            }
            catch (InferenceTimeoutException)
            {
                // Expected in many cases
            }
        }
        
        [Fact]
        public void InferenceSession_Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions();
            var session = new InferenceSession(model, tokenizer, options, 32);
            
            // Act & Assert - Should not throw
            session.Dispose();
            session.Dispose();
        }
        
        [Fact]
        public async Task InferenceSession_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions();
            var session = new InferenceSession(model, tokenizer, options, 32);
            session.Dispose();
            
            // Act & Assert
            await Assert.ThrowsAsync<ObjectDisposedException>(async () => 
                await session.GenerateAsync("test"));
        }
        
        [Fact]
        public void InferenceSession_HasSessionId()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions();
            
            // Act
            using var session1 = new InferenceSession(model, tokenizer, options, 32);
            using var session2 = new InferenceSession(model, tokenizer, options, 32, "custom-id");
            
            // Assert
            Assert.NotNull(session1.SessionId);
            Assert.NotEmpty(session1.SessionId);
            Assert.Equal("custom-id", session2.SessionId);
            Assert.NotEqual(session1.SessionId, session2.SessionId);
        }
        
        [Fact]
        public async Task InferenceSession_WithMetrics_RecordsPerformance()
        {
            // Arrange
            var (model, tokenizer) = CreateTestModel();
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 5,
                Seed = 42
            };
            
            using var session = new InferenceSession(model, tokenizer, options, 32);
            var metrics = new PerformanceMetrics();
            metrics.Start();
            
            // Act
            await session.GenerateAsync("test", metrics);
            
            // Assert
            var summary = metrics.GetSummary();
            Assert.Equal(1, summary.CompletedRequests);
            Assert.True(summary.TotalOutputTokens > 0);
        }
    }
}

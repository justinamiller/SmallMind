using System;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Runtime.Batching;
using SmallMind.Runtime.Cache;
using Xunit;

namespace SmallMind.Tests.Batching
{
    public class InferenceRequestTests
    {
        [Fact]
        public void Constructor_WithValidParameters_Succeeds()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3, 4, 5 };
            var options = new Runtime.ProductionInferenceOptions();

            // Act
            using var request = new InferenceRequest(sessionId, tokens, options);

            // Assert
            Assert.Equal(sessionId, request.SessionId);
            Assert.Same(tokens, request.PromptTokens);
            Assert.Same(options, request.Options);
            Assert.Equal(0, request.CurrentPosition);
            Assert.Equal(0, request.GeneratedTokenCount);
            Assert.False(request.IsComplete);
        }

        [Fact]
        public void Constructor_WithNullTokens_Throws()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var options = new Runtime.ProductionInferenceOptions();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new InferenceRequest(sessionId, null!, options));
        }

        [Fact]
        public void Constructor_WithNullOptions_Throws()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new InferenceRequest(sessionId, tokens, null!));
        }

        [Fact]
        public void IsCancelled_WithDefaultToken_ReturnsFalse()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };
            var options = new Runtime.ProductionInferenceOptions();

            // Act
            using var request = new InferenceRequest(sessionId, tokens, options);

            // Assert
            Assert.False(request.IsCancelled);
        }

        [Fact]
        public void IsCancelled_WithCancelledToken_ReturnsTrue()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };
            var options = new Runtime.ProductionInferenceOptions();
            var cts = new CancellationTokenSource();

            using var request = new InferenceRequest(sessionId, tokens, options, cts.Token);

            // Act
            cts.Cancel();

            // Assert
            Assert.True(request.IsCancelled);
        }

        [Fact]
        public void MarkComplete_CompletesRequest()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };
            var options = new Runtime.ProductionInferenceOptions();

            using var request = new InferenceRequest(sessionId, tokens, options);

            // Act
            request.MarkComplete();

            // Assert
            Assert.True(request.IsComplete);
        }

        [Fact]
        public async Task MarkComplete_ClosesResponseChannel()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };
            var options = new Runtime.ProductionInferenceOptions();

            using var request = new InferenceRequest(sessionId, tokens, options);

            // Act
            request.MarkComplete();

            // Assert
            var canRead = await request.ResponseReader.WaitToReadAsync();
            Assert.False(canRead);
        }

        [Fact]
        public void MarkFailed_CompletesRequestWithException()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };
            var options = new Runtime.ProductionInferenceOptions();

            using var request = new InferenceRequest(sessionId, tokens, options);
            var exception = new InvalidOperationException("Test error");

            // Act
            request.MarkFailed(exception);

            // Assert
            Assert.True(request.IsComplete);
        }

        [Fact]
        public async Task MarkFailed_PropagatesExceptionToReader()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };
            var options = new Runtime.ProductionInferenceOptions();

            using var request = new InferenceRequest(sessionId, tokens, options);
            var exception = new InvalidOperationException("Test error");

            // Act
            request.MarkFailed(exception);

            // Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var token in request.ResponseReader.ReadAllAsync())
                {
                    // Should throw before reaching here
                }
            });
        }

        [Fact]
        public void IsCompatibleWith_WithAnotherRequest_ReturnsTrue()
        {
            // Arrange
            var sessionId1 = SessionId.NewId();
            var sessionId2 = SessionId.NewId();
            var tokens1 = new[] { 1, 2, 3 };
            var tokens2 = new[] { 4, 5, 6 };
            var options = new Runtime.ProductionInferenceOptions();

            using var request1 = new InferenceRequest(sessionId1, tokens1, options);
            using var request2 = new InferenceRequest(sessionId2, tokens2, options);

            // Act
            var compatible = request1.IsCompatibleWith(request2);

            // Assert
            Assert.True(compatible);
        }

        [Fact]
        public void IsCompatibleWith_WithNull_ReturnsFalse()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };
            var options = new Runtime.ProductionInferenceOptions();

            using var request = new InferenceRequest(sessionId, tokens, options);

            // Act
            var compatible = request.IsCompatibleWith(null!);

            // Assert
            Assert.False(compatible);
        }

        [Fact]
        public void Dispose_MarksRequestComplete()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };
            var options = new Runtime.ProductionInferenceOptions();

            var request = new InferenceRequest(sessionId, tokens, options);

            // Act
            request.Dispose();

            // Assert
            Assert.True(request.IsComplete);
        }

        [Fact]
        public async Task ResponseChannel_CanWriteAndRead()
        {
            // Arrange
            var sessionId = SessionId.NewId();
            var tokens = new[] { 1, 2, 3 };
            var options = new Runtime.ProductionInferenceOptions();

            using var request = new InferenceRequest(sessionId, tokens, options);

            var token1 = new Runtime.GeneratedToken(42, "hello", 0);
            var token2 = new Runtime.GeneratedToken(43, "world", 1);

            // Act
            await request.ResponseWriter.WriteAsync(token1);
            await request.ResponseWriter.WriteAsync(token2);
            request.MarkComplete();

            // Assert
            var results = new System.Collections.Generic.List<Runtime.GeneratedToken>();
            await foreach (var token in request.ResponseReader.ReadAllAsync())
            {
                results.Add(token);
            }

            Assert.Equal(2, results.Count);
            Assert.Equal(42, results[0].TokenId);
            Assert.Equal(43, results[1].TokenId);
        }
    }
}

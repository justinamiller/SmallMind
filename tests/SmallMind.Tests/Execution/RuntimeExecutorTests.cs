using System;
using Xunit;
using SmallMind.Runtime.Cache;
using SmallMind.Runtime.Telemetry;
using SmallMind.Transformers;
using SmallMind.Tokenizers;
using ExecutionContext = SmallMind.Runtime.Execution.ExecutionContext;
using RuntimeOptions = SmallMind.Runtime.Execution.RuntimeOptions;
using RuntimeExecutor = SmallMind.Runtime.Execution.RuntimeExecutor;

namespace SmallMind.Tests.Execution
{
    public class RuntimeExecutorTests : IDisposable
    {
        private const string TestVocab = "abcdefghijklmnopqrstuvwxyz ";
        private readonly TransformerModel _model;
        private readonly ITokenizer _tokenizer;
        private readonly KvCachePool _cachePool;
        private readonly RuntimeExecutor _executor;

        public RuntimeExecutorTests()
        {
            // Create a minimal model for testing
            int vocabSize = TestVocab.Length;
            int blockSize = 32;
            int nEmbd = 16;
            int nLayer = 2;
            int nHead = 2;
            double dropout = 0.0;
            int seed = 42;

            _model = new TransformerModel(vocabSize, blockSize, nEmbd, nLayer, nHead, dropout, seed);
            _tokenizer = new CharTokenizer(TestVocab);
            _cachePool = new KvCachePool();
            _executor = new RuntimeExecutor(_model, _cachePool, blockSize);
        }

        public void Dispose()
        {
            _cachePool?.Dispose();
        }

        [Fact]
        public void Prefill_ValidPrompt_ReturnsResult()
        {
            // Arrange
            var options = new RuntimeOptions();
            var context = new ExecutionContext(options);
            var promptTokens = new int[] { 0, 1, 2, 3, 4 }; // "abcde"

            // Act
            var result = _executor.Prefill(promptTokens, context);

            // Assert
            Assert.NotNull(result.Logits);
            Assert.True(result.Logits.Length > 0);
            Assert.NotNull(result.CacheHandle);
            Assert.Equal(promptTokens.Length, result.ProcessedTokens);
            Assert.True(result.Metrics.ElapsedMs >= 0);
            Assert.Equal(promptTokens.Length, context.CurrentPosition);
            Assert.True(context.HasCache);
        }

        [Fact]
        public void Decode_AfterPrefill_ReturnsResult()
        {
            // Arrange
            var options = new RuntimeOptions();
            var context = new ExecutionContext(options);
            var promptTokens = new int[] { 0, 1, 2, 3, 4 };

            // Prefill first
            var prefillResult = _executor.Prefill(promptTokens, context);

            // Act - Decode next token
            var decodeResult = _executor.Decode(5, context);

            // Assert
            Assert.NotNull(decodeResult.Logits);
            Assert.True(decodeResult.Logits.Length > 0);
            Assert.NotNull(decodeResult.CacheHandle);
            Assert.True(decodeResult.Metrics.ElapsedMs >= 0);
            Assert.True(decodeResult.Metrics.CacheUsed);
            Assert.Equal(promptTokens.Length + 1, context.CurrentPosition);
        }

        [Fact]
        public void Decode_WithoutPrefill_ThrowsException()
        {
            // Arrange
            var options = new RuntimeOptions { RequireKvCache = true };
            var context = new ExecutionContext(options);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _executor.Decode(0, context));
        }

        [Fact]
        public void Prefill_WithExistingCache_ThrowsException()
        {
            // Arrange
            var options = new RuntimeOptions();
            var context = new ExecutionContext(options);
            var promptTokens = new int[] { 0, 1, 2 };

            // Prefill first time
            _executor.Prefill(promptTokens, context);

            // Act & Assert - Try to prefill again without reset
            Assert.Throws<InvalidOperationException>(() => 
                _executor.Prefill(promptTokens, context));
        }

        [Fact]
        public void PrefillAndDecode_Sequence_WorksCorrectly()
        {
            // Arrange
            var options = new RuntimeOptions();
            var context = new ExecutionContext(options);
            var promptTokens = new int[] { 0, 1, 2 }; // "abc"

            // Act - Prefill
            var prefillResult = _executor.Prefill(promptTokens, context);
            Assert.Equal(3, context.CurrentPosition);

            // Act - Decode multiple tokens
            var decode1 = _executor.Decode(3, context);
            Assert.Equal(4, context.CurrentPosition);

            var decode2 = _executor.Decode(4, context);
            Assert.Equal(5, context.CurrentPosition);

            var decode3 = _executor.Decode(5, context);
            Assert.Equal(6, context.CurrentPosition);

            // Assert
            Assert.NotNull(prefillResult.CacheHandle);
            Assert.Same(prefillResult.CacheHandle, decode1.CacheHandle);
            Assert.Same(prefillResult.CacheHandle, decode2.CacheHandle);
            Assert.Same(prefillResult.CacheHandle, decode3.CacheHandle);
        }
    }
}

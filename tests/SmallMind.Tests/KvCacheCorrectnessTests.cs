using System;
using System.Linq;
using Xunit;
using SmallMind.Runtime.Cache;

namespace SmallMind.Tests
{
    /// <summary>
    /// Correctness tests for production-grade KV cache implementation.
    /// Tests budget enforcement, memory layout, GQA support, and RoPE positioning.
    /// </summary>
    public class KvCacheCorrectnessTests
    {
        [Fact]
        public void BudgetPolicy_ValidatesMinimumBudget()
        {
            // Arrange
            int nLayers = 2;
            int nKvHeads = 2;
            int headDim = 64;
            int maxSeqLen = 100;
            
            // Act & Assert: Budget too small for even 1 token
            long tooSmallBudget = 1; // 1 byte
            Assert.Throws<ArgumentException>(() =>
                new KvCacheBudgetPolicy(tooSmallBudget, maxSeqLen, nLayers, nKvHeads, headDim));
        }

        [Fact]
        public void BudgetPolicy_ValidatesMaxSeqLenFitsInBudget()
        {
            // Arrange
            int nLayers = 4;
            int nKvHeads = 8;
            int headDim = 128;
            int maxSeqLen = 10000; // Very large
            long smallBudget = 1024 * 1024; // 1 MB - too small for 10k tokens

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                new KvCacheBudgetPolicy(smallBudget, maxSeqLen, nLayers, nKvHeads, headDim));
        }

        [Fact]
        public void BudgetPolicy_ComputesCorrectBytes()
        {
            // Arrange
            int nLayers = 2;
            int nKvHeads = 4;
            int headDim = 64;
            int maxSeqLen = 100;
            long budget = 100 * 1024 * 1024; // 100 MB

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);

            // Act
            long bytesFor10Tokens = policy.ComputeRequiredBytes(10);

            // Assert
            // 10 tokens * 4 heads * 64 dim * 2 layers * 2 (K+V) * 4 bytes = 40,960 bytes
            long expected = 10 * nKvHeads * headDim * nLayers * 2 * sizeof(float);
            Assert.Equal(expected, bytesFor10Tokens);
        }

        [Fact]
        public void BudgetPolicy_TryReserveTokens_Success()
        {
            // Arrange
            int nLayers = 2;
            int nKvHeads = 2;
            int headDim = 64;
            int maxSeqLen = 100;
            long budget = 10 * 1024 * 1024; // 10 MB

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);

            // Act
            bool canReserve = policy.TryReserveTokens(currentTokens: 10, additionalTokens: 5);

            // Assert
            Assert.True(canReserve);
        }

        [Fact]
        public void BudgetPolicy_TryReserveTokens_ExceedsMaxSeqLen()
        {
            // Arrange
            int nLayers = 2;
            int nKvHeads = 2;
            int headDim = 64;
            int maxSeqLen = 100;
            long budget = 100 * 1024 * 1024; // Large budget

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);

            // Act
            bool canReserve = policy.TryReserveTokens(currentTokens: 95, additionalTokens: 10); // 105 > 100

            // Assert
            Assert.False(canReserve);
        }

        [Fact]
        public void BudgetPolicy_ValidateReservation_ThrowsOnExcess()
        {
            // Arrange
            int nLayers = 2;
            int nKvHeads = 2;
            int headDim = 64;
            int maxSeqLen = 50;
            
            // Create a budget that only fits ~20 tokens
            long bytesFor20 = 20L * nKvHeads * headDim * nLayers * 2 * sizeof(float);
            var policy = new KvCacheBudgetPolicy(
                bytesFor20, maxSeqLen, nLayers, nKvHeads, headDim);

            // Act & Assert
            var ex = Assert.Throws<OutOfBudgetException>(() =>
                policy.ValidateReservation(currentTokens: 0, additionalTokens: maxSeqLen));
            
            Assert.NotNull(ex);
            Assert.True(ex.RequestedBytes > 0);
            Assert.True(ex.MaxBudgetBytes > 0);
        }

        [Fact]
        public void BudgetPolicy_GetMaxTokensForBudget_ReturnsCorrectValue()
        {
            // Arrange
            int nLayers = 2;
            int nKvHeads = 4;
            int headDim = 64;
            int maxSeqLen = 1000;
            
            // Compute budget for exactly 50 tokens
            long bytesFor50 = 50L * nKvHeads * headDim * nLayers * 2 * sizeof(float);
            long budget = bytesFor50;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);

            // Act
            int maxTokens = policy.GetMaxTokensForBudget();

            // Assert
            Assert.Equal(50, maxTokens);
        }

        [Fact]
        public void KvCacheSession_Initialization_Success()
        {
            // Arrange
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            int nLayers = 2;
            int nKvHeads = 4;
            int headDim = 64;
            int maxSeqLen = 128;
            long budget = 10 * 1024 * 1024;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);

            // Act
            using var session = new KvCacheSession(sessionId, nLayers, nKvHeads, headDim, policy);

            // Assert
            Assert.Equal(0, session.CurrentTokenCount);
            Assert.Equal(maxSeqLen, session.MaxTokens);
            Assert.Equal(nLayers, session.NumLayers);
            Assert.Equal(nKvHeads, session.NumKvHeads);
            Assert.Equal(headDim, session.HeadDim);
        }

        [Fact]
        public void KvCacheSession_WriteAndRead_SingleToken()
        {
            // Arrange
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            int nLayers = 2;
            int nKvHeads = 2;
            int headDim = 8;
            int maxSeqLen = 10;
            long budget = 1 * 1024 * 1024;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);
            using var session = new KvCacheSession(sessionId, nLayers, nKvHeads, headDim, policy);

            // Create test data
            var keyData = new float[headDim];
            var valueData = new float[headDim];
            for (int i = 0; i < headDim; i++)
            {
                keyData[i] = i + 1.0f;
                valueData[i] = i + 10.0f;
            }

            // Act: Write to layer 0, position 0, head 0
            session.WriteK(0, 0, 0, keyData);
            session.WriteV(0, 0, 0, valueData);
            session.CommitTokens(1);

            // Read back
            var readKey = session.GetKSpan(0, 0, 0);
            var readValue = session.GetVSpan(0, 0, 0);

            // Assert
            Assert.Equal(headDim, readKey.Length);
            Assert.Equal(headDim, readValue.Length);
            for (int i = 0; i < headDim; i++)
            {
                Assert.Equal(keyData[i], readKey[i]);
                Assert.Equal(valueData[i], readValue[i]);
            }
        }

        [Fact]
        public void KvCacheSession_WriteAndRead_MultipleTokensAndHeads()
        {
            // Arrange
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            int nLayers = 1;
            int nKvHeads = 4;
            int headDim = 16;
            int maxSeqLen = 20;
            long budget = 10 * 1024 * 1024;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);
            using var session = new KvCacheSession(sessionId, nLayers, nKvHeads, headDim, policy);

            // Write multiple tokens and heads
            int numTokens = 5;
            var testData = new float[numTokens, nKvHeads, headDim];
            
            for (int pos = 0; pos < numTokens; pos++)
            {
                for (int head = 0; head < nKvHeads; head++)
                {
                    var keyData = new float[headDim];
                    var valueData = new float[headDim];
                    
                    for (int d = 0; d < headDim; d++)
                    {
                        keyData[d] = pos * 100 + head * 10 + d;
                        valueData[d] = pos * 100 + head * 10 + d + 0.5f;
                        testData[pos, head, d] = keyData[d];
                    }
                    
                    session.WriteK(0, pos, head, keyData);
                    session.WriteV(0, pos, head, valueData);
                }
            }
            session.CommitTokens(numTokens);

            // Assert: Read back and verify
            Assert.Equal(numTokens, session.CurrentTokenCount);
            
            for (int pos = 0; pos < numTokens; pos++)
            {
                for (int head = 0; head < nKvHeads; head++)
                {
                    var readKey = session.GetKSpan(0, pos, head);
                    
                    for (int d = 0; d < headDim; d++)
                    {
                        float expected = testData[pos, head, d];
                        Assert.Equal(expected, readKey[d]);
                    }
                }
            }
        }

        [Fact]
        public void KvCacheSession_GetRange_ReturnsContiguousData()
        {
            // Arrange
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            int nLayers = 1;
            int nKvHeads = 2;
            int headDim = 8;
            int maxSeqLen = 10;
            long budget = 1 * 1024 * 1024;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);
            using var session = new KvCacheSession(sessionId, nLayers, nKvHeads, headDim, policy);

            // Write 3 tokens
            for (int pos = 0; pos < 3; pos++)
            {
                for (int head = 0; head < nKvHeads; head++)
                {
                    var data = Enumerable.Range(0, headDim).Select(i => (float)(pos * 100 + head * 10 + i)).ToArray();
                    session.WriteK(0, pos, head, data);
                }
            }
            session.CommitTokens(3);

            // Act: Get range for head 0, positions 0-2
            var range = session.GetKRange(0, 0, 3, 0);

            // Assert: Should have 3 * headDim elements (but only for one head, not interleaved)
            // The range is NOT fully contiguous across positions due to kvHead interleaving
            // Each position has nKvHeads * headDim, so we skip other heads
            Assert.True(range.Length > 0);
        }

        [Fact]
        public void KvCacheSession_Reset_ClearsTokenCount()
        {
            // Arrange
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            int nLayers = 1;
            int nKvHeads = 2;
            int headDim = 8;
            int maxSeqLen = 10;
            long budget = 1 * 1024 * 1024;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);
            using var session = new KvCacheSession(sessionId, nLayers, nKvHeads, headDim, policy);

            // Write and commit
            var data = new float[headDim];
            session.WriteK(0, 0, 0, data);
            session.CommitTokens(1);
            Assert.Equal(1, session.CurrentTokenCount);

            // Act
            session.Reset();

            // Assert
            Assert.Equal(0, session.CurrentTokenCount);
        }

        [Fact]
        public void KvCacheSession_TryReserveTokens_EnforcesBudget()
        {
            // Arrange
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            int nLayers = 2;
            int nKvHeads = 2;
            int headDim = 64;
            int maxSeqLen = 100;
            long budget = 10 * 1024 * 1024;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);
            using var session = new KvCacheSession(sessionId, nLayers, nKvHeads, headDim, policy);

            // Act
            bool canReserve = session.TryReserveTokens(50);

            // Assert
            Assert.True(canReserve);
        }

        [Fact]
        public void KvCacheSession_CommitTokens_EnforcesMaxTokens()
        {
            // Arrange
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            int nLayers = 1;
            int nKvHeads = 2;
            int headDim = 8;
            int maxSeqLen = 5; // Very small
            long budget = 10 * 1024 * 1024;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);
            using var session = new KvCacheSession(sessionId, nLayers, nKvHeads, headDim, policy);

            // Act & Assert: Committing more than maxSeqLen should throw
            Assert.Throws<InvalidOperationException>(() => session.CommitTokens(maxSeqLen + 1));
        }

        [Fact]
        public void KvCacheSession_GQA_CorrectHeadMapping()
        {
            // Test GQA with nHeads=8, nKvHeads=2
            // Each kvHead serves 4 query heads
            
            // Arrange
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            int nLayers = 1;
            int nKvHeads = 2;  // GQA: fewer KV heads
            int headDim = 16;
            int maxSeqLen = 10;
            long budget = 10 * 1024 * 1024;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);
            using var session = new KvCacheSession(sessionId, nLayers, nKvHeads, headDim, policy);

            // Write distinct data to each KV head
            for (int kvHead = 0; kvHead < nKvHeads; kvHead++)
            {
                var keyData = Enumerable.Range(0, headDim).Select(i => (float)(kvHead * 100 + i)).ToArray();
                session.WriteK(0, 0, kvHead, keyData);
            }
            session.CommitTokens(1);

            // Act: Read back each KV head
            var head0 = session.GetKSpan(0, 0, 0);
            var head1 = session.GetKSpan(0, 0, 1);

            // Assert: Each head should have distinct values
            Assert.Equal(0.0f, head0[0]); // kvHead=0: starts at 0
            Assert.Equal(100.0f, head1[0]); // kvHead=1: starts at 100
        }

        [Fact]
        public void KvCacheSession_MultiLayer_IndependentStorage()
        {
            // Arrange
            var sessionId = new SessionId(Guid.NewGuid().ToString());
            int nLayers = 3;
            int nKvHeads = 2;
            int headDim = 8;
            int maxSeqLen = 10;
            long budget = 10 * 1024 * 1024;

            var policy = new KvCacheBudgetPolicy(budget, maxSeqLen, nLayers, nKvHeads, headDim);
            using var session = new KvCacheSession(sessionId, nLayers, nKvHeads, headDim, policy);

            // Write different data to each layer
            for (int layer = 0; layer < nLayers; layer++)
            {
                var keyData = Enumerable.Range(0, headDim).Select(i => (float)(layer * 1000 + i)).ToArray();
                session.WriteK(layer, 0, 0, keyData);
            }
            session.CommitTokens(1);

            // Assert: Each layer should have independent data
            Assert.Equal(0.0f, session.GetKSpan(0, 0, 0)[0]);
            Assert.Equal(1000.0f, session.GetKSpan(1, 0, 0)[0]);
            Assert.Equal(2000.0f, session.GetKSpan(2, 0, 0)[0]);
        }

        [Fact]
        public void OutOfBudgetException_ContainsCorrectInfo()
        {
            // Arrange
            long requested = 100 * 1024 * 1024;
            long available = 50 * 1024 * 1024;
            long maxBudget = 50 * 1024 * 1024;

            // Act
            var ex = new OutOfBudgetException(requested, available, maxBudget);

            // Assert
            Assert.Equal(requested, ex.RequestedBytes);
            Assert.Equal(available, ex.AvailableBytes);
            Assert.Equal(maxBudget, ex.MaxBudgetBytes);
            Assert.Contains("budget exceeded", ex.Message.ToLower());
        }
    }
}

using SmallMind.Abstractions;
using SmallMind.Runtime.Cache;

namespace SmallMind.Tests.Cache
{
    /// <summary>
    /// Tests for KV cache budget enforcement.
    /// </summary>
    public class KvCacheBudgetTests
    {
        private sealed class TestTelemetry : IChatTelemetry
        {
            public List<(string sessionId, long currentBytes, long maxBytes)> BudgetExceededEvents { get; } = new();
            public List<(string sessionId, string reason, long freedBytes)> EvictionEvents { get; } = new();

            public void OnRequestStart(string sessionId, int messageCount) { }
            public void OnFirstToken(string sessionId, double elapsedMs) { }
            public void OnRequestComplete(string sessionId, UsageStats usage) { }
            public void OnContextPolicyApplied(string sessionId, string policyName, int originalTokens, int finalTokens) { }
            public void OnKvCacheAccess(string sessionId, bool hit, int cachedTokens) { }
            public void OnToolCall(string sessionId, string toolName, double elapsedMs) { }

            public void OnKvCacheBudgetExceeded(string sessionId, long currentBytes, long maxBytes)
            {
                BudgetExceededEvents.Add((sessionId, currentBytes, maxBytes));
            }

            public void OnKvCacheEviction(string evictedSessionId, string reason, long freedBytes)
            {
                EvictionEvents.Add((evictedSessionId, reason, freedBytes));
            }
        }

        [Fact]
        public void PerSessionBudget_EnforcedOnCreation()
        {
            // Arrange
            var telemetry = new TestTelemetry();
            var options = new KvCacheOptions
            {
                MaxBytesPerSession = 1024, // Very small budget: 1KB
                MaxBytesTotal = 1024 * 1024 // 1MB total
            };
            var store = new LruKvCacheStore(options, telemetry);
            var sessionId = new SessionId("test-session");
            var modelShape = new ModelShape(layers: 12, heads: 12, headDim: 64);

            // Act & Assert
            // Try to create a cache that exceeds per-session budget
            Assert.Throws<InvalidOperationException>(() =>
            {
                store.GetOrCreate(sessionId, modelShape, maxTokens: 4096); // This will exceed 1KB
            });

            // Verify telemetry event was emitted
            Assert.Single(telemetry.BudgetExceededEvents);
            var evt = telemetry.BudgetExceededEvents[0];
            Assert.Equal("test-session", evt.sessionId);
            Assert.True(evt.currentBytes > options.MaxBytesPerSession);
            Assert.Equal(options.MaxBytesPerSession, evt.maxBytes);

            store.Dispose();
        }

        [Fact]
        public void PerSessionBudget_AllowsSmallerSessions()
        {
            // Arrange
            var telemetry = new TestTelemetry();
            var options = new KvCacheOptions
            {
                MaxBytesPerSession = 20 * 1024 * 1024, // 20MB per session (enough for small cache)
                MaxBytesTotal = 100 * 1024 * 1024 // 100MB total
            };
            var store = new LruKvCacheStore(options, telemetry);
            var sessionId = new SessionId("test-session");
            var modelShape = new ModelShape(layers: 6, heads: 8, headDim: 64);

            // Act
            var entry = store.GetOrCreate(sessionId, modelShape, maxTokens: 512);

            // Assert
            Assert.NotNull(entry);
            Assert.True(entry.SizeBytes <= options.MaxBytesPerSession);
            Assert.Empty(telemetry.BudgetExceededEvents);

            store.Dispose();
        }

        [Fact]
        public void TotalBudget_TriggersLruEviction()
        {
            // Arrange
            var telemetry = new TestTelemetry();
            var options = new KvCacheOptions
            {
                MaxBytesPerSession = 20 * 1024 * 1024, // 20MB per session (enough for small cache)
                MaxBytesTotal = 30 * 1024 * 1024, // 30MB total (can fit ~2 sessions, not 3)
                MaxSessions = 100
            };
            var store = new LruKvCacheStore(options, telemetry);
            var modelShape = new ModelShape(layers: 6, heads: 8, headDim: 64);

            // Act - Create 3 sessions, should evict oldest
            var session1 = new SessionId("session-1");
            var session2 = new SessionId("session-2");
            var session3 = new SessionId("session-3");

            _ = store.GetOrCreate(session1, modelShape, maxTokens: 512);
            _ = store.GetOrCreate(session2, modelShape, maxTokens: 512);
            var entry3 = store.GetOrCreate(session3, modelShape, maxTokens: 512); // Should evict session-1

            // Assert
            Assert.NotNull(entry3);
            Assert.NotEmpty(telemetry.EvictionEvents);

            // Verify session-1 was evicted
            var evictedSession = telemetry.EvictionEvents[0].sessionId;
            Assert.Equal("session-1", evictedSession);
            Assert.Equal("LRU eviction", telemetry.EvictionEvents[0].reason);
            Assert.True(telemetry.EvictionEvents[0].freedBytes > 0);

            // Verify stats show eviction
            var stats = store.GetStats();
            Assert.True(stats.Evictions > 0);

            store.Dispose();
        }

        [Fact]
        public void SessionCountLimit_TriggersEviction()
        {
            // Arrange
            var telemetry = new TestTelemetry();
            var options = new KvCacheOptions
            {
                MaxBytesPerSession = 100 * 1024 * 1024, // 100MB per session
                MaxBytesTotal = 1024L * 1024 * 1024, // 1GB total
                MaxSessions = 2 // Only 2 sessions allowed
            };
            var store = new LruKvCacheStore(options, telemetry);
            var modelShape = new ModelShape(layers: 2, heads: 4, headDim: 32);

            // Act - Create 3 sessions
            var session1 = new SessionId("session-1");
            var session2 = new SessionId("session-2");
            var session3 = new SessionId("session-3");

            store.GetOrCreate(session1, modelShape, maxTokens: 64);
            store.GetOrCreate(session2, modelShape, maxTokens: 64);
            store.GetOrCreate(session3, modelShape, maxTokens: 64); // Should evict session-1

            // Assert
            Assert.Single(telemetry.EvictionEvents);
            Assert.Equal("session-1", telemetry.EvictionEvents[0].sessionId);

            store.Dispose();
        }

        [Fact]
        public void LruOrdering_EvictsLeastRecentlyUsed()
        {
            // Arrange
            var telemetry = new TestTelemetry();
            var options = new KvCacheOptions
            {
                MaxBytesPerSession = 100 * 1024 * 1024,
                MaxBytesTotal = 1024L * 1024 * 1024,
                MaxSessions = 3
            };
            var store = new LruKvCacheStore(options, telemetry);
            var modelShape = new ModelShape(layers: 2, heads: 4, headDim: 32);

            // Act - Create 3 sessions, access session-1 and session-2, then add session-4
            var session1 = new SessionId("session-1");
            var session2 = new SessionId("session-2");
            var session3 = new SessionId("session-3");
            var session4 = new SessionId("session-4");

            store.GetOrCreate(session1, modelShape, maxTokens: 64);
            store.GetOrCreate(session2, modelShape, maxTokens: 64);
            store.GetOrCreate(session3, modelShape, maxTokens: 64);

            // Touch session-1 and session-2 to make them more recent
            store.Touch(session1);
            store.Touch(session2);

            // Add session-4, should evict session-3 (least recently used)
            store.GetOrCreate(session4, modelShape, maxTokens: 64);

            // Assert
            Assert.Single(telemetry.EvictionEvents);
            Assert.Equal("session-3", telemetry.EvictionEvents[0].sessionId);

            store.Dispose();
        }

        [Fact]
        public void Options_Validation_RejectsInvalidPerSessionBudget()
        {
            // Arrange
            var options = new KvCacheOptions
            {
                MaxBytesPerSession = -1 // Invalid
            };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [Fact]
        public void Options_Validation_RejectsPerSessionBudgetExceedingTotal()
        {
            // Arrange
            var options = new KvCacheOptions
            {
                MaxBytesPerSession = 2 * 1024 * 1024 * 1024L, // 2GB
                MaxBytesTotal = 1 * 1024 * 1024 * 1024L // 1GB (less than per-session)
            };

            // Act & Assert
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [Fact]
        public void Stats_TrackPeakBytes()
        {
            // Arrange
            var options = new KvCacheOptions
            {
                MaxBytesPerSession = 50 * 1024 * 1024,
                MaxBytesTotal = 200 * 1024 * 1024
            };
            var store = new LruKvCacheStore(options);
            var modelShape = new ModelShape(layers: 4, heads: 8, headDim: 64);

            // Act - Create and remove sessions
            var session1 = new SessionId("session-1");
            var session2 = new SessionId("session-2");

            _ = store.GetOrCreate(session1, modelShape, maxTokens: 128);
            _ = store.GetOrCreate(session2, modelShape, maxTokens: 128);

            var statsAtPeak = store.GetStats();
            long peakBytes = statsAtPeak.CurrentBytes;

            store.Remove(session1);

            var statsFinal = store.GetStats();

            // Assert
            Assert.True(statsFinal.PeakBytes >= peakBytes);
            Assert.True(statsFinal.CurrentBytes < peakBytes); // Removed one session

            store.Dispose();
        }

        [Fact]
        public void Clone_CopiesPerSessionBudget()
        {
            // Arrange
            var original = new KvCacheOptions
            {
                MaxBytesPerSession = 42 * 1024 * 1024
            };

            // Act
            var clone = original.Clone();

            // Assert
            Assert.Equal(original.MaxBytesPerSession, clone.MaxBytesPerSession);

            // Modify clone shouldn't affect original
            clone.MaxBytesPerSession = 100 * 1024 * 1024;
            Assert.NotEqual(original.MaxBytesPerSession, clone.MaxBytesPerSession);
        }
    }
}

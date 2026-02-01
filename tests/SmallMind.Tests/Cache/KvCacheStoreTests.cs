using System;
using System.Linq;
using Xunit;
using SmallMind.Runtime.Cache;

namespace SmallMind.Tests.Cache
{
    public class KvCacheStoreTests
    {
        [Fact]
        public void SessionId_NewId_CreatesUniqueIds()
        {
            var id1 = SessionId.NewId();
            var id2 = SessionId.NewId();
            
            Assert.NotEqual(id1, id2);
            Assert.False(string.IsNullOrWhiteSpace(id1.Value));
            Assert.False(string.IsNullOrWhiteSpace(id2.Value));
        }

        [Fact]
        public void SessionId_Equality_Works()
        {
            var id1 = new SessionId("test-123");
            var id2 = new SessionId("test-123");
            var id3 = new SessionId("test-456");
            
            Assert.Equal(id1, id2);
            Assert.NotEqual(id1, id3);
            Assert.True(id1 == id2);
            Assert.True(id1 != id3);
            Assert.Equal(id1.GetHashCode(), id2.GetHashCode());
        }

        [Fact]
        public void KvCacheOptions_Validate_RequiresPositiveValues()
        {
            var options = new KvCacheOptions
            {
                MaxTokensPerSession = 0
            };
            
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
            
            options.MaxTokensPerSession = 100;
            options.MaxSessions = -1;
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
            
            options.MaxSessions = 10;
            options.MaxBytesTotal = 0;
            Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
        }

        [Fact]
        public void KvCacheEntry_AppendAndGet_Works()
        {
            var sessionId = SessionId.NewId();
            var shape = new ModelShape(layers: 2, heads: 4, headDim: 16);
            var maxTokens = 10;
            
            using var entry = new KvCacheEntry(sessionId, shape, maxTokens);
            
            Assert.Equal(0, entry.CurrentTokenCount);
            Assert.Equal(sessionId, entry.SessionId);
            
            int numNewTokens = 3;
            int stride = shape.Heads * shape.HeadDim;
            int expectedSize = numNewTokens * stride;
            
            var keyData = new float[expectedSize];
            var valueData = new float[expectedSize];
            
            for (int i = 0; i < expectedSize; i++)
            {
                keyData[i] = i * 1.0f;
                valueData[i] = i * 2.0f;
            }
            
            entry.AppendKV(0, keyData, valueData, numNewTokens);
            entry.AppendKV(1, keyData, valueData, numNewTokens);
            entry.CommitAppend(numNewTokens);
            
            Assert.Equal(numNewTokens, entry.CurrentTokenCount);
            
            var retrievedKeys = entry.GetKeys(0, 0, numNewTokens);
            var retrievedValues = entry.GetValues(0, 0, numNewTokens);
            
            Assert.Equal(expectedSize, retrievedKeys.Length);
            Assert.Equal(expectedSize, retrievedValues.Length);
            
            for (int i = 0; i < expectedSize; i++)
            {
                Assert.Equal(keyData[i], retrievedKeys[i]);
                Assert.Equal(valueData[i], retrievedValues[i]);
            }
        }

        [Fact]
        public void LruKvCacheStore_GetOrCreate_CreatesNewEntry()
        {
            var options = new KvCacheOptions
            {
                MaxSessions = 10,
                MaxTokensPerSession = 100,
                MaxBytesTotal = 10 * 1024 * 1024
            };
            
            using var store = new LruKvCacheStore(options);
            var sessionId = SessionId.NewId();
            var shape = new ModelShape(layers: 2, heads: 4, headDim: 16);
            
            var entry = store.GetOrCreate(sessionId, shape, 100);
            
            Assert.NotNull(entry);
            Assert.Equal(sessionId, entry.SessionId);
            Assert.Equal(shape, entry.ModelShape);
            
            var stats = store.GetStats();
            Assert.Equal(1, stats.CurrentSessions);
        }

        [Fact]
        public void LruKvCacheStore_EvictsLruWhenSessionLimitReached()
        {
            var options = new KvCacheOptions
            {
                MaxSessions = 3,
                MaxTokensPerSession = 100,
                MaxBytesTotal = 100 * 1024 * 1024
            };
            
            using var store = new LruKvCacheStore(options);
            var shape = new ModelShape(layers: 1, heads: 2, headDim: 8);
            
            var session1 = SessionId.NewId();
            var session2 = SessionId.NewId();
            var session3 = SessionId.NewId();
            var session4 = SessionId.NewId();
            
            store.GetOrCreate(session1, shape, 100);
            store.GetOrCreate(session2, shape, 100);
            store.GetOrCreate(session3, shape, 100);
            
            Assert.Equal(3, store.GetStats().CurrentSessions);
            
            store.Touch(session2);
            
            store.GetOrCreate(session4, shape, 100);
            
            var stats = store.GetStats();
            Assert.Equal(3, stats.CurrentSessions);
            Assert.Equal(1, stats.Evictions);
            
            Assert.False(store.TryGet(session1, out _));
            Assert.True(store.TryGet(session2, out _));
            Assert.True(store.TryGet(session3, out _));
            Assert.True(store.TryGet(session4, out _));
        }
    }
}

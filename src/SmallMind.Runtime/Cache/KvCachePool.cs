using System.Collections.Concurrent;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Pool for KV cache instances to enable reuse and reduce allocations.
    /// Thread-safe implementation using concurrent collections.
    /// Pools caches by model shape to ensure compatibility.
    /// </summary>
    internal sealed class KvCachePool : IDisposable
    {
        private sealed class PoolKey : IEquatable<PoolKey>
        {
            public readonly ModelShape Shape;
            public readonly int MaxTokens;
            private readonly int _hashCode;

            public PoolKey(ModelShape shape, int maxTokens)
            {
                Shape = shape;
                MaxTokens = maxTokens;
                _hashCode = HashCode.Combine(shape, maxTokens);
            }

            public bool Equals(PoolKey? other)
            {
                if (other is null) return false;
                return Shape.Equals(other.Shape) && MaxTokens == other.MaxTokens;
            }

            public override bool Equals(object? obj) => obj is PoolKey other && Equals(other);
            public override int GetHashCode() => _hashCode;
        }

        private readonly ConcurrentDictionary<PoolKey, ConcurrentBag<KvCacheEntry>> _pools;
        private readonly int _maxCachedEntriesPerShape;
        private bool _disposed;

        /// <summary>
        /// Creates a new KV cache pool.
        /// </summary>
        /// <param name="maxCachedEntriesPerShape">Maximum number of cached entries per shape/size combination (default: 4)</param>
        public KvCachePool(int maxCachedEntriesPerShape = 4)
        {
            if (maxCachedEntriesPerShape < 1)
                throw new ArgumentOutOfRangeException(nameof(maxCachedEntriesPerShape));

            _maxCachedEntriesPerShape = maxCachedEntriesPerShape;
            _pools = new ConcurrentDictionary<PoolKey, ConcurrentBag<KvCacheEntry>>();
        }

        /// <summary>
        /// Rents a KV cache entry from the pool or creates a new one.
        /// Caller is responsible for returning the cache via Return when done.
        /// </summary>
        /// <param name="modelShape">Model shape for this cache</param>
        /// <param name="maxTokens">Maximum tokens for this cache</param>
        /// <param name="sessionId">Session ID for the cache</param>
        /// <returns>A KV cache entry ready for use</returns>
        public KvCacheEntry Rent(ModelShape modelShape, int maxTokens, SessionId sessionId)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(KvCachePool));

            var key = new PoolKey(modelShape, maxTokens);

            // Try to get from pool
            if (_pools.TryGetValue(key, out var bag) && bag.TryTake(out var entry))
            {
                // Reset the entry before returning
                entry.Reset();
                return entry;
            }

            // Create new entry if pool is empty
            return new KvCacheEntry(sessionId, modelShape, maxTokens);
        }

        /// <summary>
        /// Returns a KV cache entry to the pool for reuse.
        /// The entry will be reset before being added back to the pool.
        /// </summary>
        /// <param name="entry">The cache entry to return</param>
        public void Return(KvCacheEntry entry)
        {
            if (entry == null)
                return;

            if (_disposed)
            {
                // Pool is disposed, just dispose the entry
                entry.Dispose();
                return;
            }

            var key = new PoolKey(entry.ModelShape, entry.MaxTokens);
            var bag = _pools.GetOrAdd(key, _ => new ConcurrentBag<KvCacheEntry>());

            // Only return to pool if under limit (avoid unbounded growth)
            if (bag.Count < _maxCachedEntriesPerShape)
            {
                // Reset before returning to pool
                entry.Reset();
                bag.Add(entry);
            }
            else
            {
                // Pool is full, dispose the entry
                entry.Dispose();
            }
        }

        /// <summary>
        /// Clears all pooled cache entries and disposes them.
        /// </summary>
        public void Clear()
        {
            foreach (var kvp in _pools)
            {
                while (kvp.Value.TryTake(out var entry))
                {
                    entry.Dispose();
                }
            }
            _pools.Clear();
        }

        /// <summary>
        /// Gets statistics about the pool.
        /// </summary>
        /// <returns>Dictionary of pool keys to entry counts</returns>
        public Dictionary<string, int> GetPoolStats()
        {
            var stats = new Dictionary<string, int>();
            foreach (var kvp in _pools)
            {
                var key = $"{kvp.Key.Shape} (max={kvp.Key.MaxTokens})";
                stats[key] = kvp.Value.Count;
            }
            return stats;
        }

        /// <summary>
        /// Disposes the pool and all cached entries.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            Clear();
            _disposed = true;
        }
    }
}

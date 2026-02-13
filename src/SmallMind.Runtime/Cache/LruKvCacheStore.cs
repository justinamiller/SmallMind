using SmallMind.Abstractions;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// LRU-based KV cache store with O(1) get/touch/remove operations.
    /// Thread-safe implementation with bounded memory and session count.
    /// Supports per-session memory budgets with budget telemetry events.
    /// </summary>
    internal sealed class LruKvCacheStore : IKvCacheStore, IDisposable
    {
        private sealed class LruNode
        {
            public SessionId SessionId;
            public KvCacheEntry Entry;
            public LruNode? Prev;
            public LruNode? Next;

            public LruNode(SessionId sessionId, KvCacheEntry entry)
            {
                SessionId = sessionId;
                Entry = entry;
            }
        }

        private readonly KvCacheOptions _options;
        private readonly Dictionary<SessionId, LruNode> _cache;
        private readonly ReaderWriterLockSlim _lock;
        private readonly IChatTelemetry? _telemetry;

        // LRU list (head = most recent, tail = least recent)
        private LruNode? _head;
        private LruNode? _tail;

        // Statistics
        private long _currentBytes;
        private long _peakBytes;
        private long _hits;
        private long _misses;
        private long _evictions;
        private long _reusedTokens;
        private bool _disposed;

        public LruKvCacheStore(KvCacheOptions options, IChatTelemetry? telemetry = null)
        {
            _options = options?.Clone() ?? throw new ArgumentNullException(nameof(options));
            _options.Validate();
            _telemetry = telemetry;

            _cache = new Dictionary<SessionId, LruNode>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        }

        public bool TryGet(SessionId sessionId, out KvCacheEntry? entry)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_cache.TryGetValue(sessionId, out var node))
                {
                    Interlocked.Increment(ref _hits);
                    Interlocked.Add(ref _reusedTokens, node.Entry.CurrentTokenCount);

                    // Move to head (most recently used)
                    _lock.EnterWriteLock();
                    try
                    {
                        MoveToHead(node);
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }

                    entry = node.Entry;
                    return true;
                }

                Interlocked.Increment(ref _misses);
                entry = null;
                return false;
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public KvCacheEntry GetOrCreate(SessionId sessionId, ModelShape modelShape, int maxTokens)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_cache.TryGetValue(sessionId, out var node))
                {
                    // Validate model shape matches
                    if (!node.Entry.ModelShape.Equals(modelShape))
                    {
                        throw new InvalidOperationException(
                            $"Session {sessionId} exists with different model shape. " +
                            $"Expected {modelShape}, got {node.Entry.ModelShape}");
                    }

                    Interlocked.Increment(ref _hits);
                    Interlocked.Add(ref _reusedTokens, node.Entry.CurrentTokenCount);

                    _lock.EnterWriteLock();
                    try
                    {
                        MoveToHead(node);
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }

                    return node.Entry;
                }

                // Create new entry
                _lock.EnterWriteLock();
                try
                {
                    Interlocked.Increment(ref _misses);

                    var entry = new KvCacheEntry(sessionId, modelShape, maxTokens);

                    // Check per-session budget
                    if (entry.SizeBytes > _options.MaxBytesPerSession)
                    {
                        // Emit telemetry event
                        _telemetry?.OnKvCacheBudgetExceeded(
                            sessionId.ToString(),
                            entry.SizeBytes,
                            _options.MaxBytesPerSession);

                        entry.Dispose();
                        throw new InvalidOperationException(
                            $"Session cache size {entry.SizeBytes / 1024 / 1024}MB exceeds " +
                            $"per-session budget {_options.MaxBytesPerSession / 1024 / 1024}MB");
                    }

                    var newNode = new LruNode(sessionId, entry);

                    // Evict if necessary before adding
                    EvictIfNecessary(entry.SizeBytes);

                    _cache[sessionId] = newNode;
                    AddToHead(newNode);

                    _currentBytes += entry.SizeBytes;
                    if (_currentBytes > _peakBytes)
                        _peakBytes = _currentBytes;

                    return entry;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public void Touch(SessionId sessionId)
        {
            _lock.EnterUpgradeableReadLock();
            try
            {
                if (_cache.TryGetValue(sessionId, out var node))
                {
                    _lock.EnterWriteLock();
                    try
                    {
                        MoveToHead(node);
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                    }
                }
            }
            finally
            {
                _lock.ExitUpgradeableReadLock();
            }
        }

        public void Remove(SessionId sessionId)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_cache.TryGetValue(sessionId, out var node))
                {
                    RemoveNode(node);
                    _cache.Remove(sessionId);

                    _currentBytes -= node.Entry.SizeBytes;
                    node.Entry.Dispose();
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var node in _cache.Values)
                {
                    node.Entry.Dispose();
                }

                _cache.Clear();
                _head = null;
                _tail = null;
                _currentBytes = 0;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public KvCacheStats GetStats()
        {
            _lock.EnterReadLock();
            try
            {
                return new KvCacheStats
                {
                    CurrentSessions = _cache.Count,
                    CurrentBytes = _currentBytes,
                    Evictions = _evictions,
                    Hits = _hits,
                    Misses = _misses,
                    ReusedTokens = _reusedTokens,
                    PeakBytes = _peakBytes
                };
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Clear();
            _lock.Dispose();
            _disposed = true;
        }

        // LRU list operations (must be called within write lock)

        private void AddToHead(LruNode node)
        {
            node.Next = _head;
            node.Prev = null;

            if (_head != null)
                _head.Prev = node;

            _head = node;

            if (_tail == null)
                _tail = node;
        }

        private void RemoveNode(LruNode node)
        {
            if (node.Prev != null)
                node.Prev.Next = node.Next;
            else
                _head = node.Next;

            if (node.Next != null)
                node.Next.Prev = node.Prev;
            else
                _tail = node.Prev;
        }

        private void MoveToHead(LruNode node)
        {
            if (node == _head)
                return;

            RemoveNode(node);
            AddToHead(node);
        }

        private void EvictIfNecessary(long newEntryBytes)
        {
            // Evict based on session count limit
            while (_cache.Count >= _options.MaxSessions && _tail != null)
            {
                EvictLru();
            }

            // Evict based on total bytes limit
            while (_currentBytes + newEntryBytes > _options.MaxBytesTotal && _tail != null)
            {
                EvictLru();
            }
        }

        private void EvictLru()
        {
            if (_tail == null)
                return;

            var lruNode = _tail;
            RemoveNode(lruNode);
            _cache.Remove(lruNode.SessionId);

            long freedBytes = lruNode.Entry.SizeBytes;
            _currentBytes -= freedBytes;

            // Emit telemetry event
            _telemetry?.OnKvCacheEviction(
                lruNode.SessionId.ToString(),
                "LRU eviction",
                freedBytes);

            lruNode.Entry.Dispose();

            Interlocked.Increment(ref _evictions);
        }
    }
}

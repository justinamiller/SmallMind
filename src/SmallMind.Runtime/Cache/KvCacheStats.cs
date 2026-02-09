namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Statistics for KV cache usage and performance.
    /// </summary>
    internal sealed class KvCacheStats
    {
        /// <summary>
        /// Gets or sets the current number of sessions in cache.
        /// </summary>
        public int CurrentSessions { get; set; }

        /// <summary>
        /// Gets or sets the current total bytes used by all cache entries.
        /// </summary>
        public long CurrentBytes { get; set; }

        /// <summary>
        /// Gets or sets the total number of cache evictions.
        /// </summary>
        public long Evictions { get; set; }

        /// <summary>
        /// Gets or sets the total number of cache hits.
        /// </summary>
        public long Hits { get; set; }

        /// <summary>
        /// Gets or sets the total number of cache misses.
        /// </summary>
        public long Misses { get; set; }

        /// <summary>
        /// Gets or sets the total number of tokens reused from cache.
        /// </summary>
        public long ReusedTokens { get; set; }

        /// <summary>
        /// Gets or sets the peak memory usage (bytes).
        /// </summary>
        public long PeakBytes { get; set; }

        /// <summary>
        /// Gets the cache hit rate (0.0 to 1.0).
        /// </summary>
        public double HitRate => (Hits + Misses) == 0 ? 0.0 : (double)Hits / (Hits + Misses);

        /// <summary>
        /// Creates a copy of these stats.
        /// </summary>
        public KvCacheStats Clone()
        {
            return new KvCacheStats
            {
                CurrentSessions = CurrentSessions,
                CurrentBytes = CurrentBytes,
                Evictions = Evictions,
                Hits = Hits,
                Misses = Misses,
                ReusedTokens = ReusedTokens,
                PeakBytes = PeakBytes
            };
        }
    }
}

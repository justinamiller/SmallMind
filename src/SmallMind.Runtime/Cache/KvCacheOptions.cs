namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Eviction policy for KV cache store.
    /// </summary>
    internal enum KvEvictionPolicy
    {
        /// <summary>
        /// Least Recently Used - evict sessions not accessed recently.
        /// </summary>
        LRU
    }

    /// <summary>
    /// Configuration options for KV cache management.
    /// </summary>
    internal sealed class KvCacheOptions
    {
        /// <summary>
        /// Gets or sets whether KV caching is enabled.
        /// Default: true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of tokens per session.
        /// When exceeded, oldest tokens are evicted or request is rejected.
        /// Default: 4096.
        /// </summary>
        public int MaxTokensPerSession { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the maximum number of concurrent sessions in cache.
        /// When exceeded, LRU sessions are evicted.
        /// Default: 1000.
        /// </summary>
        public int MaxSessions { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the maximum total memory (bytes) for all cache entries.
        /// When exceeded, LRU sessions are evicted.
        /// Default: 1GB.
        /// </summary>
        public long MaxBytesTotal { get; set; } = 1L * 1024 * 1024 * 1024;

        /// <summary>
        /// Gets or sets the maximum memory (bytes) per individual session.
        /// Prevents a single session from consuming all available cache memory.
        /// Default: 100MB.
        /// </summary>
        public long MaxBytesPerSession { get; set; } = 100L * 1024 * 1024;

        /// <summary>
        /// Gets or sets the eviction policy.
        /// Default: LRU.
        /// </summary>
        public KvEvictionPolicy Policy { get; set; } = KvEvictionPolicy.LRU;

        /// <summary>
        /// Gets or sets whether cache persists across requests.
        /// If false, cache is cleared after each request.
        /// Default: true.
        /// </summary>
        public bool PersistAcrossRequests { get; set; } = true;

        /// <summary>
        /// Enables cross-session prefix sharing for common prompts.
        /// Default: false.
        /// </summary>
        public bool EnablePrefixSharing { get; set; } = false;

        /// <summary>
        /// Quantization type for KV cache storage.
        /// Default: None (full FP32 precision).
        /// FP16 provides 2x memory reduction, INT8 provides 4x.
        /// </summary>
        public QuantizationType CacheQuantization { get; set; } = QuantizationType.None;

        /// <summary>
        /// Validates the options.
        /// </summary>
        public void Validate()
        {
            if (MaxTokensPerSession <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxTokensPerSession), "Must be greater than 0");

            if (MaxSessions <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxSessions), "Must be greater than 0");

            if (MaxBytesTotal <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxBytesTotal), "Must be greater than 0");

            if (MaxBytesPerSession <= 0)
                throw new ArgumentOutOfRangeException(nameof(MaxBytesPerSession), "Must be greater than 0");

            if (MaxBytesPerSession > MaxBytesTotal)
                throw new ArgumentOutOfRangeException(nameof(MaxBytesPerSession),
                    "Cannot exceed MaxBytesTotal");
        }

        /// <summary>
        /// Creates a copy of these options.
        /// </summary>
        public KvCacheOptions Clone()
        {
            return new KvCacheOptions
            {
                Enabled = Enabled,
                MaxTokensPerSession = MaxTokensPerSession,
                MaxSessions = MaxSessions,
                MaxBytesTotal = MaxBytesTotal,
                MaxBytesPerSession = MaxBytesPerSession,
                Policy = Policy,
                PersistAcrossRequests = PersistAcrossRequests,
                EnablePrefixSharing = EnablePrefixSharing,
                CacheQuantization = CacheQuantization
            };
        }
    }
}

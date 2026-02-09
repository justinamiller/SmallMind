namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Interface for KV cache storage with lifecycle management.
    /// </summary>
    internal interface IKvCacheStore
    {
        /// <summary>
        /// Tries to get an existing cache entry for the given session.
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <param name="entry">The cache entry if found</param>
        /// <returns>True if the entry exists, false otherwise</returns>
        bool TryGet(SessionId sessionId, out KvCacheEntry? entry);

        /// <summary>
        /// Gets an existing cache entry or creates a new one.
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <param name="modelShape">Model shape for validation</param>
        /// <param name="maxTokens">Maximum tokens per session</param>
        /// <returns>The cache entry</returns>
        KvCacheEntry GetOrCreate(SessionId sessionId, ModelShape modelShape, int maxTokens);

        /// <summary>
        /// Marks a session as recently used (for LRU tracking).
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        void Touch(SessionId sessionId);

        /// <summary>
        /// Removes a session from the cache.
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        void Remove(SessionId sessionId);

        /// <summary>
        /// Clears all cache entries.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets current cache statistics.
        /// </summary>
        KvCacheStats GetStats();
    }
}

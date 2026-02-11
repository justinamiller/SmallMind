using System;
using SmallMind.Runtime.Cache;

namespace SmallMind.Runtime.Execution
{
    /// <summary>
    /// Handle for a pooled KV cache instance.
    /// Provides access to cached key/value tensors across transformer layers.
    /// Designed for reuse across multiple generation steps and chat turns.
    /// </summary>
    internal sealed class KvCacheHandle : IDisposable
    {
        private readonly KvCacheEntry _cacheEntry;
        private bool _disposed;
        
        /// <summary>
        /// Gets the current position in the cache (number of tokens stored).
        /// </summary>
        public int CurrentPosition => _cacheEntry.CurrentTokenCount;
        
        /// <summary>
        /// Gets the maximum number of tokens this cache can hold.
        /// </summary>
        public int MaxTokens => _cacheEntry.MaxTokens;
        
        /// <summary>
        /// Gets the model shape this cache was created for.
        /// </summary>
        public ModelShape ModelShape => _cacheEntry.ModelShape;
        
        /// <summary>
        /// Gets the session ID for this cache.
        /// </summary>
        public SessionId SessionId => _cacheEntry.SessionId;
        
        /// <summary>
        /// Gets the approximate memory size in bytes.
        /// </summary>
        public long SizeBytes => _cacheEntry.SizeBytes;
        
        /// <summary>
        /// Gets whether the cache can support sliding window.
        /// Always true in current implementation.
        /// </summary>
        public bool SupportsSliding => true;
        
        internal KvCacheHandle(KvCacheEntry cacheEntry)
        {
            _cacheEntry = cacheEntry ?? throw new ArgumentNullException(nameof(cacheEntry));
        }
        
        /// <summary>
        /// Gets the underlying cache entry (internal use only).
        /// </summary>
        internal KvCacheEntry CacheEntry => _cacheEntry;
        
        /// <summary>
        /// Ensures the cache has capacity for the required number of additional tokens.
        /// </summary>
        /// <param name="requiredTokens">Number of tokens to add</param>
        /// <returns>True if capacity is available, false otherwise</returns>
        public bool EnsureCapacity(int requiredTokens)
        {
            return _cacheEntry.EnsureCapacity(requiredTokens);
        }
        
        /// <summary>
        /// Appends K/V tensors for new tokens at the specified layer.
        /// </summary>
        /// <param name="layer">Layer index</param>
        /// <param name="keyData">Key tensor data for new tokens</param>
        /// <param name="valueData">Value tensor data for new tokens</param>
        /// <param name="numNewTokens">Number of new tokens being added</param>
        public void AppendKV(int layer, ReadOnlySpan<float> keyData, ReadOnlySpan<float> valueData, int numNewTokens)
        {
            _cacheEntry.AppendKV(layer, keyData, valueData, numNewTokens);
        }
        
        /// <summary>
        /// Commits the appended tokens, advancing the position counter.
        /// Must be called after all layers have been appended with AppendKV.
        /// </summary>
        /// <param name="numNewTokens">Number of tokens to commit</param>
        public void CommitAppend(int numNewTokens)
        {
            _cacheEntry.CommitAppend(numNewTokens);
        }
        
        /// <summary>
        /// Gets a read-only span of the cached keys for a layer.
        /// </summary>
        /// <param name="layer">Layer index</param>
        /// <param name="startToken">Starting token position</param>
        /// <param name="tokenCount">Number of tokens to retrieve</param>
        public ReadOnlySpan<float> GetKeys(int layer, int startToken, int tokenCount)
        {
            return _cacheEntry.GetKeys(layer, startToken, tokenCount);
        }
        
        /// <summary>
        /// Gets a read-only span of the cached values for a layer.
        /// </summary>
        /// <param name="layer">Layer index</param>
        /// <param name="startToken">Starting token position</param>
        /// <param name="tokenCount">Number of tokens to retrieve</param>
        public ReadOnlySpan<float> GetValues(int layer, int startToken, int tokenCount)
        {
            return _cacheEntry.GetValues(layer, startToken, tokenCount);
        }
        
        /// <summary>
        /// Resets the cache, clearing all stored tokens.
        /// Cache can be reused for a new sequence.
        /// </summary>
        public void Reset()
        {
            _cacheEntry.Reset();
        }
        
        /// <summary>
        /// Implements sliding window for the cache.
        /// Removes oldest tokens to make room for new ones.
        /// TODO: Implement actual sliding logic (current implementation just resets).
        /// </summary>
        /// <param name="windowSize">Size of the sliding window to maintain</param>
        public void Slide(int windowSize)
        {
            // TODO: Implement proper sliding window that preserves last windowSize tokens
            // For now, just reset (conservative approach)
            if (CurrentPosition > windowSize)
            {
                Reset();
            }
        }
        
        /// <summary>
        /// Disposes the cache handle.
        /// Note: Does NOT dispose the underlying cache entry - that's managed by the pool.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            
            // Do not dispose _cacheEntry - it's owned by the pool
            // The pool will manage its lifecycle
            
            _disposed = true;
        }
    }
}

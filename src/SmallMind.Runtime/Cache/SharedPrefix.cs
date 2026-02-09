using System;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Represents a shared prompt prefix that can be reused across sessions.
    /// </summary>
    internal sealed class SharedPrefix
    {
        public string PrefixHash { get; }
        public int[] TokenIds { get; }
        public int Length => TokenIds.Length;
        
        private int _referenceCount;
        public int ReferenceCount 
        { 
            get => System.Threading.Interlocked.CompareExchange(ref _referenceCount, 0, 0);
            set => System.Threading.Interlocked.Exchange(ref _referenceCount, value);
        }
        
        public DateTime LastUsed { get; set; }
        
        // Cached K/V for this prefix (shared across sessions)
        public float[][]? CachedKeys { get; set; }
        public float[][]? CachedValues { get; set; }
        
        public SharedPrefix(string prefixHash, int[] tokenIds)
        {
            PrefixHash = prefixHash ?? throw new ArgumentNullException(nameof(prefixHash));
            TokenIds = tokenIds ?? throw new ArgumentNullException(nameof(tokenIds));
            _referenceCount = 0;
            LastUsed = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Increments the reference count atomically.
        /// </summary>
        public void IncrementReference()
        {
            System.Threading.Interlocked.Increment(ref _referenceCount);
        }
        
        /// <summary>
        /// Decrements the reference count atomically.
        /// </summary>
        public void DecrementReference()
        {
            System.Threading.Interlocked.Decrement(ref _referenceCount);
        }
    }
}

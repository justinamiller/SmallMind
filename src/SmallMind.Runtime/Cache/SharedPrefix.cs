using System;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Represents a shared prompt prefix that can be reused across sessions.
    /// </summary>
    public sealed class SharedPrefix
    {
        public string PrefixHash { get; }
        public int[] TokenIds { get; }
        public int Length => TokenIds.Length;
        public int ReferenceCount { get; set; }
        public DateTime LastUsed { get; set; }
        
        // Cached K/V for this prefix (shared across sessions)
        public float[][]? CachedKeys { get; set; }
        public float[][]? CachedValues { get; set; }
        
        public SharedPrefix(string prefixHash, int[] tokenIds)
        {
            PrefixHash = prefixHash ?? throw new ArgumentNullException(nameof(prefixHash));
            TokenIds = tokenIds ?? throw new ArgumentNullException(nameof(tokenIds));
            ReferenceCount = 0;
            LastUsed = DateTime.UtcNow;
        }
    }
}

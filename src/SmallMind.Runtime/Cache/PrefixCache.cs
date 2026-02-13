using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Cache for shared prompt prefixes across sessions.
    /// Uses hash-based deduplication and LRU eviction.
    /// </summary>
    internal sealed class PrefixCache
    {
        private readonly ConcurrentDictionary<string, SharedPrefix> _prefixes;
        private readonly int _maxPrefixes;

        public PrefixCache(int maxPrefixes = 100)
        {
            _maxPrefixes = maxPrefixes;
            _prefixes = new ConcurrentDictionary<string, SharedPrefix>();
        }

        /// <summary>
        /// Computes a hash for the given token sequence.
        /// </summary>
        public static string ComputePrefixHash(ReadOnlySpan<int> tokenIds)
        {
            // Use first 64 tokens or all if shorter (common system prompts are ~50-100 tokens)
            int hashLen = Math.Min(tokenIds.Length, 64);
            Span<byte> bytes = stackalloc byte[hashLen * 4];

            for (int i = 0; i < hashLen; i++)
            {
                BitConverter.TryWriteBytes(bytes.Slice(i * 4, 4), tokenIds[i]);
            }

            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(bytes, hash);

            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Gets or creates a shared prefix.
        /// </summary>
        public SharedPrefix GetOrCreate(int[] tokenIds)
        {
            string hash = ComputePrefixHash(tokenIds);

            return _prefixes.GetOrAdd(hash, _ =>
            {
                var prefix = new SharedPrefix(hash, (int[])tokenIds.Clone());

                // Evict if needed
                if (_prefixes.Count >= _maxPrefixes)
                {
                    EvictLeastRecentlyUsed();
                }

                return prefix;
            });
        }

        /// <summary>
        /// Increments reference count for a prefix.
        /// </summary>
        public void AddReference(string prefixHash)
        {
            if (_prefixes.TryGetValue(prefixHash, out var prefix))
            {
                prefix.IncrementReference();
                prefix.LastUsed = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Decrements reference count for a prefix.
        /// </summary>
        public void RemoveReference(string prefixHash)
        {
            if (_prefixes.TryGetValue(prefixHash, out var prefix))
            {
                prefix.DecrementReference();
            }
        }

        private void EvictLeastRecentlyUsed()
        {
            SharedPrefix? oldest = null;
            string? oldestHash = null;

            foreach (var kvp in _prefixes)
            {
                if (kvp.Value.ReferenceCount == 0 &&
                    (oldest == null || kvp.Value.LastUsed < oldest.LastUsed))
                {
                    oldest = kvp.Value;
                    oldestHash = kvp.Key;
                }
            }

            if (oldestHash != null)
            {
                _prefixes.TryRemove(oldestHash, out _);
            }
        }
    }
}

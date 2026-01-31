using System;
using System.Collections.Concurrent;

namespace TinyLLM.Core
{
    /// <summary>
    /// Object pooling for tensor arrays to reduce GC pressure.
    /// Implements bucketed pooling for common tensor sizes.
    /// </summary>
    public sealed class TensorPool
    {
        private readonly ConcurrentBag<float[]>[] _pools;
        private static readonly int[] _sizes = { 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536 };
        
        // Singleton instance
        private static readonly Lazy<TensorPool> _instance = new Lazy<TensorPool>(() => new TensorPool());
        public static TensorPool Shared => _instance.Value;
        
        public TensorPool()
        {
            _pools = new ConcurrentBag<float[]>[_sizes.Length];
            for (int i = 0; i < _pools.Length; i++)
            {
                _pools[i] = new ConcurrentBag<float[]>();
            }
        }
        
        /// <summary>
        /// Rent an array of at least the requested size from the pool.
        /// Returns a pooled array if available, otherwise allocates new.
        /// </summary>
        public float[] Rent(int minSize)
        {
            int bucketIndex = GetBucketIndex(minSize);
            
            if (bucketIndex >= 0 && _pools[bucketIndex].TryTake(out var array))
            {
                return array;
            }
            
            // Allocate new with the bucket size (or exact size if too large)
            int actualSize = bucketIndex >= 0 ? _sizes[bucketIndex] : minSize;
            return new float[actualSize];
        }
        
        /// <summary>
        /// Return an array to the pool for reuse.
        /// Clears the array before pooling for security/correctness.
        /// </summary>
        public void Return(float[] array, bool clearArray = true)
        {
            if (array == null) return;
            
            int bucketIndex = GetBucketIndex(array.Length);
            
            // Only pool if it matches a bucket size exactly
            if (bucketIndex >= 0 && array.Length == _sizes[bucketIndex])
            {
                if (clearArray)
                {
                    Array.Clear(array);
                }
                _pools[bucketIndex].Add(array);
            }
            // Otherwise, let GC handle it
        }
        
        private int GetBucketIndex(int size)
        {
            // Find the smallest bucket that fits the requested size
            for (int i = 0; i < _sizes.Length; i++)
            {
                if (size <= _sizes[i])
                {
                    return i;
                }
            }
            return -1; // Size too large for pooling
        }
        
        /// <summary>
        /// Clear all pooled arrays (for cleanup/testing)
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < _pools.Length; i++)
            {
                _pools[i].Clear();
            }
        }
    }
}

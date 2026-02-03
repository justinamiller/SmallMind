using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Object pooling for tensor arrays to reduce GC pressure.
    /// Implements bucketed pooling for common tensor sizes.
    /// </summary>
    public sealed class TensorPool : IDisposable
    {
        private readonly ConcurrentBag<float[]>[] _pools;
        private static readonly int[] _sizes = { 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384, 32768, 65536, 131072, 262144, 524288 };
        private static readonly int[] _capacities = { 32, 32, 32, 32, 16, 16, 8, 8, 4, 4, 2, 2, 1, 1 }; // Capacity per bucket
        private readonly int[] _counts; // Track count per bucket
        private long _totalRents;
        private long _totalReturns;
        private long _totalAllocations;
        private bool _disposed;
        
        // Singleton instance
        private static readonly Lazy<TensorPool> _instance = new Lazy<TensorPool>(() => new TensorPool());
        public static TensorPool Shared => _instance.Value;
        
        public TensorPool()
        {
            _pools = new ConcurrentBag<float[]>[_sizes.Length];
            _counts = new int[_sizes.Length];
            for (int i = 0; i < _pools.Length; i++)
            {
                _pools[i] = new ConcurrentBag<float[]>();
            }
        }
        
        /// <summary>
        /// Rent an array of at least the requested size from the pool.
        /// Returns a pooled array if available, otherwise allocates new.
        /// </summary>
        /// <param name="minSize">Minimum size of array needed.</param>
        /// <returns>A float array of at least the requested size.</returns>
        /// <exception cref="Exceptions.SmallMindObjectDisposedException">Thrown when pool has been disposed.</exception>
        public float[] Rent(int minSize)
        {
            Validation.Guard.NotDisposed(_disposed, nameof(TensorPool));
            Validation.Guard.GreaterThan(minSize, 0);
            
            Interlocked.Increment(ref _totalRents);
            
            int bucketIndex = GetBucketIndex(minSize);
            
            if (bucketIndex >= 0 && _pools[bucketIndex].TryTake(out var array))
            {
                Interlocked.Decrement(ref _counts[bucketIndex]);
                return array;
            }
            
            // Allocate new with the bucket size (or exact size if too large)
            int actualSize = bucketIndex >= 0 ? _sizes[bucketIndex] : minSize;
            Interlocked.Increment(ref _totalAllocations);
            return new float[actualSize];
        }
        
        /// <summary>
        /// Rent an array of at least the requested size from the pool.
        /// Returns a pooled array if available, otherwise allocates new.
        /// </summary>
        /// <param name="minSize">Minimum size of array needed.</param>
        /// <param name="capacity">The actual capacity of the returned array.</param>
        /// <returns>A float array of at least the requested size.</returns>
        /// <exception cref="Exceptions.SmallMindObjectDisposedException">Thrown when pool has been disposed.</exception>
        public float[] Rent(int minSize, out int capacity)
        {
            var array = Rent(minSize);
            capacity = array.Length;
            return array;
        }
        
        /// <summary>
        /// Return an array to the pool for reuse.
        /// Clears the array before pooling for security/correctness.
        /// </summary>
        /// <param name="array">Array to return to the pool.</param>
        /// <param name="clearArray">Whether to clear array contents before pooling.</param>
        public void Return(float[] array, bool clearArray = true)
        {
            if (array == null) return;
            if (_disposed) return; // Silently ignore returns after disposal
            
            Interlocked.Increment(ref _totalReturns);
            
            int bucketIndex = GetBucketIndex(array.Length);
            
            // Only pool if it matches a bucket size exactly and bucket is not full
            if (bucketIndex >= 0 && array.Length == _sizes[bucketIndex])
            {
                int currentCount = _counts[bucketIndex];
                int capacity = _capacities[bucketIndex];
                
                // Only add if under capacity
                if (currentCount < capacity)
                {
                    if (clearArray)
                    {
                        Array.Clear(array);
                    }
                    _pools[bucketIndex].Add(array);
                    Interlocked.Increment(ref _counts[bucketIndex]);
                }
                // Otherwise, let GC handle it (trim excess)
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
            Validation.Guard.NotDisposed(_disposed, nameof(TensorPool));
            
            for (int i = 0; i < _pools.Length; i++)
            {
                _pools[i].Clear();
                _counts[i] = 0;
            }
        }
        
        /// <summary>
        /// Get statistics about pool usage
        /// </summary>
        public (long totalRents, long totalReturns, long totalAllocations, long pooledBytes) GetStats()
        {
            long pooledBytes = 0;
            for (int i = 0; i < _sizes.Length; i++)
            {
                pooledBytes += _counts[i] * _sizes[i] * sizeof(float);
            }
            return (_totalRents, _totalReturns, _totalAllocations, pooledBytes);
        }
        
        /// <summary>
        /// Disposes the tensor pool, clearing all pooled arrays.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            
            for (int i = 0; i < _pools.Length; i++)
            {
                _pools[i].Clear();
            }
            
            _disposed = true;
        }
    }
}

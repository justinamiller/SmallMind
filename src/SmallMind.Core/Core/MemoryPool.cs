using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Threading;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Object pooling for tensor arrays to reduce GC pressure.
    /// Uses ArrayPool for efficient memory management with automatic size bucketing.
    /// </summary>
    internal sealed class TensorPool : IDisposable
    {
        // Use ArrayPool.Shared for backing storage - provides automatic bucketing and thread-safety
        private readonly ArrayPool<float> _arrayPool;
        
        // Track statistics for monitoring pool effectiveness
        private long _totalRents;
        private long _totalReturns;
        private bool _disposed;
        
        // Singleton instance
        private static readonly Lazy<TensorPool> _instance = new Lazy<TensorPool>(() => new TensorPool());
        public static TensorPool Shared => _instance.Value;
        
        public TensorPool()
        {
            // Use the shared ArrayPool for efficient memory management
            _arrayPool = ArrayPool<float>.Shared;
        }
        
        /// <summary>
        /// Rent an array of at least the requested size from the pool.
        /// Returns a pooled array if available, otherwise allocates new.
        /// The returned array may be larger than requested for efficient pooling.
        /// </summary>
        /// <param name="minSize">Minimum size of array needed.</param>
        /// <returns>A float array of at least the requested size.</returns>
        /// <exception cref="Exceptions.SmallMindObjectDisposedException">Thrown when pool has been disposed.</exception>
        public float[] Rent(int minSize)
        {
            Validation.Guard.NotDisposed(_disposed, nameof(TensorPool));
            Validation.Guard.GreaterThan(minSize, 0);
            
            Interlocked.Increment(ref _totalRents);
            
            // ArrayPool automatically handles size bucketing and returns appropriately sized arrays
            return _arrayPool.Rent(minSize);
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
            
            // ArrayPool.Return may throw if array wasn't rented from this pool
            // Catch and ignore to allow graceful handling of external arrays
            try
            {
                _arrayPool.Return(array, clearArray);
            }
            catch (ArgumentException)
            {
                // Array wasn't from this pool, ignore and let GC handle it
            }
        }
        
        /// <summary>
        /// Clear all pooled arrays (for cleanup/testing).
        /// Note: ArrayPool.Shared manages its own memory, so this is a no-op.
        /// Statistics are reset instead.
        /// </summary>
        public void Clear()
        {
            Validation.Guard.NotDisposed(_disposed, nameof(TensorPool));
            
            // ArrayPool.Shared manages its own memory internally
            // We can't force it to clear, but we can reset our statistics
            Interlocked.Exchange(ref _totalRents, 0);
            Interlocked.Exchange(ref _totalReturns, 0);
        }
        
        /// <summary>
        /// Get statistics about pool usage.
        /// Note: ArrayPool doesn't track allocations separately, so totalAllocations is estimated.
        /// </summary>
        public (long totalRents, long totalReturns, long totalAllocations, long pooledBytes) GetStats()
        {
            long outstanding = _totalRents - _totalReturns;
            // ArrayPool.Shared manages its own memory, we can't accurately track pooledBytes
            // Return 0 for pooledBytes since ArrayPool manages this internally
            return (_totalRents, _totalReturns, outstanding, 0);
        }
        
        /// <summary>
        /// Disposes the tensor pool.
        /// Note: ArrayPool.Shared is a singleton and doesn't need disposal.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            // ArrayPool.Shared is a singleton and doesn't need disposal
        }
    }
}

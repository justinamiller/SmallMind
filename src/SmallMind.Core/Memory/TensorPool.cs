using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SmallMind.Core.Memory
{
    /// <summary>
    /// Simple tensor buffer pool for reusing intermediate computation buffers.
    /// Reduces GC pressure by reusing float arrays across forward passes.
    /// Thread-safe implementation for parallel execution.
    /// </summary>
    public sealed class TensorPool
    {
        private readonly ConcurrentDictionary<int, ConcurrentBag<float[]>> _pools;
        private readonly int[] _standardSizes;
        
        /// <summary>
        /// Creates a new TensorPool with standard buffer sizes.
        /// </summary>
        public TensorPool()
        {
            _pools = new ConcurrentDictionary<int, ConcurrentBag<float[]>>();
            
            // Define standard buffer sizes for common tensor dimensions
            _standardSizes = new int[]
            {
                64, 128, 256, 512, 1024, 2048, 4096, 8192, 
                16384, 32768, 65536, 131072, 262144, 524288, 1048576
            };
        }
        
        /// <summary>
        /// Rent a buffer of the specified size.
        /// </summary>
        public float[] Rent(int size)
        {
            if (size <= 0)
                throw new ArgumentException("Size must be positive", nameof(size));
                
            int poolSize = GetPoolSize(size);
            
            if (_pools.TryGetValue(poolSize, out var bag) && bag.TryTake(out var buffer))
            {
                return buffer;
            }
            
            return new float[poolSize];
        }
        
        /// <summary>
        /// Return a buffer to the pool for reuse.
        /// </summary>
        public void Return(float[] buffer)
        {
            if (buffer == null || buffer.Length == 0)
                return;
                
            int size = buffer.Length;
            
            if (IsStandardSize(size))
            {
                var bag = _pools.GetOrAdd(size, _ => new ConcurrentBag<float[]>());
                bag.Add(buffer);
            }
        }
        
        /// <summary>
        /// Clear all pooled buffers.
        /// </summary>
        public void Clear()
        {
            _pools.Clear();
        }
        
        private int GetPoolSize(int size)
        {
            foreach (var standardSize in _standardSizes)
            {
                if (size <= standardSize)
                    return standardSize;
            }
            
            int powerOf2 = 1048576;
            while (powerOf2 < size)
            {
                powerOf2 *= 2;
            }
            return powerOf2;
        }
        
        private bool IsStandardSize(int size)
        {
            foreach (var standardSize in _standardSizes)
            {
                if (size == standardSize)
                    return true;
            }
            
            if (size >= 1048576)
            {
                return (size & (size - 1)) == 0;
            }
            
            return false;
        }
    }
}

using System.IO.MemoryMappedFiles;
using SmallMind.Core.Validation;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Key-Value cache using memory-mapped files for large context windows.
    /// Enables disk-backed storage to reduce RAM usage for attention mechanisms.
    /// </summary>
    internal sealed class KVCache : IDisposable
    {
        private readonly MemoryMappedFile? _memoryFile;
        private readonly MemoryMappedViewAccessor? _accessor;
        private readonly long _size;
        private readonly bool _useMemoryMapping;
        private float[]? _inMemoryCache;
        private bool _disposed;
        private readonly string? _fileName;

        /// <summary>
        /// Gets the total size of the cache in bytes.
        /// </summary>
        public long SizeBytes => _size;

        /// <summary>
        /// Gets whether this cache uses memory mapping (vs in-memory storage).
        /// </summary>
        public bool UsesMemoryMapping => _useMemoryMapping;

        /// <summary>
        /// Create a KV cache with memory-mapped file backing.
        /// </summary>
        /// <param name="fileName">Path to the memory-mapped file.</param>
        /// <param name="capacity">Number of float values to store.</param>
        /// <param name="useMemoryMapping">If true, uses memory-mapped file; otherwise uses in-memory array.</param>
        public KVCache(string fileName, long capacity, bool useMemoryMapping = true)
        {
            Guard.NotNullOrWhiteSpace(fileName, nameof(fileName));
            Guard.GreaterThan(capacity, 0L, nameof(capacity));

            _size = capacity * sizeof(float);
            _useMemoryMapping = useMemoryMapping;
            _fileName = fileName;

            if (useMemoryMapping)
            {
                try
                {
                    // Create directory if it doesn't exist
                    var directory = Path.GetDirectoryName(fileName);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Create memory-mapped file
                    _memoryFile = MemoryMappedFile.CreateFromFile(
                        fileName,
                        FileMode.Create,
                        null,
                        _size,
                        MemoryMappedFileAccess.ReadWrite);

                    _accessor = _memoryFile.CreateViewAccessor(0, _size, MemoryMappedFileAccess.ReadWrite);
                }
                catch (Exception ex)
                {
                    // Clean up on failure
                    _accessor?.Dispose();
                    _memoryFile?.Dispose();
                    throw new InvalidOperationException(
                        $"Failed to create memory-mapped file '{fileName}': {ex.Message}",
                        ex);
                }
            }
            else
            {
                // Use in-memory array as fallback
                _inMemoryCache = new float[capacity];
            }
        }

        /// <summary>
        /// Create an in-memory KV cache (no memory mapping).
        /// </summary>
        /// <param name="capacity">Number of float values to store.</param>
        public KVCache(long capacity)
        {
            Guard.GreaterThan(capacity, 0L, nameof(capacity));

            _size = capacity * sizeof(float);
            _useMemoryMapping = false;
            _inMemoryCache = new float[capacity];
        }

        /// <summary>
        /// Write a float value at the specified offset.
        /// </summary>
        /// <param name="offset">Offset in number of floats (not bytes).</param>
        /// <param name="value">Value to write.</param>
        public void Write(long offset, float value)
        {
            Guard.NotDisposed(_disposed, nameof(KVCache));
            Guard.InRange(offset, 0L, (_size / sizeof(float)) - 1, nameof(offset));

            if (_useMemoryMapping)
            {
                _accessor!.Write(offset * sizeof(float), value);
            }
            else
            {
                _inMemoryCache![offset] = value;
            }
        }

        /// <summary>
        /// Read a float value from the specified offset.
        /// </summary>
        /// <param name="offset">Offset in number of floats (not bytes).</param>
        /// <returns>The float value at the offset.</returns>
        public float Read(long offset)
        {
            Guard.NotDisposed(_disposed, nameof(KVCache));
            Guard.InRange(offset, 0L, (_size / sizeof(float)) - 1, nameof(offset));

            if (_useMemoryMapping)
            {
                return _accessor!.ReadSingle(offset * sizeof(float));
            }
            else
            {
                return _inMemoryCache![offset];
            }
        }

        /// <summary>
        /// Write multiple values starting at the specified offset.
        /// </summary>
        /// <param name="offset">Starting offset in number of floats.</param>
        /// <param name="values">Values to write.</param>
        public void WriteArray(long offset, ReadOnlySpan<float> values)
        {
            Guard.NotDisposed(_disposed, nameof(KVCache));

            long maxOffset = (_size / sizeof(float));
            if (offset + values.Length > maxOffset)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    $"Write would exceed cache capacity. Offset: {offset}, Length: {values.Length}, Capacity: {maxOffset}");
            }

            if (_useMemoryMapping)
            {
                for (int i = 0; i < values.Length; i++)
                {
                    _accessor!.Write((offset + i) * sizeof(float), values[i]);
                }
            }
            else
            {
                for (int i = 0; i < values.Length; i++)
                {
                    _inMemoryCache![offset + i] = values[i];
                }
            }
        }

        /// <summary>
        /// Read multiple values starting at the specified offset.
        /// </summary>
        /// <param name="offset">Starting offset in number of floats.</param>
        /// <param name="count">Number of values to read.</param>
        /// <param name="destination">Destination span to write values to.</param>
        public void ReadArray(long offset, int count, Span<float> destination)
        {
            Guard.NotDisposed(_disposed, nameof(KVCache));

            if (count > destination.Length)
            {
                throw new ArgumentException(
                    $"Destination span too small. Required: {count}, Available: {destination.Length}",
                    nameof(destination));
            }

            long maxOffset = (_size / sizeof(float));
            if (offset + count > maxOffset)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(offset),
                    $"Read would exceed cache capacity. Offset: {offset}, Count: {count}, Capacity: {maxOffset}");
            }

            if (_useMemoryMapping)
            {
                for (int i = 0; i < count; i++)
                {
                    destination[i] = _accessor!.ReadSingle((offset + i) * sizeof(float));
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    destination[i] = _inMemoryCache![offset + i];
                }
            }
        }

        /// <summary>
        /// Clear the entire cache by setting all values to zero.
        /// </summary>
        public void Clear()
        {
            Guard.NotDisposed(_disposed, nameof(KVCache));

            long capacity = _size / sizeof(float);

            if (_useMemoryMapping)
            {
                // Clear in chunks to avoid too many individual writes
                const int chunkSize = 1024;
                var zeros = new float[chunkSize];

                for (long i = 0; i < capacity; i += chunkSize)
                {
                    int count = (int)Math.Min(chunkSize, capacity - i);
                    WriteArray(i, zeros.AsSpan(0, count));
                }
            }
            else
            {
                _inMemoryCache.AsSpan().Clear();
            }
        }

        /// <summary>
        /// Flush any pending writes to disk (for memory-mapped files).
        /// </summary>
        public void Flush()
        {
            Guard.NotDisposed(_disposed, nameof(KVCache));

            if (_useMemoryMapping)
            {
                _accessor?.Flush();
            }
        }

        /// <summary>
        /// Dispose the KV cache and release resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            if (_useMemoryMapping)
            {
                _accessor?.Dispose();
                _memoryFile?.Dispose();

                // Clean up the file
                try
                {
                    if (!string.IsNullOrEmpty(_fileName) && File.Exists(_fileName))
                    {
                        File.Delete(_fileName);
                    }
                }
                catch
                {
                    // Ignore errors during cleanup
                }
            }

            _inMemoryCache = null;
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// KV cache manager for transformer attention layers.
    /// Manages separate caches for keys and values across multiple layers.
    /// </summary>
    internal sealed class MultiLayerKVCache : IDisposable
    {
        private readonly KVCache[] _keyCaches;
        private readonly KVCache[] _valueCaches;
        private readonly int _numLayers;
        private readonly long _cacheCapacityPerLayer;
        private readonly bool _useMemoryMapping;
        private bool _disposed;

        /// <summary>
        /// Creates a multi-layer KV cache.
        /// </summary>
        /// <param name="numLayers">Number of transformer layers.</param>
        /// <param name="maxSeqLen">Maximum sequence length.</param>
        /// <param name="numHeads">Number of attention heads.</param>
        /// <param name="headDim">Dimension of each attention head.</param>
        /// <param name="useMemoryMapping">Whether to use memory-mapped files.</param>
        /// <param name="cacheDirectory">Directory for cache files (required if useMemoryMapping=true).</param>
        public MultiLayerKVCache(
            int numLayers,
            int maxSeqLen,
            int numHeads,
            int headDim,
            bool useMemoryMapping = false,
            string? cacheDirectory = null)
        {
            Guard.GreaterThan(numLayers, 0, nameof(numLayers));
            Guard.GreaterThan(maxSeqLen, 0, nameof(maxSeqLen));
            Guard.GreaterThan(numHeads, 0, nameof(numHeads));
            Guard.GreaterThan(headDim, 0, nameof(headDim));

            if (useMemoryMapping && string.IsNullOrWhiteSpace(cacheDirectory))
            {
                throw new Exceptions.ValidationException(
                    "Cache directory must be specified when using memory mapping",
                    nameof(cacheDirectory));
            }

            _numLayers = numLayers;
            _cacheCapacityPerLayer = (long)maxSeqLen * numHeads * headDim;
            _useMemoryMapping = useMemoryMapping;

            _keyCaches = new KVCache[numLayers];
            _valueCaches = new KVCache[numLayers];

            // Create caches for each layer
            for (int i = 0; i < numLayers; i++)
            {
                if (useMemoryMapping)
                {
                    string keyFile = Path.Combine(cacheDirectory!, $"kv_cache_layer{i}_keys.bin");
                    string valueFile = Path.Combine(cacheDirectory!, $"kv_cache_layer{i}_values.bin");

                    _keyCaches[i] = new KVCache(keyFile, _cacheCapacityPerLayer, true);
                    _valueCaches[i] = new KVCache(valueFile, _cacheCapacityPerLayer, true);
                }
                else
                {
                    _keyCaches[i] = new KVCache(_cacheCapacityPerLayer);
                    _valueCaches[i] = new KVCache(_cacheCapacityPerLayer);
                }
            }
        }

        /// <summary>
        /// Gets the key cache for a specific layer.
        /// </summary>
        public KVCache GetKeyCache(int layerIndex)
        {
            Guard.NotDisposed(_disposed, nameof(MultiLayerKVCache));
            Guard.InRange(layerIndex, 0, _numLayers - 1, nameof(layerIndex));
            return _keyCaches[layerIndex];
        }

        /// <summary>
        /// Gets the value cache for a specific layer.
        /// </summary>
        public KVCache GetValueCache(int layerIndex)
        {
            Guard.NotDisposed(_disposed, nameof(MultiLayerKVCache));
            Guard.InRange(layerIndex, 0, _numLayers - 1, nameof(layerIndex));
            return _valueCaches[layerIndex];
        }

        /// <summary>
        /// Clear all caches.
        /// </summary>
        public void ClearAll()
        {
            Guard.NotDisposed(_disposed, nameof(MultiLayerKVCache));

            for (int i = 0; i < _numLayers; i++)
            {
                _keyCaches[i].Clear();
                _valueCaches[i].Clear();
            }
        }

        /// <summary>
        /// Flush all caches to disk.
        /// </summary>
        public void FlushAll()
        {
            Guard.NotDisposed(_disposed, nameof(MultiLayerKVCache));

            if (_useMemoryMapping)
            {
                for (int i = 0; i < _numLayers; i++)
                {
                    _keyCaches[i].Flush();
                    _valueCaches[i].Flush();
                }
            }
        }

        /// <summary>
        /// Dispose all caches and release resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            for (int i = 0; i < _numLayers; i++)
            {
                _keyCaches[i]?.Dispose();
                _valueCaches[i]?.Dispose();
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

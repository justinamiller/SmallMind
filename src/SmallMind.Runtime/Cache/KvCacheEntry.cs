using System.Buffers;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Model shape information for cache validation.
    /// </summary>
    internal readonly struct ModelShape : IEquatable<ModelShape>
    {
        public readonly int Layers;
        public readonly int Heads;
        public readonly int HeadDim;

        public ModelShape(int layers, int heads, int headDim)
        {
            Layers = layers;
            Heads = heads;
            HeadDim = headDim;
        }

        public bool Equals(ModelShape other) =>
            Layers == other.Layers && Heads == other.Heads && HeadDim == other.HeadDim;

        public override bool Equals(object? obj) => obj is ModelShape other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Layers, Heads, HeadDim);

        public override string ToString() => $"Layers={Layers}, Heads={Heads}, HeadDim={HeadDim}";
    }

    /// <summary>
    /// Per-session KV cache entry with pooled memory management.
    /// Stores key and value tensors for all layers using ArrayPool for efficiency.
    /// </summary>
    internal sealed class KvCacheEntry : IDisposable
    {
        private readonly SessionId _sessionId;
        private readonly ModelShape _modelShape;
        private readonly int _maxTokens;
        private readonly ArrayPool<float> _pool;

        private float[][] _keyCaches;   // [layer][position * heads * headDim]
        private float[][] _valueCaches; // [layer][position * heads * headDim]
        private int _currentTokenCount;
        private long _sizeBytes;
        private bool _disposed;

        /// <summary>
        /// Gets the session ID for this cache entry.
        /// </summary>
        public SessionId SessionId => _sessionId;

        /// <summary>
        /// Gets the model shape this cache was created for.
        /// </summary>
        public ModelShape ModelShape => _modelShape;

        /// <summary>
        /// Gets the current number of cached tokens.
        /// </summary>
        public int CurrentTokenCount => _currentTokenCount;

        /// <summary>
        /// Gets the maximum number of tokens this cache can hold.
        /// </summary>
        public int MaxTokens => _maxTokens;

        /// <summary>
        /// Gets the approximate memory size in bytes.
        /// </summary>
        public long SizeBytes => _sizeBytes;

        /// <summary>
        /// Creates a new KV cache entry.
        /// </summary>
        public KvCacheEntry(SessionId sessionId, ModelShape modelShape, int maxTokens)
        {
            if (maxTokens <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxTokens));

            _sessionId = sessionId;
            _modelShape = modelShape;
            _maxTokens = maxTokens;
            _pool = ArrayPool<float>.Shared;

            int cacheSize = maxTokens * modelShape.Heads * modelShape.HeadDim;
            _sizeBytes = 2L * modelShape.Layers * cacheSize * sizeof(float); // K + V

            _keyCaches = new float[modelShape.Layers][];
            _valueCaches = new float[modelShape.Layers][];

            for (int i = 0; i < modelShape.Layers; i++)
            {
                _keyCaches[i] = _pool.Rent(cacheSize);
                _valueCaches[i] = _pool.Rent(cacheSize);

                // Clear rented arrays - use Span.Clear for better performance
                _keyCaches[i].AsSpan(0, cacheSize).Clear();
                _valueCaches[i].AsSpan(0, cacheSize).Clear();
            }

            _currentTokenCount = 0;
        }

        /// <summary>
        /// Ensures the cache has capacity for the required number of tokens.
        /// </summary>
        public bool EnsureCapacity(int requiredTokens)
        {
            if (_currentTokenCount + requiredTokens > _maxTokens)
                return false;

            return true;
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
            if (layer < 0 || layer >= _modelShape.Layers)
                throw new ArgumentOutOfRangeException(nameof(layer));

            if (_currentTokenCount + numNewTokens > _maxTokens)
                throw new InvalidOperationException($"Cannot append {numNewTokens} tokens, would exceed max capacity {_maxTokens}");

            int stride = _modelShape.Heads * _modelShape.HeadDim;
            int expectedSize = numNewTokens * stride;

            if (keyData.Length != expectedSize || valueData.Length != expectedSize)
                throw new ArgumentException($"Expected {expectedSize} elements for {numNewTokens} tokens, got K={keyData.Length}, V={valueData.Length}");

            int offset = _currentTokenCount * stride;

            keyData.CopyTo(_keyCaches[layer].AsSpan(offset, expectedSize));
            valueData.CopyTo(_valueCaches[layer].AsSpan(offset, expectedSize));
        }

        /// <summary>
        /// Commits the appended tokens, advancing the position counter.
        /// Must be called after all layers have been appended with AppendKV.
        /// </summary>
        public void CommitAppend(int numNewTokens)
        {
            _currentTokenCount += numNewTokens;
        }

        /// <summary>
        /// Gets a read-only span of the cached keys for a layer.
        /// </summary>
        public ReadOnlySpan<float> GetKeys(int layer, int startToken, int tokenCount)
        {
            if (layer < 0 || layer >= _modelShape.Layers)
                throw new ArgumentOutOfRangeException(nameof(layer));

            if (startToken + tokenCount > _currentTokenCount)
                throw new ArgumentOutOfRangeException(nameof(tokenCount));

            int stride = _modelShape.Heads * _modelShape.HeadDim;
            int offset = startToken * stride;
            int length = tokenCount * stride;

            return new ReadOnlySpan<float>(_keyCaches[layer], offset, length);
        }

        /// <summary>
        /// Gets a read-only span of the cached values for a layer.
        /// </summary>
        public ReadOnlySpan<float> GetValues(int layer, int startToken, int tokenCount)
        {
            if (layer < 0 || layer >= _modelShape.Layers)
                throw new ArgumentOutOfRangeException(nameof(layer));

            if (startToken + tokenCount > _currentTokenCount)
                throw new ArgumentOutOfRangeException(nameof(tokenCount));

            int stride = _modelShape.Heads * _modelShape.HeadDim;
            int offset = startToken * stride;
            int length = tokenCount * stride;

            return new ReadOnlySpan<float>(_valueCaches[layer], offset, length);
        }

        /// <summary>
        /// Resets the cache, clearing all stored tokens.
        /// </summary>
        public void Reset()
        {
            _currentTokenCount = 0;

            // No need to clear the arrays, we'll just overwrite as we go
        }

        /// <summary>
        /// Implements sliding window by keeping only the last windowSize tokens.
        /// Shifts cached data to the beginning and updates token count.
        /// </summary>
        /// <param name="windowSize">Number of tokens to retain (must be less than current count)</param>
        public void Slide(int windowSize)
        {
            if (windowSize < 0)
                throw new ArgumentOutOfRangeException(nameof(windowSize));

            if (windowSize >= _currentTokenCount)
                return; // Nothing to slide

            int tokensToKeep = Math.Min(windowSize, _currentTokenCount);
            int startPos = _currentTokenCount - tokensToKeep;
            int stride = _modelShape.Heads * _modelShape.HeadDim;
            int srcOffset = startPos * stride;
            int length = tokensToKeep * stride;

            // Shift data to beginning of arrays
            for (int layer = 0; layer < _modelShape.Layers; layer++)
            {
                // Use Buffer.BlockCopy for efficient memory move
                Buffer.BlockCopy(_keyCaches[layer], srcOffset * sizeof(float), _keyCaches[layer], 0, length * sizeof(float));
                Buffer.BlockCopy(_valueCaches[layer], srcOffset * sizeof(float), _valueCaches[layer], 0, length * sizeof(float));
            }

            _currentTokenCount = tokensToKeep;
        }

        /// <summary>
        /// Disposes the cache and returns pooled arrays.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_keyCaches != null)
            {
                for (int i = 0; i < _keyCaches.Length; i++)
                {
                    if (_keyCaches[i] != null)
                    {
                        _pool.Return(_keyCaches[i]);
                        _keyCaches[i] = null!;
                    }
                }
            }

            if (_valueCaches != null)
            {
                for (int i = 0; i < _valueCaches.Length; i++)
                {
                    if (_valueCaches[i] != null)
                    {
                        _pool.Return(_valueCaches[i]);
                        _valueCaches[i] = null!;
                    }
                }
            }

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

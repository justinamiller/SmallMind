using System;

namespace SmallMind.Core
{
    /// <summary>
    /// Per-session inference state for KV cache and batching.
    /// Maintains cache buffers and position tracking for a single inference session.
    /// </summary>
    public sealed class InferenceSession : IDisposable
    {
        private readonly int _numLayers;
        private readonly int _maxSeqLen;
        private readonly int _numHeads;
        private readonly int _headDim;
        
        // Pre-allocated KV cache buffers: [layer][position * numHeads * headDim]
        // Layout: K[layer][position][head][head_dim] flattened to K[layer][pos*nHead*headDim + h*headDim + d]
        private readonly float[][] _keyCaches;
        private readonly float[][] _valueCaches;
        
        // Current position in the sequence (number of tokens processed)
        private int _currentPosition;
        
        // Session active flag
        private bool _isActive;
        private bool _disposed;

        public int CurrentPosition => _currentPosition;
        public bool IsActive => _isActive;
        public int MaxSeqLen => _maxSeqLen;

        public InferenceSession(int numLayers, int maxSeqLen, int numHeads, int headDim)
        {
            if (numLayers <= 0) throw new ArgumentException("numLayers must be positive", nameof(numLayers));
            if (maxSeqLen <= 0) throw new ArgumentException("maxSeqLen must be positive", nameof(maxSeqLen));
            if (numHeads <= 0) throw new ArgumentException("numHeads must be positive", nameof(numHeads));
            if (headDim <= 0) throw new ArgumentException("headDim must be positive", nameof(headDim));
            
            _numLayers = numLayers;
            _maxSeqLen = maxSeqLen;
            _numHeads = numHeads;
            _headDim = headDim;
            
            // Pre-allocate cache buffers
            int cacheSize = maxSeqLen * numHeads * headDim;
            _keyCaches = new float[numLayers][];
            _valueCaches = new float[numLayers][];
            
            for (int i = 0; i < numLayers; i++)
            {
                _keyCaches[i] = new float[cacheSize];
                _valueCaches[i] = new float[cacheSize];
            }
            
            _currentPosition = 0;
            _isActive = true;
        }

        /// <summary>
        /// Get the key cache buffer for a specific layer.
        /// </summary>
        public float[] GetKeyCache(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= _numLayers)
                throw new ArgumentOutOfRangeException(nameof(layerIndex));
            return _keyCaches[layerIndex];
        }

        /// <summary>
        /// Get the value cache buffer for a specific layer.
        /// </summary>
        public float[] GetValueCache(int layerIndex)
        {
            if (layerIndex < 0 || layerIndex >= _numLayers)
                throw new ArgumentOutOfRangeException(nameof(layerIndex));
            return _valueCaches[layerIndex];
        }

        /// <summary>
        /// Advance the current position by the specified number of tokens.
        /// </summary>
        public void AdvancePosition(int numTokens)
        {
            if (numTokens <= 0)
                throw new ArgumentException("numTokens must be positive", nameof(numTokens));
            
            _currentPosition += numTokens;
            
            if (_currentPosition > _maxSeqLen)
                throw new InvalidOperationException($"Position {_currentPosition} exceeds max sequence length {_maxSeqLen}");
        }

        /// <summary>
        /// Reset the session for a new inference run.
        /// </summary>
        public void Reset()
        {
            _currentPosition = 0;
            
            // Clear cache buffers
            for (int i = 0; i < _numLayers; i++)
            {
                Array.Clear(_keyCaches[i], 0, _keyCaches[i].Length);
                Array.Clear(_valueCaches[i], 0, _valueCaches[i].Length);
            }
        }

        /// <summary>
        /// Mark session as inactive (can be reused after reset).
        /// </summary>
        public void Deactivate()
        {
            _isActive = false;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _isActive = false;
            _disposed = true;
        }
    }
}

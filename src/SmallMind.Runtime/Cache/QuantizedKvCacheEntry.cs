using System;
using System.Buffers;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// KV cache entry with quantized storage for memory efficiency.
    /// Supports FP16 (2x reduction) and INT8 (4x reduction).
    /// </summary>
    public sealed class QuantizedKvCacheEntry : IDisposable
    {
        private readonly SessionId _sessionId;
        private readonly ModelShape _modelShape;
        private readonly int _maxTokens;
        private readonly QuantizationType _quantization;
        private readonly ArrayPool<byte> _bytePool;
        private readonly ArrayPool<Half> _halfPool;
        
        private object[] _keyCaches;   // byte[] or Half[] depending on quantization
        private object[] _valueCaches;
        private float[][] _scales;       // For INT8 only
        private float[][] _offsets;      // For INT8 only
        private int _currentTokenCount;
        private bool _disposed;
        
        public SessionId SessionId => _sessionId;
        public ModelShape ModelShape => _modelShape;
        public int CurrentTokenCount => _currentTokenCount;
        public int MaxTokens => _maxTokens;
        
        public QuantizedKvCacheEntry(
            SessionId sessionId,
            ModelShape modelShape,
            int maxTokens,
            QuantizationType quantization)
        {
            _sessionId = sessionId;
            _modelShape = modelShape;
            _maxTokens = maxTokens;
            _quantization = quantization;
            _bytePool = ArrayPool<byte>.Shared;
            _halfPool = ArrayPool<Half>.Shared;
            
            int cacheSize = maxTokens * modelShape.Heads * modelShape.HeadDim;
            
            _keyCaches = new object[modelShape.Layers];
            _valueCaches = new object[modelShape.Layers];
            
            if (quantization == QuantizationType.INT8)
            {
                _scales = new float[modelShape.Layers][];
                _offsets = new float[modelShape.Layers][];
                
                for (int i = 0; i < modelShape.Layers; i++)
                {
                    _keyCaches[i] = _bytePool.Rent(cacheSize);
                    _valueCaches[i] = _bytePool.Rent(cacheSize);
                    _scales[i] = new float[1]; // One scale per cache
                    _offsets[i] = new float[1];
                }
            }
            else if (quantization == QuantizationType.FP16)
            {
                for (int i = 0; i < modelShape.Layers; i++)
                {
                    _keyCaches[i] = _halfPool.Rent(cacheSize);
                    _valueCaches[i] = _halfPool.Rent(cacheSize);
                }
            }
        }
        
        /// <summary>
        /// Appends K/V data with automatic quantization.
        /// </summary>
        public void AppendKV(int layer, ReadOnlySpan<float> keyData, ReadOnlySpan<float> valueData, int numNewTokens)
        {
            if (_quantization == QuantizationType.INT8)
            {
                var keyCache = (byte[])_keyCaches[layer];
                var valueCache = (byte[])_valueCaches[layer];
                
                QuantizationHelpers.QuantizeToInt8(keyData, keyCache.AsSpan(0, keyData.Length),
                    out _scales[layer][0], out _offsets[layer][0]);
                QuantizationHelpers.QuantizeToInt8(valueData, valueCache.AsSpan(0, valueData.Length),
                    out float vScale, out float vOffset);
            }
            else if (_quantization == QuantizationType.FP16)
            {
                var keyCache = (Half[])_keyCaches[layer];
                var valueCache = (Half[])_valueCaches[layer];
                
                for (int i = 0; i < keyData.Length; i++)
                {
                    keyCache[i] = QuantizationHelpers.FloatToHalf(keyData[i]);
                    valueCache[i] = QuantizationHelpers.FloatToHalf(valueData[i]);
                }
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            // Return pooled arrays
            for (int i = 0; i < _modelShape.Layers; i++)
            {
                if (_quantization == QuantizationType.INT8)
                {
                    if (_keyCaches[i] is byte[] kb) _bytePool.Return(kb);
                    if (_valueCaches[i] is byte[] vb) _bytePool.Return(vb);
                }
                else if (_quantization == QuantizationType.FP16)
                {
                    if (_keyCaches[i] is Half[] kh) _halfPool.Return(kh);
                    if (_valueCaches[i] is Half[] vh) _halfPool.Return(vh);
                }
            }
            
            _disposed = true;
        }
    }
}

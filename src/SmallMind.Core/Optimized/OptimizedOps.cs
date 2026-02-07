using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using SmallMind.Core.Utilities;

namespace SmallMind.Core.Optimized
{
    /// <summary>
    /// High-performance SIMD-accelerated operations optimized for transformer attention.
    /// Provides fused operations to reduce memory bandwidth and improve cache efficiency.
    /// </summary>
    public static class OptimizedOps
    {
        private static readonly int VectorSize = Vector<float>.Count;
        
        
        /// <summary>
        /// Fused scale + causal mask + softmax. Reduces memory bandwidth.
        /// Optimizes the attention mechanism by combining multiple operations.
        /// </summary>
        public static void FusedScaleMaskSoftmax(float[] scores, float scale, float[] output, int seqLen)
        {
            FusedScaleMaskSoftmax(scores, 0, scale, output, 0, seqLen, seqLen, 0);
        }
        
        /// <summary>
        /// Fused scale + causal mask + softmax with offset support.
        /// Reduces memory bandwidth by combining multiple operations.
        /// </summary>
        public static void FusedScaleMaskSoftmax(float[] scores, int scoresOffset, float scale, float[] output, int outputOffset, int seqLen)
        {
            FusedScaleMaskSoftmax(scores, scoresOffset, scale, output, outputOffset, seqLen, seqLen, 0);
        }
        
        /// <summary>
        /// Fused scale + causal mask + softmax with KV-cache support.
        /// For KV-cache, kSeqLen may be larger than seqLen (includes cached past).
        /// cacheOffset is the position offset in the cache (how many past tokens are cached).
        /// Optimized to avoid redundant scale multiplies and vectorize normalize step.
        /// </summary>
        public static void FusedScaleMaskSoftmax(float[] scores, int scoresOffset, float scale, float[] output, int outputOffset, int seqLen, int kSeqLen, int cacheOffset)
        {
            for (int i = 0; i < seqLen; i++)
            {
                int rowOffset = i * kSeqLen;
                int rowStart = scoresOffset + rowOffset;
                int outRowStart = outputOffset + rowOffset;
                
                // For KV-cache: position in full sequence is cacheOffset + i
                // Can attend to positions 0..(cacheOffset + i)
                int validCols = cacheOffset + i + 1; // Causal mask with cache offset
                
                // Pass 1: Find max of (score * scale)
                float maxVal = float.NegativeInfinity;
                for (int j = 0; j < validCols; j++)
                {
                    float s = scores[rowStart + j] * scale;
                    if (s > maxVal) maxVal = s;
                }
                
                // Pass 2: Compute exp and sum
                // NOTE: We compute (score * scale - maxVal) in one expression to avoid redundant multiply
                float sum = 0;
                for (int j = 0; j < validCols; j++)
                {
                    float e = MathUtils.FastExp(scores[rowStart + j] * scale - maxVal);
                    output[outRowStart + j] = e;
                    sum += e;
                }
                
                // Pass 3: Normalize with SIMD
                float invSum = 1f / sum;
                if (Vector.IsHardwareAccelerated && validCols >= VectorSize)
                {
                    var vInvSum = new Vector<float>(invSum);
                    int j = 0;
                    for (; j <= validCols - VectorSize; j += VectorSize)
                    {
                        var v = new Vector<float>(output, outRowStart + j);
                        (v * vInvSum).CopyTo(output, outRowStart + j);
                    }
                    for (; j < validCols; j++)
                        output[outRowStart + j] *= invSum;
                }
                else
                {
                    for (int j = 0; j < validCols; j++)
                        output[outRowStart + j] *= invSum;
                }
                
                // Zero out masked positions
                for (int j = validCols; j < kSeqLen; j++)
                    output[outRowStart + j] = 0;
            }
        }
        
        /// <summary>
        /// SIMD element-wise add: C = A + B
        /// </summary>
        public static void Add(float[] a, float[] b, float[] c, int length)
        {
            int i = 0;
            if (Vector.IsHardwareAccelerated)
            {
                for (; i <= length - VectorSize; i += VectorSize)
                {
                    var va = new Vector<float>(a, i);
                    var vb = new Vector<float>(b, i);
                    (va + vb).CopyTo(c, i);
                }
            }
            for (; i < length; i++)
                c[i] = a[i] + b[i];
        }
        
        /// <summary>
        /// SIMD scalar multiply: B = A * scalar
        /// </summary>
        public static void Scale(float[] a, float scalar, float[] b, int length)
        {
            int i = 0;
            if (Vector.IsHardwareAccelerated)
            {
                var vScalar = new Vector<float>(scalar);
                for (; i <= length - VectorSize; i += VectorSize)
                {
                    var va = new Vector<float>(a, i);
                    (va * vScalar).CopyTo(b, i);
                }
            }
            for (; i < length; i++)
                b[i] = a[i] * scalar;
        }
    }
    
    /// <summary>
    /// KV-Cache for efficient autoregressive generation.
    /// Reduces O(n²) to O(n) per token during text generation.
    /// </summary>
    public class KVCache
    {
        private readonly float[][] _keyCache;
        private readonly float[][] _valueCache;
        private readonly int _numLayers;
        private readonly int _numHeads;
        private readonly int _headDim;
        private readonly int _maxSeqLen;
        
        public int CurrentLength { get; private set; }
        
        public KVCache(int numLayers, int numHeads, int headDim, int maxSeqLen)
        {
            _numLayers = numLayers;
            _numHeads = numHeads;
            _headDim = headDim;
            _maxSeqLen = maxSeqLen;
            
            int cacheSize = maxSeqLen * numHeads * headDim;
            _keyCache = new float[numLayers][];
            _valueCache = new float[numLayers][];
            
            for (int l = 0; l < numLayers; l++)
            {
                _keyCache[l] = new float[cacheSize];
                _valueCache[l] = new float[cacheSize];
            }
        }
        
        public void AppendKV(int layer, float[] newK, float[] newV, int numTokens)
        {
            if (layer < 0 || layer >= _numLayers)
                throw new ArgumentOutOfRangeException(nameof(layer), $"Layer must be between 0 and {_numLayers - 1}");
            
            if (CurrentLength + numTokens > _maxSeqLen)
                throw new InvalidOperationException($"Cannot append {numTokens} tokens: would exceed maximum sequence length {_maxSeqLen}");
            
            int stride = _numHeads * _headDim;
            int offset = CurrentLength * stride;
            Array.Copy(newK, 0, _keyCache[layer], offset, numTokens * stride);
            Array.Copy(newV, 0, _valueCache[layer], offset, numTokens * stride);
        }
        
        public float[] GetKeys(int layer)
        {
            if (layer < 0 || layer >= _numLayers)
                throw new ArgumentOutOfRangeException(nameof(layer), $"Layer must be between 0 and {_numLayers - 1}");
            return _keyCache[layer];
        }
        
        public float[] GetValues(int layer)
        {
            if (layer < 0 || layer >= _numLayers)
                throw new ArgumentOutOfRangeException(nameof(layer), $"Layer must be between 0 and {_numLayers - 1}");
            return _valueCache[layer];
        }
        
        public void IncrementPosition(int count = 1)
        {
            if (CurrentLength + count > _maxSeqLen)
                throw new InvalidOperationException($"Cannot increment position by {count}: would exceed maximum sequence length {_maxSeqLen}");
            CurrentLength += count;
        }
        
        public void Reset() => CurrentLength = 0;
    }
    
    /// <summary>
    /// Optimized object pool for float arrays with power-of-2 sizing.
    /// Reduces GC pressure through array reuse.
    /// </summary>
    public sealed class OptimizedArrayPool
    {
        private const int MaxPooledArraySize = 1024 * 1024; // 1MB
        private const int MaxArraysPerBucket = 32;
        
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, 
            System.Collections.Concurrent.ConcurrentBag<float[]>> _pools = new();
        
        public static OptimizedArrayPool Shared { get; } = new();
        
        public float[] Rent(int size)
        {
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size), "Size must be greater than 0");
            
            int poolSize = RoundUpToPowerOf2(size);
            if (_pools.TryGetValue(poolSize, out var bag) && bag.TryTake(out var array))
                return array;
            return new float[poolSize];
        }
        
        public void Return(float[] array)
        {
            if (array == null || array.Length > MaxPooledArraySize) return;
            var bag = _pools.GetOrAdd(array.Length, _ => new());
            if (bag.Count < MaxArraysPerBucket) bag.Add(array);
        }
        
        private static int RoundUpToPowerOf2(int v)
        {
            if (v <= 0) return 1;
            v--; v |= v >> 1; v |= v >> 2; v |= v >> 4; v |= v >> 8; v |= v >> 16;
            return v + 1;
        }
    }
}

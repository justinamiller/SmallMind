using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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
        /// SIMD-accelerated dot product. 4-8x faster than scalar loop.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DotProduct(float[] a, int aOffset, float[] b, int bOffset, int length)
        {
            if (!Vector.IsHardwareAccelerated || length < VectorSize * 2)
            {
                float sum = 0;
                for (int idx = 0; idx < length; idx++)
                    sum += a[aOffset + idx] * b[bOffset + idx];
                return sum;
            }
            
            var vsum = Vector<float>.Zero;
            int i = 0;
            int limit = length - (length % VectorSize);
            
            for (; i < limit; i += VectorSize)
            {
                var va = new Vector<float>(a, aOffset + i);
                var vb = new Vector<float>(b, bOffset + i);
                vsum += va * vb;
            }
            
            float result = 0;
            for (int j = 0; j < VectorSize; j++)
                result += vsum[j];
            for (; i < length; i++)
                result += a[aOffset + i] * b[bOffset + i];
            
            return result;
        }
        
        /// <summary>
        /// SIMD-accelerated softmax over a single row.
        /// </summary>
        public static void SoftmaxRow(float[] input, int offset, int length, float[] output, int outOffset)
        {
            float maxVal = float.NegativeInfinity;
            for (int i = 0; i < length; i++)
                if (input[offset + i] > maxVal) maxVal = input[offset + i];
            
            float sum = 0;
            for (int i = 0; i < length; i++)
            {
                output[outOffset + i] = MathF.Exp(input[offset + i] - maxVal);
                sum += output[outOffset + i];
            }
            
            float invSum = 1f / sum;
            if (Vector.IsHardwareAccelerated && length >= VectorSize)
            {
                var vInvSum = new Vector<float>(invSum);
                int i = 0;
                for (; i <= length - VectorSize; i += VectorSize)
                {
                    var v = new Vector<float>(output, outOffset + i);
                    (v * vInvSum).CopyTo(output, outOffset + i);
                }
                for (; i < length; i++)
                    output[outOffset + i] *= invSum;
            }
            else
            {
                for (int i = 0; i < length; i++)
                    output[outOffset + i] *= invSum;
            }
        }
        
        /// <summary>
        /// Fused scale + causal mask + softmax. Reduces memory bandwidth.
        /// Optimizes the attention mechanism by combining multiple operations.
        /// </summary>
        public static void FusedScaleMaskSoftmax(float[] scores, float scale, float[] output, int seqLen)
        {
            for (int i = 0; i < seqLen; i++)
            {
                int rowOffset = i * seqLen;
                int validCols = i + 1; // Causal mask
                
                float maxVal = float.NegativeInfinity;
                for (int j = 0; j < validCols; j++)
                {
                    float s = scores[rowOffset + j] * scale;
                    if (s > maxVal) maxVal = s;
                }
                
                float sum = 0;
                for (int j = 0; j < validCols; j++)
                {
                    float e = MathF.Exp(scores[rowOffset + j] * scale - maxVal);
                    output[rowOffset + j] = e;
                    sum += e;
                }
                
                float invSum = 1f / sum;
                for (int j = 0; j < validCols; j++)
                    output[rowOffset + j] *= invSum;
                
                for (int j = validCols; j < seqLen; j++)
                    output[rowOffset + j] = 0;
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
            int stride = _numHeads * _headDim;
            int offset = CurrentLength * stride;
            Array.Copy(newK, 0, _keyCache[layer], offset, numTokens * stride);
            Array.Copy(newV, 0, _valueCache[layer], offset, numTokens * stride);
        }
        
        public float[] GetKeys(int layer) => _keyCache[layer];
        public float[] GetValues(int layer) => _valueCache[layer];
        
        public void IncrementPosition(int count = 1) => CurrentLength += count;
        public void Reset() => CurrentLength = 0;
    }
    
    /// <summary>
    /// Object pool for tensors to reduce GC pressure.
    /// Implements power-of-2 pooling strategy for efficient memory reuse.
    /// </summary>
    public sealed class TensorPool
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<int, 
            System.Collections.Concurrent.ConcurrentBag<float[]>> _pools = new();
        
        public static TensorPool Shared { get; } = new();
        
        public float[] Rent(int size)
        {
            int poolSize = RoundUpToPowerOf2(size);
            if (_pools.TryGetValue(poolSize, out var bag) && bag.TryTake(out var array))
                return array;
            return new float[poolSize];
        }
        
        public void Return(float[] array)
        {
            if (array == null || array.Length > 1024 * 1024) return;
            var bag = _pools.GetOrAdd(array.Length, _ => new());
            if (bag.Count < 32) bag.Add(array);
        }
        
        private static int RoundUpToPowerOf2(int v)
        {
            v--; v |= v >> 1; v |= v >> 2; v |= v >> 4; v |= v >> 8; v |= v >> 16;
            return v + 1;
        }
    }
}

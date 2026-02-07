using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Production-grade KV cache session with SIMD-friendly contiguous memory layout.
    /// 
    /// Memory Layout:
    /// - Contiguous arrays for K and V separately: float[nLayers * maxTokens * nKvHeads * headDim]
    /// - Stride calculations for efficient access:
    ///   - HeadDim stride: headDim (fastest changing, contiguous for SIMD)
    ///   - KvHead stride: headDim
    ///   - Position stride: nKvHeads * headDim
    ///   - Layer stride: maxTokens * nKvHeads * headDim
    /// 
    /// Layout: [layer][position][kvHead][headDim]
    /// This ensures:
    /// 1. headDim is contiguous for vectorized operations
    /// 2. Reading all positions for a head is stride-predictable
    /// 3. CPU cache-friendly access patterns
    /// 
    /// RoPE Contract:
    /// - K tensors are stored POST-RoPE application
    /// - RoPE must be applied before calling WriteK
    /// - Cached K values are ready for attention computation
    /// 
    /// GQA Support:
    /// - nKvHeads can be less than nHeads (e.g., nHeads=8, nKvHeads=2)
    /// - Head mapping: kvHeadIndex = headIndex / (nHeads / nKvHeads)
    /// - Attention layer handles broadcasting during score computation
    /// </summary>
    public sealed class KvCacheSession : IDisposable
    {
        private readonly SessionId _sessionId;
        private readonly KvCacheBudgetPolicy _budgetPolicy;
        private readonly ArrayPool<float> _pool;
        
        private readonly int _nLayers;
        private readonly int _nKvHeads;
        private readonly int _headDim;
        private readonly int _maxTokens;
        
        // Contiguous storage for K and V
        private float[] _keyCache;    // [nLayers * maxTokens * nKvHeads * headDim]
        private float[] _valueCache;  // [nLayers * maxTokens * nKvHeads * headDim]
        
        // Stride constants for efficient indexing
        private readonly int _strideHeadDim;     // = headDim (fastest changing)
        private readonly int _strideKvHead;      // = headDim
        private readonly int _stridePosition;    // = nKvHeads * headDim
        private readonly int _strideLayer;       // = maxTokens * nKvHeads * headDim
        
        private int _currentTokenCount;
        private bool _disposed;

        /// <summary>
        /// Gets the session ID.
        /// </summary>
        public SessionId SessionId => _sessionId;

        /// <summary>
        /// Gets the current number of cached tokens.
        /// </summary>
        public int CurrentTokenCount => _currentTokenCount;

        /// <summary>
        /// Gets the maximum number of tokens this cache can hold.
        /// </summary>
        public int MaxTokens => _maxTokens;

        /// <summary>
        /// Gets the number of layers.
        /// </summary>
        public int NumLayers => _nLayers;

        /// <summary>
        /// Gets the number of KV heads.
        /// </summary>
        public int NumKvHeads => _nKvHeads;

        /// <summary>
        /// Gets the head dimension.
        /// </summary>
        public int HeadDim => _headDim;

        /// <summary>
        /// Creates a new KV cache session with pre-allocated contiguous memory.
        /// </summary>
        /// <param name="sessionId">Session identifier</param>
        /// <param name="nLayers">Number of transformer layers</param>
        /// <param name="nKvHeads">Number of KV heads (for GQA)</param>
        /// <param name="headDim">Head dimension</param>
        /// <param name="budgetPolicy">Budget policy for memory enforcement</param>
        public KvCacheSession(
            SessionId sessionId,
            int nLayers,
            int nKvHeads,
            int headDim,
            KvCacheBudgetPolicy budgetPolicy)
        {
            if (nLayers <= 0)
                throw new ArgumentOutOfRangeException(nameof(nLayers));
            if (nKvHeads <= 0)
                throw new ArgumentOutOfRangeException(nameof(nKvHeads));
            if (headDim <= 0)
                throw new ArgumentOutOfRangeException(nameof(headDim));
            if (budgetPolicy == null)
                throw new ArgumentNullException(nameof(budgetPolicy));

            _sessionId = sessionId;
            _budgetPolicy = budgetPolicy;
            _pool = ArrayPool<float>.Shared;
            
            _nLayers = nLayers;
            _nKvHeads = nKvHeads;
            _headDim = headDim;
            _maxTokens = budgetPolicy.MaxSeqLen;

            // Calculate strides for contiguous layout: [layer][position][kvHead][headDim]
            _strideHeadDim = headDim;
            _strideKvHead = headDim;
            _stridePosition = nKvHeads * headDim;
            _strideLayer = _maxTokens * nKvHeads * headDim;

            // Pre-allocate contiguous arrays for K and V
            int totalElements = nLayers * _maxTokens * nKvHeads * headDim;
            
            // Validate budget before allocation
            _budgetPolicy.ValidateReservation(0, _maxTokens);
            
            // Use ArrayPool for efficient memory management
            _keyCache = _pool.Rent(totalElements);
            _valueCache = _pool.Rent(totalElements);
            
            // Clear allocated memory (rented arrays may contain stale data)
            _keyCache.AsSpan(0, totalElements).Clear();
            _valueCache.AsSpan(0, totalElements).Clear();

            _currentTokenCount = 0;
        }

        /// <summary>
        /// Tries to reserve space for additional tokens.
        /// </summary>
        /// <param name="additionalTokens">Number of tokens to reserve</param>
        /// <returns>True if reservation succeeded, false if budget would be exceeded</returns>
        public bool TryReserveTokens(int additionalTokens)
        {
            if (additionalTokens <= 0)
                return true;

            return _budgetPolicy.TryReserveTokens(_currentTokenCount, additionalTokens);
        }

        /// <summary>
        /// Validates that space can be reserved for additional tokens.
        /// Throws OutOfBudgetException if budget would be exceeded.
        /// </summary>
        /// <param name="additionalTokens">Number of tokens to reserve</param>
        public void ValidateReservation(int additionalTokens)
        {
            if (additionalTokens <= 0)
                return;

            _budgetPolicy.ValidateReservation(_currentTokenCount, additionalTokens);
        }

        /// <summary>
        /// Writes K tensor for a specific layer, position, and kvHead.
        /// IMPORTANT: K must be POST-RoPE (RoPE applied before calling this method).
        /// </summary>
        /// <param name="layer">Layer index</param>
        /// <param name="position">Sequence position</param>
        /// <param name="kvHead">KV head index</param>
        /// <param name="src">Source data (headDim elements)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteK(int layer, int position, int kvHead, ReadOnlySpan<float> src)
        {
#if DEBUG
            ValidateWrite(layer, position, kvHead, src.Length);
#endif
            int offset = ComputeOffset(layer, position, kvHead);
            src.CopyTo(_keyCache.AsSpan(offset, _headDim));
        }

        /// <summary>
        /// Writes V tensor for a specific layer, position, and kvHead.
        /// </summary>
        /// <param name="layer">Layer index</param>
        /// <param name="position">Sequence position</param>
        /// <param name="kvHead">KV head index</param>
        /// <param name="src">Source data (headDim elements)</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WriteV(int layer, int position, int kvHead, ReadOnlySpan<float> src)
        {
#if DEBUG
            ValidateWrite(layer, position, kvHead, src.Length);
#endif
            int offset = ComputeOffset(layer, position, kvHead);
            src.CopyTo(_valueCache.AsSpan(offset, _headDim));
        }

        /// <summary>
        /// Gets a read-only span of K data for a specific layer, position, and kvHead.
        /// </summary>
        /// <param name="layer">Layer index</param>
        /// <param name="position">Sequence position</param>
        /// <param name="kvHead">KV head index</param>
        /// <returns>ReadOnlySpan of headDim elements</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<float> GetKSpan(int layer, int position, int kvHead)
        {
#if DEBUG
            ValidateRead(layer, position, kvHead);
#endif
            int offset = ComputeOffset(layer, position, kvHead);
            return new ReadOnlySpan<float>(_keyCache, offset, _headDim);
        }

        /// <summary>
        /// Gets a read-only span of V data for a specific layer, position, and kvHead.
        /// </summary>
        /// <param name="layer">Layer index</param>
        /// <param name="position">Sequence position</param>
        /// <param name="kvHead">KV head index</param>
        /// <returns>ReadOnlySpan of headDim elements</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<float> GetVSpan(int layer, int position, int kvHead)
        {
#if DEBUG
            ValidateRead(layer, position, kvHead);
#endif
            int offset = ComputeOffset(layer, position, kvHead);
            return new ReadOnlySpan<float>(_valueCache, offset, _headDim);
        }

        /// <summary>
        /// Gets a contiguous span of all K data for a layer and position range.
        /// Useful for bulk operations during attention computation.
        /// </summary>
        /// <param name="layer">Layer index</param>
        /// <param name="startPos">Start position (inclusive)</param>
        /// <param name="endPos">End position (exclusive)</param>
        /// <param name="kvHead">KV head index</param>
        /// <returns>ReadOnlySpan of (endPos - startPos) * headDim elements</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<float> GetKRange(int layer, int startPos, int endPos, int kvHead)
        {
#if DEBUG
            if (startPos < 0 || startPos >= endPos || endPos > _currentTokenCount)
                throw new ArgumentOutOfRangeException(nameof(startPos));
            ValidateLayerAndHead(layer, kvHead);
#endif
            int offset = ComputeOffset(layer, startPos, kvHead);
            int count = (endPos - startPos) * _stridePosition / _nKvHeads; // Per head stride
            return new ReadOnlySpan<float>(_keyCache, offset, count);
        }

        /// <summary>
        /// Gets a contiguous span of all V data for a layer and position range.
        /// </summary>
        /// <param name="layer">Layer index</param>
        /// <param name="startPos">Start position (inclusive)</param>
        /// <param name="endPos">End position (exclusive)</param>
        /// <param name="kvHead">KV head index</param>
        /// <returns>ReadOnlySpan of (endPos - startPos) * headDim elements</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ReadOnlySpan<float> GetVRange(int layer, int startPos, int endPos, int kvHead)
        {
#if DEBUG
            if (startPos < 0 || startPos >= endPos || endPos > _currentTokenCount)
                throw new ArgumentOutOfRangeException(nameof(startPos));
            ValidateLayerAndHead(layer, kvHead);
#endif
            int offset = ComputeOffset(layer, startPos, kvHead);
            int count = (endPos - startPos) * _stridePosition / _nKvHeads;
            return new ReadOnlySpan<float>(_valueCache, offset, count);
        }

        /// <summary>
        /// Commits new tokens after they have been written via WriteK/WriteV.
        /// Advances the current token count.
        /// </summary>
        /// <param name="numNewTokens">Number of new tokens to commit</param>
        public void CommitTokens(int numNewTokens)
        {
            if (numNewTokens <= 0)
                return;

            if (_currentTokenCount + numNewTokens > _maxTokens)
            {
                throw new InvalidOperationException(
                    $"Cannot commit {numNewTokens} tokens. " +
                    $"Current: {_currentTokenCount}, Max: {_maxTokens}");
            }

            _currentTokenCount += numNewTokens;
        }

        /// <summary>
        /// Resets the cache, clearing the current token count.
        /// Memory is not cleared (will be overwritten on next write).
        /// </summary>
        public void Reset()
        {
            _currentTokenCount = 0;
        }

        /// <summary>
        /// Computes the flat array offset for a given layer, position, and kvHead.
        /// Layout: [layer][position][kvHead][headDim]
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int ComputeOffset(int layer, int position, int kvHead)
        {
            return layer * _strideLayer +
                   position * _stridePosition +
                   kvHead * _strideKvHead;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateWrite(int layer, int position, int kvHead, int srcLength)
        {
            if (layer < 0 || layer >= _nLayers)
                throw new ArgumentOutOfRangeException(nameof(layer));
            if (position < 0 || position >= _maxTokens)
                throw new ArgumentOutOfRangeException(nameof(position));
            if (kvHead < 0 || kvHead >= _nKvHeads)
                throw new ArgumentOutOfRangeException(nameof(kvHead));
            if (srcLength != _headDim)
                throw new ArgumentException($"Expected {_headDim} elements, got {srcLength}", nameof(srcLength));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateRead(int layer, int position, int kvHead)
        {
            if (layer < 0 || layer >= _nLayers)
                throw new ArgumentOutOfRangeException(nameof(layer));
            if (position < 0 || position >= _currentTokenCount)
                throw new ArgumentOutOfRangeException(nameof(position));
            if (kvHead < 0 || kvHead >= _nKvHeads)
                throw new ArgumentOutOfRangeException(nameof(kvHead));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ValidateLayerAndHead(int layer, int kvHead)
        {
            if (layer < 0 || layer >= _nLayers)
                throw new ArgumentOutOfRangeException(nameof(layer));
            if (kvHead < 0 || kvHead >= _nKvHeads)
                throw new ArgumentOutOfRangeException(nameof(kvHead));
        }

        /// <summary>
        /// Disposes the cache and returns pooled arrays.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_keyCache != null)
            {
                _pool.Return(_keyCache, clearArray: false); // No need to clear, will be cleared on next allocation
                _keyCache = null!;
            }

            if (_valueCache != null)
            {
                _pool.Return(_valueCache, clearArray: false);
                _valueCache = null!;
            }

            _disposed = true;
        }
    }
}

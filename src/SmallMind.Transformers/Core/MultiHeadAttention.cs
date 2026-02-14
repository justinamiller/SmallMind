using System.Numerics;
using System.Runtime.CompilerServices;
using SmallMind.Core.Core;
using SmallMind.Core.Optimized;
using SmallMind.Core.Simd;

namespace SmallMind.Transformers
{
    internal sealed class MultiHeadAttention
    {
        private readonly int _nEmbd;
        private readonly int _nHead;
        private readonly int _nKvHead;  // Number of key/value heads (for GQA)
        private readonly int _headSize;
        internal readonly Linear _qkv;
        internal readonly Linear _proj;
        private readonly Dropout _attnDropout;
        private readonly Dropout _projDropout;
        private readonly bool[,] _causalMask;
        private readonly int _blockSize;
        private readonly RotaryEmbedding? _rope;  // Optional RoPE support

        // Workspace tensors for reuse in forward pass (avoid allocations)
        // These persist across forward passes and are sized dynamically
        // They use regular Tensor (not PooledTensor) to avoid premature disposal
        private Tensor? _qWorkspace;
        private Tensor? _kWorkspace;
        private Tensor? _vWorkspace;
        private Tensor? _scoresWorkspace;
        private Tensor? _attnOutputWorkspace;
        private Tensor? _reshapedOutputWorkspace;  // Added for ReshapeAttentionOutput

        // TIER-1 OPTIMIZATION: Track workspace freshness to skip clearing newly allocated arrays.
        // ConditionalWeakTable provides no-allocation tracking without preventing GC.
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<Tensor, object?> _freshWorkspaces = new();

        // Cached shape arrays to avoid per-forward allocations
        // These are reused and updated with current batch/sequence dimensions
        private readonly int[] _qShapeCache = new int[4];      // [B, nHead, T, headSize]
        private readonly int[] _kShapeCache = new int[4];      // [B, nKvHead, T, headSize]
        private readonly int[] _vShapeCache = new int[4];      // [B, nKvHead, T, headSize]
        private readonly int[] _scoresShapeCache = new int[4]; // [B, nHead, T, fullSeqLen]
        private readonly int[] _reshapedShapeCache = new int[3]; // [B, T, nEmbd]
        private readonly int[] _cacheShapeCache = new int[4];  // [B, nKvHead, blockSize, headSize]

        // KV-Cache support for efficient autoregressive generation
        private Tensor? _cachedKeys;    // Cached keys from previous tokens
        private Tensor? _cachedValues;  // Cached values from previous tokens
        private int _cachePosition;     // Current position in the cache
        private bool _useKVCache;       // Whether to use KV-cache (inference mode)

        private bool _isTraining = true;

        public List<Tensor> Parameters { get; private set; }

        /// <summary>
        /// Creates a multi-head attention layer.
        /// </summary>
        /// <param name="nEmbd">Embedding dimension</param>
        /// <param name="nHead">Number of query heads</param>
        /// <param name="blockSize">Maximum sequence length</param>
        /// <param name="dropout">Dropout probability</param>
        /// <param name="random">Random number generator</param>
        /// <param name="nKvHead">Number of key/value heads (default: same as nHead). Use less for GQA/MQA.</param>
        /// <param name="useRope">Whether to use Rotary Position Embeddings</param>
        /// <param name="ropeTheta">RoPE base frequency (default 10000.0)</param>
        public MultiHeadAttention(int nEmbd, int nHead, int blockSize, float dropout, Random random,
            int? nKvHead = null, bool useRope = false, float ropeTheta = 10000.0f)
        {
            _nEmbd = nEmbd;
            _nHead = nHead;
            _nKvHead = nKvHead ?? nHead;  // Default: same as nHead (standard MHA)
            _headSize = nEmbd / nHead;
            _blockSize = blockSize;
            _useKVCache = false;
            _cachePosition = 0;

            if (_nEmbd % _nHead != 0)
            {
                throw new ArgumentException("Embedding dimension must be divisible by number of heads");
            }

            if (_nHead % _nKvHead != 0)
            {
                throw new ArgumentException($"Number of query heads ({_nHead}) must be divisible by number of KV heads ({_nKvHead})");
            }

            // Linear projection for Q, K, V
            // Q: nEmbd -> nEmbd (nHead * headSize)
            // K, V: nEmbd -> nKvHead * headSize
            int kvDim = _nKvHead * _headSize;
            _qkv = new Linear(_nEmbd, _nEmbd + 2 * kvDim, random: random);
            _proj = new Linear(_nEmbd, _nEmbd, random: random);
            _attnDropout = new Dropout(dropout, random);
            _projDropout = new Dropout(dropout, random);

            // Create causal mask: lower triangular matrix
            _causalMask = new bool[blockSize, blockSize];
            for (int i = 0; i < blockSize; i++)
            {
                for (int j = 0; j < blockSize; j++)
                {
                    _causalMask[i, j] = i >= j;
                }
            }

            // Initialize RoPE if requested
            if (useRope)
            {
                _rope = new RotaryEmbedding(blockSize, _headSize, ropeTheta);
            }

            Parameters = new List<Tensor>();
            Parameters.AddRange(_qkv.Parameters);
            Parameters.AddRange(_proj.Parameters);
        }

        /// <summary>
        /// Enable KV-cache for efficient autoregressive generation.
        /// Call this before inference to reuse past key/value computations.
        /// </summary>
        public void EnableKVCache()
        {
            _useKVCache = true;
            _cachePosition = 0;
        }

        /// <summary>
        /// Disable KV-cache and reset cache state.
        /// </summary>
        public void DisableKVCache()
        {
            _useKVCache = false;
            _cachePosition = 0;
            _cachedKeys = null;
            _cachedValues = null;
        }

        /// <summary>
        /// Reset KV-cache position to start a new sequence.
        /// Keeps the cache allocated but resets the position counter.
        /// </summary>
        public void ResetKVCache()
        {
            _cachePosition = 0;
        }

        /// <summary>
        /// Get or allocate workspace tensor for the given shape.
        /// Reuses existing workspace if shape matches, otherwise allocates new one.
        /// Uses regular Tensor (not PooledTensor) because these workspaces persist across forward passes.
        /// </summary>
        /// <param name="workspace">Reference to the workspace tensor field</param>
        /// <param name="shape">Desired tensor shape</param>
        /// <param name="clearBeforeReuse">Whether to clear the workspace when reusing (default: true).
        /// Set to false ONLY when the workspace will be fully overwritten by store-once kernels
        /// (e.g., MatMulTransposeB, softmax output). Keep true for accumulation kernels (e.g., MatMul FMA).</param>
        /// <returns>Workspace tensor ready for use</returns>
        private Tensor GetOrAllocateWorkspace(ref Tensor? workspace, int[] shape, bool clearBeforeReuse = true)
            => GetOrAllocateWorkspace(ref workspace, new ReadOnlySpan<int>(shape), clearBeforeReuse);

        /// <summary>
        /// Get or allocate workspace tensor for the given shape (span-based, zero-allocation).
        /// Reuses existing workspace if shape matches, otherwise allocates new one.
        /// Use with cached shape arrays to avoid per-forward allocations.
        /// </summary>
        /// <param name="workspace">Reference to the workspace tensor field</param>
        /// <param name="shape">Desired tensor shape (as span)</param>
        /// <param name="clearBeforeReuse">Whether to clear the workspace when reusing (default: true).
        /// Set to false ONLY when the workspace will be fully overwritten by store-once kernels
        /// (e.g., MatMulTransposeB, softmax output). Keep true for accumulation kernels (e.g., MatMul FMA).</param>
        /// <returns>Workspace tensor ready for use</returns>
        private Tensor GetOrAllocateWorkspace(ref Tensor? workspace, ReadOnlySpan<int> shape, bool clearBeforeReuse = true)
        {
            // Check if we can reuse existing workspace
            if (workspace != null && TransformerHelpers.ShapesMatch(workspace.Shape, shape))
            {
                // TIER-1 OPTIMIZATION: Conditionally clear workspace based on kernel requirements.
                // - Accumulation kernels (MatMul with FMA: C += A*B) require zeroed buffers.
                // - Store-once kernels (MatMulTransposeB: C = sum, softmax) can skip clearing.
                // - Newly allocated arrays are already zeroed by runtime, skip redundant clear.

                bool isFresh = _freshWorkspaces.TryGetValue(workspace, out _);
                if (!isFresh && clearBeforeReuse)
                {
                    Array.Clear(workspace.Data, 0, workspace.Data.Length);
                }

                // Mark as used (no longer fresh)
                _freshWorkspaces.Remove(workspace);

                return workspace;
            }

            // Allocate new workspace (must create array here, but only on shape change)
            var shapeArray = shape.ToArray();
            workspace = new Tensor(shapeArray, requiresGrad: _isTraining);

            // TIER-1 OPTIMIZATION: Mark newly allocated workspace as fresh.
            // The backing array is zeroed by the runtime, so no need to clear it.
            _freshWorkspaces.AddOrUpdate(workspace, null);

            return workspace;
        }

        public Tensor Forward(Tensor x)
        {
            // x shape: (B, T, n_embd)
            int B = x.Shape[0];
            int T = x.Shape[1];

            // Compute Q, K, V: (B, T, n_embd) -> (B, T, nEmbd + 2*kvDim)
            var qkv = _qkv.Forward(x);

            // Use cached shape arrays to avoid allocations (update in place)
            TransformerHelpers.UpdateShapeCache4D(_qShapeCache, B, _nHead, T, _headSize);
            TransformerHelpers.UpdateShapeCache4D(_kShapeCache, B, _nKvHead, T, _headSize);
            TransformerHelpers.UpdateShapeCache4D(_vShapeCache, B, _nKvHead, T, _headSize);

            // TIER-1 AUDIT: These workspaces are fully overwritten by Array.Copy (store-once).
            // clearBeforeReuse=false skips redundant zeroing for 30-50% speedup on workspace reuse.
            var q = GetOrAllocateWorkspace(ref _qWorkspace, _qShapeCache, clearBeforeReuse: false);
            var k = GetOrAllocateWorkspace(ref _kWorkspace, _kShapeCache, clearBeforeReuse: false);
            var v = GetOrAllocateWorkspace(ref _vWorkspace, _vShapeCache, clearBeforeReuse: false);

            // Split into Q, K, V and reshape
            // Q: (B, T, nEmbd) -> (B, nHead, T, headSize)
            // K, V: (B, T, kvDim) -> (B, nKvHead, T, headSize)
            ExtractAndReshapeQInPlace(qkv, q, B, T);
            ExtractAndReshapeKVInPlace(qkv, k, v, B, T);

            // Apply RoPE if enabled
            if (_rope != null)
            {
                _rope.ApplyInPlace(
                    q.Data.AsSpan(),
                    k.Data.AsSpan(),
                    _cachePosition,  // Position offset for incremental decoding
                    B,
                    _nHead,
                    _nKvHead,
                    T);
            }

            // KV-Cache: Use cached keys/values if available
            Tensor kFull, vFull;
            int fullSeqLen;

            if (_useKVCache && !_isTraining)
            {
                // Initialize cache on first use (use nKvHead for GQA)
                if (_cachedKeys == null)
                {
                    // Use cached shape array to avoid allocation
                    TransformerHelpers.UpdateShapeCache4D(_cacheShapeCache, B, _nKvHead, _blockSize, _headSize);
                    // Clone the cache shape for tensor storage (one-time allocation)
                    _cachedKeys = new Tensor((int[])_cacheShapeCache.Clone(), requiresGrad: false);
                    _cachedValues = new Tensor((int[])_cacheShapeCache.Clone(), requiresGrad: false);
                }

                // Append new K, V to cache
                int cacheOffset = _cachePosition * _headSize;
                for (int b = 0; b < B; b++)
                {
                    for (int h = 0; h < _nKvHead; h++)
                    {
                        int bhOffset = (b * _nKvHead + h) * T * _headSize;
                        int cacheIndex = (b * _nKvHead + h) * _blockSize * _headSize + cacheOffset;

                        // Copy new keys and values to cache
                        Array.Copy(k.Data, bhOffset, _cachedKeys.Data, cacheIndex, T * _headSize);
                        Array.Copy(v.Data, bhOffset, _cachedValues.Data, cacheIndex, T * _headSize);
                    }
                }

                // Use full cache (past + current)
                fullSeqLen = _cachePosition + T;
                kFull = _cachedKeys;
                vFull = _cachedValues;

                // Increment cache position for next forward pass
                _cachePosition += T;
            }
            else
            {
                // No caching: use current K, V
                kFull = k;
                vFull = v;
                fullSeqLen = T;
            }

            // Use workspace for attention scores (update cached shape)
            TransformerHelpers.UpdateShapeCache4D(_scoresShapeCache, B, _nHead, T, fullSeqLen);
            // TIER-1 AUDIT: Scores workspace is fully overwritten by MatMulTransposeB (store-once: C = sum).
            // clearBeforeReuse=false eliminates unnecessary zeroing of potentially large (T×T) score matrices.
            var att = GetOrAllocateWorkspace(ref _scoresWorkspace, _scoresShapeCache, clearBeforeReuse: false);
            ComputeAttentionScoresInPlace(q, kFull, att, B, T, fullSeqLen);

            // Use workspace for attention output (reuse qShapeCache)
            // IMPORTANT: y workspace MUST be cleared because ApplyAttentionInPlace uses MatMul with FMA (C += A*B).
            // MatMul accumulates into the output buffer, so it must start zeroed.
            var y = GetOrAllocateWorkspace(ref _attnOutputWorkspace, _qShapeCache, clearBeforeReuse: true);
            ApplyAttentionInPlace(att, vFull, y, B, T, fullSeqLen);

            // Reshape back: (B, nHead, T, headSize) -> (B, T, n_embd)
            // Use cached shape array to avoid allocation
            TransformerHelpers.UpdateShapeCache3D(_reshapedShapeCache, B, T, _nEmbd);
            // TIER-1 AUDIT: Reshaped workspace is fully overwritten by Array.Copy (store-once).
            var yReshaped = GetOrAllocateWorkspace(ref _reshapedOutputWorkspace, _reshapedShapeCache, clearBeforeReuse: false);
            ReshapeAttentionOutputInPlace(y, yReshaped, B, T);

            // Final projection and dropout
            var output = _proj.Forward(yReshaped);
            output = _projDropout.Forward(output);

            return output;
        }

        private Tensor ExtractAndReshapeQKV(Tensor qkv, int index, int B, int T)
        {
            // Extract Q, K, or V from concatenated QKV
            // qkv: (B, T, 3 * n_embd)
            // Extract one third and reshape to (B, nHead, T, headSize)

            var extracted = new Tensor(new int[] { B, _nHead, T, _headSize }, requiresGrad: true);

            // Optimized version: process by head chunks to improve cache locality
            // Each head processes contiguous memory blocks
            int embdOffset = index * _nEmbd;  // Offset for Q (0), K (_nEmbd), or V (2*_nEmbd)

            // Parallelize over batch when B >= 4
            TransformerHelpers.ParallelOrSequential(B, b =>
            {
                int batchInOffset = b * T * 3 * _nEmbd;
                int batchOutOffset = b * _nHead * T * _headSize;

                for (int h = 0; h < _nHead; h++)
                {
                    int headInOffset = embdOffset + h * _headSize;
                    int headOutOffset = batchOutOffset + h * T * _headSize;

                    for (int t = 0; t < T; t++)
                    {
                        int srcIdx = batchInOffset + t * 3 * _nEmbd + headInOffset;
                        int dstIdx = headOutOffset + t * _headSize;

                        // Copy entire head dimension at once using Array.Copy (faster than element-by-element)
                        Array.Copy(qkv.Data, srcIdx, extracted.Data, dstIdx, _headSize);
                    }
                }
            });

            return extracted;
        }

        /// <summary>
        /// In-place version of ExtractAndReshapeQKV that writes to a pre-allocated tensor.
        /// Avoids allocation overhead for repeated forward passes.
        /// </summary>
        private void ExtractAndReshapeQKVInPlace(Tensor qkv, Tensor dest, int index, int B, int T)
        {
            // Extract Q, K, or V from concatenated QKV into dest
            // qkv: (B, T, 3 * n_embd)
            // dest: (B, nHead, T, headSize) - pre-allocated

            int embdOffset = index * _nEmbd;  // Offset for Q (0), K (_nEmbd), or V (2*_nEmbd)

            // Parallelize over batch when B >= 4
            TransformerHelpers.ParallelOrSequential(B, b =>
            {
                int batchInOffset = b * T * 3 * _nEmbd;
                int batchOutOffset = b * _nHead * T * _headSize;

                for (int h = 0; h < _nHead; h++)
                {
                    int headInOffset = embdOffset + h * _headSize;
                    int headOutOffset = batchOutOffset + h * T * _headSize;

                    for (int t = 0; t < T; t++)
                    {
                        int srcIdx = batchInOffset + t * 3 * _nEmbd + headInOffset;
                        int dstIdx = headOutOffset + t * _headSize;

                        Array.Copy(qkv.Data, srcIdx, dest.Data, dstIdx, _headSize);
                    }
                }
            });
        }

        /// <summary>
        /// Extract Q from concatenated QKV output (supports GQA).
        /// QKV layout: [Q(nEmbd), K(kvDim), V(kvDim)]
        /// TIER-3 OPTIMIZATION: Restructured for cache locality + Buffer.BlockCopy
        /// </summary>
        private void ExtractAndReshapeQInPlace(Tensor qkv, Tensor q, int B, int T)
        {
            // Q is first nEmbd elements
            // dest Q: (B, nHead, T, headSize)

            int qkvDim = _nEmbd + 2 * _nKvHead * _headSize;

            TransformerHelpers.ParallelOrSequential(B, b =>
            {
                int batchInOffset = b * T * qkvDim;
                int batchOutOffset = b * _nHead * T * _headSize;

                // TIER-3: Restructure loops - timestep t outermost for sequential reads
                for (int t = 0; t < T; t++)
                {
                    int srcBase = batchInOffset + t * qkvDim;

                    // Copy all Q heads for this timestep
                    for (int h = 0; h < _nHead; h++)
                    {
                        int srcIdx = srcBase + h * _headSize;
                        int dstIdx = batchOutOffset + h * T * _headSize + t * _headSize;

                        // TIER-3: Use Buffer.BlockCopy (faster than Array.Copy for bulk floats)
                        Buffer.BlockCopy(qkv.Data, srcIdx * 4, q.Data, dstIdx * 4, _headSize * 4);
                    }
                }
            });
        }

        /// <summary>
        /// Extract K and V from concatenated QKV output (supports GQA).
        /// QKV layout: [Q(nEmbd), K(kvDim), V(kvDim)]
        /// TIER-3 OPTIMIZATION: Restructured for cache locality + Buffer.BlockCopy
        /// </summary>
        private void ExtractAndReshapeKVInPlace(Tensor qkv, Tensor k, Tensor v, int B, int T)
        {
            // K starts after Q (offset = nEmbd)
            // V starts after K (offset = nEmbd + kvDim)
            // dest K, V: (B, nKvHead, T, headSize)

            int kvDim = _nKvHead * _headSize;
            int qkvDim = _nEmbd + 2 * kvDim;

            TransformerHelpers.ParallelOrSequential(B, b =>
            {
                int batchInOffset = b * T * qkvDim;
                int batchOutOffset = b * _nKvHead * T * _headSize;

                // TIER-3: Restructure loops - timestep t outermost for sequential reads
                for (int t = 0; t < T; t++)
                {
                    int srcBase = batchInOffset + t * qkvDim;

                    // Copy all K and V heads for this timestep
                    for (int h = 0; h < _nKvHead; h++)
                    {
                        int kSrcIdx = srcBase + _nEmbd + h * _headSize;
                        int vSrcIdx = srcBase + _nEmbd + kvDim + h * _headSize;
                        int dstIdx = batchOutOffset + h * T * _headSize + t * _headSize;

                        // TIER-3: Use Buffer.BlockCopy (faster than Array.Copy for bulk floats)
                        Buffer.BlockCopy(qkv.Data, kSrcIdx * 4, k.Data, dstIdx * 4, _headSize * 4);
                        Buffer.BlockCopy(qkv.Data, vSrcIdx * 4, v.Data, dstIdx * 4, _headSize * 4);
                    }
                }
            });
        }

        private Tensor ComputeAttentionScores(Tensor q, Tensor k, int B, int T)
        {
            // q, k: (B, nHead, T, headSize)
            // output: (B, nHead, T, T)

            // Use pooled tensor to reduce allocation pressure in inference hot path
            var scores = _isTraining
                ? new Tensor(new int[] { B, _nHead, T, T }, requiresGrad: true)
                : Tensor.CreatePooled(new int[] { B, _nHead, T, T }, requiresGrad: false);
            float scale = 1.0f / MathF.Sqrt(_headSize);

            // Parallelize over batch and head dimensions for better performance
            // Use parallel processing when B * nHead >= 4
            int totalParallelWork = B * _nHead;
            TransformerHelpers.ParallelOrSequential(totalParallelWork, bh =>
            {
                int b = bh / _nHead;
                int h = bh % _nHead;
                int bhOffset = (b * _nHead + h) * T * _headSize;
                int scoreOffset = (b * _nHead + h) * T * T;

                for (int i = 0; i < T; i++)
                {
                    int qOffset = bhOffset + i * _headSize;
                    int scoreRowOffset = scoreOffset + i * T;

                    // Only compute for valid positions (causal mask: j <= i)
                    for (int j = 0; j <= i; j++)
                    {
                        int kOffset = bhOffset + j * _headSize;

                        // Use SIMD-accelerated dot product from MatMulOps
                        float sum = MatMulOps.DotProduct(
                            new ReadOnlySpan<float>(q.Data, qOffset, _headSize),
                            new ReadOnlySpan<float>(k.Data, kOffset, _headSize)
                        );

                        scores.Data[scoreRowOffset + j] = sum * scale;
                    }

                    // Positions j > i are already zero (tensor initialized to zeros)
                    // Set them to NegativeInfinity for softmax to ignore
                    for (int j = i + 1; j < T; j++)
                    {
                        scores.Data[scoreRowOffset + j] = float.NegativeInfinity;
                    }
                }
            });

            // Apply softmax over last dimension
            return ApplySoftmax(scores, B, T);
        }

        private Tensor ApplySoftmax(Tensor scores, int B, int T)
        {
            // Use pooled tensor for inference to reduce allocation pressure
            var result = _isTraining
                ? new Tensor(scores.Shape, requiresGrad: true)
                : Tensor.CreatePooled(scores.Shape, requiresGrad: false);

            // Parallelize softmax computation over batch and head dimensions
            int totalParallelWork = B * _nHead;
            TransformerHelpers.ParallelOrSequential(totalParallelWork, bh =>
            {
                int b = bh / _nHead;
                int h = bh % _nHead;

                for (int i = 0; i < T; i++)
                {
                    int offset = ((b * _nHead + h) * T + i) * T;

                    // Find max for numerical stability (only over valid positions: j <= i for causal mask)
                    float max = float.NegativeInfinity;
                    for (int j = 0; j <= i; j++)
                    {
                        if (scores.Data[offset + j] > max)
                            max = scores.Data[offset + j];
                    }

                    // Exp and sum - branchless for valid positions
                    float sum = 0;
                    for (int j = 0; j <= i; j++)
                    {
                        float exp = MathF.Exp(scores.Data[offset + j] - max);
                        result.Data[offset + j] = exp;
                        sum += exp;
                    }

                    // Clear masked positions (i+1 to T-1) - already zero from tensor init

                    // Normalize only valid positions
                    if (sum > 0)
                    {
                        float invSum = 1.0f / sum;
                        for (int j = 0; j <= i; j++)
                        {
                            result.Data[offset + j] *= invSum;
                        }
                    }
                }
            });

            return _attnDropout.Forward(result);
        }

        private Tensor ApplyAttention(Tensor att, Tensor v, int B, int T)
        {
            // att: (B, nHead, T, T)
            // v: (B, nHead, T, headSize)
            // output: (B, nHead, T, headSize)

            // Use pooled tensor for inference to reduce allocation pressure
            var output = _isTraining
                ? new Tensor(new int[] { B, _nHead, T, _headSize }, requiresGrad: true)
                : Tensor.CreatePooled(new int[] { B, _nHead, T, _headSize }, requiresGrad: false);

            // Parallelize attention application over batch and head dimensions
            int totalParallelWork = B * _nHead;
            TransformerHelpers.ParallelOrSequential(totalParallelWork, bh =>
            {
                int b = bh / _nHead;
                int h = bh % _nHead;

                for (int i = 0; i < T; i++)
                {
                    for (int d = 0; d < _headSize; d++)
                    {
                        float sum = 0;
                        for (int j = 0; j < T; j++)
                        {
                            int attIdx = ((b * _nHead + h) * T + i) * T + j;
                            int vIdx = ((b * _nHead + h) * T + j) * _headSize + d;
                            sum += att.Data[attIdx] * v.Data[vIdx];
                        }
                        int outIdx = ((b * _nHead + h) * T + i) * _headSize + d;
                        output.Data[outIdx] = sum;
                    }
                }
            });

            return output;
        }

        private Tensor ReshapeAttentionOutput(Tensor y, int B, int T)
        {
            // y: (B, nHead, T, headSize) -> (B, T, n_embd)
            // Use pooled tensor for inference to reduce allocation pressure
            var output = _isTraining
                ? new Tensor(new int[] { B, T, _nEmbd }, requiresGrad: true)
                : Tensor.CreatePooled(new int[] { B, T, _nEmbd }, requiresGrad: false);

            // Optimized version: process by head chunks with Array.Copy
            for (int b = 0; b < B; b++)
            {
                int batchInOffset = b * _nHead * T * _headSize;
                int batchOutOffset = b * T * _nEmbd;

                for (int t = 0; t < T; t++)
                {
                    int timeOutOffset = batchOutOffset + t * _nEmbd;

                    for (int h = 0; h < _nHead; h++)
                    {
                        int srcIdx = batchInOffset + h * T * _headSize + t * _headSize;
                        int dstIdx = timeOutOffset + h * _headSize;

                        // Copy entire head dimension at once
                        Array.Copy(y.Data, srcIdx, output.Data, dstIdx, _headSize);
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// In-place version of ReshapeAttentionOutput that writes to a pre-allocated tensor.
        /// Reshapes from (B, nHead, T, headSize) to (B, T, n_embd).
        /// </summary>
        private void ReshapeAttentionOutputInPlace(Tensor y, Tensor output, int B, int T)
        {
            // y: (B, nHead, T, headSize) -> output: (B, T, n_embd)

            // Optimized version: process by head chunks with Array.Copy
            for (int b = 0; b < B; b++)
            {
                int batchInOffset = b * _nHead * T * _headSize;
                int batchOutOffset = b * T * _nEmbd;

                for (int t = 0; t < T; t++)
                {
                    int timeOutOffset = batchOutOffset + t * _nEmbd;

                    for (int h = 0; h < _nHead; h++)
                    {
                        int srcIdx = batchInOffset + h * T * _headSize + t * _headSize;
                        int dstIdx = timeOutOffset + h * _headSize;

                        // Copy entire head dimension at once
                        Array.Copy(y.Data, srcIdx, output.Data, dstIdx, _headSize);
                    }
                }
            }
        }

        /// <summary>
        /// In-place version of ComputeAttentionScores that writes to a pre-allocated tensor.
        /// Computes Q * K^T / sqrt(d_k) with causal masking and softmax.
        /// Uses fused scale+mask+softmax operation for better performance.
        /// Supports KV-cache where K may have more positions than Q.
        /// Supports GQA where query heads are mapped to fewer KV heads.
        /// </summary>
        private void ComputeAttentionScoresInPlace(Tensor q, Tensor k, Tensor scores, int B, int T, int kSeqLen)
        {
            // q: (B, nHead, T, headSize) - query for current tokens
            // k: (B, nKvHead, kSeqLen, headSize) - keys (may include cached past, GQA)
            // scores: (B, nHead, T, kSeqLen) - pre-allocated, will be modified in-place

            float scale = 1.0f / MathF.Sqrt(_headSize);
            int headsPerKvHead = _nHead / _nKvHead;  // For GQA head mapping

            int totalParallelWork = B * _nHead;
            if (totalParallelWork >= 4)
            {
                Parallel.For(0, totalParallelWork, bh =>
                {
                    int b = bh / _nHead;
                    int h = bh % _nHead;

                    // Map query head to KV head (for GQA)
                    int kvHead = h / headsPerKvHead;

                    int qOffset = (b * _nHead + h) * T * _headSize;
                    int kOffset = (b * _nKvHead + kvHead) * kSeqLen * _headSize;
                    int scoreOffset = (b * _nHead + h) * T * kSeqLen;

                    // Step 1: Batched matrix multiplication Q @ K^T
                    ReadOnlySpan<float> qMatrix = new ReadOnlySpan<float>(q.Data, qOffset, T * _headSize);
                    ReadOnlySpan<float> kMatrix = new ReadOnlySpan<float>(k.Data, kOffset, kSeqLen * _headSize);
                    Span<float> scoresMatrix = new Span<float>(scores.Data, scoreOffset, T * kSeqLen);

                    // Compute Q @ K^T using optimized batched MatMul
                    MatMulOps.MatMulTransposeB(qMatrix, kMatrix, scoresMatrix, T, _headSize, kSeqLen);

                    // Step 2: Apply fused scale+mask+softmax
                    // For KV-cache, the causal mask needs to account for cache offset
                    int cacheOffset = kSeqLen - T;
                    OptimizedOps.FusedScaleMaskSoftmax(scores.Data, scoreOffset, scale, scores.Data, scoreOffset, T, kSeqLen, cacheOffset);
                });
            }
            else
            {
                for (int b = 0; b < B; b++)
                {
                    for (int h = 0; h < _nHead; h++)
                    {
                        // Map query head to KV head (for GQA)
                        int kvHead = h / headsPerKvHead;

                        int qOffset = (b * _nHead + h) * T * _headSize;
                        int kOffset = (b * _nKvHead + kvHead) * kSeqLen * _headSize;
                        int scoreOffset = (b * _nHead + h) * T * kSeqLen;

                        // Step 1: Batched matrix multiplication Q @ K^T
                        ReadOnlySpan<float> qMatrix = new ReadOnlySpan<float>(q.Data, qOffset, T * _headSize);
                        ReadOnlySpan<float> kMatrix = new ReadOnlySpan<float>(k.Data, kOffset, kSeqLen * _headSize);
                        Span<float> scoresMatrix = new Span<float>(scores.Data, scoreOffset, T * kSeqLen);

                        MatMulOps.MatMulTransposeB(qMatrix, kMatrix, scoresMatrix, T, _headSize, kSeqLen);

                        // Step 2: Apply fused scale+mask+softmax for this (batch, head)
                        int cacheOffset = kSeqLen - T;
                        OptimizedOps.FusedScaleMaskSoftmax(scores.Data, scoreOffset, scale, scores.Data, scoreOffset, T, kSeqLen, cacheOffset);
                    }
                }
            }

            // Apply dropout if in training mode
            // Note: Since we're using in-place operations, we modify scores directly
            // The dropout is applied to the tensor passed in, which is the workspace
        }

        // Overload for backward compatibility (no KV-cache)
        private void ComputeAttentionScoresInPlace(Tensor q, Tensor k, Tensor scores, int B, int T)
        {
            ComputeAttentionScoresInPlace(q, k, scores, B, T, T);
        }

        /// <summary>
        /// Apply softmax in-place to the scores tensor.
        /// </summary>
        private void ApplySoftmaxInPlace(Tensor scores, int B, int T)
        {
            int totalParallelWork = B * _nHead;
            TransformerHelpers.ParallelOrSequential(totalParallelWork, bh =>
            {
                int b = bh / _nHead;
                int h = bh % _nHead;

                for (int i = 0; i < T; i++)
                {
                    int offset = ((b * _nHead + h) * T + i) * T;

                    // Find max for numerical stability
                    float max = float.NegativeInfinity;
                    for (int j = 0; j <= i; j++)
                    {
                        if (scores.Data[offset + j] > max)
                            max = scores.Data[offset + j];
                    }

                    // Exp and sum
                    float sum = 0;
                    for (int j = 0; j <= i; j++)
                    {
                        float exp = MathF.Exp(scores.Data[offset + j] - max);
                        scores.Data[offset + j] = exp;
                        sum += exp;
                    }

                    // Normalize
                    if (sum > 0)
                    {
                        float invSum = 1.0f / sum;
                        for (int j = 0; j <= i; j++)
                        {
                            scores.Data[offset + j] *= invSum;
                        }
                    }

                    // Clear masked positions
                    for (int j = i + 1; j < T; j++)
                    {
                        scores.Data[offset + j] = 0;
                    }
                }
            });
        }

        /// <summary>
        /// In-place version of ApplyAttention that writes to a pre-allocated tensor.
        /// Computes attention_weights * V using optimized MatMul kernel.
        /// Supports KV-cache where V may have more positions than output.
        /// Supports GQA where query heads are mapped to fewer KV heads.
        /// Tier-0 optimization: Replaced triple nested loops with MatMul kernel for better performance.
        /// </summary>
        private void ApplyAttentionInPlace(Tensor att, Tensor v, Tensor output, int B, int T, int vSeqLen)
        {
            // att: (B, nHead, T, vSeqLen) - attention weights
            // v: (B, nKvHead, vSeqLen, headSize) - values (may include cached past, GQA)
            // output: (B, nHead, T, headSize) - pre-allocated

            // For each batch and head, perform: output[b,h] = att[b,h] @ v[b,kvh]
            // where att[b,h] is (T × vSeqLen) and v[b,kvh] is (vSeqLen × headSize)
            // resulting in output[b,h] as (T × headSize)

            int headsPerKvHead = _nHead / _nKvHead;  // For GQA head mapping
            int totalParallelWork = B * _nHead;
            TransformerHelpers.ParallelOrSequential(totalParallelWork, bh =>
            {
                int b = bh / _nHead;
                int h = bh % _nHead;

                // Map query head to KV head (for GQA)
                int kvHead = h / headsPerKvHead;

                // Calculate offsets for this batch and head
                int attOffset = (b * _nHead + h) * T * vSeqLen;
                int vOffset = (b * _nKvHead + kvHead) * vSeqLen * _headSize;
                int outOffset = (b * _nHead + h) * T * _headSize;

                // Use MatMul: att[b,h] @ v[b,kvh] -> output[b,h]
                // att: (T × vSeqLen), v: (vSeqLen × headSize), output: (T × headSize)
                SmallMind.Core.Simd.MatMulOps.MatMul(
                    att.Data.AsSpan(attOffset, T * vSeqLen),
                    v.Data.AsSpan(vOffset, vSeqLen * _headSize),
                    output.Data.AsSpan(outOffset, T * _headSize),
                    T, vSeqLen, _headSize
                );
            });
        }

        // Overload for backward compatibility (no KV-cache)
        private void ApplyAttentionInPlace(Tensor att, Tensor v, Tensor output, int B, int T)
        {
            ApplyAttentionInPlace(att, v, output, B, T, T);
        }

        public void Train()
        {
            _isTraining = true;
            _qkv.Train();
            _proj.Train();
            _attnDropout.Train();
            _projDropout.Train();
        }

        public void Eval()
        {
            _isTraining = false;
            _qkv.Eval();
            _proj.Eval();
            _attnDropout.Eval();
            _projDropout.Eval();
        }
    }

    /// <summary>
    /// Feed-forward MLP with GELU activation.
    /// </summary>
}

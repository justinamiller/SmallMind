using System.Runtime.CompilerServices;
using System.Numerics;
using SmallMind.Abstractions.Telemetry;
using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;
using SmallMind.Core.Validation;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Decoder-only Transformer model (GPT-style) implemented in pure C#.
    /// Uses custom Tensor and neural network layers.
    /// </summary>
    internal sealed class TransformerModel
    {
        private readonly int _blockSize;
        private readonly int _vocabSize;
        private readonly int _nEmbd;
        private readonly int _nLayer;
        private readonly int _nHead;
        private readonly double _dropout;
        private readonly Random _random;
        private readonly IRuntimeLogger _logger;

        /// <summary>
        /// Whether the model is in training mode.
        /// </summary>
        private bool _isTraining = true;

        // Embedding layers
        private readonly Embedding _tokenEmbedding;
        private readonly Embedding? _positionEmbedding;  // Nullable for RoPE models
        private readonly Dropout _embDropout;

        // Transformer blocks
        private readonly List<TransformerBlock> _blocks;

        // Final layer norm and linear head
        private readonly Module _lnFinal;  // LayerNorm or RMSNorm
        private readonly Linear _lmHead;

        // RoPE configuration
        private readonly bool _useRope;

        // Cached position indices to avoid recreating each forward pass
        private readonly Dictionary<int, Tensor> _positionIndicesCache;

        // Workspace for reusing intermediate tensors during forward pass
        private readonly TensorWorkspace _workspace;

        public List<Tensor> Parameters { get; private set; }

        /// <summary>
        /// Vocabulary size (number of unique tokens).
        /// </summary>
        public int VocabSize => _vocabSize;

        /// <summary>
        /// Maximum sequence length (context window).
        /// </summary>
        public int BlockSize => _blockSize;

        /// <summary>
        /// Embedding dimension.
        /// </summary>
        public int EmbedDim => _nEmbd;

        /// <summary>
        /// Number of attention heads per layer.
        /// </summary>
        public int NumHeads => _nHead;

        /// <summary>
        /// Number of transformer layers.
        /// </summary>
        public int NumLayers => _nLayer;

        public TransformerModel(int vocabSize, int blockSize, int nEmbd, int nLayer, int nHead, double dropout, int seed = 42, IRuntimeLogger? logger = null)
        {
            Guard.GreaterThan(vocabSize, 0);
            Guard.GreaterThan(blockSize, 0);
            Guard.GreaterThan(nEmbd, 0);
            Guard.GreaterThan(nLayer, 0);
            Guard.GreaterThan(nHead, 0);
            Guard.InRange(dropout, 0.0, 1.0);

            if (nEmbd % nHead != 0)
            {
                throw new ValidationException(
                    $"Embedding dimension {nEmbd} must be divisible by number of heads {nHead}",
                    nameof(nEmbd));
            }

            _logger = logger ?? NullRuntimeLogger.Instance;
            _vocabSize = vocabSize;
            _blockSize = blockSize;
            _nEmbd = nEmbd;
            _nLayer = nLayer;
            _nHead = nHead;
            _dropout = dropout;
            _random = new Random(seed);
            _useRope = false;  // GPT-2 uses learned positional embeddings

            // Initialize position indices cache
            _positionIndicesCache = new Dictionary<int, Tensor>();

            // Initialize tensor workspace for reusing intermediate tensors
            _workspace = new TensorWorkspace();

            // Token and position embeddings
            _tokenEmbedding = new Embedding(_vocabSize, _nEmbd, _random);
            _positionEmbedding = new Embedding(_blockSize, _nEmbd, _random);
            _embDropout = new Dropout((float)dropout, _random);

            // Stack of transformer blocks - pre-size to layer count
            _blocks = new List<TransformerBlock>(_nLayer);
            for (int i = 0; i < _nLayer; i++)
            {
                _blocks.Add(new TransformerBlock(_nEmbd, _nHead, _blockSize, (float)dropout, _random));
            }

            // Final layer norm and language model head
            _lnFinal = new LayerNorm(_nEmbd);
            _lmHead = new Linear(_nEmbd, _vocabSize, useBias: false, _random);

            // Collect all parameters
            Parameters = new List<Tensor>();
            Parameters.AddRange(_tokenEmbedding.Parameters);
            Parameters.AddRange(_positionEmbedding.Parameters);
            for (int i = 0; i < _blocks.Count; i++)
            {
                Parameters.AddRange(_blocks[i].Parameters);
            }
            Parameters.AddRange(_lnFinal.Parameters);
            Parameters.AddRange(_lmHead.Parameters);

            long totalParams = GetTotalParameterCount();
            _logger.Info($"TransformerModel initialized: vocab={_vocabSize}, block_size={_blockSize}, " +
                            $"n_embd={_nEmbd}, n_layer={_nLayer}, n_head={_nHead}, dropout={_dropout}");
            _logger.Info($"Total parameters: {totalParams:N0} ({Parameters.Count} tensors)");

            // Warn if approaching billion-parameter scale
            if (totalParams > 500_000_000)
            {
                _logger.Warn($"Large model detected ({totalParams / 1_000_000}M parameters). " +
                                "Consider using quantization (Q8/Q4) for memory efficiency.");
            }
        }

        /// <summary>
        /// ModelConfig-based constructor for Llama/Mistral/Phi architectures.
        /// Supports RoPE, RMSNorm, GQA, and SwiGLU based on config.
        /// </summary>
        public TransformerModel(ModelConfig config, int seed = 42, IRuntimeLogger? logger = null)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            Guard.GreaterThan(config.VocabSize, 0);
            Guard.GreaterThan(config.ContextLength, 0);
            Guard.GreaterThan(config.EmbeddingLength, 0);
            Guard.GreaterThan(config.BlockCount, 0);
            Guard.GreaterThan(config.HeadCount, 0);
            Guard.InRange(config.Dropout, 0.0, 1.0);

            _logger = logger ?? NullRuntimeLogger.Instance;
            _vocabSize = config.VocabSize;
            _blockSize = config.ContextLength;
            _nEmbd = config.EmbeddingLength;
            _nLayer = config.BlockCount;
            _nHead = config.HeadCount;
            _dropout = config.Dropout;
            _random = new Random(seed);
            _useRope = config.UseRope;

            // Initialize position indices cache
            _positionIndicesCache = new Dictionary<int, Tensor>();

            // Initialize tensor workspace for reusing intermediate tensors
            _workspace = new TensorWorkspace();

            // Token embeddings (always needed)
            _tokenEmbedding = new Embedding(_vocabSize, _nEmbd, _random);
            _embDropout = new Dropout((float)_dropout, _random);

            // Position embeddings (only for non-RoPE models)
            if (!_useRope)
            {
                _positionEmbedding = new Embedding(_blockSize, _nEmbd, _random);
            }
            else
            {
                _positionEmbedding = null;  // RoPE models don't use learned positional embeddings
            }

            // Stack of transformer blocks
            _blocks = new List<TransformerBlock>(_nLayer);
            for (int i = 0; i < _nLayer; i++)
            {
                _blocks.Add(new TransformerBlock(config, _random));
            }

            // Final layer norm
            if (config.UseRmsNorm)
            {
                _lnFinal = new RMSNorm(_nEmbd, (float)config.NormEps);
            }
            else
            {
                _lnFinal = new LayerNorm(_nEmbd);
            }

            // Language model head
            _lmHead = new Linear(_nEmbd, _vocabSize, useBias: config.UseBias, _random);

            // Collect all parameters
            Parameters = new List<Tensor>();
            Parameters.AddRange(_tokenEmbedding.Parameters);
            if (_positionEmbedding != null)
            {
                Parameters.AddRange(_positionEmbedding.Parameters);
            }
            for (int i = 0; i < _blocks.Count; i++)
            {
                Parameters.AddRange(_blocks[i].Parameters);
            }
            Parameters.AddRange(_lnFinal.Parameters);
            Parameters.AddRange(_lmHead.Parameters);

            long totalParams = GetTotalParameterCount();
            _logger.Info($"TransformerModel initialized from ModelConfig:");
            _logger.Info($"  Architecture: {config.Architecture}");
            _logger.Info($"  Vocab: {_vocabSize}, Context: {_blockSize}, Embedding: {_nEmbd}");
            _logger.Info($"  Layers: {_nLayer}, Heads: {_nHead} (KV heads: {config.HeadCountKv})");
            _logger.Info($"  Features: RoPE={_useRope}, RMSNorm={config.UseRmsNorm}, SwiGLU={config.UseSwiGlu}");
            _logger.Info($"  Total parameters: {totalParams:N0} ({Parameters.Count} tensors)");

            if (totalParams > 500_000_000)
            {
                _logger.Warn($"Large model detected ({totalParams / 1_000_000}M parameters).");
            }
        }

        public Tensor Forward(Tensor idx)
        {
            return Forward(idx, positionOffset: 0);
        }

        /// <summary>
        /// Forward pass with optional position offset for incremental decoding.
        /// Tier-0 optimization: Support for prefill + incremental decode with correct absolute positions.
        /// </summary>
        /// <param name="idx">Token indices (batch_size, sequence_length)</param>
        /// <param name="positionOffset">Starting position for position embeddings (0 for prefill, currentSeqLen for decode)</param>
        public Tensor Forward(Tensor idx, int positionOffset)
        {
            Guard.NotNull(idx);

            // idx shape: (batch_size, sequence_length)
            int B = idx.Shape[0];
            int T = idx.Shape[1];

            if (T + positionOffset > _blockSize)
            {
                throw new ArgumentException($"Sequence length {T} + offset {positionOffset} exceeds block size {_blockSize}");
            }

            // Token embeddings: (B, T) -> (B, T, n_embd)
            // Reuse workspace tensor for token embeddings using stackalloc
            Span<int> embedShape = stackalloc int[3] { B, T, _nEmbd };
            var tokEmbDest = _workspace.GetOrCreate("tokEmb", embedShape, _isTraining);
            var tokEmb = _tokenEmbedding.Forward(idx, tokEmbDest);

            Tensor x;

            if (_useRope)
            {
                // RoPE models: no learned positional embeddings
                // Position information is added via RoPE in attention layers
                x = _embDropout.Forward(tokEmb);
            }
            else
            {
                // GPT-2 style: add learned positional embeddings
                // Position embeddings: get cached position indices or create new
                // Tier-0 optimization: Use positionOffset to support incremental decode
                int cacheKey = T * 100000 + positionOffset; // Unique key combining T and offset
                if (!_positionIndicesCache.TryGetValue(cacheKey, out var posIndices))
                {
                    // Position indices are cached, so one-time allocation is acceptable
                    posIndices = new Tensor(new float[T], new int[] { T });
                    for (int i = 0; i < T; i++)
                    {
                        posIndices.Data[i] = positionOffset + i; // Apply offset for absolute position
                    }
                    _positionIndicesCache[cacheKey] = posIndices;
                }

                // Reuse workspace tensor for position embeddings
                Span<int> posEmbShape = stackalloc int[2] { T, _nEmbd };
                var posEmbDest = _workspace.GetOrCreate("posEmb", posEmbShape, _isTraining);
                var posEmb = _positionEmbedding!.Forward(posIndices, posEmbDest);

                // Add token and position embeddings: (B, T, n_embd)
                // Reuse workspace for the result (reuse embedShape)
                var addEmbDest = _workspace.GetOrCreate("addEmb", embedShape, _isTraining);
                x = AddPositionEmbeddings(tokEmb, posEmb, addEmbDest, B, T, _nEmbd);
                x = _embDropout.Forward(x);
            }

            // Pass through transformer blocks
            for (int i = 0; i < _blocks.Count; i++)
            {
                x = _blocks[i].Forward(x);
            }

            // Final layer norm: (B, T, n_embd)
            var lnFinalOut = _workspace.GetOrCreate("lnFinalOut", x.Shape, _isTraining);
            ForwardNorm(_lnFinal, x, lnFinalOut);

            // Language model head: (B, T, n_embd) -> (B, T, vocab_size)
            var logits = _lmHead.Forward(lnFinalOut);

            return logits;
        }

        /// <summary>
        /// Helper to forward through norm layer with destination tensor.
        /// Supports both LayerNorm and RMSNorm which have dest-overload methods.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ForwardNorm(Module norm, Tensor input, Tensor dest)
            => TransformerHelpers.ForwardNorm(norm, input, dest);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Tensor AddPositionEmbeddings(Tensor tokEmb, Tensor posEmb, Tensor dest, int B, int T, int nEmbd)
        {
            int vectorSize = Vector<float>.Count;

            // posEmb is (T, nEmbd), need to broadcast to (B, T, nEmbd)
            // Optimization: Pre-calculate offsets to reduce redundant calculations
            for (int b = 0; b < B; b++)
            {
                for (int t = 0; t < T; t++)
                {
                    int resultOffset = (b * T + t) * nEmbd;
                    int tokEmbOffset = (b * T + t) * nEmbd;
                    int posEmbOffset = t * nEmbd;

                    // SIMD-accelerated addition
                    int e = 0;
                    for (; e <= nEmbd - vectorSize; e += vectorSize)
                    {
                        var vTok = new Vector<float>(tokEmb.Data.AsSpan(tokEmbOffset + e));
                        var vPos = new Vector<float>(posEmb.Data.AsSpan(posEmbOffset + e));
                        (vTok + vPos).CopyTo(dest.Data.AsSpan(resultOffset + e));
                    }

                    // Scalar remainder
                    for (; e < nEmbd; e++)
                    {
                        dest.Data[resultOffset + e] =
                            tokEmb.Data[tokEmbOffset + e] + posEmb.Data[posEmbOffset + e];
                    }
                }
            }

            // Backward
            if (tokEmb.RequiresGrad || posEmb.RequiresGrad)
            {
                dest.SetBackward(() =>
                {
                    if (tokEmb.RequiresGrad)
                    {
                        // Use unsafe pointers for gradient accumulation
                        unsafe
                        {
                            fixed (float* pTokGrad = tokEmb.Grad, pDestGrad = dest.Grad)
                            {
                                for (int i = 0; i < dest.Size; i++)
                                {
                                    pTokGrad[i] += pDestGrad[i];
                                }
                            }
                        }
                    }
                    if (posEmb.RequiresGrad)
                    {
                        // Use unsafe pointers for triple-nested loop
                        unsafe
                        {
                            fixed (float* pPosGrad = posEmb.Grad, pDestGrad = dest.Grad)
                            {
                                for (int b = 0; b < B; b++)
                                {
                                    int bOffset = b * T * nEmbd;
                                    for (int t = 0; t < T; t++)
                                    {
                                        int posIdx = t * nEmbd;
                                        int destIdx = bOffset + t * nEmbd;

                                        for (int e = 0; e < nEmbd; e++)
                                        {
                                            pPosGrad[posIdx + e] += pDestGrad[destIdx + e];
                                        }
                                    }
                                }
                            }
                        }
                    }
                });
            }

            return dest;
        }

        public void Train()
        {
            _isTraining = true;
            _tokenEmbedding.Train();
            _positionEmbedding?.Train();
            _embDropout.Train();
            _lnFinal.Train();
            _lmHead.Train();
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].Train();
            }
        }

        public void Eval()
        {
            _isTraining = false;
            _tokenEmbedding.Eval();
            _positionEmbedding?.Eval();
            _embDropout.Eval();
            _lnFinal.Eval();
            _lmHead.Eval();
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].Eval();
            }
        }

        /// <summary>
        /// Enable KV-cache for all transformer blocks.
        /// Call before inference to enable efficient autoregressive generation.
        /// </summary>
        public void EnableKVCache()
        {
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].EnableKVCache();
            }
        }

        /// <summary>
        /// Disable KV-cache for all transformer blocks.
        /// Call after inference to free cache memory.
        /// </summary>
        public void DisableKVCache()
        {
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].DisableKVCache();
            }
        }

        /// <summary>
        /// Reset KV-cache position for all transformer blocks.
        /// Call before starting a new sequence to reset cache state.
        /// </summary>
        public void ResetKVCache()
        {
            for (int i = 0; i < _blocks.Count; i++)
            {
                _blocks[i].ResetKVCache();
            }
        }

        /// <summary>
        /// Calculate total number of parameters in the model.
        /// Uses long to support billion-parameter models.
        /// </summary>
        /// <returns>Total parameter count.</returns>
        public long GetTotalParameterCount()
        {
            long total = 0;
            foreach (var param in Parameters)
            {
                total += param.Size;
            }
            return total;
        }

        /// <summary>
        /// Get memory footprint estimate in bytes.
        /// </summary>
        /// <param name="includingGradients">Include gradient memory.</param>
        /// <returns>Estimated bytes.</returns>
        public long GetMemoryFootprintBytes(bool includingGradients = false)
        {
            long totalParams = GetTotalParameterCount();
            long bytes = totalParams * sizeof(float); // FP32 parameters

            if (includingGradients)
            {
                bytes += totalParams * sizeof(float); // FP32 gradients
            }

            return bytes;
        }

        /// <summary>
        /// Get named parameters for weight loading.
        /// Returns a dictionary mapping GGUF-style canonical names to tensor references.
        /// </summary>
        public Dictionary<string, Tensor> GetNamedParameters()
        {
            var namedParams = new Dictionary<string, Tensor>();

            // Token embeddings
            namedParams["token_embd.weight"] = _tokenEmbedding.Parameters[0];

            // Position embeddings (only for non-RoPE models)
            if (_positionEmbedding != null)
            {
                namedParams["pos_embd.weight"] = _positionEmbedding.Parameters[0];
            }

            // Transformer blocks
            for (int i = 0; i < _blocks.Count; i++)
            {
                var blockParams = GetBlockNamedParameters(_blocks[i], i);
                foreach (var kvp in blockParams)
                {
                    namedParams[kvp.Key] = kvp.Value;
                }
            }

            // Final layer norm
            var finalNormParams = GetNormNamedParameters(_lnFinal, "output_norm");
            foreach (var kvp in finalNormParams)
            {
                namedParams[kvp.Key] = kvp.Value;
            }

            // Output head
            namedParams["output.weight"] = _lmHead.Weight;
            if (_lmHead.Bias != null)
            {
                namedParams["output.bias"] = _lmHead.Bias;
            }

            return namedParams;
        }

        private Dictionary<string, Tensor> GetBlockNamedParameters(TransformerBlock block, int layerIndex)
        {
            var namedParams = new Dictionary<string, Tensor>();
            string prefix = $"blk.{layerIndex}.";

            // Attention norm
            var attnNormParams = GetNormNamedParameters(block._ln1, $"{prefix}attn_norm");
            foreach (var kvp in attnNormParams)
            {
                namedParams[kvp.Key] = kvp.Value;
            }

            // Attention QKV and output projection
            var attnParams = GetAttentionNamedParameters(block._attn, prefix);
            foreach (var kvp in attnParams)
            {
                namedParams[kvp.Key] = kvp.Value;
            }

            // FFN norm
            var ffnNormParams = GetNormNamedParameters(block._ln2, $"{prefix}ffn_norm");
            foreach (var kvp in ffnNormParams)
            {
                namedParams[kvp.Key] = kvp.Value;
            }

            // MLP/FFN
            var mlpParams = GetMlpNamedParameters(block.MlpModule, prefix);
            foreach (var kvp in mlpParams)
            {
                namedParams[kvp.Key] = kvp.Value;
            }

            return namedParams;
        }

        private Dictionary<string, Tensor> GetNormNamedParameters(Module norm, string baseName)
        {
            var namedParams = new Dictionary<string, Tensor>();

            if (norm is LayerNorm layerNorm)
            {
                namedParams[$"{baseName}.weight"] = layerNorm.Gamma;
                namedParams[$"{baseName}.bias"] = layerNorm.Beta;
            }
            else if (norm is RMSNorm rmsNorm)
            {
                namedParams[$"{baseName}.weight"] = rmsNorm.Gamma;
            }

            return namedParams;
        }

        private Dictionary<string, Tensor> GetAttentionNamedParameters(MultiHeadAttention attn, string prefix)
        {
            var namedParams = new Dictionary<string, Tensor>();

            // Combined QKV tensor (SmallMind uses single Linear for QKV)
            // This will be filled by merging separate Q/K/V tensors from GGUF
            namedParams[$"{prefix}attn_qkv.weight"] = attn._qkv.Weight;
            if (attn._qkv.Bias != null)
            {
                namedParams[$"{prefix}attn_qkv.bias"] = attn._qkv.Bias;
            }

            // Output projection
            namedParams[$"{prefix}attn_output.weight"] = attn._proj.Weight;
            if (attn._proj.Bias != null)
            {
                namedParams[$"{prefix}attn_output.bias"] = attn._proj.Bias;
            }

            return namedParams;
        }

        private Dictionary<string, Tensor> GetMlpNamedParameters(object mlp, string prefix)
        {
            var namedParams = new Dictionary<string, Tensor>();

            if (mlp is MLP standardMlp)
            {
                namedParams[$"{prefix}ffn_up.weight"] = standardMlp._fc1.Weight;
                if (standardMlp._fc1.Bias != null)
                {
                    namedParams[$"{prefix}ffn_up.bias"] = standardMlp._fc1.Bias;
                }

                namedParams[$"{prefix}ffn_down.weight"] = standardMlp._fc2.Weight;
                if (standardMlp._fc2.Bias != null)
                {
                    namedParams[$"{prefix}ffn_down.bias"] = standardMlp._fc2.Bias;
                }
            }
            else if (mlp is GatedMLP gatedMlp)
            {
                namedParams[$"{prefix}ffn_gate.weight"] = gatedMlp._gateProj.Weight;
                if (gatedMlp._gateProj.Bias != null)
                {
                    namedParams[$"{prefix}ffn_gate.bias"] = gatedMlp._gateProj.Bias;
                }

                namedParams[$"{prefix}ffn_up.weight"] = gatedMlp._upProj.Weight;
                if (gatedMlp._upProj.Bias != null)
                {
                    namedParams[$"{prefix}ffn_up.bias"] = gatedMlp._upProj.Bias;
                }

                namedParams[$"{prefix}ffn_down.weight"] = gatedMlp._downProj.Weight;
                if (gatedMlp._downProj.Bias != null)
                {
                    namedParams[$"{prefix}ffn_down.bias"] = gatedMlp._downProj.Bias;
                }
            }

            return namedParams;
        }
    }
}

using SmallMind.Abstractions.Telemetry;
using SmallMind.Quantization.IO.Gguf;
using SmallMind.Quantization.Tensors;
using SmallMind.Runtime.Telemetry;
using SmallMind.Tokenizers;
using SmallMind.Tokenizers.Gguf;
using SmallMind.Transformers;
using Tensor = SmallMind.Core.Core.Tensor;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Loads models from GGUF format files with full weight loading.
    /// Extracts ModelConfig and tokenizer from GGUF metadata.
    /// Constructs TransformerModel with appropriate architecture (Llama, Mistral, Phi, GPT-2).
    /// Phase 3 implementation: loads actual weights from GGUF into model.
    /// 
    /// NOTE: This loader dequantizes all weights to FP32 for compatibility with TransformerModel.
    /// For production quantized inference that keeps weights in compressed format and uses
    /// fused kernels, use SmallMind.Quantization.IO.Gguf.GgufImporter instead, which preserves
    /// quantized formats (Q4_K, Q6_K, etc.) and enables memory-efficient inference via IWeightTensor.
    /// </summary>
    internal sealed class GgufModelLoader
    {
        private const int GgufBlockSize = 32; // GGUF uses block size 32 for quantization

        /// <summary>
        /// Load a model from a GGUF file with full weight loading.
        /// Reads metadata, extracts config and tokenizer, builds TransformerModel, and loads weights.
        /// 
        /// NOTE: This method dequantizes all weights to FP32. For memory-efficient quantized inference,
        /// consider using GgufImporter from SmallMind.Quantization which preserves quantized formats.
        /// </summary>
        /// <param name="ggufPath">Path to GGUF file</param>
        /// <param name="seed">Random seed for model initialization</param>
        /// <param name="useMmap">Whether to use memory-mapped file reading for faster loading</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>Tuple of (model, tokenizer, config)</returns>
        public static (TransformerModel model, ITokenizer tokenizer, ModelConfig config) LoadFromGguf(
            string ggufPath,
            int seed = 42,
            bool useMmap = false,
            IInternalRuntimeLogger? logger = null)
        {
            logger ??= NullInternalRuntimeLogger.Instance;

            if (string.IsNullOrEmpty(ggufPath))
                throw new ArgumentNullException(nameof(ggufPath));
            if (!File.Exists(ggufPath))
                throw new FileNotFoundException($"GGUF file not found: {ggufPath}");

            logger.LogInfo($"Loading GGUF model from: {ggufPath}");

            // Read GGUF model info
            GgufModelInfo modelInfo;
            using (var stream = File.OpenRead(ggufPath))
            using (var reader = new GgufReader(stream))
            {
                modelInfo = reader.ReadModelInfo();
            }

            // Extract ModelConfig from metadata
            var config = ModelConfig.FromGgufMetadata(modelInfo.Metadata);
            logger.LogInfo($"Model architecture: {config.Architecture}");
            logger.LogInfo($"Context length: {config.ContextLength}, Embedding: {config.EmbeddingLength}");
            logger.LogInfo($"Layers: {config.BlockCount}, Heads: {config.HeadCount} (KV: {config.HeadCountKv})");
            logger.LogInfo($"RoPE freq base: {config.RopeFreqBase}");
            logger.LogInfo($"Vocab size: {config.VocabSize}");

            // Extract tokenizer from metadata using new GgufTokenizerFactory
            // Convert internal logger to public logger for tokenizer factory
            IRuntimeLogger publicLogger = logger is RuntimeLoggerAdapter adapter
                ? adapter.PublicLogger
                : NullRuntimeLogger.Instance;

            var (tokenizer, diagnostics) = GgufTokenizerFactory.CreateTokenizer(
                modelInfo.Metadata,
                publicLogger);

            if (tokenizer == null)
            {
                throw new NotSupportedException(
                    "Failed to create tokenizer from GGUF file. " +
                    "Ensure the file contains tokenizer metadata (tokenizer.ggml.*). " +
                    $"Diagnostics: {string.Join(", ", diagnostics.Issues.Select(i => i.message))}");
            }

            // Log tokenizer diagnostics
            if (diagnostics.HasIssues)
            {
                foreach (var (reason, message) in diagnostics.Issues)
                {
                    logger.LogWarning($"Tokenizer diagnostic [{reason}]: {message}");
                }
            }

            logger.LogInfo($"Created tokenizer: {diagnostics.TokenizerType ?? "Unknown"} " +
                          $"(vocab size: {diagnostics.VocabSize}, " +
                          $"has merges: {diagnostics.HasMerges}, " +
                          $"merge count: {diagnostics.MergeCount})");

            // Build TransformerModel from config
            var model = new TransformerModel(config, seed);

            // Load weights from GGUF into model
            logger.LogInfo($"Loading weights from GGUF{(useMmap ? " (using mmap)" : "")}...");
            LoadWeights(ggufPath, model, modelInfo, config, useMmap, logger);
            logger.LogInfo("Model loaded successfully.");

            return (model, tokenizer, config);
        }

        /// <summary>
        /// Load weights from GGUF file into TransformerModel.
        /// Reads tensors, dequantizes them, and injects into model parameters.
        /// </summary>
        private static void LoadWeights(string ggufPath, TransformerModel model, GgufModelInfo modelInfo, ModelConfig config, bool useMmap, IInternalRuntimeLogger logger)
        {
            // Get named parameters from model
            var namedParams = model.GetNamedParameters();

            // Create tensor name mapping from GGUF to SmallMind
            var tensorMapping = CreateTensorMapping(modelInfo, config);

            // Track which parameters we've loaded
            var loadedParams = new HashSet<string>();

            // Counters for tracking tensor reads
            int mainLoopReads = 0;
            int qkvSkipped = 0;
            int qkvReads = 0;

            // Read and load tensors using appropriate reader
            if (useMmap)
            {
                using (var mmapReader = new GgufMmapReader(ggufPath))
                {
                    LoadWeightsWithReader(mmapReader, modelInfo, config, tensorMapping, namedParams, loadedParams,
                        ref mainLoopReads, ref qkvSkipped, ref qkvReads, logger);
                }
            }
            else
            {
                using (var stream = File.OpenRead(ggufPath))
                using (var reader = new GgufReader(stream))
                {
                    // Re-read model info to position stream correctly
                    reader.ReadModelInfo();

                    LoadWeightsWithReader(reader, modelInfo, config, tensorMapping, namedParams, loadedParams,
                        ref mainLoopReads, ref qkvSkipped, ref qkvReads, logger);
                }
            }

            // Handle weight tying (copy token embeddings to output if missing)
            HandleWeightTying(namedParams, loadedParams, config, logger);

            // Report loading summary
            logger.LogInfo($"Loaded {loadedParams.Count} / {namedParams.Count} parameters");
            logger.LogInfo($"Tensor reads: {mainLoopReads} (main loop) + {qkvReads} (Q/K/V merge) = {mainLoopReads + qkvReads} total");
            logger.LogDebug($"Q/K/V tensors skipped in main loop: {qkvSkipped}");

            // Build the set of SmallMind parameter names that THIS GGUF file is expected to provide,
            // derived from the tensor mapping.  Tensors absent from the mapping (e.g. LLaMA/Mistral
            // bias tensors that the architecture simply does not use) are optional by design and must
            // never trigger a hard-fail.
            //
            // PENDING_Q/K/V_X and PENDING_QKV_X (weight, non-bias) entries get merged into
            // blk.X.attn_qkv.weight; PENDING_*_BIAS_* entries are optional and are skipped.
            var expectedFromGguf = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (ggufName, smName) in tensorMapping)
            {
                if (smName == "MERGE_QKV")
                    continue;

                if (smName.StartsWith("PENDING_"))
                {
                    // Skip bias PENDING entries (they are optional in LLaMA-family models).
                    if (smName.Contains("BIAS"))
                        continue;

                    // Non-bias PENDING entries resolve to the merged QKV weight tensor.
                    int layer = ExtractLayerIndex(ggufName);
                    if (layer >= 0)
                        expectedFromGguf.Add($"blk.{layer}.attn_qkv.weight");
                }
                else
                {
                    expectedFromGguf.Add(smName);
                }
            }

            // Hard-fail only if a tensor the GGUF file advertises was not successfully loaded.
            var missingCritical = new List<string>();
            foreach (var key in expectedFromGguf)
            {
                if (!loadedParams.Contains(key) && IsCriticalParameter(key))
                {
                    missingCritical.Add(key);
                }
            }

            if (missingCritical.Count > 0)
            {
                // Log all missing params before throwing so the user sees the full list.
                logger.LogError($"{missingCritical.Count} required tensor(s) were not loaded from the GGUF file:");
                int displayCount = Math.Min(10, missingCritical.Count);
                for (int i = 0; i < displayCount; i++)
                {
                    logger.LogError($"  - {missingCritical[i]}");
                }
                if (missingCritical.Count > 10)
                {
                    logger.LogError($"  ... and {missingCritical.Count - 10} more");
                }

                throw new InvalidOperationException(
                    $"GGUF weight loading failed: {missingCritical.Count} required tensor(s) missing. " +
                    $"First missing: '{missingCritical[0]}'. " +
                    "The model cannot produce valid output without these weights. " +
                    "Verify the GGUF file is complete and matches the declared architecture.");
            }
        }

        /// <summary>
        /// Load weights using the provided tensor data reader (stream-based or mmap).
        /// </summary>
        private static void LoadWeightsWithReader(ITensorDataReader reader, GgufModelInfo modelInfo,
            ModelConfig config, Dictionary<string, string> tensorMapping, Dictionary<string, Tensor> namedParams,
            HashSet<string> loadedParams, ref int mainLoopReads, ref int qkvSkipped, ref int qkvReads, IInternalRuntimeLogger logger)
        {
            bool hasQuantizedTensors = false;
            bool loggedQuantWarning = false;

            foreach (var tensorInfo in modelInfo.Tensors)
            {
                string ggufName = tensorInfo.Name;

                // Skip if this tensor isn't in our mapping
                if (!tensorMapping.TryGetValue(ggufName, out var smName))
                {
                    logger.LogDebug($"  Skipping unmapped tensor: {ggufName}");
                    continue;
                }

                // Track quantized tensors and log warning once
                if (!loggedQuantWarning && IsQuantizedType(tensorInfo.Type))
                {
                    hasQuantizedTensors = true;
                    loggedQuantWarning = true;
                    logger.LogWarning("Loading quantized GGUF model - weights will be dequantized to FP32.");
                    logger.LogWarning("For memory-efficient quantized inference, consider using GgufImporter from SmallMind.Quantization.");
                }

                // Handle special cases BEFORE reading/dequantizing to avoid double-read
                if (smName == "MERGE_QKV")
                {
                    // Q/K/V tensors need to be merged - handled separately
                    qkvSkipped++;
                    continue;
                }
                else if (smName.StartsWith("PENDING_"))
                {
                    // This is a Q/K/V component - will be merged later
                    qkvSkipped++;
                    continue;
                }

                // Read and dequantize tensor
                float[] data = ReadAndDequantizeTensor(reader, tensorInfo);
                mainLoopReads++;

                // Get target parameter
                if (!namedParams.TryGetValue(smName, out var targetParam))
                {
                    logger.LogWarning($"  No parameter found for {smName} (from {ggufName})");
                    continue;
                }

                // Copy weights with shape validation
                CopyWeights(data, targetParam, ggufName, smName, tensorInfo.Dimensions, logger);
                loadedParams.Add(smName);
            }

            // Handle QKV merging
            qkvReads = MergeQKVWeights(reader, modelInfo, config, namedParams, tensorMapping, loadedParams, logger);
        }

        /// <summary>
        /// Create mapping from GGUF tensor names to SmallMind parameter names.
        /// </summary>
        private static Dictionary<string, string> CreateTensorMapping(GgufModelInfo modelInfo, ModelConfig config)
        {
            var mapping = new Dictionary<string, string>();

            // OPTIMIZATION: Create HashSet for O(1) tensor name lookups instead of repeated .Any() calls
            var tensorNames = new HashSet<string>(modelInfo.Tensors.Count);
            for (int i = 0; i < modelInfo.Tensors.Count; i++)
            {
                tensorNames.Add(modelInfo.Tensors[i].Name);
            }

            // Determine architecture naming convention
            bool isLlamaFamily = config.Architecture.StartsWith("llama") ||
                                config.Architecture.StartsWith("mistral") ||
                                config.Architecture.StartsWith("phi");
            bool isGpt2 = config.Architecture.StartsWith("gpt");

            if (isLlamaFamily)
            {
                // Token embeddings
                mapping["token_embd.weight"] = "token_embd.weight";

                // Output norm and head
                mapping["output_norm.weight"] = "output_norm.weight";

                // Output head (may not exist due to weight tying)
                if (tensorNames.Contains("output.weight"))
                {
                    mapping["output.weight"] = "output.weight";
                }

                // Per-layer mappings
                for (int i = 0; i < config.BlockCount; i++)
                {
                    string ggufPrefix = $"blk.{i}.";
                    string smPrefix = $"blk.{i}.";

                    // Attention norm
                    mapping[$"{ggufPrefix}attn_norm.weight"] = $"{smPrefix}attn_norm.weight";

                    // Attention Q/K/V (separate in GGUF, combined in SmallMind)
                    // Mark these for merging
                    mapping[$"{ggufPrefix}attn_q.weight"] = $"PENDING_Q_{i}";
                    mapping[$"{ggufPrefix}attn_k.weight"] = $"PENDING_K_{i}";
                    mapping[$"{ggufPrefix}attn_v.weight"] = $"PENDING_V_{i}";

                    // Attention output
                    mapping[$"{ggufPrefix}attn_output.weight"] = $"{smPrefix}attn_output.weight";

                    // FFN norm
                    mapping[$"{ggufPrefix}ffn_norm.weight"] = $"{smPrefix}ffn_norm.weight";

                    // FFN projections
                    if (config.UseSwiGlu)
                    {
                        mapping[$"{ggufPrefix}ffn_gate.weight"] = $"{smPrefix}ffn_gate.weight";
                        mapping[$"{ggufPrefix}ffn_up.weight"] = $"{smPrefix}ffn_up.weight";
                        mapping[$"{ggufPrefix}ffn_down.weight"] = $"{smPrefix}ffn_down.weight";
                    }
                    else
                    {
                        mapping[$"{ggufPrefix}ffn_up.weight"] = $"{smPrefix}ffn_up.weight";
                        mapping[$"{ggufPrefix}ffn_down.weight"] = $"{smPrefix}ffn_down.weight";
                    }
                }
            }
            else if (isGpt2)
            {
                // GPT-2 tensor naming (uses different conventions)
                // Token embeddings
                mapping["token_embd.weight"] = "token_embd.weight";

                // Position embeddings (GPT-2 uses learned, not RoPE)
                if (tensorNames.Contains("position_embd.weight"))
                {
                    mapping["position_embd.weight"] = "position_embd.weight";
                }

                // Output norm (LayerNorm with bias)
                mapping["output_norm.weight"] = "output_norm.weight";
                if (tensorNames.Contains("output_norm.bias"))
                {
                    mapping["output_norm.bias"] = "output_norm.bias";
                }

                // Output head (may be tied to token embeddings)
                if (tensorNames.Contains("output.weight"))
                {
                    mapping["output.weight"] = "output.weight";
                }

                // Per-layer mappings for GPT-2
                for (int i = 0; i < config.BlockCount; i++)
                {
                    string ggufPrefix = $"blk.{i}.";
                    string smPrefix = $"blk.{i}.";

                    // Attention LayerNorm (with bias for GPT-2)
                    mapping[$"{ggufPrefix}attn_norm.weight"] = $"{smPrefix}attn_norm.weight";
                    if (tensorNames.Contains($"{ggufPrefix}attn_norm.bias"))
                    {
                        mapping[$"{ggufPrefix}attn_norm.bias"] = $"{smPrefix}attn_norm.bias";
                    }

                    // Attention weights - GPT-2 may have combined or separate Q/K/V
                    // Check if combined qkv exists
                    if (tensorNames.Contains($"{ggufPrefix}attn_qkv.weight"))
                    {
                        // Combined QKV (needs to be split in SmallMind)
                        mapping[$"{ggufPrefix}attn_qkv.weight"] = $"PENDING_QKV_{i}";
                        if (tensorNames.Contains($"{ggufPrefix}attn_qkv.bias"))
                        {
                            mapping[$"{ggufPrefix}attn_qkv.bias"] = $"PENDING_QKV_BIAS_{i}";
                        }
                    }
                    else
                    {
                        // Separate Q/K/V (need to be merged in SmallMind)
                        mapping[$"{ggufPrefix}attn_q.weight"] = $"PENDING_Q_{i}";
                        mapping[$"{ggufPrefix}attn_k.weight"] = $"PENDING_K_{i}";
                        mapping[$"{ggufPrefix}attn_v.weight"] = $"PENDING_V_{i}";

                        // GPT-2 has biases
                        if (tensorNames.Contains($"{ggufPrefix}attn_q.bias"))
                        {
                            mapping[$"{ggufPrefix}attn_q.bias"] = $"PENDING_Q_BIAS_{i}";
                            mapping[$"{ggufPrefix}attn_k.bias"] = $"PENDING_K_BIAS_{i}";
                            mapping[$"{ggufPrefix}attn_v.bias"] = $"PENDING_V_BIAS_{i}";
                        }
                    }

                    // Attention output projection
                    mapping[$"{ggufPrefix}attn_output.weight"] = $"{smPrefix}attn_output.weight";
                    if (tensorNames.Contains($"{ggufPrefix}attn_output.bias"))
                    {
                        mapping[$"{ggufPrefix}attn_output.bias"] = $"{smPrefix}attn_output.bias";
                    }

                    // FFN LayerNorm (with bias)
                    mapping[$"{ggufPrefix}ffn_norm.weight"] = $"{smPrefix}ffn_norm.weight";
                    if (tensorNames.Contains($"{ggufPrefix}ffn_norm.bias"))
                    {
                        mapping[$"{ggufPrefix}ffn_norm.bias"] = $"{smPrefix}ffn_norm.bias";
                    }

                    // FFN projections (GPT-2 uses GELU, not SwiGLU)
                    mapping[$"{ggufPrefix}ffn_up.weight"] = $"{smPrefix}ffn_up.weight";
                    mapping[$"{ggufPrefix}ffn_down.weight"] = $"{smPrefix}ffn_down.weight";

                    if (tensorNames.Contains($"{ggufPrefix}ffn_up.bias"))
                    {
                        mapping[$"{ggufPrefix}ffn_up.bias"] = $"{smPrefix}ffn_up.bias";
                        mapping[$"{ggufPrefix}ffn_down.bias"] = $"{smPrefix}ffn_down.bias";
                    }
                }
            }

            return mapping;
        }

        /// <summary>
        /// Read and dequantize a GGUF tensor to float array.
        /// </summary>
        private static float[] ReadAndDequantizeTensor(ITensorDataReader reader, GgufTensorInfo tensorInfo)
        {
            byte[] rawData = reader.ReadTensorData(tensorInfo.Offset, tensorInfo.Size);

            return tensorInfo.Type switch
            {
                GgufTensorType.F32 => ConvertF32Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.F16 => ConvertF16Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q8_0 => ConvertQ8_0Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q4_0 => ConvertQ4_0Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q5_0 => ConvertQ5_0Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q5_1 => ConvertQ5_1Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q6_K => ConvertQ6_KTensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q5_K => ConvertQ5_KTensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q4_K => ConvertQ4_KTensor(rawData, tensorInfo.Dimensions),
                _ => throw new NotSupportedException($"Unsupported tensor type: {tensorInfo.Type}")
            };
        }

        private static float[] ConvertF32Tensor(byte[] rawData, ulong[] dimensions)
        {
            int totalElements = CalculateTotalElements(dimensions);
            var floatData = new float[totalElements];
            Buffer.BlockCopy(rawData, 0, floatData, 0, rawData.Length);
            return floatData;
        }

        private static float[] ConvertF16Tensor(byte[] rawData, ulong[] dimensions)
        {
            int totalElements = CalculateTotalElements(dimensions);
            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int i = 0; i < totalElements; i++)
                {
                    ushort halfBits = br.ReadUInt16();
                    floatData[i] = HalfToFloat(halfBits);
                }
            }

            return floatData;
        }

        private static float[] ConvertQ8_0Tensor(byte[] rawData, ulong[] dimensions)
        {
            int totalElements = CalculateTotalElements(dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;
            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read int8 values
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);

                    for (int i = blockStart; i < blockEnd; i++)
                    {
                        sbyte quantized = br.ReadSByte();
                        floatData[i] = quantized * scale;
                    }
                }
            }

            return floatData;
        }

        private static float[] ConvertQ4_0Tensor(byte[] rawData, ulong[] dimensions)
        {
            int totalElements = CalculateTotalElements(dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;
            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    int blockStart = blockIdx * GgufBlockSize;
                    const int halfBlock = GgufBlockSize / 2; // 16

                    // GGUF Q4_0 split layout (matches ggml dequantize_row_q4_0):
                    //   byte j (j=0..15): low nibble  → element j,      dequant = (nibble & 0xF) - 8
                    //                     high nibble → element j + 16, dequant = (nibble >> 4)  - 8
                    // Values are unsigned 4-bit offset by 8, NOT two's-complement.
                    for (int j = 0; j < halfBlock; j++)
                    {
                        byte packedByte = br.ReadByte();

                        int e0 = blockStart + j;             // low nibble → first half
                        int e1 = blockStart + j + halfBlock; // high nibble → second half

                        if (e0 < totalElements)
                            floatData[e0] = ((packedByte & 0xF) - 8) * scale;
                        if (e1 < totalElements)
                            floatData[e1] = ((packedByte >> 4) - 8) * scale;
                    }
                }
            }

            return floatData;
        }

        /// <summary>
        /// Q5_0: 5-bit quantization with per-block fp16 scale.
        /// Block structure: scale (fp16) + quant values (5-bit, block size 32).
        /// </summary>
        private static float[] ConvertQ5_0Tensor(byte[] rawData, ulong[] dimensions)
        {
            int totalElements = CalculateTotalElements(dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;
            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale (2 bytes)
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);

                    // Read high bits (4 bytes = 32 bits, one per value)
                    uint qh = br.ReadUInt32();

                    // Read low 4-bit values (16 bytes, packed split-half like Q4_0)
                    byte[] qs = br.ReadBytes(16);

                    int blockStart = blockIdx * GgufBlockSize;
                    const int halfBlock = GgufBlockSize / 2; // 16

                    // Split-half layout (matches ggml dequantize_row_q5_0):
                    // For j=0..15:
                    //   Element j:      (qs[j] & 0xF) | high_bit_j       - 16
                    //   Element j + 16: (qs[j] >> 4)  | high_bit_(j+16)  - 16
                    for (int j = 0; j < halfBlock; j++)
                    {
                        int e0 = blockStart + j;
                        int e1 = blockStart + j + halfBlock;

                        int xh0 = (int)(((qh >> j) & 1) << 4);
                        int xh1 = (int)(((qh >> (j + halfBlock)) & 1) << 4);

                        int x0 = (qs[j] & 0xF) | xh0;
                        int x1 = ((qs[j] >> 4) & 0xF) | xh1;

                        if (e0 < totalElements)
                            floatData[e0] = (x0 - 16) * scale;
                        if (e1 < totalElements)
                            floatData[e1] = (x1 - 16) * scale;
                    }
                }
            }

            return floatData;
        }

        /// <summary>
        /// Q5_1: 5-bit quantization with per-block fp16 scale and min.
        /// Block structure: scale (fp16) + min (fp16) + quant values (5-bit, block size 32).
        /// </summary>
        private static float[] ConvertQ5_1Tensor(byte[] rawData, ulong[] dimensions)
        {
            int totalElements = CalculateTotalElements(dimensions);
            int numBlocks = (totalElements + GgufBlockSize - 1) / GgufBlockSize;
            var floatData = new float[totalElements];

            using (var ms = new MemoryStream(rawData))
            using (var br = new BinaryReader(ms))
            {
                for (int blockIdx = 0; blockIdx < numBlocks; blockIdx++)
                {
                    // Read fp16 scale and min (4 bytes)
                    ushort scaleHalf = br.ReadUInt16();
                    float scale = HalfToFloat(scaleHalf);
                    ushort minHalf = br.ReadUInt16();
                    float min = HalfToFloat(minHalf);

                    // Read high bits (4 bytes = 32 bits, one per value)
                    uint qh = br.ReadUInt32();

                    // Read low 4-bit values (16 bytes, packed split-half)
                    byte[] qs = br.ReadBytes(16);

                    int blockStart = blockIdx * GgufBlockSize;
                    const int halfBlock = GgufBlockSize / 2; // 16

                    // Split-half layout (matches ggml dequantize_row_q5_1):
                    // For j=0..15:
                    //   Element j:      (qs[j] & 0xF) | high_bit_j,       value * scale + min
                    //   Element j + 16: (qs[j] >> 4)  | high_bit_(j+16),  value * scale + min
                    for (int j = 0; j < halfBlock; j++)
                    {
                        int e0 = blockStart + j;
                        int e1 = blockStart + j + halfBlock;

                        int xh0 = (int)(((qh >> j) & 1) << 4);
                        int xh1 = (int)(((qh >> (j + halfBlock)) & 1) << 4);

                        int x0 = (qs[j] & 0xF) | xh0;
                        int x1 = ((qs[j] >> 4) & 0xF) | xh1;

                        if (e0 < totalElements)
                            floatData[e0] = x0 * scale + min;
                        if (e1 < totalElements)
                            floatData[e1] = x1 * scale + min;
                    }
                }
            }

            return floatData;
        }

        /// <summary>
        /// Q4_K: K-quant 4-bit with 6-bit scale quantization per super-block.
        /// Super-block size: 256. Uses 8 scales and 8 mins per super-block.
        /// Block structure: d (fp16), dmin (fp16), scales (12 bytes), qs (128 bytes).
        /// Total: 144 bytes per super-block.
        /// </summary>
        private static float[] ConvertQ4_KTensor(byte[] rawData, ulong[] dimensions)
        {
            const int SuperBlockSize = 256;
            const int BytesPerBlock = 144;  // 2 + 2 + 12 + 128

            int totalElements = CalculateTotalElements(dimensions);
            int numSuperBlocks = (totalElements + SuperBlockSize - 1) / SuperBlockSize;
            var floatData = new float[totalElements];

            ReadOnlySpan<byte> src = rawData;
            int srcOffset = 0;

            // Allocate once outside loop to avoid CA2014 warning
            Span<byte> scales = stackalloc byte[8];
            Span<byte> mins = stackalloc byte[8];

            for (int sbIdx = 0; sbIdx < numSuperBlocks; sbIdx++)
            {
                // Read super-block scale and min (fp16)
                ushort dBits = BitConverter.ToUInt16(src.Slice(srcOffset, 2));
                ushort dminBits = BitConverter.ToUInt16(src.Slice(srcOffset + 2, 2));
                float d = HalfToFloat(dBits);
                float dmin = HalfToFloat(dminBits);

                // Read 12 bytes containing 8 6-bit scales and 8 6-bit mins
                ReadOnlySpan<byte> q = src.Slice(srcOffset + 4, 12);

                // Unpack scales and mins using llama.cpp get_scale_min_k4 logic:
                // j<4: scale=q[j]&63, min=q[j+4]&63
                // j>=4: scale=(q[j+4]&0xF)|((q[j-4]>>6)<<4), min=(q[j+4]>>4)|((q[j]>>6)<<4)
                for (int j = 0; j < 8; j++)
                {
                    if (j < 4)
                    {
                        scales[j] = (byte)(q[j] & 63);
                        mins[j] = (byte)(q[j + 4] & 63);
                    }
                    else
                    {
                        scales[j] = (byte)((q[j + 4] & 0xF) | ((q[j - 4] >> 6) << 4));
                        mins[j] = (byte)((q[j + 4] >> 4) | ((q[j] >> 6) << 4));
                    }
                }

                // Read quantized values (128 bytes, 2 values per byte)
                ReadOnlySpan<byte> qs = src.Slice(srcOffset + 16, 128);

                int sbStart = sbIdx * SuperBlockSize;
                int sbEnd = Math.Min(sbStart + SuperBlockSize, totalElements);

                // Process in groups of 64 (split-half: 32 low nibbles + 32 high nibbles)
                // Matches llama.cpp dequantize_row_q4_K layout
                int qsIdx = 0;
                int scaleIdx = 0;

                for (int j = 0; j < SuperBlockSize && sbStart + j < sbEnd; j += 64)
                {
                    float d1 = d * scales[scaleIdx];
                    float m1 = dmin * mins[scaleIdx];
                    float d2 = d * scales[scaleIdx + 1];
                    float m2 = dmin * mins[scaleIdx + 1];

                    // First 32 elements: low nibbles with scale d1/m1
                    for (int l = 0; l < 32 && sbStart + j + l < sbEnd; l++)
                    {
                        floatData[sbStart + j + l] = d1 * (qs[qsIdx + l] & 0xF) - m1;
                    }

                    // Next 32 elements: high nibbles with scale d2/m2
                    for (int l = 0; l < 32 && sbStart + j + 32 + l < sbEnd; l++)
                    {
                        floatData[sbStart + j + 32 + l] = d2 * ((qs[qsIdx + l] >> 4) & 0xF) - m2;
                    }

                    qsIdx += 32;
                    scaleIdx += 2;
                }

                srcOffset += BytesPerBlock;
            }

            return floatData;
        }

        /// <summary>
        /// Q5_K: K-quant 5-bit with 6-bit scale quantization per super-block.
        /// Super-block size: 256. Uses 8 scales and 8 mins per super-block.
        /// NOTE: This is a simplified dequantization. For production use, keep weights quantized.
        /// </summary>
        private static float[] ConvertQ5_KTensor(byte[] rawData, ulong[] dimensions)
        {
            const int SuperBlockSize = 256;
            const int BytesPerBlock = 176;  // 2 + 2 + 12 + 32 + 128

            int totalElements = CalculateTotalElements(dimensions);
            int numSuperBlocks = (totalElements + SuperBlockSize - 1) / SuperBlockSize;
            var floatData = new float[totalElements];

            ReadOnlySpan<byte> src = rawData;
            int srcOffset = 0;

            // Allocate once outside loop
            Span<byte> scales = stackalloc byte[8];
            Span<byte> mins = stackalloc byte[8];

            for (int sbIdx = 0; sbIdx < numSuperBlocks; sbIdx++)
            {
                // Read super-block scale and min (fp16)
                ushort dBits = BitConverter.ToUInt16(src.Slice(srcOffset, 2));
                ushort dminBits = BitConverter.ToUInt16(src.Slice(srcOffset + 2, 2));
                float d = HalfToFloat(dBits);
                float dmin = HalfToFloat(dminBits);

                // Read 12 bytes containing 8 6-bit scales and 8 6-bit mins
                ReadOnlySpan<byte> q = src.Slice(srcOffset + 4, 12);

                // Unpack scales and mins using llama.cpp get_scale_min_k4 logic (same as Q4_K)
                for (int j = 0; j < 8; j++)
                {
                    if (j < 4)
                    {
                        scales[j] = (byte)(q[j] & 63);
                        mins[j] = (byte)(q[j + 4] & 63);
                    }
                    else
                    {
                        scales[j] = (byte)((q[j + 4] & 0xF) | ((q[j - 4] >> 6) << 4));
                        mins[j] = (byte)((q[j + 4] >> 4) | ((q[j] >> 6) << 4));
                    }
                }

                // Read high bits (32 bytes, each byte provides bits for elements across groups)
                ReadOnlySpan<byte> qh = src.Slice(srcOffset + 16, 32);

                // Read low nibbles (128 bytes, 2 values per byte)
                ReadOnlySpan<byte> ql = src.Slice(srcOffset + 48, 128);

                int sbStart = sbIdx * SuperBlockSize;
                int sbEnd = Math.Min(sbStart + SuperBlockSize, totalElements);

                // Process in groups of 64 (split-half like Q4_K, plus high bits from qh)
                // Matches llama.cpp dequantize_row_q5_K layout
                int qlIdx = 0;
                int scaleIdx = 0;
                byte u1 = 1, u2 = 2;  // Bit masks for high bits, shift left by 2 each group

                for (int j = 0; j < SuperBlockSize && sbStart + j < sbEnd; j += 64)
                {
                    float d1 = d * scales[scaleIdx];
                    float m1 = dmin * mins[scaleIdx];
                    float d2 = d * scales[scaleIdx + 1];
                    float m2 = dmin * mins[scaleIdx + 1];

                    // First 32 elements: low nibbles + high bit (u1) with d1/m1
                    for (int l = 0; l < 32 && sbStart + j + l < sbEnd; l++)
                    {
                        int q5 = (ql[qlIdx + l] & 0xF) + ((qh[l] & u1) != 0 ? 16 : 0);
                        floatData[sbStart + j + l] = d1 * q5 - m1;
                    }

                    // Next 32 elements: high nibbles + high bit (u2) with d2/m2
                    for (int l = 0; l < 32 && sbStart + j + 32 + l < sbEnd; l++)
                    {
                        int q5 = ((ql[qlIdx + l] >> 4) & 0xF) + ((qh[l] & u2) != 0 ? 16 : 0);
                        floatData[sbStart + j + 32 + l] = d2 * q5 - m2;
                    }

                    qlIdx += 32;
                    scaleIdx += 2;
                    u1 <<= 2;
                    u2 <<= 2;
                }

                srcOffset += BytesPerBlock;
            }

            return floatData;
        }

        /// <summary>
        /// Q6_K: K-quant 6-bit with per-sub-block scales.
        /// Super-block size: 256. Uses 16 sub-blocks of 16 values each.
        /// Block structure: ql (128 bytes), qh (64 bytes), scales (16 bytes int8), d (fp16).
        /// Total: 210 bytes per super-block.
        /// NOTE: This is a simplified dequantization. For production use, keep weights quantized.
        /// </summary>
        private static float[] ConvertQ6_KTensor(byte[] rawData, ulong[] dimensions)
        {
            const int SuperBlockSize = 256;
            const int BytesPerBlock = 210;  // 128 + 64 + 16 + 2

            int totalElements = CalculateTotalElements(dimensions);
            int numSuperBlocks = (totalElements + SuperBlockSize - 1) / SuperBlockSize;
            var floatData = new float[totalElements];

            ReadOnlySpan<byte> src = rawData;
            int srcOffset = 0;

            for (int sbIdx = 0; sbIdx < numSuperBlocks; sbIdx++)
            {
                // Read ql (128 bytes - lower 4 bits of 6-bit values)
                ReadOnlySpan<byte> ql = src.Slice(srcOffset, 128);

                // Read qh (64 bytes - upper 2 bits of 6-bit values)
                ReadOnlySpan<byte> qh = src.Slice(srcOffset + 128, 64);

                // Read scales (16 signed int8 values)
                ReadOnlySpan<byte> scalesBytes = src.Slice(srcOffset + 192, 16);

                // Read super-block scale d (fp16)
                ushort dBits = BitConverter.ToUInt16(src.Slice(srcOffset + 208, 2));
                float d = HalfToFloat(dBits);

                int sbStart = sbIdx * SuperBlockSize;
                int sbEnd = Math.Min(sbStart + SuperBlockSize, totalElements);

                // Process in groups of 128 (matching llama.cpp dequantize_row_q6_K layout)
                // Each group uses 64 bytes of ql, 32 bytes of qh, 8 scales
                int qlOffset = 0;
                int qhOffset = 0;
                int scOffset = 0;

                for (int n = 0; n < SuperBlockSize && sbStart + n < sbEnd; n += 128)
                {
                    for (int l = 0; l < 32; l++)
                    {
                        int isIdx = l / 16;

                        // Reconstruct 4 6-bit values from ql and qh
                        // q1: ql[l] low nibble + qh[l] bits 0-1 → element n+l
                        // q2: ql[l+32] low nibble + qh[l] bits 2-3 → element n+l+32
                        // q3: ql[l] HIGH nibble + qh[l] bits 4-5 → element n+l+64
                        // q4: ql[l+32] HIGH nibble + qh[l] bits 6-7 → element n+l+96
                        int q1 = (ql[qlOffset + l] & 0xF) | (((qh[qhOffset + l] >> 0) & 3) << 4);
                        int q2 = (ql[qlOffset + l + 32] & 0xF) | (((qh[qhOffset + l] >> 2) & 3) << 4);
                        int q3 = ((ql[qlOffset + l] >> 4) & 0xF) | (((qh[qhOffset + l] >> 4) & 3) << 4);
                        int q4 = ((ql[qlOffset + l + 32] >> 4) & 0xF) | (((qh[qhOffset + l] >> 6) & 3) << 4);

                        // Scales are signed int8, indexed by sub-block within the 128-value group
                        sbyte sc0 = (sbyte)scalesBytes[scOffset + isIdx + 0];
                        sbyte sc2 = (sbyte)scalesBytes[scOffset + isIdx + 2];
                        sbyte sc4 = (sbyte)scalesBytes[scOffset + isIdx + 4];
                        sbyte sc6 = (sbyte)scalesBytes[scOffset + isIdx + 6];

                        // 6-bit value (0-63) centered by -32 → signed range (-32..+31)
                        int idx0 = sbStart + n + l;
                        int idx1 = sbStart + n + l + 32;
                        int idx2 = sbStart + n + l + 64;
                        int idx3 = sbStart + n + l + 96;

                        if (idx0 < sbEnd) floatData[idx0] = d * sc0 * (q1 - 32);
                        if (idx1 < sbEnd) floatData[idx1] = d * sc2 * (q2 - 32);
                        if (idx2 < sbEnd) floatData[idx2] = d * sc4 * (q3 - 32);
                        if (idx3 < sbEnd) floatData[idx3] = d * sc6 * (q4 - 32);
                    }

                    qlOffset += 64;
                    qhOffset += 32;
                    scOffset += 8;
                }

                srcOffset += BytesPerBlock;
            }

            return floatData;
        }

        private static float HalfToFloat(ushort half)
        {
            uint sign = (uint)(half >> 15) & 0x1u;
            uint exponent = (uint)(half >> 10) & 0x1Fu;
            uint mantissa = (uint)half & 0x3FFu;

            uint result;

            if (exponent == 0)
            {
                if (mantissa == 0)
                {
                    result = sign << 31;
                }
                else
                {
                    exponent = 1;
                    while ((mantissa & 0x400) == 0)
                    {
                        mantissa <<= 1;
                        exponent--;
                    }
                    mantissa &= 0x3FFu;
                    result = (sign << 31) | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
                }
            }
            else if (exponent == 0x1F)
            {
                result = (sign << 31) | 0x7F800000u | (mantissa << 13);
            }
            else
            {
                result = (sign << 31) | ((exponent + (127 - 15)) << 23) | (mantissa << 13);
            }

            byte[] bytes = BitConverter.GetBytes(result);
            return BitConverter.ToSingle(bytes, 0);
        }

        private static int CalculateTotalElements(ulong[] dimensions)
        {
            int total = 1;
            foreach (var dim in dimensions)
            {
                total *= (int)dim;
            }
            return total;
        }

        /// <summary>
        /// Copy weights from source array to target tensor with shape validation.
        /// Handles transposition if needed based on Linear weight layout (outFeatures, inFeatures).
        /// </summary>
        private static void CopyWeights(float[] source, Tensor target,
            string ggufName, string smName, ulong[] ggufDims, IInternalRuntimeLogger logger)
        {
            // Validate total element count matches
            int sourceSize = source.Length;
            int targetSize = target.Size;

            if (sourceSize != targetSize)
            {
                // Check if transpose might fix it (2D tensors only)
                if (ggufDims.Length == 2)
                {
                    int ggufRows = (int)ggufDims[0];
                    int ggufCols = (int)ggufDims[1];
                    int targetRows = target.Shape[0];
                    int targetCols = target.Shape.Length > 1 ? target.Shape[1] : 1;

                    // If dimensions are swapped, transpose
                    if (ggufRows == targetCols && ggufCols == targetRows)
                    {
                        logger.LogDebug($"  {smName}: Transposing from ({ggufRows}, {ggufCols}) to ({targetRows}, {targetCols})");
                        TransposeAndCopy(source, target.Data, ggufRows, ggufCols);
                        return;
                    }
                }

                throw new InvalidOperationException(
                    $"Shape mismatch for {smName} (from {ggufName}):\n" +
                    $"  GGUF dimensions: [{string.Join(", ", ggufDims)}] = {sourceSize} elements\n" +
                    $"  Target shape: [{string.Join(", ", target.Shape)}] = {targetSize} elements");
            }

            // Direct copy
            Array.Copy(source, target.Data, sourceSize);
            logger.LogDebug($"  {smName}: Loaded {sourceSize} elements from {ggufName}");
        }

        /// <summary>
        /// Transpose a 2D matrix and copy to destination.
        /// </summary>
        private static void TransposeAndCopy(float[] source, float[] dest, int rows, int cols)
        {
            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    dest[j * rows + i] = source[i * cols + j];
                }
            }
        }

        /// <summary>
        /// Merge separate Q/K/V weight tensors from GGUF into combined QKV tensor in SmallMind.
        /// Supports GQA (Grouped Query Attention) where K/V have fewer heads than Q.
        /// Returns the count of Q/K/V tensor reads.
        /// </summary>
        private static int MergeQKVWeights(ITensorDataReader reader, GgufModelInfo modelInfo,
            ModelConfig config, Dictionary<string, Tensor> namedParams,
            Dictionary<string, string> tensorMapping, HashSet<string> loadedParams, IInternalRuntimeLogger logger)
        {
            int qkvReadsCount = 0;

            // Precompute expected GQA dimensions from config for validation
            int headSize = config.HeadCount > 0 ? config.EmbeddingLength / config.HeadCount : 0;
            int expectedQRows = config.EmbeddingLength;          // nHead * headSize = nEmbd
            int expectedKvRows = config.HeadCountKv * headSize;  // nKvHead * headSize (< nEmbd for GQA)
            bool isGqa = config.HeadCountKv < config.HeadCount;

            // Group Q/K/V tensors by layer
            var qkvGroups = new Dictionary<int, (GgufTensorInfo? q, GgufTensorInfo? k, GgufTensorInfo? v)>();

            foreach (var tensorInfo in modelInfo.Tensors)
            {
                string name = tensorInfo.Name;

                if (name.Contains(".attn_q.weight"))
                {
                    int layer = ExtractLayerIndex(name);
                    if (!qkvGroups.TryGetValue(layer, out var group))
                        group = (null, null, null);
                    qkvGroups[layer] = (tensorInfo, group.k, group.v);
                }
                else if (name.Contains(".attn_k.weight"))
                {
                    int layer = ExtractLayerIndex(name);
                    if (!qkvGroups.TryGetValue(layer, out var group))
                        group = (null, null, null);
                    qkvGroups[layer] = (group.q, tensorInfo, group.v);
                }
                else if (name.Contains(".attn_v.weight"))
                {
                    int layer = ExtractLayerIndex(name);
                    if (!qkvGroups.TryGetValue(layer, out var group))
                        group = (null, null, null);
                    qkvGroups[layer] = (group.q, group.k, tensorInfo);
                }
            }

            // Merge each layer's Q/K/V
            foreach (var (layer, (q, k, v)) in qkvGroups)
            {
                if (q == null || k == null || v == null)
                {
                    logger.LogWarning($"  Incomplete Q/K/V set for layer {layer}");
                    continue;
                }

                string targetName = $"blk.{layer}.attn_qkv.weight";
                if (!namedParams.TryGetValue(targetName, out var targetParam))
                {
                    logger.LogWarning($"  No target parameter for {targetName}");
                    continue;
                }

                // Read Q/K/V tensors
                float[] qData = ReadAndDequantizeTensor(reader, q);
                float[] kData = ReadAndDequantizeTensor(reader, k);
                float[] vData = ReadAndDequantizeTensor(reader, v);
                qkvReadsCount += 3; // Count Q, K, V reads

                // Build human-readable shape strings for diagnostics (GGUF dims: [cols, rows])
                string qShape = FormatTensorShape(q.Dimensions);
                string kShape = FormatTensorShape(k.Dimensions);
                string vShape = FormatTensorShape(v.Dimensions);

                // Merge: [Q, K, V] concatenation
                // GGUF stores as row-major (outFeatures, inFeatures); SmallMind uses same layout.
                // For GQA: Q has nEmbd rows, K/V each have nKvHead*headSize rows.
                int qSize = qData.Length;
                int kSize = kData.Length;
                int vSize = vData.Length;
                int totalSize = qSize + kSize + vSize;

                if (isGqa && layer == 0)
                {
                    logger.LogDebug($"  GQA detected: Q={qShape}, K={kShape}, V={vShape}" +
                        $" (nHead={config.HeadCount}, nKvHead={config.HeadCountKv})");
                }

                if (totalSize != targetParam.Size)
                {
                    // Provide a GQA-aware error message showing the actual vs expected dimensions
                    int qRows = qSize > 0 && q.Dimensions.Length >= 2 ? (int)q.Dimensions[1] : 0;
                    int kRows = kSize > 0 && k.Dimensions.Length >= 2 ? (int)k.Dimensions[1] : 0;
                    bool isGqaShapeMismatch = qRows > 0 && kRows > 0 && kRows < qRows;

                    // For GQA hint: infer expected nKvHead from K's actual row count and headSize
                    // headSize = nEmbd / nHead; expected nKvHead = kRows / headSize
                    string hint = string.Empty;
                    if (isGqaShapeMismatch && headSize > 0)
                    {
                        int inferredKvHead = kRows / headSize;
                        hint = $"\n  Hint: K/V have fewer rows than Q — this is a GQA model." +
                               $" K rows={kRows} implies nKvHead={inferredKvHead}," +
                               $" but config has nKvHead={config.HeadCountKv}." +
                               $" Verify the GGUF metadata key '{config.Architecture}.attention.head_count_kv' is present and correct.";
                    }

                    throw new InvalidOperationException(
                        $"QKV merge dimension mismatch for layer {layer}:\n" +
                        $"  Q={qShape}, K={kShape}, V={vShape}\n" +
                        $"  Combined elements: Q({qSize}) + K({kSize}) + V({vSize}) = {totalSize}\n" +
                        $"  Target tensor size: {targetParam.Size}\n" +
                        $"  (config: nHead={config.HeadCount}, nKvHead={config.HeadCountKv}, nEmbd={config.EmbeddingLength})" +
                        hint);
                }

                // Copy Q, then K, then V (row-major layout preserved)
                Array.Copy(qData, 0, targetParam.Data, 0, qSize);
                Array.Copy(kData, 0, targetParam.Data, qSize, kSize);
                Array.Copy(vData, 0, targetParam.Data, qSize + kSize, vSize);

                logger.LogDebug($"  blk.{layer}.attn_qkv.weight: Merged Q={qShape} + K={kShape} + V={vShape} = {totalSize} elements");
                loadedParams.Add(targetName);
            }

            return qkvReadsCount;
        }

        /// <summary>
        /// Format GGUF tensor dimensions as a readable shape string.
        /// GGUF stores dims[0]=cols (input), dims[1]=rows (output) for 2D tensors.
        /// Displayed as (rows x cols) to match weight matrix convention.
        /// </summary>
        private static string FormatTensorShape(ulong[] dims)
        {
            if (dims == null || dims.Length == 0)
                return "(empty)";
            if (dims.Length == 1)
                return $"({dims[0]})";
            if (dims.Length == 2)
                // GGUF: dims[0]=cols(in), dims[1]=rows(out) → display as (rows x cols)
                return $"({dims[1]}x{dims[0]})";
            // For higher-rank tensors, reverse to show outermost dimension first
            var parts = new string[dims.Length];
            for (int i = 0; i < dims.Length; i++)
                parts[i] = dims[dims.Length - 1 - i].ToString();
            return $"({string.Join("x", parts)})";
        }

        private static int ExtractLayerIndex(string tensorName)
        {
            // Extract layer number from name like "blk.3.attn_q.weight"
            var parts = tensorName.Split('.');
            if (parts.Length >= 2 && parts[0] == "blk" && int.TryParse(parts[1], out int layer))
            {
                return layer;
            }
            return -1;
        }

        /// <summary>
        /// Handle weight tying: copy token embeddings to output head if missing.
        /// </summary>
        private static void HandleWeightTying(Dictionary<string, Tensor> namedParams,
            HashSet<string> loadedParams, ModelConfig config, IInternalRuntimeLogger logger)
        {
            // Check if output.weight is loaded
            if (!loadedParams.Contains("output.weight") && 
                namedParams.TryGetValue("output.weight", out var outputWeight) &&
                namedParams.TryGetValue("token_embd.weight", out var tokenEmbed))
            {
                logger.LogInfo("  Applying weight tying: copying token_embd.weight to output.weight");

                if (tokenEmbed.Size != outputWeight.Size)
                {
                    throw new InvalidOperationException(
                        $"Weight tying size mismatch:\n" +
                        $"  Token embedding: {tokenEmbed.Size}\n" +
                        $"  Output weight: {outputWeight.Size}");
                }

                Array.Copy(tokenEmbed.Data, outputWeight.Data, tokenEmbed.Size);
                loadedParams.Add("output.weight");
            }
        }

        private static bool IsCriticalParameter(string paramName)
        {
            // Parameters that must be loaded for model to work
            return paramName.Contains("token_embd") ||
                   paramName.Contains("output_norm") ||
                   paramName.Contains("output.weight") ||
                   paramName.Contains("attn_qkv") ||
                   paramName.Contains("attn_output") ||
                   paramName.Contains("ffn");
        }

        /// <summary>
        /// Check if a GGUF tensor type is quantized (not FP32/FP16).
        /// </summary>
        private static bool IsQuantizedType(GgufTensorType type)
        {
            return type != GgufTensorType.F32 && type != GgufTensorType.F16;
        }

        /// <summary>
        /// Load just the model configuration from a GGUF file without building the model.
        /// Useful for inspecting model metadata.
        /// </summary>
        /// <param name="ggufPath">Path to GGUF file</param>
        /// <returns>ModelConfig extracted from metadata</returns>
        public static ModelConfig LoadConfigFromGguf(string ggufPath)
        {
            if (string.IsNullOrEmpty(ggufPath))
                throw new ArgumentNullException(nameof(ggufPath));
            if (!File.Exists(ggufPath))
                throw new FileNotFoundException($"GGUF file not found: {ggufPath}");

            using var stream = File.OpenRead(ggufPath);
            using var reader = new GgufReader(stream);
            var modelInfo = reader.ReadModelInfo();

            return ModelConfig.FromGgufMetadata(modelInfo.Metadata);
        }

        /// <summary>
        /// Load just the tokenizer from a GGUF file.
        /// </summary>
        /// <param name="ggufPath">Path to GGUF file</param>
        /// <returns>Tokenizer extracted from metadata</returns>
        public static ITokenizer LoadTokenizerFromGguf(string ggufPath)
        {
            if (string.IsNullOrEmpty(ggufPath))
                throw new ArgumentNullException(nameof(ggufPath));
            if (!File.Exists(ggufPath))
                throw new FileNotFoundException($"GGUF file not found: {ggufPath}");

            using var stream = File.OpenRead(ggufPath);
            using var reader = new GgufReader(stream);
            var modelInfo = reader.ReadModelInfo();

            var (tokenizer, diagnostics) = GgufTokenizerFactory.CreateTokenizer(
                modelInfo.Metadata,
                NullRuntimeLogger.Instance);

            if (tokenizer == null)
            {
                throw new NotSupportedException(
                    "Failed to create tokenizer from GGUF file. " +
                    "Ensure the file contains tokenizer metadata. " +
                    $"Diagnostics: {string.Join(", ", diagnostics.Issues.Select(i => i.message))}");
            }

            return tokenizer;
        }

        /// <summary>
        /// Generates a compatibility report for a GGUF model without loading it.
        /// Use this to check if a model is supported before attempting to load.
        /// </summary>
        /// <param name="ggufPath">Path to GGUF file</param>
        /// <returns>Compatibility report with detailed diagnostics</returns>
        public static Gguf.GgufCompatibilityReport GetCompatibilityReport(string ggufPath)
        {
            if (string.IsNullOrEmpty(ggufPath))
                throw new ArgumentNullException(nameof(ggufPath));
            if (!File.Exists(ggufPath))
                throw new FileNotFoundException($"GGUF file not found: {ggufPath}");

            // Read GGUF model info
            GgufModelInfo modelInfo;
            using (var stream = File.OpenRead(ggufPath))
            using (var reader = new GgufReader(stream))
            {
                modelInfo = reader.ReadModelInfo();
            }

            // Create decoder registry to check support
            var registry = new Gguf.TensorDecoders.TensorDecoderRegistry();

            // Generate report
            return Gguf.GgufCompatibilityReport.FromModelInfo(
                modelInfo,
                type => registry.IsSupported(type));
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SmallMind.Quantization.IO.Gguf;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using SmallMind.Quantization.Tensors;
using Tensor = SmallMind.Core.Core.Tensor;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Loads models from GGUF format files with full weight loading.
    /// Extracts ModelConfig and tokenizer from GGUF metadata.
    /// Constructs TransformerModel with appropriate architecture (Llama, Mistral, Phi, GPT-2).
    /// Phase 3 implementation: loads actual weights from GGUF into model.
    /// </summary>
    public sealed class GgufModelLoader
    {
        private const int GgufBlockSize = 32; // GGUF uses block size 32 for quantization

        /// <summary>
        /// Load a model from a GGUF file with full weight loading.
        /// Reads metadata, extracts config and tokenizer, builds TransformerModel, and loads weights.
        /// </summary>
        /// <param name="ggufPath">Path to GGUF file</param>
        /// <param name="seed">Random seed for model initialization</param>
        /// <returns>Tuple of (model, tokenizer, config)</returns>
        public static (TransformerModel model, ITokenizer tokenizer, ModelConfig config) LoadFromGguf(
            string ggufPath, 
            int seed = 42)
        {
            if (string.IsNullOrEmpty(ggufPath))
                throw new ArgumentNullException(nameof(ggufPath));
            if (!File.Exists(ggufPath))
                throw new FileNotFoundException($"GGUF file not found: {ggufPath}");

            Console.WriteLine($"Loading GGUF model from: {ggufPath}");

            // Read GGUF model info
            GgufModelInfo modelInfo;
            using (var stream = File.OpenRead(ggufPath))
            using (var reader = new GgufReader(stream))
            {
                modelInfo = reader.ReadModelInfo();
            }

            // Extract ModelConfig from metadata
            var config = ModelConfig.FromGgufMetadata(modelInfo.Metadata);
            Console.WriteLine($"Model architecture: {config.Architecture}");
            Console.WriteLine($"Context length: {config.ContextLength}, Embedding: {config.EmbeddingLength}");
            Console.WriteLine($"Layers: {config.BlockCount}, Heads: {config.HeadCount} (KV: {config.HeadCountKv})");
            Console.WriteLine($"RoPE freq base: {config.RopeFreqBase}");
            Console.WriteLine($"Vocab size: {config.VocabSize}");

            // Extract tokenizer from metadata
            var tokenizer = GgufTokenizerExtractor.ExtractTokenizer(modelInfo.Metadata);
            if (tokenizer == null)
            {
                throw new NotSupportedException(
                    "Failed to extract tokenizer from GGUF file. " +
                    "Ensure the file contains tokenizer metadata (tokenizer.ggml.*).");
            }

            // Build TransformerModel from config
            var model = new TransformerModel(config, seed);

            // Load weights from GGUF into model
            Console.WriteLine("Loading weights from GGUF...");
            LoadWeights(ggufPath, model, modelInfo, config);
            Console.WriteLine("Model loaded successfully.");

            return (model, tokenizer, config);
        }

        /// <summary>
        /// Load weights from GGUF file into TransformerModel.
        /// Reads tensors, dequantizes them, and injects into model parameters.
        /// </summary>
        private static void LoadWeights(string ggufPath, TransformerModel model, GgufModelInfo modelInfo, ModelConfig config)
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

            // Read and load tensors
            using (var stream = File.OpenRead(ggufPath))
            using (var reader = new GgufReader(stream))
            {
                // Re-read model info to position stream correctly
                reader.ReadModelInfo();

                foreach (var tensorInfo in modelInfo.Tensors)
                {
                    string ggufName = tensorInfo.Name;

                    // Skip if this tensor isn't in our mapping
                    if (!tensorMapping.ContainsKey(ggufName))
                    {
                        Console.WriteLine($"  Skipping unmapped tensor: {ggufName}");
                        continue;
                    }

                    string smName = tensorMapping[ggufName];

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
                        Console.WriteLine($"  Warning: No parameter found for {smName} (from {ggufName})");
                        continue;
                    }

                    // Copy weights with shape validation
                    CopyWeights(data, targetParam, ggufName, smName, tensorInfo.Dimensions);
                    loadedParams.Add(smName);
                }

                // Handle QKV merging
                qkvReads = MergeQKVWeights(reader, modelInfo, namedParams, tensorMapping, loadedParams);
            }

            // Handle weight tying (copy token embeddings to output if missing)
            HandleWeightTying(namedParams, loadedParams, config);

            // Report loading summary
            Console.WriteLine($"Loaded {loadedParams.Count} / {namedParams.Count} parameters");
            Console.WriteLine($"Tensor reads: {mainLoopReads} (main loop) + {qkvReads} (Q/K/V merge) = {mainLoopReads + qkvReads} total");
            Console.WriteLine($"Q/K/V tensors skipped in main loop: {qkvSkipped}");

            // Check for missing critical parameters
            var missingCritical = namedParams.Keys
                .Where(k => !loadedParams.Contains(k) && IsCriticalParameter(k))
                .ToList();

            if (missingCritical.Any())
            {
                Console.WriteLine("WARNING: Missing critical parameters:");
                foreach (var name in missingCritical)
                {
                    Console.WriteLine($"  - {name}");
                }
            }
        }

        /// <summary>
        /// Create mapping from GGUF tensor names to SmallMind parameter names.
        /// </summary>
        private static Dictionary<string, string> CreateTensorMapping(GgufModelInfo modelInfo, ModelConfig config)
        {
            var mapping = new Dictionary<string, string>();

            // Determine architecture naming convention
            bool isLlamaFamily = config.Architecture.StartsWith("llama") || 
                                config.Architecture.StartsWith("mistral") || 
                                config.Architecture.StartsWith("phi");

            if (isLlamaFamily)
            {
                // Token embeddings
                mapping["token_embd.weight"] = "token_embd.weight";

                // Output norm and head
                mapping["output_norm.weight"] = "output_norm.weight";
                
                // Output head (may not exist due to weight tying)
                if (modelInfo.Tensors.Any(t => t.Name == "output.weight"))
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

            return mapping;
        }

        /// <summary>
        /// Read and dequantize a GGUF tensor to float array.
        /// </summary>
        private static float[] ReadAndDequantizeTensor(GgufReader reader, GgufTensorInfo tensorInfo)
        {
            byte[] rawData = reader.ReadTensorData(tensorInfo.Offset, tensorInfo.Size);

            return tensorInfo.Type switch
            {
                GgufTensorType.F32 => ConvertF32Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.F16 => ConvertF16Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q8_0 => ConvertQ8_0Tensor(rawData, tensorInfo.Dimensions),
                GgufTensorType.Q4_0 => ConvertQ4_0Tensor(rawData, tensorInfo.Dimensions),
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

                    // Read 4-bit packed values (16 bytes = 32 values)
                    int blockStart = blockIdx * GgufBlockSize;
                    int blockEnd = Math.Min(blockStart + GgufBlockSize, totalElements);
                    
                    for (int i = blockStart; i < blockEnd; i += 2)
                    {
                        byte packedByte = br.ReadByte();
                        
                        // Low nibble
                        byte nibble0 = (byte)(packedByte & 0xF);
                        int q0 = Q4Tensor.DecodeNibble(nibble0);
                        floatData[i] = q0 * scale;
                        
                        // High nibble (if not last element)
                        if (i + 1 < blockEnd)
                        {
                            byte nibble1 = (byte)((packedByte >> 4) & 0xF);
                            int q1 = Q4Tensor.DecodeNibble(nibble1);
                            floatData[i + 1] = q1 * scale;
                        }
                    }
                }
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
            string ggufName, string smName, ulong[] ggufDims)
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
                        Console.WriteLine($"  {smName}: Transposing from ({ggufRows}, {ggufCols}) to ({targetRows}, {targetCols})");
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
            Console.WriteLine($"  {smName}: Loaded {sourceSize} elements from {ggufName}");
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
        /// Returns the count of Q/K/V tensor reads.
        /// </summary>
        private static int MergeQKVWeights(GgufReader reader, GgufModelInfo modelInfo, 
            Dictionary<string, Tensor> namedParams, 
            Dictionary<string, string> tensorMapping, HashSet<string> loadedParams)
        {
            int qkvReadsCount = 0;
            
            // Group Q/K/V tensors by layer
            var qkvGroups = new Dictionary<int, (GgufTensorInfo? q, GgufTensorInfo? k, GgufTensorInfo? v)>();

            foreach (var tensorInfo in modelInfo.Tensors)
            {
                string name = tensorInfo.Name;
                
                if (name.Contains(".attn_q.weight"))
                {
                    int layer = ExtractLayerIndex(name);
                    if (!qkvGroups.ContainsKey(layer))
                        qkvGroups[layer] = (null, null, null);
                    qkvGroups[layer] = (tensorInfo, qkvGroups[layer].k, qkvGroups[layer].v);
                }
                else if (name.Contains(".attn_k.weight"))
                {
                    int layer = ExtractLayerIndex(name);
                    if (!qkvGroups.ContainsKey(layer))
                        qkvGroups[layer] = (null, null, null);
                    qkvGroups[layer] = (qkvGroups[layer].q, tensorInfo, qkvGroups[layer].v);
                }
                else if (name.Contains(".attn_v.weight"))
                {
                    int layer = ExtractLayerIndex(name);
                    if (!qkvGroups.ContainsKey(layer))
                        qkvGroups[layer] = (null, null, null);
                    qkvGroups[layer] = (qkvGroups[layer].q, qkvGroups[layer].k, tensorInfo);
                }
            }

            // Merge each layer's Q/K/V
            foreach (var (layer, (q, k, v)) in qkvGroups)
            {
                if (q == null || k == null || v == null)
                {
                    Console.WriteLine($"  Warning: Incomplete Q/K/V set for layer {layer}");
                    continue;
                }

                string targetName = $"blk.{layer}.attn_qkv.weight";
                if (!namedParams.TryGetValue(targetName, out var targetParam))
                {
                    Console.WriteLine($"  Warning: No target parameter for {targetName}");
                    continue;
                }

                // Read Q/K/V tensors
                float[] qData = ReadAndDequantizeTensor(reader, q);
                float[] kData = ReadAndDequantizeTensor(reader, k);
                float[] vData = ReadAndDequantizeTensor(reader, v);
                qkvReadsCount += 3; // Count Q, K, V reads

                // Merge: [Q, K, V] concatenation
                // GGUF stores as (out, in) but we need to match SmallMind's layout
                int qSize = qData.Length;
                int kSize = kData.Length;
                int vSize = vData.Length;
                int totalSize = qSize + kSize + vSize;

                if (totalSize != targetParam.Size)
                {
                    throw new InvalidOperationException(
                        $"QKV merge size mismatch for layer {layer}:\n" +
                        $"  Q: {qSize}, K: {kSize}, V: {vSize}, Total: {totalSize}\n" +
                        $"  Target: {targetParam.Size}");
                }

                // Copy Q, then K, then V
                Array.Copy(qData, 0, targetParam.Data, 0, qSize);
                Array.Copy(kData, 0, targetParam.Data, qSize, kSize);
                Array.Copy(vData, 0, targetParam.Data, qSize + kSize, vSize);

                Console.WriteLine($"  blk.{layer}.attn_qkv.weight: Merged Q({qSize}) + K({kSize}) + V({vSize}) = {totalSize} elements");
                loadedParams.Add(targetName);
            }
            
            return qkvReadsCount;
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
            HashSet<string> loadedParams, ModelConfig config)
        {
            // Check if output.weight is loaded
            if (!loadedParams.Contains("output.weight") && namedParams.ContainsKey("output.weight"))
            {
                Console.WriteLine("  Applying weight tying: copying token_embd.weight to output.weight");
                
                var tokenEmbed = namedParams["token_embd.weight"];
                var outputWeight = namedParams["output.weight"];

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

            var tokenizer = GgufTokenizerExtractor.ExtractTokenizer(modelInfo.Metadata);
            if (tokenizer == null)
            {
                throw new NotSupportedException(
                    "Failed to extract tokenizer from GGUF file. " +
                    "Ensure the file contains tokenizer metadata.");
            }

            return tokenizer;
        }
    }
}

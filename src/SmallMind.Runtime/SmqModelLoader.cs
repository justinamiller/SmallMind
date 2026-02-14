using SmallMind.Abstractions.Telemetry;
using SmallMind.Core.Core;
using SmallMind.Quantization.IO.Smq;
using SmallMind.Quantization.Tensors;
using SmallMind.Runtime.Telemetry;
using SmallMind.Tokenizers;
using SmallMind.Tokenizers.Gguf;
using SmallMind.Transformers;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Loads models from SMQ format files.
    /// Currently dequantizes to FP32 for compatibility with TransformerModel.
    /// 
    /// NOTE: This loader dequantizes quantized tensors to FP32. For true quantized inference
    /// that preserves memory savings and uses fused kernels, future versions will support
    /// loading quantized weights directly via IWeightTensor.
    /// </summary>
    internal sealed class SmqModelLoader
    {
        /// <summary>
        /// Load a model from an SMQ file with dequantization to FP32.
        /// </summary>
        /// <param name="smqPath">Path to SMQ file</param>
        /// <param name="config">Model configuration</param>
        /// <param name="seed">Random seed for model initialization</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>Tuple of (model, tokenizer, metadata)</returns>
        public static (TransformerModel model, ITokenizer? tokenizer, Dictionary<string, object>? metadata) LoadFromSmq(
            string smqPath,
            ModelConfig config,
            int seed = 42,
            IInternalRuntimeLogger? logger = null)
        {
            logger ??= NullInternalRuntimeLogger.Instance;

            if (string.IsNullOrEmpty(smqPath))
                throw new ArgumentNullException(nameof(smqPath));
            if (!File.Exists(smqPath))
                throw new FileNotFoundException($"SMQ file not found: {smqPath}");

            logger.LogInfo($"Loading SMQ model from: {smqPath}");

            // Read SMQ file
            Dictionary<string, object> tensors;
            Dictionary<string, object>? metadata;
            
            using (var stream = File.OpenRead(smqPath))
            using (var reader = new SmqReader(stream))
            {
                reader.ReadHeader();
                metadata = reader.GetMetadata();
                tensors = reader.LoadAllTensors();
            }

            logger.LogInfo($"Loaded {tensors.Count} tensors from SMQ file");

            // Check for quantized tensors and warn
            bool hasQuantizedTensors = tensors.Values.Any(t => 
                t is Q8Tensor || t is Q4Tensor || t is Q4_1Tensor || t is Q5_0Tensor || 
                t is Q4KTensor || t is Q6KTensor);
            
            if (hasQuantizedTensors)
            {
                logger.LogWarning("SMQ file contains quantized tensors - dequantizing to FP32.");
                logger.LogWarning("For memory-efficient quantized inference, future versions will support IWeightTensor.");
            }

            // Create TransformerModel
            var model = new TransformerModel(
                vocabSize: config.VocabSize,
                blockSize: config.ContextLength,
                nEmbd: config.EmbeddingLength,
                nLayer: config.BlockCount,
                nHead: config.HeadCount,
                dropout: 0.0,
                seed: seed);

            // Load weights from SMQ tensors into model
            logger.LogInfo("Loading weights into model...");
            LoadWeights(tensors, model, config, logger);
            logger.LogInfo("Model loaded successfully.");

            // Extract tokenizer from metadata if present
            ITokenizer? tokenizer = null;
            if (metadata != null)
            {
                try
                {
                    var (extractedTokenizer, diagnostics) = GgufTokenizerFactory.CreateTokenizer(
                        metadata,
                        NullRuntimeLogger.Instance);
                    
                    if (extractedTokenizer != null)
                    {
                        tokenizer = extractedTokenizer;
                        logger.LogInfo($"Created tokenizer: {diagnostics.TokenizerType ?? "Unknown"}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning($"Failed to extract tokenizer from metadata: {ex.Message}");
                }
            }

            return (model, tokenizer, metadata);
        }

        /// <summary>
        /// Load weights from SMQ tensors into TransformerModel.
        /// Handles dequantization of quantized tensors.
        /// </summary>
        private static void LoadWeights(
            Dictionary<string, object> smqTensors,
            TransformerModel model,
            ModelConfig config,
            IInternalRuntimeLogger logger)
        {
            var namedParams = model.GetNamedParameters();
            var loadedParams = new HashSet<string>();

            // Track statistics
            int quantizedCount = 0;
            int fp32Count = 0;

            foreach (var kvp in smqTensors)
            {
                string smqName = kvp.Key;
                object smqTensor = kvp.Value;

                // Try to find corresponding parameter in model
                // SMQ names should match TransformerModel parameter names
                if (!namedParams.TryGetValue(smqName, out var targetParam))
                {
                    logger.LogDebug($"  Skipping SMQ tensor (no matching parameter): {smqName}");
                    continue;
                }

                // Dequantize if necessary and load into parameter
                float[] data = DequantizeTensor(smqTensor, smqName, logger, out bool wasQuantized);
                
                if (wasQuantized)
                    quantizedCount++;
                else
                    fp32Count++;

                // Validate shape matches
                if (data.Length != targetParam.Size)
                {
                    logger.LogWarning($"  Shape mismatch for {smqName}: SMQ has {data.Length} elements, model expects {targetParam.Size}");
                    continue;
                }

                // Copy data into model parameter
                Array.Copy(data, targetParam.Data, data.Length);
                loadedParams.Add(smqName);
            }

            logger.LogInfo($"Loaded {loadedParams.Count}/{namedParams.Count} parameters ({quantizedCount} quantized, {fp32Count} FP32)");

            // Report missing parameters
            var missingParams = namedParams.Keys.Except(loadedParams).ToList();
            if (missingParams.Count > 0)
            {
                logger.LogWarning($"{missingParams.Count} parameters not loaded from SMQ file:");
                int displayCount = Math.Min(10, missingParams.Count);
                for (int i = 0; i < displayCount; i++)
                {
                    logger.LogWarning($"  - {missingParams[i]}");
                }
                if (missingParams.Count > 10)
                {
                    logger.LogWarning($"  ... and {missingParams.Count - 10} more");
                }
            }
        }

        /// <summary>
        /// Dequantize a tensor to FP32 array.
        /// </summary>
        private static float[] DequantizeTensor(object tensor, string name, IInternalRuntimeLogger logger, out bool wasQuantized)
        {
            wasQuantized = false;

            if (tensor is Fp32Tensor fp32Tensor)
            {
                return fp32Tensor.Data;
            }
            else if (tensor is Q8Tensor q8Tensor)
            {
                wasQuantized = true;
                logger.LogDebug($"  Dequantizing Q8_0 tensor: {name}");
                return q8Tensor.Dequantize();
            }
            else if (tensor is Q4Tensor q4Tensor)
            {
                wasQuantized = true;
                logger.LogDebug($"  Dequantizing Q4_0 tensor: {name}");
                return q4Tensor.Dequantize();
            }
            else if (tensor is Q4_1Tensor q4_1Tensor)
            {
                wasQuantized = true;
                logger.LogDebug($"  Dequantizing Q4_1 tensor: {name}");
                return q4_1Tensor.Dequantize();
            }
            else if (tensor is Q5_0Tensor q5_0Tensor)
            {
                wasQuantized = true;
                logger.LogDebug($"  Dequantizing Q5_0 tensor: {name}");
                return q5_0Tensor.Dequantize();
            }
            else if (tensor is Q4KTensor q4kTensor)
            {
                wasQuantized = true;
                logger.LogDebug($"  Dequantizing Q4_K tensor: {name}");
                return q4kTensor.Dequantize();
            }
            else if (tensor is Q6KTensor q6kTensor)
            {
                wasQuantized = true;
                logger.LogDebug($"  Dequantizing Q6_K tensor: {name}");
                return q6kTensor.Dequantize();
            }
            else
            {
                throw new NotSupportedException($"Unsupported tensor type for {name}: {tensor.GetType().Name}");
            }
        }
    }
}

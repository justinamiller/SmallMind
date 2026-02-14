using SmallMind.Abstractions.Telemetry;
using SmallMind.Core.Core;
using SmallMind.Quantization.Abstractions;
using SmallMind.Quantization.IO.Smq;
using SmallMind.Quantization.Tensors;
using SmallMind.Runtime.Telemetry;
using SmallMind.Tokenizers;
using SmallMind.Tokenizers.Gguf;
using SmallMind.Transformers;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Loads models from SMQ format files with optional quantization preservation.
    /// When preserveQuantization is true, keeps weights in quantized format for memory-efficient inference.
    /// </summary>
    internal sealed class SmqModelLoader
    {
        /// <summary>
        /// Load a model from an SMQ file with optional quantization preservation.
        /// </summary>
        /// <param name="smqPath">Path to SMQ file</param>
        /// <param name="config">Model configuration</param>
        /// <param name="seed">Random seed for model initialization</param>
        /// <param name="preserveQuantization">If true, preserves quantized weights for fused kernel inference (default: true)</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>Tuple of (model, tokenizer, metadata)</returns>
        public static (TransformerModel model, ITokenizer? tokenizer, Dictionary<string, object>? metadata) LoadFromSmq(
            string smqPath,
            ModelConfig config,
            int seed = 42,
            bool preserveQuantization = true,
            IInternalRuntimeLogger? logger = null)
        {
            logger ??= NullInternalRuntimeLogger.Instance;

            if (string.IsNullOrEmpty(smqPath))
                throw new ArgumentNullException(nameof(smqPath));
            if (!File.Exists(smqPath))
                throw new FileNotFoundException($"SMQ file not found: {smqPath}");

            logger.LogInfo($"Loading SMQ model from: {smqPath} (preserveQuantization={preserveQuantization})");

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

            // Check for quantized tensors
            bool hasQuantizedTensors = tensors.Values.Any(t => 
                t is Q8Tensor || t is Q4Tensor || t is Q4_1Tensor || t is Q5_0Tensor || 
                t is Q4KTensor || t is Q6KTensor);
            
            if (hasQuantizedTensors)
            {
                if (preserveQuantization)
                {
                    logger.LogInfo("SMQ file contains quantized tensors - preserving for fused kernel inference.");
                }
                else
                {
                    logger.LogWarning("SMQ file contains quantized tensors - dequantizing to FP32 (preserveQuantization=false).");
                }
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
            LoadWeights(tensors, model, config, preserveQuantization, logger);
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
        /// Can preserve quantization for Linear layers or dequantize to FP32.
        /// </summary>
        private static void LoadWeights(
            Dictionary<string, object> smqTensors,
            TransformerModel model,
            ModelConfig config,
            bool preserveQuantization,
            IInternalRuntimeLogger logger)
        {
            var namedParams = model.GetNamedParameters();
            var loadedParams = new HashSet<string>();

            // Get all Linear layers from the model for quantized weight injection
            var linearLayers = preserveQuantization ? GetLinearLayers(model) : new Dictionary<string, Linear>();

            // Track statistics
            int quantizedPreservedCount = 0;
            int quantizedDequantizedCount = 0;
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

                // Check if this is a quantized tensor that can be preserved
                bool isQuantized = smqTensor is Q8Tensor || smqTensor is Q4Tensor || 
                                   smqTensor is Q4_1Tensor || smqTensor is Q5_0Tensor ||
                                   smqTensor is Q4KTensor || smqTensor is Q6KTensor;

                if (preserveQuantization && isQuantized && linearLayers.TryGetValue(smqName, out var linearLayer))
                {
                    // Try to inject quantized weight directly
                    if (TryInjectQuantizedWeight(smqTensor, linearLayer, smqName, logger))
                    {
                        quantizedPreservedCount++;
                        loadedParams.Add(smqName);
                        continue;
                    }
                }

                // Fall back to dequantization
                float[] data = DequantizeTensor(smqTensor, smqName, logger, out bool wasQuantized);
                
                if (wasQuantized)
                    quantizedDequantizedCount++;
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

            if (preserveQuantization && quantizedPreservedCount > 0)
            {
                logger.LogInfo($"Loaded {loadedParams.Count}/{namedParams.Count} parameters " +
                    $"({quantizedPreservedCount} quantized preserved, {quantizedDequantizedCount} quantized dequantized, {fp32Count} FP32)");
                logger.LogInfo($"Memory saved: ~{quantizedPreservedCount * 7.5:F1}x reduction on {quantizedPreservedCount} parameters");
            }
            else
            {
                logger.LogInfo($"Loaded {loadedParams.Count}/{namedParams.Count} parameters " +
                    $"({quantizedDequantizedCount} quantized dequantized, {fp32Count} FP32)");
            }

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
        /// Get all Linear layers from the model for quantized weight injection.
        /// </summary>
        private static Dictionary<string, Linear> GetLinearLayers(TransformerModel model)
        {
            var layers = new Dictionary<string, Linear>();
            var namedParams = model.GetNamedParameters();
            
            // Access model's internal structure to find Linear layers
            // This requires knowledge of TransformerModel's architecture
            // For now, we'll use reflection to find Linear layers
            var modelType = model.GetType();
            var fields = modelType.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Linear))
                {
                    var linear = field.GetValue(model) as Linear;
                    if (linear != null && linear.Weight != null)
                    {
                        // Try to match by parameter name
                        // Linear layers typically have "weight" parameter
                        foreach (var paramName in namedParams.Keys)
                        {
                            if (namedParams[paramName] == linear.Weight)
                            {
                                layers[paramName] = linear;
                                break;
                            }
                        }
                    }
                }
                else if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(List<>))
                {
                    // Check for List<TransformerBlock> or similar
                    var list = field.GetValue(model) as System.Collections.IEnumerable;
                    if (list != null)
                    {
                        foreach (var item in list)
                        {
                            ExtractLinearLayersFromObject(item, namedParams, layers);
                        }
                    }
                }
            }
            
            return layers;
        }

        /// <summary>
        /// Recursively extract Linear layers from an object (e.g., TransformerBlock).
        /// </summary>
        private static void ExtractLinearLayersFromObject(object obj, Dictionary<string, Tensor> namedParams, Dictionary<string, Linear> layers)
        {
            if (obj == null) return;
            
            var type = obj.GetType();
            var fields = type.GetFields(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(Linear))
                {
                    var linear = field.GetValue(obj) as Linear;
                    if (linear != null && linear.Weight != null)
                    {
                        foreach (var paramName in namedParams.Keys)
                        {
                            if (namedParams[paramName] == linear.Weight)
                            {
                                layers[paramName] = linear;
                                break;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Try to inject quantized weight into Linear layer.
        /// Returns true if successful, false if not supported.
        /// </summary>
        private static bool TryInjectQuantizedWeight(object smqTensor, Linear linearLayer, string name, IInternalRuntimeLogger logger)
        {
            try
            {
                IWeightTensor? weightTensor = CreateWeightTensor(smqTensor);
                
                if (weightTensor == null)
                {
                    logger.LogDebug($"  Cannot preserve quantization for {name}: unsupported tensor type");
                    return false;
                }

                linearLayer.SetQuantizedWeight(weightTensor);
                logger.LogDebug($"  Preserved quantized weight: {name} ({weightTensor.Scheme})");
                return true;
            }
            catch (Exception ex)
            {
                logger.LogWarning($"  Failed to inject quantized weight for {name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Create IWeightTensor from SMQ tensor.
        /// </summary>
        private static IWeightTensor? CreateWeightTensor(object smqTensor)
        {
            return smqTensor switch
            {
                Q4KTensor q4k => new Q4KWeightTensor(q4k),
                Q6KTensor q6k => new Q6KWeightTensor(q6k),
                Q8Tensor q8 => new Q8WeightTensor(q8),
                Q4Tensor q4 => new Q4WeightTensor(q4),
                Q4_1Tensor q4_1 => new Q4_1WeightTensor(q4_1),
                Q5_0Tensor q5_0 => new Q5_0WeightTensor(q5_0),
                Fp32Tensor fp32 => CreateF32WeightTensor(fp32),
                _ => null
            };
        }

        /// <summary>
        /// Create F32WeightTensor from Fp32Tensor.
        /// Assumes 2D matrix (rows, cols) for weight tensors.
        /// </summary>
        private static F32WeightTensor? CreateF32WeightTensor(Fp32Tensor fp32)
        {
            if (fp32.Rank != 2)
            {
                // Not a 2D matrix weight, skip
                return null;
            }
            
            int rows = fp32.Dimensions[0];
            int cols = fp32.Dimensions[1];
            return new F32WeightTensor(fp32.Data, rows, cols);
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

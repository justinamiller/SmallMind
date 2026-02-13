using SmallMind.Core.Utilities;
using SmallMind.Core.Validation;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Utilities and validation for billion-parameter model support.
    /// Provides memory estimation, configuration validation, and scale recommendations.
    /// </summary>
    internal static class LargeModelSupport
    {
        /// <summary>
        /// Maximum safe parameters for in-memory FP32 models (int32 limit consideration).
        /// </summary>
        public const long MAX_SAFE_PARAMS_FP32 = 2_000_000_000L; // 2B parameters (~8GB)

        /// <summary>
        /// Recommended threshold for using quantization.
        /// </summary>
        public const long QUANTIZATION_THRESHOLD = 500_000_000L; // 500M parameters

        /// <summary>
        /// Recommended threshold for model sharding.
        /// </summary>
        public const long SHARDING_THRESHOLD = 1_000_000_000L; // 1B parameters

        /// <summary>
        /// Calculate total parameter count for a transformer model configuration.
        /// Uses long arithmetic to support billion-parameter models.
        /// </summary>
        public static long CalculateParameterCount(
            int vocabSize,
            int blockSize,
            int embeddingDim,
            int numLayers,
            int numHeads,
            int ffnMultiplier = 4)
        {
            Guard.GreaterThan(vocabSize, 0, nameof(vocabSize));
            Guard.GreaterThan(blockSize, 0, nameof(blockSize));
            Guard.GreaterThan(embeddingDim, 0, nameof(embeddingDim));
            Guard.GreaterThan(numLayers, 0, nameof(numLayers));
            Guard.GreaterThan(numHeads, 0, nameof(numHeads));
            Guard.GreaterThan(ffnMultiplier, 0, nameof(ffnMultiplier));

            if (embeddingDim % numHeads != 0)
            {
                throw new Exceptions.ValidationException(
                    $"Embedding dimension {embeddingDim} must be divisible by number of heads {numHeads}",
                    nameof(embeddingDim));
            }

            long totalParams = 0;

            // Token embedding: vocab_size × embed_dim
            totalParams += (long)vocabSize * embeddingDim;

            // Position embedding: block_size × embed_dim
            totalParams += (long)blockSize * embeddingDim;

            // Per-layer parameters
            for (int layer = 0; layer < numLayers; layer++)
            {
                // Multi-head attention:
                // - Q, K, V projections: 3 × (embed_dim × embed_dim)
                // - Output projection: embed_dim × embed_dim
                long attentionParams = 4L * embeddingDim * embeddingDim;

                // Layer norm 1 (2 × embed_dim for gamma and beta)
                long layerNorm1Params = 2L * embeddingDim;

                // Feed-forward network:
                // - Up projection: embed_dim × (ffn_multiplier × embed_dim)
                // - Down projection: (ffn_multiplier × embed_dim) × embed_dim
                long ffnHiddenDim = (long)ffnMultiplier * embeddingDim;
                long ffnParams = embeddingDim * ffnHiddenDim + ffnHiddenDim * embeddingDim;

                // Layer norm 2
                long layerNorm2Params = 2L * embeddingDim;

                totalParams += attentionParams + layerNorm1Params + ffnParams + layerNorm2Params;
            }

            // Final layer norm
            totalParams += 2L * embeddingDim;

            // LM head (output projection): embed_dim × vocab_size
            totalParams += (long)embeddingDim * vocabSize;

            return totalParams;
        }

        /// <summary>
        /// Estimate memory requirements in bytes for a model configuration.
        /// </summary>
        /// <param name="parameterCount">Total parameter count.</param>
        /// <param name="bytesPerParam">Bytes per parameter (4 for FP32, 2 for FP16, 1 for Q8, 0.5 for Q4).</param>
        /// <param name="includeGradients">Include memory for gradients (training).</param>
        /// <param name="includeOptimizer">Include memory for optimizer state (Adam: 2x params).</param>
        /// <returns>Estimated memory in bytes.</returns>
        public static long EstimateMemoryBytes(
            long parameterCount,
            double bytesPerParam = 4.0, // FP32 default
            bool includeGradients = false,
            bool includeOptimizer = false)
        {
            Guard.GreaterThan(parameterCount, 0L, nameof(parameterCount));
            Guard.GreaterThan(bytesPerParam, 0.0, nameof(bytesPerParam));

            long memory = (long)(parameterCount * bytesPerParam);

            if (includeGradients)
            {
                // Gradients are typically FP32
                memory += parameterCount * 4L;
            }

            if (includeOptimizer)
            {
                // Adam optimizer: 2x parameter count (momentum + variance in FP32)
                memory += parameterCount * 4L * 2L;
            }

            return memory;
        }

        /// <summary>
        /// Get recommended configuration for large models.
        /// </summary>
        /// <param name="parameterCount">Target parameter count.</param>
        /// <returns>Recommendation message.</returns>
        public static string GetRecommendation(long parameterCount)
        {
            if (parameterCount < QUANTIZATION_THRESHOLD)
            {
                return $"✓ Model size ({FormatParameters(parameterCount)}) is suitable for FP32 inference.";
            }
            else if (parameterCount < SHARDING_THRESHOLD)
            {
                return $"⚠ Model size ({FormatParameters(parameterCount)}): Recommend Q8 or Q4 quantization for memory efficiency.\n" +
                       $"  Expected memory: FP32={ByteSizeFormatter.FormatBytes(EstimateMemoryBytes(parameterCount, 4.0))}, " +
                       $"Q8={ByteSizeFormatter.FormatBytes(EstimateMemoryBytes(parameterCount, 1.0))}, " +
                       $"Q4={ByteSizeFormatter.FormatBytes(EstimateMemoryBytes(parameterCount, 0.5))}";
            }
            else if (parameterCount < MAX_SAFE_PARAMS_FP32)
            {
                return $"⚠ Large model ({FormatParameters(parameterCount)}): REQUIRES quantization (Q8/Q4) to avoid memory overflow.\n" +
                       $"  Expected memory: Q8={ByteSizeFormatter.FormatBytes(EstimateMemoryBytes(parameterCount, 1.0))}, " +
                       $"Q4={ByteSizeFormatter.FormatBytes(EstimateMemoryBytes(parameterCount, 0.5))}\n" +
                       $"  Note: FP32 ({ByteSizeFormatter.FormatBytes(EstimateMemoryBytes(parameterCount, 4.0))}) may exceed available RAM.";
            }
            else
            {
                return $"❌ Very large model ({FormatParameters(parameterCount)}): Exceeds safe single-tensor limits.\n" +
                       $"  Required: Model sharding + Q4 quantization.\n" +
                       $"  Expected memory with Q4: {ByteSizeFormatter.FormatBytes(EstimateMemoryBytes(parameterCount, 0.5))}\n" +
                       $"  Consider using specialized frameworks (LLaMA.cpp, vLLM) for models > 2B parameters.";
            }
        }

        /// <summary>
        /// Validate if a model configuration can be safely loaded.
        /// </summary>
        /// <param name="vocabSize">Vocabulary size.</param>
        /// <param name="blockSize">Maximum sequence length.</param>
        /// <param name="embeddingDim">Embedding dimension.</param>
        /// <param name="numLayers">Number of layers.</param>
        /// <param name="numHeads">Number of attention heads.</param>
        /// <param name="availableMemoryBytes">Available system memory in bytes (0 = no limit).</param>
        /// <param name="quantizationBits">Quantization bits (32, 16, 8, or 4).</param>
        /// <exception cref="Exceptions.ValidationException">Thrown if configuration exceeds limits.</exception>
        public static void ValidateConfiguration(
            int vocabSize,
            int blockSize,
            int embeddingDim,
            int numLayers,
            int numHeads,
            long availableMemoryBytes = 0,
            int quantizationBits = 32)
        {
            long paramCount = CalculateParameterCount(vocabSize, blockSize, embeddingDim, numLayers, numHeads);

            // Check tensor size limits
            long maxTensorSize = (long)vocabSize * embeddingDim;
            if (maxTensorSize > int.MaxValue)
            {
                throw new Exceptions.ValidationException(
                    $"Embedding tensor size ({vocabSize} × {embeddingDim} = {maxTensorSize:N0}) exceeds int32 limit ({int.MaxValue:N0}). " +
                    $"Reduce vocab_size or embedding_dim, or implement tensor sharding.",
                    nameof(vocabSize));
            }

            // Check memory if available
            if (availableMemoryBytes > 0)
            {
                double bytesPerParam = quantizationBits / 8.0;
                long requiredMemory = EstimateMemoryBytes(paramCount, bytesPerParam);

                if (requiredMemory > availableMemoryBytes)
                {
                    throw new Exceptions.ValidationException(
                        $"Model requires {ByteSizeFormatter.FormatBytes(requiredMemory)} but only {ByteSizeFormatter.FormatBytes(availableMemoryBytes)} available. " +
                        $"Consider using stronger quantization or reducing model size.",
                        nameof(availableMemoryBytes));
                }
            }
        }

        /// <summary>
        /// Format parameter count in human-readable form.
        /// </summary>
        public static string FormatParameters(long count)
        {
            if (count >= 1_000_000_000)
                return $"{count / 1_000_000_000.0:F2}B";
            else if (count >= 1_000_000)
                return $"{count / 1_000_000.0:F1}M";
            else if (count >= 1_000)
                return $"{count / 1_000.0:F1}K";
            else
                return count.ToString();
        }


    }
}

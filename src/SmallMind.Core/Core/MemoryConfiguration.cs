using System;
using SmallMind.Core.Validation;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Memory configuration and optimization settings for transformer models.
    /// Dynamically adjusts token limits based on available system memory.
    /// Supports both advisory and strict memory budgeting modes.
    /// </summary>
    public class MemoryConfiguration
    {
        /// <summary>
        /// Default maximum context size for systems with 16GB RAM.
        /// </summary>
        public const int DEFAULT_MAX_TOKENS_16GB = 32768; // 32k tokens

        /// <summary>
        /// Maximum context size for systems with 32GB RAM.
        /// </summary>
        public const int DEFAULT_MAX_TOKENS_32GB = 49152; // 48k tokens

        /// <summary>
        /// Maximum context size for systems with 64GB RAM.
        /// </summary>
        public const int DEFAULT_MAX_TOKENS_64GB = 65536; // 64k tokens

        /// <summary>
        /// Maximum context size for systems with 128GB+ RAM.
        /// </summary>
        public const int DEFAULT_MAX_TOKENS_128GB = 131072; // 128k tokens

        /// <summary>
        /// Minimum safe context size.
        /// </summary>
        public const int MIN_SAFE_TOKENS = 2048;

        private int _maxContextTokens;
        private readonly long _systemMemoryBytes;
        private readonly bool _enableGradientCheckpointing;
        private readonly bool _enableMixedPrecision;
        private readonly bool _enableMemoryMapping;
        private readonly int _checkpointInterval;
        private readonly int _slidingWindowSize;
        private readonly int _slidingWindowStride;
        
        // Strict memory budgeting (optional)
        private readonly StrictMemoryBudget? _strictBudget;

        /// <summary>
        /// Gets the maximum context size in tokens.
        /// </summary>
        public int MaxContextTokens => _maxContextTokens;

        /// <summary>
        /// Gets the total available system memory in bytes.
        /// </summary>
        public long SystemMemoryBytes => _systemMemoryBytes;

        /// <summary>
        /// Gets the total available system memory in GB.
        /// </summary>
        public double SystemMemoryGB => _systemMemoryBytes / (1024.0 * 1024.0 * 1024.0);

        /// <summary>
        /// Gets whether gradient checkpointing is enabled.
        /// </summary>
        public bool EnableGradientCheckpointing => _enableGradientCheckpointing;

        /// <summary>
        /// Gets whether mixed precision training is enabled.
        /// </summary>
        public bool EnableMixedPrecision => _enableMixedPrecision;

        /// <summary>
        /// Gets whether memory-mapped KV cache is enabled.
        /// </summary>
        public bool EnableMemoryMapping => _enableMemoryMapping;

        /// <summary>
        /// Gets the gradient checkpoint interval.
        /// </summary>
        public int CheckpointInterval => _checkpointInterval;

        /// <summary>
        /// Gets the sliding window size for large context processing.
        /// </summary>
        public int SlidingWindowSize => _slidingWindowSize;

        /// <summary>
        /// Gets the sliding window stride.
        /// </summary>
        public int SlidingWindowStride => _slidingWindowStride;

        /// <summary>
        /// Gets whether strict memory budgeting is enabled.
        /// </summary>
        public bool IsStrictMode => _strictBudget != null;

        /// <summary>
        /// Gets the strict memory budget (if enabled).
        /// </summary>
        public StrictMemoryBudget? StrictBudget => _strictBudget;

        /// <summary>
        /// Creates a memory configuration with automatic detection of system memory.
        /// </summary>
        /// <param name="enableGradientCheckpointing">Enable gradient checkpointing for memory savings.</param>
        /// <param name="enableMixedPrecision">Enable mixed precision (FP16/FP32) training.</param>
        /// <param name="enableMemoryMapping">Enable memory-mapped KV cache.</param>
        /// <param name="checkpointInterval">Interval for gradient checkpoints (e.g., 2 = every 2nd layer).</param>
        /// <param name="customMaxTokens">Custom max tokens override (0 = auto-detect based on RAM).</param>
        /// <param name="strictBudget">Optional strict memory budget for hard limits.</param>
        public MemoryConfiguration(
            bool enableGradientCheckpointing = true,
            bool enableMixedPrecision = false,
            bool enableMemoryMapping = false,
            int checkpointInterval = 2,
            int customMaxTokens = 0,
            StrictMemoryBudget? strictBudget = null)
        {
            Guard.GreaterThanOrEqualTo(checkpointInterval, 1, nameof(checkpointInterval));

            // Get available system memory
            _systemMemoryBytes = GetAvailableMemoryBytes();

            _enableGradientCheckpointing = enableGradientCheckpointing;
            _enableMixedPrecision = enableMixedPrecision;
            _enableMemoryMapping = enableMemoryMapping;
            _checkpointInterval = checkpointInterval;
            _strictBudget = strictBudget;

            // Auto-configure max tokens based on available RAM
            if (customMaxTokens > 0)
            {
                _maxContextTokens = customMaxTokens;
            }
            else
            {
                _maxContextTokens = CalculateMaxTokens(_systemMemoryBytes);
            }

            // Configure sliding window based on max context tokens
            // Use 1/8 of max tokens for window size with 50% overlap
            _slidingWindowSize = Math.Max(2048, _maxContextTokens / 8);
            _slidingWindowStride = _slidingWindowSize / 2;
        }

        /// <summary>
        /// Creates a memory configuration with explicit memory amount.
        /// </summary>
        /// <param name="memoryGB">Available memory in gigabytes.</param>
        /// <param name="enableGradientCheckpointing">Enable gradient checkpointing.</param>
        /// <param name="enableMixedPrecision">Enable mixed precision training.</param>
        /// <param name="enableMemoryMapping">Enable memory-mapped KV cache.</param>
        /// <param name="checkpointInterval">Interval for gradient checkpoints.</param>
        /// <param name="strictBudget">Optional strict memory budget for hard limits.</param>
        public MemoryConfiguration(
            double memoryGB,
            bool enableGradientCheckpointing = true,
            bool enableMixedPrecision = false,
            bool enableMemoryMapping = false,
            int checkpointInterval = 2,
            StrictMemoryBudget? strictBudget = null)
        {
            Guard.GreaterThan(memoryGB, 0.0, nameof(memoryGB));
            Guard.GreaterThanOrEqualTo(checkpointInterval, 1, nameof(checkpointInterval));

            _systemMemoryBytes = (long)(memoryGB * 1024 * 1024 * 1024);
            _enableGradientCheckpointing = enableGradientCheckpointing;
            _enableMixedPrecision = enableMixedPrecision;
            _enableMemoryMapping = enableMemoryMapping;
            _checkpointInterval = checkpointInterval;
            _strictBudget = strictBudget;

            _maxContextTokens = CalculateMaxTokens(_systemMemoryBytes);

            _slidingWindowSize = Math.Max(2048, _maxContextTokens / 8);
            _slidingWindowStride = _slidingWindowSize / 2;
        }

        /// <summary>
        /// Get available system memory in bytes.
        /// </summary>
        private static long GetAvailableMemoryBytes()
        {
            try
            {
                // Use GC memory info to get total available memory
                var gcMemInfo = GC.GetGCMemoryInfo();
                return gcMemInfo.TotalAvailableMemoryBytes;
            }
            catch
            {
                // Fallback: assume 16GB if detection fails
                return 16L * 1024 * 1024 * 1024;
            }
        }

        /// <summary>
        /// Calculate maximum token context size based on available memory.
        /// </summary>
        private static int CalculateMaxTokens(long memoryBytes)
        {
            double memoryGB = memoryBytes / (1024.0 * 1024.0 * 1024.0);

            if (memoryGB >= 128)
            {
                return DEFAULT_MAX_TOKENS_128GB;
            }
            else if (memoryGB >= 64)
            {
                return DEFAULT_MAX_TOKENS_64GB;
            }
            else if (memoryGB >= 32)
            {
                return DEFAULT_MAX_TOKENS_32GB;
            }
            else if (memoryGB >= 16)
            {
                return DEFAULT_MAX_TOKENS_16GB;
            }
            else
            {
                // For systems with less than 16GB, scale linearly
                // At 8GB, use 8k tokens; at 4GB, use 2k tokens (minimum)
                int tokens = (int)(memoryGB * 2048);
                return Math.Max(MIN_SAFE_TOKENS, tokens);
            }
        }

        /// <summary>
        /// Update the maximum context tokens.
        /// </summary>
        /// <param name="maxTokens">New maximum token count.</param>
        public void SetMaxContextTokens(int maxTokens)
        {
            Guard.GreaterThan(maxTokens, 0, nameof(maxTokens));
            _maxContextTokens = maxTokens;
        }

        /// <summary>
        /// Estimate memory usage for a given model configuration.
        /// Uses LargeModelSupport for billion-parameter model calculations.
        /// Returns detailed breakdown for budget checking.
        /// </summary>
        /// <param name="vocabSize">Vocabulary size.</param>
        /// <param name="embeddingDim">Embedding dimension.</param>
        /// <param name="numLayers">Number of transformer layers.</param>
        /// <param name="numHeads">Number of attention heads.</param>
        /// <param name="batchSize">Batch size.</param>
        /// <param name="seqLength">Sequence length.</param>
        /// <returns>Memory breakdown with detailed component sizes.</returns>
        public MemoryBreakdown EstimateMemoryBreakdown(
            int vocabSize,
            int embeddingDim,
            int numLayers,
            int numHeads,
            int batchSize,
            int seqLength)
        {
            Guard.GreaterThan(vocabSize, 0, nameof(vocabSize));
            Guard.GreaterThan(embeddingDim, 0, nameof(embeddingDim));
            Guard.GreaterThan(numLayers, 0, nameof(numLayers));
            Guard.GreaterThan(numHeads, 0, nameof(numHeads));
            Guard.GreaterThan(batchSize, 0, nameof(batchSize));
            Guard.GreaterThan(seqLength, 0, nameof(seqLength));

            // Calculate total parameters using large model support
            long totalParams = LargeModelSupport.CalculateParameterCount(
                vocabSize, seqLength, embeddingDim, numLayers, numHeads);

            // Model parameters in FP32 or FP16
            int bytesPerParam = _enableMixedPrecision ? 2 : 4;
            long modelMemory = totalParams * bytesPerParam;

            // Activations during forward pass
            long activationsPerLayer = (long)batchSize * seqLength * embeddingDim;
            long attentionScores = (long)batchSize * numHeads * seqLength * seqLength;

            int numStoredLayers = _enableGradientCheckpointing 
                ? (numLayers + _checkpointInterval - 1) / _checkpointInterval 
                : numLayers;

            long activationsMemory = numStoredLayers * activationsPerLayer * 4; // FP32 for activations
            activationsMemory += numStoredLayers * attentionScores * 4; // Attention scores

            // KV cache (if not using memory mapping)
            long kvCacheMemory = 0;
            if (!_enableMemoryMapping)
            {
                long kvCachePerLayer = (long)batchSize * seqLength * embeddingDim * 2; // K and V
                kvCacheMemory = numLayers * kvCachePerLayer * 4;
            }

            // Gradients (same size as parameters during training)
            long gradientsMemory = totalParams * 4; // Gradients always in FP32

            // Optimizer state (Adam: 2x parameters for momentum and variance)
            long optimizerMemory = totalParams * 4 * 2; // Adam state in FP32

            // Overhead (buffers, temporary allocations, etc.)
            long overhead = (long)((modelMemory + activationsMemory) * 0.1); // 10% overhead

            return new MemoryBreakdown
            {
                ModelParametersBytes = modelMemory,
                ActivationsBytes = activationsMemory,
                KVCacheBytes = kvCacheMemory,
                GradientsBytes = gradientsMemory,
                OptimizerStateBytes = optimizerMemory,
                OverheadBytes = overhead
            };
        }

        /// <summary>
        /// Estimate memory usage for a given model configuration (legacy method).
        /// </summary>
        /// <returns>Total estimated memory in bytes.</returns>
        public long EstimateMemoryUsage(
            int vocabSize,
            int embeddingDim,
            int numLayers,
            int numHeads,
            int batchSize,
            int seqLength)
        {
            var breakdown = EstimateMemoryBreakdown(
                vocabSize, embeddingDim, numLayers, numHeads, batchSize, seqLength);
            return breakdown.TotalBytes;
        }

        /// <summary>
        /// Performs a pre-flight check before running a model.
        /// Validates memory requirements against budget constraints.
        /// </summary>
        /// <param name="vocabSize">Vocabulary size.</param>
        /// <param name="embeddingDim">Embedding dimension.</param>
        /// <param name="numLayers">Number of transformer layers.</param>
        /// <param name="numHeads">Number of attention heads.</param>
        /// <param name="batchSize">Batch size.</param>
        /// <param name="seqLength">Sequence length.</param>
        /// <returns>Budget check result indicating whether operation can proceed.</returns>
        public BudgetCheckResult CheckBeforeRun(
            int vocabSize,
            int embeddingDim,
            int numLayers,
            int numHeads,
            int batchSize,
            int seqLength)
        {
            var breakdown = EstimateMemoryBreakdown(
                vocabSize, embeddingDim, numLayers, numHeads, batchSize, seqLength);

            // If strict budget is enabled, use it for checking
            if (_strictBudget != null)
            {
                return _strictBudget.CheckBudget(breakdown, isSessionAllocation: false);
            }

            // Otherwise, use advisory limits (80% of available memory)
            long available = (long)(_systemMemoryBytes * 0.8);
            
            if (breakdown.TotalBytes <= available)
            {
                return BudgetCheckResult.Success(breakdown.TotalBytes, available, breakdown);
            }
            else
            {
                var reason = $"Estimated {breakdown.TotalBytes / 1024.0 / 1024.0:F2}MB exceeds " +
                            $"available memory of {available / 1024.0 / 1024.0:F2}MB (80% of system)";
                return BudgetCheckResult.Failure(breakdown.TotalBytes, available, reason, breakdown);
            }
        }

        /// <summary>
        /// Check if the current configuration can fit within available memory.
        /// </summary>
        /// <param name="vocabSize">Vocabulary size.</param>
        /// <param name="embeddingDim">Embedding dimension.</param>
        /// <param name="numLayers">Number of transformer layers.</param>
        /// <param name="numHeads">Number of attention heads.</param>
        /// <param name="batchSize">Batch size.</param>
        /// <param name="seqLength">Sequence length.</param>
        /// <returns>True if configuration fits in memory.</returns>
        public bool CanFitInMemory(
            int vocabSize,
            int embeddingDim,
            int numLayers,
            int numHeads,
            int batchSize,
            int seqLength)
        {
            long required = EstimateMemoryUsage(vocabSize, embeddingDim, numLayers, numHeads, batchSize, seqLength);
            
            // Use 80% of available memory as safe threshold
            long available = (long)(_systemMemoryBytes * 0.8);
            
            return required <= available;
        }

        /// <summary>
        /// Get a summary of the memory configuration.
        /// </summary>
        public string GetSummary()
        {
            var summary = $"Memory Configuration:\n" +
                   $"  System Memory: {SystemMemoryGB:F2} GB\n" +
                   $"  Max Context Tokens: {MaxContextTokens:N0}\n" +
                   $"  Gradient Checkpointing: {(EnableGradientCheckpointing ? "Enabled" : "Disabled")} " +
                   $"(Interval: {CheckpointInterval})\n" +
                   $"  Mixed Precision: {(EnableMixedPrecision ? "Enabled (FP16/FP32)" : "Disabled (FP32)")}\n" +
                   $"  Memory Mapping: {(EnableMemoryMapping ? "Enabled" : "Disabled")}\n" +
                   $"  Sliding Window: {SlidingWindowSize} tokens (Stride: {SlidingWindowStride})\n" +
                   $"  Strict Mode: {(IsStrictMode ? "Enabled" : "Disabled")}";
            
            if (_strictBudget != null)
            {
                summary += $"\n\n{_strictBudget.GetSummary()}";
            }
            
            return summary;
        }
    }
}

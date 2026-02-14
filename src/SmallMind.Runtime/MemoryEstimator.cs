namespace SmallMind.Runtime
{
    /// <summary>
    /// Utilities for estimating memory usage of inference operations.
    /// Provides rough estimates for capacity planning without running actual inference.
    /// </summary>
    internal static class MemoryEstimator
    {
        private const int FloatSize = sizeof(float);
        private const int IntSize = sizeof(int);

        /// <summary>
        /// Estimate memory usage for a single inference session.
        /// Includes model parameters, KV cache, and temporary buffers.
        /// </summary>
        /// <param name="modelParams">Number of model parameters (floats)</param>
        /// <param name="options">Inference options</param>
        /// <param name="nEmbd">Embedding dimension</param>
        /// <param name="nLayer">Number of layers</param>
        /// <param name="nHead">Number of attention heads</param>
        /// <returns>Estimated bytes required</returns>
        public static long EstimateSessionBytes(
            long modelParams,
            ProductionInferenceOptions options,
            int nEmbd,
            int nLayer,
            int nHead)
        {
            if (modelParams <= 0)
                throw new ArgumentOutOfRangeException(nameof(modelParams));
            if (nEmbd <= 0)
                throw new ArgumentOutOfRangeException(nameof(nEmbd));
            if (nLayer <= 0)
                throw new ArgumentOutOfRangeException(nameof(nLayer));
            if (nHead <= 0)
                throw new ArgumentOutOfRangeException(nameof(nHead));

            options?.Validate();

            // Model parameters (shared across sessions, counted once)
            long modelBytes = modelParams * FloatSize;

            // KV cache per session
            // Each layer stores key and value for each position
            int maxContext = options?.MaxContextTokens ?? 2048;
            int headDim = nEmbd / nHead;

            // Per layer: 2 caches (K and V) * max_context * num_heads * head_dim
            long kvCachePerLayer = 2L * maxContext * nHead * headDim * FloatSize;
            long totalKvCache = kvCachePerLayer * nLayer;

            // Working memory (logits, activations, temp buffers)
            // Rough estimate: ~3x embedding dimension for activations per token
            int vocabSize = 256; // Rough estimate, can be passed as parameter
            long workingMemory = (long)maxContext * nEmbd * 3 * FloatSize;

            // Logits buffer (vocab_size * float)
            long logitsBuffer = (long)vocabSize * FloatSize;

            // Probability buffer for sampling
            long probsBuffer = (long)vocabSize * FloatSize;

            // Context token buffer
            long contextBuffer = (long)maxContext * IntSize;

            long totalBytes = modelBytes + totalKvCache + workingMemory + logitsBuffer + probsBuffer + contextBuffer;

            return totalBytes;
        }

        /// <summary>
        /// Estimate memory for the engine with concurrent sessions.
        /// Model weights are shared, but each session has its own state.
        /// </summary>
        /// <param name="modelParams">Number of model parameters</param>
        /// <param name="options">Inference options per session</param>
        /// <param name="nEmbd">Embedding dimension</param>
        /// <param name="nLayer">Number of layers</param>
        /// <param name="nHead">Number of attention heads</param>
        /// <param name="maxConcurrentSessions">Maximum concurrent sessions</param>
        /// <returns>Estimated total bytes required</returns>
        public static long EstimateEngineBytes(
            long modelParams,
            ProductionInferenceOptions options,
            int nEmbd,
            int nLayer,
            int nHead,
            int maxConcurrentSessions)
        {
            if (maxConcurrentSessions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxConcurrentSessions));

            // Model weights (shared, counted once)
            long modelBytes = modelParams * FloatSize;

            // Per-session state (not including model)
            long sessionBytes = EstimateSessionBytes(modelParams, options, nEmbd, nLayer, nHead) - modelBytes;

            // Total = model + (per-session state * num sessions)
            return modelBytes + (sessionBytes * maxConcurrentSessions);
        }

        /// <summary>
        /// Estimate KV cache size only.
        /// Useful for understanding cache memory requirements.
        /// </summary>
        /// <param name="maxContextTokens">Maximum context length</param>
        /// <param name="nEmbd">Embedding dimension</param>
        /// <param name="nLayer">Number of layers</param>
        /// <param name="nHead">Number of attention heads</param>
        /// <returns>Estimated bytes for KV cache</returns>
        public static long EstimateKvCacheBytes(
            int maxContextTokens,
            int nEmbd,
            int nLayer,
            int nHead)
        {
            if (maxContextTokens <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxContextTokens));
            if (nEmbd <= 0)
                throw new ArgumentOutOfRangeException(nameof(nEmbd));
            if (nLayer <= 0)
                throw new ArgumentOutOfRangeException(nameof(nLayer));
            if (nHead <= 0)
                throw new ArgumentOutOfRangeException(nameof(nHead));

            int headDim = nEmbd / nHead;

            // 2 caches (K and V) per layer
            long cachePerLayer = 2L * maxContextTokens * nHead * headDim * FloatSize;
            return cachePerLayer * nLayer;
        }

    }
}

namespace SmallMind.Runtime.Execution
{
    /// <summary>
    /// Result of a prefill operation (prompt processing).
    /// Contains logits for the last token and the populated KV cache handle.
    /// </summary>
    internal readonly struct PrefillResult
    {
        /// <summary>
        /// Logits for the last token in the prompt (vocab_size).
        /// Used for sampling the first generated token.
        /// </summary>
        public readonly ReadOnlyMemory<float> Logits;

        /// <summary>
        /// Handle to the KV cache populated during prefill.
        /// Must be reused for subsequent decode steps.
        /// </summary>
        public readonly KvCacheHandle CacheHandle;

        /// <summary>
        /// Number of tokens processed during prefill.
        /// </summary>
        public readonly int ProcessedTokens;

        /// <summary>
        /// Metrics captured during prefill.
        /// </summary>
        public readonly PrefillMetrics Metrics;

        public PrefillResult(
            ReadOnlyMemory<float> logits,
            KvCacheHandle cacheHandle,
            int processedTokens,
            PrefillMetrics metrics)
        {
            Logits = logits;
            CacheHandle = cacheHandle;
            ProcessedTokens = processedTokens;
            Metrics = metrics;
        }
    }

    /// <summary>
    /// Result of a decode operation (single token generation).
    /// Contains logits for sampling the next token and the updated KV cache handle.
    /// </summary>
    internal readonly struct DecodeResult
    {
        /// <summary>
        /// Logits for the next token (vocab_size).
        /// Used for sampling the next token in the sequence.
        /// </summary>
        public readonly ReadOnlyMemory<float> Logits;

        /// <summary>
        /// Handle to the updated KV cache.
        /// Contains all previous tokens plus the newly decoded token.
        /// </summary>
        public readonly KvCacheHandle CacheHandle;

        /// <summary>
        /// Metrics captured during this decode step.
        /// </summary>
        public readonly DecodeMetrics Metrics;

        public DecodeResult(
            ReadOnlyMemory<float> logits,
            KvCacheHandle cacheHandle,
            DecodeMetrics metrics)
        {
            Logits = logits;
            CacheHandle = cacheHandle;
            Metrics = metrics;
        }
    }
}

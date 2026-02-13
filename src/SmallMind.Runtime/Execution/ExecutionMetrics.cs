namespace SmallMind.Runtime.Execution
{
    /// <summary>
    /// Metrics captured during prefill phase (prompt processing).
    /// Tracks timing and throughput for initial context population.
    /// </summary>
    internal readonly struct PrefillMetrics
    {
        /// <summary>
        /// Number of tokens processed during prefill.
        /// </summary>
        public readonly int TokenCount;

        /// <summary>
        /// Time taken for prefill in milliseconds.
        /// </summary>
        public readonly double ElapsedMs;

        /// <summary>
        /// Tokens processed per second during prefill.
        /// </summary>
        public readonly double TokensPerSecond;

        public PrefillMetrics(int tokenCount, double elapsedMs)
        {
            TokenCount = tokenCount;
            ElapsedMs = elapsedMs;
            TokensPerSecond = elapsedMs > 0 ? tokenCount / (elapsedMs / 1000.0) : 0.0;
        }
    }

    /// <summary>
    /// Metrics captured during decode phase (single token generation).
    /// Tracks per-token latency for autoregressive generation.
    /// </summary>
    internal readonly struct DecodeMetrics
    {
        /// <summary>
        /// Time taken for this decode step in milliseconds.
        /// </summary>
        public readonly double ElapsedMs;

        /// <summary>
        /// Current sequence position (number of tokens generated so far).
        /// </summary>
        public readonly int Position;

        /// <summary>
        /// Whether KV cache was used for this decode step.
        /// </summary>
        public readonly bool CacheUsed;

        public DecodeMetrics(double elapsedMs, int position, bool cacheUsed)
        {
            ElapsedMs = elapsedMs;
            Position = position;
            CacheUsed = cacheUsed;
        }
    }
}

using System;

namespace SmallMind.Engine
{
    /// <summary>
    /// Diagnostic metrics for a chat session.
    /// Tracks performance, caching efficiency, and recovery events.
    /// </summary>
    internal sealed class ChatSessionDiagnostics
    {
        /// <summary>
        /// Gets the total number of conversation turns.
        /// </summary>
        public int TotalTurns { get; init; }

        /// <summary>
        /// Gets the number of turns that required truncation.
        /// </summary>
        public int TruncatedTurns { get; init; }

        /// <summary>
        /// Gets the number of KV cache hits (reused cached computations).
        /// </summary>
        public int KvCacheHits { get; init; }

        /// <summary>
        /// Gets the number of KV cache misses (full recomputation required).
        /// </summary>
        public int KvCacheMisses { get; init; }

        /// <summary>
        /// Gets the number of NaN/Inf recovery events.
        /// </summary>
        public int NaNRecoveries { get; init; }

        /// <summary>
        /// Gets the number of degenerate output recovery events (repetition, etc).
        /// </summary>
        public int DegenerateOutputRecoveries { get; init; }

        /// <summary>
        /// Gets the total number of tokens generated.
        /// </summary>
        public long TotalTokensGenerated { get; init; }

        /// <summary>
        /// Gets the total number of tokens served from cache.
        /// </summary>
        public long TotalTokensFromCache { get; init; }

        /// <summary>
        /// Gets the average tokens per second across all generations.
        /// </summary>
        public double AverageTokensPerSecond { get; init; }

        /// <summary>
        /// Gets the total inference time spent generating.
        /// </summary>
        public TimeSpan TotalInferenceTime { get; init; }
    }
}

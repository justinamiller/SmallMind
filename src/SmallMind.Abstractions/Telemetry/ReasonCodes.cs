namespace SmallMind.Abstractions.Telemetry
{
    /// <summary>
    /// Reason codes for why runtime stopped or degraded.
    /// Used for both logging and metrics to provide consistent diagnostics.
    /// </summary>
    public enum RuntimeStopReason
    {
        /// <summary>
        /// Inference completed normally (EOS token or stop sequence).
        /// </summary>
        Completed = 0,

        /// <summary>
        /// Maximum token limit reached.
        /// </summary>
        MaxTokens = 1,

        /// <summary>
        /// Cancelled by caller via CancellationToken.
        /// </summary>
        CancelledByCaller = 2,

        /// <summary>
        /// Cancelled due to timeout.
        /// </summary>
        CancelledByTimeout = 3,

        /// <summary>
        /// Stopped due to backpressure (buffer full).
        /// </summary>
        BackpressureBufferFull = 4,

        /// <summary>
        /// Stopped due to backpressure (queue full).
        /// </summary>
        BackpressureQueueFull = 5,

        /// <summary>
        /// Stopped due to memory budget exceeded.
        /// </summary>
        MemoryBudgetExceeded = 6,

        /// <summary>
        /// Stopped due to memory budget soft limit.
        /// </summary>
        MemoryBudgetSoftLimit = 7,

        /// <summary>
        /// Stopped due to error.
        /// </summary>
        Error = 99
    }

    /// <summary>
    /// Reason codes for runtime degradation or warnings.
    /// </summary>
    public enum RuntimeDegradeReason
    {
        /// <summary>
        /// No degradation.
        /// </summary>
        None = 0,

        /// <summary>
        /// GGUF tokenizer metadata is missing.
        /// </summary>
        TokenizerGgufMetadataMissing = 100,

        /// <summary>
        /// Tokenizer vocabulary is partial/incomplete.
        /// </summary>
        TokenizerVocabPartial = 101,

        /// <summary>
        /// Tokenizer merges are missing.
        /// </summary>
        TokenizerMergesMissing = 102,

        /// <summary>
        /// Falling back to byte-level BPE tokenizer.
        /// </summary>
        TokenizerFallbackByteBpe = 103,

        /// <summary>
        /// Falling back to token-table-only tokenizer.
        /// </summary>
        TokenizerFallbackTokenTableOnly = 104,

        /// <summary>
        /// Memory budget approaching soft limit.
        /// </summary>
        MemoryBudgetApproachingSoftLimit = 200,

        /// <summary>
        /// KV cache eviction occurred.
        /// </summary>
        KvCacheEviction = 300,

        /// <summary>
        /// Context truncation occurred.
        /// </summary>
        ContextTruncation = 301
    }

    /// <summary>
    /// Memory budget enforcement mode.
    /// </summary>
    public enum MemoryBudgetMode
    {
        /// <summary>
        /// No memory budget enforcement.
        /// </summary>
        None = 0,

        /// <summary>
        /// Best-effort budget enforcement (logs warnings).
        /// </summary>
        BestEffort = 1,

        /// <summary>
        /// Strict budget enforcement (throws on violation).
        /// </summary>
        Strict = 2
    }
}

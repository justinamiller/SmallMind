using SmallMind.Runtime.Telemetry;

namespace SmallMind.Runtime.Execution
{
    /// <summary>
    /// Execution context that maintains state across prefill and decode operations.
    /// Contains KV cache handle, position tracking, and telemetry.
    /// Designed to be reused across multiple tokens in a generation sequence.
    /// </summary>
    internal sealed class ExecutionContext : IDisposable
    {
        private bool _disposed;

        /// <summary>
        /// Gets or sets the KV cache handle for this execution context.
        /// Null before prefill, populated after prefill, updated after each decode.
        /// </summary>
        public KvCacheHandle? CacheHandle { get; set; }

        /// <summary>
        /// Gets or sets the current position in the sequence.
        /// Updated after each prefill/decode step.
        /// </summary>
        public int CurrentPosition { get; set; }

        /// <summary>
        /// Gets the runtime options for this execution context.
        /// </summary>
        public RuntimeOptions Options { get; }

        /// <summary>
        /// Gets the telemetry collector for this execution context.
        /// </summary>
        public IRuntimeMetrics Telemetry { get; }

        /// <summary>
        /// Gets whether the context has an active KV cache.
        /// </summary>
        public bool HasCache => CacheHandle != null;

        /// <summary>
        /// Gets whether the context is in prefill mode (no cache populated yet).
        /// </summary>
        public bool IsPrefillMode => !HasCache;

        /// <summary>
        /// Gets whether the context is in decode mode (cache is populated).
        /// </summary>
        public bool IsDecodeMode => HasCache;

        public ExecutionContext(RuntimeOptions options, IRuntimeMetrics? telemetry = null)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Telemetry = telemetry ?? NullRuntimeMetrics.Instance;
            CurrentPosition = 0;
        }

        /// <summary>
        /// Resets the execution context for a new sequence.
        /// Resets cache and position but keeps options and telemetry.
        /// </summary>
        public void Reset()
        {
            CacheHandle?.Reset();
            CurrentPosition = 0;
        }

        /// <summary>
        /// Disposes the execution context and releases the cache handle.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            CacheHandle?.Dispose();
            CacheHandle = null;

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}

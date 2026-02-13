namespace SmallMind
{
    /// <summary>
    /// Diagnostics sink for observability and instrumentation.
    /// Implement this interface to receive diagnostic events from the engine.
    /// All methods must be thread-safe.
    /// </summary>
    public interface ISmallMindDiagnosticsSink
    {
        /// <summary>
        /// Called when a diagnostic event occurs.
        /// Implementations must not throw exceptions.
        /// </summary>
        /// <param name="e">The diagnostic event.</param>
        void OnEvent(in SmallMindDiagnosticEvent e);
    }

    /// <summary>
    /// Type of diagnostic event.
    /// </summary>
    public enum DiagnosticEventType
    {
        /// <summary>
        /// Engine was created.
        /// </summary>
        EngineCreated,

        /// <summary>
        /// Model was loaded successfully.
        /// </summary>
        ModelLoaded,

        /// <summary>
        /// Model load failed.
        /// </summary>
        ModelLoadFailed,

        /// <summary>
        /// Session was created.
        /// </summary>
        SessionCreated,

        /// <summary>
        /// Session was disposed.
        /// </summary>
        SessionDisposed,

        /// <summary>
        /// Request started.
        /// </summary>
        RequestStarted,

        /// <summary>
        /// Token was emitted (streaming only).
        /// </summary>
        TokenEmitted,

        /// <summary>
        /// Request completed successfully.
        /// </summary>
        RequestCompleted,

        /// <summary>
        /// Request failed.
        /// </summary>
        RequestFailed,

        /// <summary>
        /// Request was cancelled.
        /// </summary>
        RequestCancelled
    }

    /// <summary>
    /// Diagnostic event emitted by the engine.
    /// Designed to minimize allocations in hot paths.
    /// </summary>
    public readonly struct SmallMindDiagnosticEvent
    {
        /// <summary>
        /// Gets the event type.
        /// </summary>
        public DiagnosticEventType EventType { get; init; }

        /// <summary>
        /// Gets the timestamp when this event occurred.
        /// </summary>
        public DateTimeOffset Timestamp { get; init; }

        /// <summary>
        /// Gets the request ID (for correlation).
        /// </summary>
        public Guid RequestId { get; init; }

        /// <summary>
        /// Gets the session ID (if applicable).
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// Gets the duration in milliseconds (for completed events).
        /// </summary>
        public double? DurationMs { get; init; }

        /// <summary>
        /// Gets the number of prompt tokens (if applicable).
        /// </summary>
        public int? PromptTokens { get; init; }

        /// <summary>
        /// Gets the number of completion tokens (if applicable).
        /// </summary>
        public int? CompletionTokens { get; init; }

        /// <summary>
        /// Gets the error message (if EventType is *Failed or *Cancelled).
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// Gets additional metadata as key-value pairs.
        /// Avoid allocating strings in hot paths - use primitives when possible.
        /// </summary>
        public string? Metadata { get; init; }
    }
}

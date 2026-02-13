namespace SmallMind.Abstractions.Telemetry
{
    /// <summary>
    /// Log levels for runtime logging.
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Trace-level logging (most verbose).
        /// </summary>
        Trace = 0,

        /// <summary>
        /// Debug-level logging.
        /// </summary>
        Debug = 1,

        /// <summary>
        /// Informational logging.
        /// </summary>
        Info = 2,

        /// <summary>
        /// Warning-level logging.
        /// </summary>
        Warn = 3,

        /// <summary>
        /// Error-level logging.
        /// </summary>
        Error = 4
    }

    /// <summary>
    /// Runtime logger interface for SmallMind.
    /// Provides structured logging without external dependencies.
    /// Implementations must be thread-safe.
    /// </summary>
    public interface IRuntimeLogger
    {
        /// <summary>
        /// Logs a message at the specified level.
        /// </summary>
        /// <param name="level">Log level.</param>
        /// <param name="message">Log message.</param>
        void Log(LogLevel level, string message);

        /// <summary>
        /// Logs a structured event.
        /// </summary>
        /// <param name="evt">Runtime log event.</param>
        void LogEvent(in RuntimeLogEvent evt);

        /// <summary>
        /// Logs a trace-level message.
        /// </summary>
        /// <param name="message">Log message.</param>
        void Trace(string message) => Log(LogLevel.Trace, message);

        /// <summary>
        /// Logs a debug-level message.
        /// </summary>
        /// <param name="message">Log message.</param>
        void Debug(string message) => Log(LogLevel.Debug, message);

        /// <summary>
        /// Logs an info-level message.
        /// </summary>
        /// <param name="message">Log message.</param>
        void Info(string message) => Log(LogLevel.Info, message);

        /// <summary>
        /// Logs a warning-level message.
        /// </summary>
        /// <param name="message">Log message.</param>
        void Warn(string message) => Log(LogLevel.Warn, message);

        /// <summary>
        /// Logs an error-level message.
        /// </summary>
        /// <param name="message">Log message.</param>
        /// <param name="exception">Optional exception.</param>
        void Error(string message, Exception? exception = null);
    }

    /// <summary>
    /// Structured runtime log event.
    /// Designed for low-allocation logging in hot paths.
    /// </summary>
    public readonly struct RuntimeLogEvent
    {
        /// <summary>
        /// Gets the event ID (for categorization).
        /// </summary>
        public int EventId { get; init; }

        /// <summary>
        /// Gets the event name.
        /// </summary>
        public string? EventName { get; init; }

        /// <summary>
        /// Gets the log level.
        /// </summary>
        public LogLevel Level { get; init; }

        /// <summary>
        /// Gets the correlation ID (for multi-session scenarios).
        /// </summary>
        public Guid CorrelationId { get; init; }

        /// <summary>
        /// Gets the message template.
        /// </summary>
        public string? MessageTemplate { get; init; }

        /// <summary>
        /// Gets the formatted message (alternative to MessageTemplate).
        /// </summary>
        public string? Message { get; init; }

        /// <summary>
        /// Gets optional exception.
        /// </summary>
        public Exception? Exception { get; init; }

        /// <summary>
        /// Gets property 1 name (for structured properties).
        /// </summary>
        public string? Property1Name { get; init; }

        /// <summary>
        /// Gets property 1 value.
        /// </summary>
        public object? Property1Value { get; init; }

        /// <summary>
        /// Gets property 2 name.
        /// </summary>
        public string? Property2Name { get; init; }

        /// <summary>
        /// Gets property 2 value.
        /// </summary>
        public object? Property2Value { get; init; }

        /// <summary>
        /// Gets property 3 name.
        /// </summary>
        public string? Property3Name { get; init; }

        /// <summary>
        /// Gets property 3 value.
        /// </summary>
        public object? Property3Value { get; init; }
    }

    /// <summary>
    /// Null runtime logger (does nothing).
    /// </summary>
    public sealed class NullRuntimeLogger : IRuntimeLogger
    {
        /// <summary>
        /// Gets the singleton instance.
        /// </summary>
        public static readonly NullRuntimeLogger Instance = new();

        private NullRuntimeLogger() { }

        /// <inheritdoc/>
        public void Log(LogLevel level, string message) { }

        /// <inheritdoc/>
        public void LogEvent(in RuntimeLogEvent evt) { }

        /// <inheritdoc/>
        public void Error(string message, Exception? exception = null) { }
    }
}

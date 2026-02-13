namespace SmallMind.Abstractions.Telemetry
{
    /// <summary>
    /// Console-based runtime logger.
    /// Writes log messages to the console with timestamps and log levels.
    /// Thread-safe.
    /// </summary>
    public sealed class ConsoleRuntimeLogger : IRuntimeLogger
    {
        private readonly LogLevel _minimumLevel;
        private readonly object _lock = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="ConsoleRuntimeLogger"/> class.
        /// </summary>
        /// <param name="minimumLevel">Minimum log level to output. Default is Info.</param>
        public ConsoleRuntimeLogger(LogLevel minimumLevel = LogLevel.Info)
        {
            _minimumLevel = minimumLevel;
        }

        /// <inheritdoc/>
        public void Log(LogLevel level, string message)
        {
            if (level < _minimumLevel) return;

            lock (_lock)
            {
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelStr = level switch
                {
                    LogLevel.Trace => "TRACE",
                    LogLevel.Debug => "DEBUG",
                    LogLevel.Info => "INFO ",
                    LogLevel.Warn => "WARN ",
                    LogLevel.Error => "ERROR",
                    _ => "?????",
                };

                Console.WriteLine($"[{levelStr}] {timestamp} - {message}");
            }
        }

        /// <inheritdoc/>
        public void LogEvent(in RuntimeLogEvent evt)
        {
            if (evt.Level < _minimumLevel) return;

            lock (_lock)
            {
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var levelStr = evt.Level switch
                {
                    LogLevel.Trace => "TRACE",
                    LogLevel.Debug => "DEBUG",
                    LogLevel.Info => "INFO ",
                    LogLevel.Warn => "WARN ",
                    LogLevel.Error => "ERROR",
                    _ => "?????",
                };

                var message = evt.Message ?? evt.MessageTemplate ?? "";
                var eventName = evt.EventName != null ? $"[{evt.EventName}]" : "";
                var correlationId = evt.CorrelationId != Guid.Empty ? $" CorrelationId={evt.CorrelationId:N}" : "";

                Console.Write($"[{levelStr}] {timestamp}{eventName}{correlationId} - {message}");

                // Add structured properties
                if (evt.Property1Name != null)
                {
                    Console.Write($" {evt.Property1Name}={evt.Property1Value}");
                }
                if (evt.Property2Name != null)
                {
                    Console.Write($" {evt.Property2Name}={evt.Property2Value}");
                }
                if (evt.Property3Name != null)
                {
                    Console.Write($" {evt.Property3Name}={evt.Property3Value}");
                }

                Console.WriteLine();

                if (evt.Exception != null)
                {
                    Console.WriteLine($"  Exception: {evt.Exception}");
                }
            }
        }

        /// <inheritdoc/>
        public void Error(string message, Exception? exception = null)
        {
            if (LogLevel.Error < _minimumLevel) return;

            lock (_lock)
            {
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                Console.WriteLine($"[ERROR] {timestamp} - {message}");
                if (exception != null)
                {
                    Console.WriteLine($"  Exception: {exception}");
                }
            }
        }
    }
}

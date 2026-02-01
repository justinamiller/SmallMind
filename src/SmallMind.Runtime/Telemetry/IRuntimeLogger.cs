using System;

namespace SmallMind.Runtime.Telemetry
{
    /// <summary>
    /// Simple logging interface for runtime diagnostics.
    /// No dependencies on external logging frameworks.
    /// </summary>
    public interface IRuntimeLogger
    {
        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? exception = null);
    }

    /// <summary>
    /// Console-based runtime logger.
    /// </summary>
    public sealed class ConsoleRuntimeLogger : IRuntimeLogger
    {
        public void LogDebug(string message)
        {
            Console.WriteLine($"[DEBUG] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {message}");
        }

        public void LogInfo(string message)
        {
            Console.WriteLine($"[INFO] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {message}");
        }

        public void LogWarning(string message)
        {
            Console.WriteLine($"[WARN] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {message}");
        }

        public void LogError(string message, Exception? exception = null)
        {
            Console.WriteLine($"[ERROR] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} - {message}");
            if (exception != null)
                Console.WriteLine($"Exception: {exception}");
        }
    }

    /// <summary>
    /// Null logger that does nothing (for production where logging is not desired).
    /// </summary>
    public sealed class NullRuntimeLogger : IRuntimeLogger
    {
        public static readonly NullRuntimeLogger Instance = new NullRuntimeLogger();
        
        private NullRuntimeLogger() { }

        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? exception = null) { }
    }
}

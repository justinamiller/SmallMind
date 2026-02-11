using System;
using SmallMind.Abstractions.Telemetry;

namespace SmallMind.Runtime.Telemetry
{
    /// <summary>
    /// Simple logging interface for runtime diagnostics (internal adapter).
    /// Bridges to the public IRuntimeLogger from SmallMind.Abstractions.
    /// </summary>
    internal interface IInternalRuntimeLogger
    {
        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? exception = null);
    }

    /// <summary>
    /// Adapter that bridges internal logging to public IRuntimeLogger.
    /// </summary>
    internal sealed class RuntimeLoggerAdapter : IInternalRuntimeLogger
    {
        private readonly IRuntimeLogger _publicLogger;

        public RuntimeLoggerAdapter(IRuntimeLogger publicLogger)
        {
            _publicLogger = publicLogger ?? NullRuntimeLogger.Instance;
        }

        public void LogDebug(string message)
        {
            _publicLogger.Debug(message);
        }

        public void LogInfo(string message)
        {
            _publicLogger.Info(message);
        }

        public void LogWarning(string message)
        {
            _publicLogger.Warn(message);
        }

        public void LogError(string message, Exception? exception = null)
        {
            _publicLogger.Error(message, exception);
        }
    }

    /// <summary>
    /// Null logger that does nothing (for production where logging is not desired).
    /// </summary>
    internal sealed class NullInternalRuntimeLogger : IInternalRuntimeLogger
    {
        public static readonly NullInternalRuntimeLogger Instance = new NullInternalRuntimeLogger();
        
        private NullInternalRuntimeLogger() { }

        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message, Exception? exception = null) { }
    }
}

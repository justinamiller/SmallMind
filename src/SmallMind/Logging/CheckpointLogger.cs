using System;
using Microsoft.Extensions.Logging;

namespace SmallMind.Logging
{
    /// <summary>
    /// Source-generated high-performance logging for checkpoint operations.
    /// </summary>
    public static partial class CheckpointLogger
    {
        [LoggerMessage(
            EventId = 3001,
            Level = LogLevel.Information,
            Message = "Loading checkpoint from {CheckpointPath}")]
        public static partial void CheckpointLoading(
            this ILogger logger,
            string checkpointPath);

        [LoggerMessage(
            EventId = 3002,
            Level = LogLevel.Information,
            Message = "Checkpoint loaded successfully: {ParameterCount} parameters")]
        public static partial void CheckpointLoaded(
            this ILogger logger,
            int parameterCount);

        [LoggerMessage(
            EventId = 3003,
            Level = LogLevel.Error,
            Message = "Failed to load checkpoint from {CheckpointPath}: {ErrorMessage}")]
        public static partial void CheckpointLoadFailed(
            this ILogger logger,
            string checkpointPath,
            string errorMessage,
            Exception exception);

        [LoggerMessage(
            EventId = 3004,
            Level = LogLevel.Information,
            Message = "Saving checkpoint to {CheckpointPath}")]
        public static partial void CheckpointSaving(
            this ILogger logger,
            string checkpointPath);

        [LoggerMessage(
            EventId = 3005,
            Level = LogLevel.Error,
            Message = "Failed to save checkpoint to {CheckpointPath}: {ErrorMessage}")]
        public static partial void CheckpointSaveFailed(
            this ILogger logger,
            string checkpointPath,
            string errorMessage,
            Exception exception);
    }
}

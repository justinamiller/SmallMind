using System;
using Microsoft.Extensions.Logging;

namespace SmallMind.Logging
{
    /// <summary>
    /// Source-generated high-performance logging for training operations.
    /// </summary>
    public static partial class TrainingLogger
    {
        [LoggerMessage(
            EventId = 1001,
            Level = LogLevel.Information,
            Message = "Training started: {Steps} steps, batch size: {BatchSize}, learning rate: {LearningRate}")]
        public static partial void TrainingStarted(
            this ILogger logger,
            int steps,
            int batchSize,
            double learningRate);

        [LoggerMessage(
            EventId = 1002,
            Level = LogLevel.Information,
            Message = "Training step {Step}/{TotalSteps} - Loss: {Loss:F4}, LR: {LearningRate:F6}, Time: {TimeMs:F0}ms")]
        public static partial void TrainingStep(
            this ILogger logger,
            int step,
            int totalSteps,
            float loss,
            float learningRate,
            double timeMs);

        [LoggerMessage(
            EventId = 1003,
            Level = LogLevel.Information,
            Message = "Validation loss: {ValidationLoss:F4}")]
        public static partial void ValidationLoss(
            this ILogger logger,
            float validationLoss);

        [LoggerMessage(
            EventId = 1004,
            Level = LogLevel.Information,
            Message = "Checkpoint saved to {CheckpointPath}")]
        public static partial void CheckpointSaved(
            this ILogger logger,
            string checkpointPath);

        [LoggerMessage(
            EventId = 1005,
            Level = LogLevel.Information,
            Message = "Training completed. Total time: {TotalSeconds:F2}s, Total tokens: {TotalTokens:N0}, Avg tokens/sec: {TokensPerSec:F0}")]
        public static partial void TrainingCompleted(
            this ILogger logger,
            double totalSeconds,
            long totalTokens,
            double tokensPerSec);

        [LoggerMessage(
            EventId = 1006,
            Level = LogLevel.Warning,
            Message = "Training cancelled at step {Step}. Checkpoint saved to {CheckpointPath}")]
        public static partial void TrainingCancelled(
            this ILogger logger,
            int step,
            string checkpointPath);

        [LoggerMessage(
            EventId = 1007,
            Level = LogLevel.Error,
            Message = "Training failed at step {Step} with error: {ErrorMessage}")]
        public static partial void TrainingFailed(
            this ILogger logger,
            int step,
            string errorMessage,
            Exception exception);

        [LoggerMessage(
            EventId = 1008,
            Level = LogLevel.Warning,
            Message = "Gradient health check: NaN count: {NaNCount}, Inf count: {InfCount}, Max gradient: {MaxGradient:E2}")]
        public static partial void GradientHealthCheck(
            this ILogger logger,
            int nanCount,
            int infCount,
            float maxGradient);

        [LoggerMessage(
            EventId = 1009,
            Level = LogLevel.Warning,
            Message = "Numerical instability detected: Loss is {Loss} at step {Step}")]
        public static partial void NumericalInstability(
            this ILogger logger,
            float loss,
            int step);
    }
}

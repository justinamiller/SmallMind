using System;
using Microsoft.Extensions.Logging;

namespace SmallMind.Logging
{
    /// <summary>
    /// Source-generated high-performance logging for inference operations.
    /// </summary>
    public static partial class InferenceLogger
    {
        [LoggerMessage(
            EventId = 2001,
            Level = LogLevel.Information,
            Message = "Generation started: max tokens: {MaxTokens}, temperature: {Temperature}")]
        public static partial void GenerationStarted(
            this ILogger logger,
            int maxTokens,
            double temperature);

        [LoggerMessage(
            EventId = 2002,
            Level = LogLevel.Information,
            Message = "Generated {TokenCount} tokens in {DurationMs:F0}ms ({TokensPerSec:F1} tokens/sec)")]
        public static partial void GenerationCompleted(
            this ILogger logger,
            int tokenCount,
            double durationMs,
            double tokensPerSec);

        [LoggerMessage(
            EventId = 2003,
            Level = LogLevel.Debug,
            Message = "Token generated: {Token} (position {Position})")]
        public static partial void TokenGenerated(
            this ILogger logger,
            string token,
            int position);

        [LoggerMessage(
            EventId = 2004,
            Level = LogLevel.Warning,
            Message = "Generation stopped early: {Reason}")]
        public static partial void GenerationStoppedEarly(
            this ILogger logger,
            string reason);
    }
}

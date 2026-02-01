using Microsoft.Extensions.Logging;

namespace SmallMind.Domain
{
    /// <summary>
    /// Source-generated high-performance logging for domain reasoning operations.
    /// </summary>
    public static partial class DomainReasoningLogger
    {
        [LoggerMessage(
            EventId = 3001,
            Level = LogLevel.Information,
            Message = "Domain reasoning request started: RequestId={RequestId}, Domain={DomainName}/{DomainVersion}, MaxInputTokens={MaxInputTokens}, MaxOutputTokens={MaxOutputTokens}")]
        public static partial void RequestStarted(
            this ILogger logger,
            string requestId,
            string domainName,
            string domainVersion,
            int maxInputTokens,
            int maxOutputTokens);

        [LoggerMessage(
            EventId = 3002,
            Level = LogLevel.Information,
            Message = "Domain reasoning completed: RequestId={RequestId}, Status={Status}, Duration={DurationMs:F0}ms, InputTokens={InputTokens}, OutputTokens={OutputTokens}")]
        public static partial void RequestCompleted(
            this ILogger logger,
            string requestId,
            DomainAnswerStatus status,
            double durationMs,
            int inputTokens,
            int outputTokens);

        [LoggerMessage(
            EventId = 3003,
            Level = LogLevel.Warning,
            Message = "Domain reasoning rejected: RequestId={RequestId}, Status={Status}, Reason={Reason}")]
        public static partial void RequestRejected(
            this ILogger logger,
            string requestId,
            DomainAnswerStatus status,
            string reason);

        [LoggerMessage(
            EventId = 3004,
            Level = LogLevel.Error,
            Message = "Domain reasoning failed: RequestId={RequestId}, Error={ErrorMessage}")]
        public static partial void RequestFailed(
            this ILogger logger,
            string requestId,
            string errorMessage,
            System.Exception? exception = null);

        [LoggerMessage(
            EventId = 3005,
            Level = LogLevel.Debug,
            Message = "Token masked by policy: TokenId={TokenId}, Policy={PolicyName}")]
        public static partial void TokenMasked(
            this ILogger logger,
            int tokenId,
            string policyName);

        [LoggerMessage(
            EventId = 3006,
            Level = LogLevel.Debug,
            Message = "Output format validated: Format={Format}, Valid={IsValid}, Reason={Reason}")]
        public static partial void OutputValidated(
            this ILogger logger,
            OutputFormat format,
            bool isValid,
            string? reason);

        [LoggerMessage(
            EventId = 3007,
            Level = LogLevel.Information,
            Message = "Provenance tracked: Confidence={Confidence:F3}, EvidenceItems={EvidenceCount}")]
        public static partial void ProvenanceTracked(
            this ILogger logger,
            double confidence,
            int evidenceCount);

        [LoggerMessage(
            EventId = 3008,
            Level = LogLevel.Warning,
            Message = "Execution timeout: RequestId={RequestId}, MaxTime={MaxTimeSeconds:F1}s, Elapsed={ElapsedSeconds:F1}s")]
        public static partial void ExecutionTimeout(
            this ILogger logger,
            string requestId,
            double maxTimeSeconds,
            double elapsedSeconds);
    }
}

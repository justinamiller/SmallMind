using System;

namespace SmallMind.Domain
{
    /// <summary>
    /// Represents the result of a domain-bounded reasoning request.
    /// </summary>
    public class DomainAnswer
    {
        /// <summary>
        /// Gets or sets the generated text response.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the status of the answer.
        /// </summary>
        public DomainAnswerStatus Status { get; set; }

        /// <summary>
        /// Gets or sets the reason for rejection (if Status is not Success).
        /// </summary>
        public string? RejectionReason { get; set; }

        /// <summary>
        /// Gets or sets the duration of the request processing.
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Gets or sets the number of input tokens processed.
        /// </summary>
        public int InputTokens { get; set; }

        /// <summary>
        /// Gets or sets the number of output tokens generated.
        /// </summary>
        public int OutputTokens { get; set; }

        /// <summary>
        /// Gets or sets the provenance information (if enabled).
        /// </summary>
        public DomainProvenance? Provenance { get; set; }

        /// <summary>
        /// Gets or sets the request ID for tracking.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Creates a successful domain answer.
        /// </summary>
        /// <param name="text">The generated text.</param>
        /// <param name="duration">The processing duration.</param>
        /// <param name="inputTokens">Number of input tokens.</param>
        /// <param name="outputTokens">Number of output tokens.</param>
        /// <param name="requestId">Optional request ID.</param>
        /// <returns>A successful domain answer.</returns>
        public static DomainAnswer Success(string text, TimeSpan duration, int inputTokens, int outputTokens, string? requestId = null)
        {
            return new DomainAnswer
            {
                Text = text ?? string.Empty,
                Status = DomainAnswerStatus.Success,
                Duration = duration,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Creates a rejected domain answer.
        /// </summary>
        /// <param name="status">The rejection status.</param>
        /// <param name="reason">The reason for rejection.</param>
        /// <param name="duration">The processing duration.</param>
        /// <param name="requestId">Optional request ID.</param>
        /// <returns>A rejected domain answer.</returns>
        public static DomainAnswer Rejected(DomainAnswerStatus status, string reason, TimeSpan duration, string? requestId = null)
        {
            return new DomainAnswer
            {
                Status = status,
                RejectionReason = reason,
                Duration = duration,
                RequestId = requestId
            };
        }

        /// <summary>
        /// Creates a failed domain answer.
        /// </summary>
        /// <param name="reason">The failure reason.</param>
        /// <param name="duration">The processing duration.</param>
        /// <param name="requestId">Optional request ID.</param>
        /// <returns>A failed domain answer.</returns>
        public static DomainAnswer Failed(string reason, TimeSpan duration, string? requestId = null)
        {
            return new DomainAnswer
            {
                Status = DomainAnswerStatus.Failed,
                RejectionReason = reason,
                Duration = duration,
                RequestId = requestId
            };
        }
    }
}

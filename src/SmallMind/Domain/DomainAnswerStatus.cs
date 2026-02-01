namespace SmallMind.Domain
{
    /// <summary>
    /// Represents the status of a domain-bound reasoning answer.
    /// </summary>
    public enum DomainAnswerStatus
    {
        /// <summary>
        /// The request completed successfully.
        /// </summary>
        Success,

        /// <summary>
        /// The request was rejected because it was out of domain.
        /// </summary>
        RejectedOutOfDomain,

        /// <summary>
        /// The request was rejected due to policy violation.
        /// </summary>
        RejectedPolicy,

        /// <summary>
        /// The request was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The request failed due to an error.
        /// </summary>
        Failed
    }
}

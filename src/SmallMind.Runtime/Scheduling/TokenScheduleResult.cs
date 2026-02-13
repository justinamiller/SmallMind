namespace SmallMind.Runtime.Scheduling
{
    /// <summary>
    /// Result of token scheduling operation.
    /// Contains detailed information for reproducibility and auditing.
    /// </summary>
    internal sealed class TokenScheduleResult
    {
        /// <summary>
        /// Gets the unique identifier for this schedule.
        /// </summary>
        public string ScheduleId { get; }

        /// <summary>
        /// Gets the scheduling policy used.
        /// </summary>
        public SchedulingPolicy Policy { get; }

        /// <summary>
        /// Gets the total number of tokens scheduled.
        /// </summary>
        public int TotalTokens { get; }

        /// <summary>
        /// Gets the order in which tokens will be generated.
        /// Maps position -> token generation order.
        /// </summary>
        public IReadOnlyList<int> GenerationOrder { get; }

        /// <summary>
        /// Gets the resource allocation for this schedule.
        /// Tracks memory, compute, and other resource assignments.
        /// </summary>
        public IReadOnlyDictionary<string, long> ResourceAllocation { get; }

        /// <summary>
        /// Gets when this schedule was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; }

        /// <summary>
        /// Gets the deterministic seed used for this schedule (if any).
        /// </summary>
        public uint? Seed { get; }

        /// <summary>
        /// Creates a new token schedule result.
        /// </summary>
        public TokenScheduleResult(
            string scheduleId,
            SchedulingPolicy policy,
            int totalTokens,
            IReadOnlyList<int> generationOrder,
            IReadOnlyDictionary<string, long> resourceAllocation,
            uint? seed = null)
        {
            ScheduleId = scheduleId ?? throw new ArgumentNullException(nameof(scheduleId));
            Policy = policy;
            TotalTokens = totalTokens;
            GenerationOrder = generationOrder ?? throw new ArgumentNullException(nameof(generationOrder));
            ResourceAllocation = resourceAllocation ?? throw new ArgumentNullException(nameof(resourceAllocation));
            CreatedAt = DateTimeOffset.UtcNow;
            Seed = seed;
        }

        /// <summary>
        /// Gets a summary of this schedule for logging/auditing.
        /// </summary>
        public string GetSummary()
        {
            return $"Schedule {ScheduleId}: Policy={Policy}, Tokens={TotalTokens}, " +
                   $"Seed={Seed?.ToString() ?? "none"}, Created={CreatedAt:O}";
        }
    }
}

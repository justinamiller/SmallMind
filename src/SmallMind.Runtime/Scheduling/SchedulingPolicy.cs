namespace SmallMind.Runtime.Scheduling
{
    /// <summary>
    /// Scheduling policy for deterministic token generation.
    /// Defines the order in which tokens are scheduled for processing.
    /// </summary>
    internal enum SchedulingPolicy
    {
        /// <summary>
        /// First-In-First-Out: Process requests in the order they arrive.
        /// Guarantees fairness and predictable ordering.
        /// </summary>
        FIFO,

        /// <summary>
        /// Round-Robin: Distribute tokens evenly across requests.
        /// Prevents starvation and ensures all requests make progress.
        /// </summary>
        RoundRobin,

        /// <summary>
        /// Priority-based: Schedule based on request priority.
        /// Higher priority requests are processed first.
        /// </summary>
        Priority
    }
}

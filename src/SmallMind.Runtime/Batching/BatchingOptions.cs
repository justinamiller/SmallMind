using SmallMind.Core.Exceptions;
using SmallMind.Runtime.Scheduling;

namespace SmallMind.Runtime.Batching
{
    /// <summary>
    /// Configuration options for batched inference scheduling.
    /// Controls batch formation, timeout, and queue management.
    /// </summary>
    internal sealed class BatchingOptions
    {
        /// <summary>
        /// Gets or sets whether batching is enabled.
        /// Default: false (batching disabled).
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of requests per batch.
        /// Default: 8.
        /// </summary>
        public int MaxBatchSize { get; set; } = 8;

        /// <summary>
        /// Gets or sets the maximum time to wait in milliseconds before executing a partial batch.
        /// Lower values reduce latency but may decrease throughput.
        /// Default: 10ms.
        /// </summary>
        public int MaxBatchWaitMs { get; set; } = 10;

        /// <summary>
        /// Gets or sets the maximum total queued requests.
        /// Incoming requests exceeding this limit will be rejected.
        /// Default: 100.
        /// </summary>
        public int MaxTotalQueuedRequests { get; set; } = 100;

        /// <summary>
        /// Gets or sets whether to batch only during prefill phase.
        /// When true, only initial prompt processing is batched.
        /// When false, both prefill and decode phases are batched (higher complexity).
        /// Default: true (prefill-only batching).
        /// </summary>
        public bool PrefillOnly { get; set; } = true;

        /// <summary>
        /// Gets or sets whether deterministic scheduling is enabled.
        /// When true, token generation order is guaranteed to be reproducible.
        /// Default: false.
        /// </summary>
        public bool EnableDeterministicScheduling { get; set; } = false;

        /// <summary>
        /// Gets or sets the scheduling policy when deterministic mode is enabled.
        /// Default: FIFO (First-In-First-Out).
        /// </summary>
        public SchedulingPolicy SchedulingPolicy { get; set; } = SchedulingPolicy.FIFO;

        /// <summary>
        /// Gets or sets the deterministic seed for scheduling.
        /// Only used when EnableDeterministicScheduling is true.
        /// </summary>
        public uint? DeterministicSeed { get; set; }

        /// <summary>
        /// Validates the batching options and throws if invalid.
        /// </summary>
        /// <exception cref="ValidationException">Thrown when options are invalid.</exception>
        public void Validate()
        {
            if (MaxBatchSize <= 0)
            {
                throw new ValidationException("MaxBatchSize must be greater than 0", nameof(MaxBatchSize));
            }

            if (MaxBatchWaitMs < 0)
            {
                throw new ValidationException("MaxBatchWaitMs cannot be negative", nameof(MaxBatchWaitMs));
            }

            if (MaxTotalQueuedRequests <= 0)
            {
                throw new ValidationException("MaxTotalQueuedRequests must be greater than 0", nameof(MaxTotalQueuedRequests));
            }
        }

        /// <summary>
        /// Creates a copy of these options.
        /// </summary>
        public BatchingOptions Clone()
        {
            return new BatchingOptions
            {
                Enabled = Enabled,
                MaxBatchSize = MaxBatchSize,
                MaxBatchWaitMs = MaxBatchWaitMs,
                MaxTotalQueuedRequests = MaxTotalQueuedRequests,
                PrefillOnly = PrefillOnly,
                EnableDeterministicScheduling = EnableDeterministicScheduling,
                SchedulingPolicy = SchedulingPolicy,
                DeterministicSeed = DeterministicSeed
            };
        }
    }
}

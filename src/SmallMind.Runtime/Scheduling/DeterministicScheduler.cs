using SmallMind.Core.Validation;

namespace SmallMind.Runtime.Scheduling
{
    /// <summary>
    /// Deterministic token scheduler for guaranteed reproducibility.
    /// Provides first-class scheduling with auditable and replayable token generation.
    /// Thread-safe and designed for production use.
    /// </summary>
    internal sealed class DeterministicScheduler
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, TokenScheduleResult> _scheduleHistory;
        private int _scheduleCounter;

        /// <summary>
        /// Gets the total number of schedules created.
        /// </summary>
        public int TotalSchedules
        {
            get
            {
                lock (_lock)
                {
                    return _scheduleCounter;
                }
            }
        }

        /// <summary>
        /// Creates a new deterministic scheduler.
        /// </summary>
        public DeterministicScheduler()
        {
            _scheduleHistory = new Dictionary<string, TokenScheduleResult>();
            _scheduleCounter = 0;
        }

        /// <summary>
        /// Schedule token generation with guaranteed reproducibility.
        /// </summary>
        /// <param name="promptTokens">Input prompt tokens</param>
        /// <param name="maxNewTokens">Maximum new tokens to generate</param>
        /// <param name="policy">Scheduling policy to use</param>
        /// <param name="seed">Deterministic seed (optional)</param>
        /// <param name="priority">Priority for priority-based scheduling (0-100, higher is more important)</param>
        /// <returns>Token schedule result with order, timing, and resource allocation</returns>
        public TokenScheduleResult Schedule(
            int[] promptTokens,
            int maxNewTokens,
            SchedulingPolicy policy,
            uint? seed = null,
            int priority = 50)
        {
            Guard.NotNull(promptTokens, nameof(promptTokens));
            Guard.GreaterThan(maxNewTokens, 0, nameof(maxNewTokens));
            Guard.InRange(priority, 0, 100, nameof(priority));

            lock (_lock)
            {
                // Generate unique schedule ID
                var scheduleId = GenerateScheduleId();

                // Calculate generation order based on policy
                var generationOrder = CalculateGenerationOrder(
                    promptTokens.Length,
                    maxNewTokens,
                    policy,
                    seed,
                    priority);

                // Calculate resource allocation
                var resourceAllocation = CalculateResourceAllocation(
                    promptTokens.Length,
                    maxNewTokens);

                // Create schedule result
                var result = new TokenScheduleResult(
                    scheduleId,
                    policy,
                    promptTokens.Length + maxNewTokens,
                    generationOrder,
                    resourceAllocation,
                    seed);

                // Store in history for auditing
                _scheduleHistory[scheduleId] = result;

                return result;
            }
        }

        /// <summary>
        /// Retrieve a schedule by ID for replay or auditing.
        /// </summary>
        /// <param name="scheduleId">Schedule identifier</param>
        /// <returns>Schedule result if found, null otherwise</returns>
        public TokenScheduleResult? GetSchedule(string scheduleId)
        {
            Guard.NotNullOrWhiteSpace(scheduleId, nameof(scheduleId));

            lock (_lock)
            {
                return _scheduleHistory.TryGetValue(scheduleId, out var result) ? result : null;
            }
        }

        /// <summary>
        /// Clear schedule history (for memory management).
        /// </summary>
        public void ClearHistory()
        {
            lock (_lock)
            {
                _scheduleHistory.Clear();
            }
        }

        /// <summary>
        /// Get all schedule IDs in history.
        /// </summary>
        public IReadOnlyList<string> GetScheduleIds()
        {
            lock (_lock)
            {
                return _scheduleHistory.Keys.ToList();
            }
        }

        private string GenerateScheduleId()
        {
            // Called under lock, no need for Interlocked
            _scheduleCounter++;
            return $"sched_{_scheduleCounter}_{DateTimeOffset.UtcNow.Ticks}";
        }

        private IReadOnlyList<int> CalculateGenerationOrder(
            int promptLength,
            int maxNewTokens,
            SchedulingPolicy policy,
            uint? seed,
            int priority)
        {
            int totalPositions = maxNewTokens;
            var order = new int[totalPositions];

            switch (policy)
            {
                case SchedulingPolicy.FIFO:
                    // Sequential order: 0, 1, 2, 3, ...
                    for (int i = 0; i < totalPositions; i++)
                    {
                        order[i] = i;
                    }
                    break;

                case SchedulingPolicy.RoundRobin:
                    // Interleaved order for fairness across multiple requests
                    // For single request, same as FIFO
                    // In multi-request scenarios, this would alternate between requests
                    for (int i = 0; i < totalPositions; i++)
                    {
                        order[i] = i;
                    }
                    break;

                case SchedulingPolicy.Priority:
                    // Higher priority tokens first, then sequential
                    // For now, treat all tokens with same priority
                    // Future: could prioritize certain token positions
                    for (int i = 0; i < totalPositions; i++)
                    {
                        order[i] = i;
                    }
                    break;

                default:
                    throw new ArgumentException($"Unsupported scheduling policy: {policy}");
            }

            // If seed is provided, apply deterministic shuffling for reproducibility testing
            if (seed.HasValue && policy == SchedulingPolicy.Priority)
            {
                // Use seed to create deterministic but varied ordering
                var rng = new Random((int)seed.Value);
                // Fisher-Yates shuffle with deterministic seed
                for (int i = totalPositions - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    int temp = order[i];
                    order[i] = order[j];
                    order[j] = temp;
                }
            }

            return order;
        }

        private IReadOnlyDictionary<string, long> CalculateResourceAllocation(
            int promptLength,
            int maxNewTokens)
        {
            // Calculate estimated resource requirements
            int totalTokens = promptLength + maxNewTokens;

            // Estimate memory per token (in bytes)
            // This is a simplified model: actual depends on embedding dimension, etc.
            const long BytesPerToken = 4096; // ~4KB per token (rough estimate)
            long totalMemory = totalTokens * BytesPerToken;

            // Estimate compute units (arbitrary units for scheduling)
            long computeUnits = totalTokens * 100; // 100 units per token

            // Create allocation dictionary
            return new Dictionary<string, long>
            {
                ["memory_bytes"] = totalMemory,
                ["compute_units"] = computeUnits,
                ["prompt_tokens"] = promptLength,
                ["max_new_tokens"] = maxNewTokens,
                ["total_tokens"] = totalTokens
            };
        }
    }
}

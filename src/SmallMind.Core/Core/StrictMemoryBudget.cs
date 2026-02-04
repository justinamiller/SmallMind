using System;
using SmallMind.Core.Validation;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Strict memory budget configuration with hard guarantees.
    /// Unlike advisory limits, strict budgets reject operations that would exceed limits.
    /// Designed for production environments requiring predictable resource usage.
    /// </summary>
    public sealed class StrictMemoryBudget
    {
        /// <summary>
        /// Gets the hard maximum memory limit in bytes.
        /// Operations exceeding this limit will be rejected.
        /// </summary>
        public long MaxBytesHard { get; }

        /// <summary>
        /// Gets the maximum memory allowed per session in bytes.
        /// Each session is bounded to prevent runaway resource usage.
        /// </summary>
        public long MaxBytesPerSession { get; }

        /// <summary>
        /// Gets whether to reject operations that would exceed the budget.
        /// When true, operations are rejected before execution.
        /// When false, operations may proceed but will be terminated if limit is exceeded.
        /// </summary>
        public bool RejectOnExceed { get; }

        /// <summary>
        /// Gets whether to pre-allocate memory buffers.
        /// When true, memory is allocated upfront for predictable performance.
        /// When false, memory is allocated on-demand (lower initial cost, potential latency spikes).
        /// </summary>
        public bool PreAllocate { get; }

        /// <summary>
        /// Gets the safety margin as a percentage (0.0 to 1.0).
        /// Actual limit is MaxBytesHard * (1 - SafetyMargin) to prevent edge cases.
        /// Default: 0.1 (10% safety margin).
        /// </summary>
        public double SafetyMargin { get; }

        /// <summary>
        /// Creates a new strict memory budget.
        /// </summary>
        /// <param name="maxBytesHard">Hard maximum memory limit in bytes</param>
        /// <param name="maxBytesPerSession">Maximum memory per session in bytes (0 = unlimited)</param>
        /// <param name="rejectOnExceed">Whether to reject operations on budget exceedance</param>
        /// <param name="preAllocate">Whether to pre-allocate memory buffers</param>
        /// <param name="safetyMargin">Safety margin percentage (0.0 to 1.0)</param>
        public StrictMemoryBudget(
            long maxBytesHard,
            long maxBytesPerSession = 0,
            bool rejectOnExceed = true,
            bool preAllocate = true,
            double safetyMargin = 0.1)
        {
            Guard.GreaterThan(maxBytesHard, 0L, nameof(maxBytesHard));
            Guard.GreaterThanOrEqualTo(maxBytesPerSession, 0L, nameof(maxBytesPerSession));
            Guard.InRange(safetyMargin, 0.0, 1.0, nameof(safetyMargin));

            MaxBytesHard = maxBytesHard;
            MaxBytesPerSession = maxBytesPerSession > 0 ? maxBytesPerSession : maxBytesHard;
            RejectOnExceed = rejectOnExceed;
            PreAllocate = preAllocate;
            SafetyMargin = safetyMargin;
        }

        /// <summary>
        /// Gets the effective hard limit after applying safety margin.
        /// </summary>
        public long EffectiveHardLimit => (long)(MaxBytesHard * (1.0 - SafetyMargin));

        /// <summary>
        /// Gets the effective session limit after applying safety margin.
        /// </summary>
        public long EffectiveSessionLimit => (long)(MaxBytesPerSession * (1.0 - SafetyMargin));

        /// <summary>
        /// Checks whether an allocation of the specified size would fit within the budget.
        /// </summary>
        /// <param name="requiredBytes">Required memory in bytes</param>
        /// <param name="isSessionAllocation">Whether this is a per-session allocation</param>
        /// <returns>True if allocation fits within budget</returns>
        public bool CanAllocate(long requiredBytes, bool isSessionAllocation = false)
        {
            Guard.GreaterThanOrEqualTo(requiredBytes, 0L, nameof(requiredBytes));

            long limit = isSessionAllocation ? EffectiveSessionLimit : EffectiveHardLimit;
            return requiredBytes <= limit;
        }

        /// <summary>
        /// Creates a budget check result for the specified memory requirement.
        /// </summary>
        /// <param name="breakdown">Memory breakdown</param>
        /// <param name="isSessionAllocation">Whether this is a per-session allocation</param>
        /// <returns>Budget check result</returns>
        public BudgetCheckResult CheckBudget(MemoryBreakdown breakdown, bool isSessionAllocation = false)
        {
            // Note: breakdown is now a struct, so no null check needed

            long requiredBytes = breakdown.TotalBytes;
            long limit = isSessionAllocation ? EffectiveSessionLimit : EffectiveHardLimit;

            if (requiredBytes <= limit)
            {
                return BudgetCheckResult.Success(requiredBytes, limit, breakdown);
            }
            else
            {
                var reason = $"Required {requiredBytes / 1024.0 / 1024.0:F2}MB exceeds " +
                            $"{(isSessionAllocation ? "session" : "hard")} limit of {limit / 1024.0 / 1024.0:F2}MB";
                return BudgetCheckResult.Failure(requiredBytes, limit, reason, breakdown);
            }
        }

        /// <summary>
        /// Gets a summary of the budget configuration.
        /// </summary>
        public string GetSummary()
        {
            return $"Strict Memory Budget:\n" +
                   $"  Hard Limit:       {MaxBytesHard / 1024.0 / 1024.0:F2} MB\n" +
                   $"  Session Limit:    {MaxBytesPerSession / 1024.0 / 1024.0:F2} MB\n" +
                   $"  Effective Hard:   {EffectiveHardLimit / 1024.0 / 1024.0:F2} MB\n" +
                   $"  Effective Session:{EffectiveSessionLimit / 1024.0 / 1024.0:F2} MB\n" +
                   $"  Safety Margin:    {SafetyMargin * 100:F1}%\n" +
                   $"  Reject on Exceed: {RejectOnExceed}\n" +
                   $"  Pre-Allocate:     {PreAllocate}";
        }
    }
}

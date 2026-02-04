using System;

namespace SmallMind.Core.Core
{
    /// <summary>
    /// Result of a memory budget pre-flight check.
    /// Indicates whether an operation can proceed within budget constraints.
    /// </summary>
    public sealed class BudgetCheckResult
    {
        /// <summary>
        /// Gets whether the operation can proceed within budget.
        /// </summary>
        public bool CanProceed { get; }

        /// <summary>
        /// Gets the estimated memory required in bytes.
        /// </summary>
        public long EstimatedMemoryBytes { get; }

        /// <summary>
        /// Gets the available memory budget in bytes.
        /// </summary>
        public long AvailableBudgetBytes { get; }

        /// <summary>
        /// Gets the reason if the check failed.
        /// </summary>
        public string? FailureReason { get; }

        /// <summary>
        /// Gets detailed breakdown of memory requirements.
        /// </summary>
        public MemoryBreakdown? Breakdown { get; }

        private BudgetCheckResult(
            bool canProceed,
            long estimatedMemoryBytes,
            long availableBudgetBytes,
            string? failureReason,
            MemoryBreakdown? breakdown)
        {
            CanProceed = canProceed;
            EstimatedMemoryBytes = estimatedMemoryBytes;
            AvailableBudgetBytes = availableBudgetBytes;
            FailureReason = failureReason;
            Breakdown = breakdown;
        }

        /// <summary>
        /// Creates a successful budget check result.
        /// </summary>
        public static BudgetCheckResult Success(
            long estimatedMemoryBytes,
            long availableBudgetBytes,
            MemoryBreakdown breakdown)
        {
            return new BudgetCheckResult(
                true,
                estimatedMemoryBytes,
                availableBudgetBytes,
                null,
                breakdown);
        }

        /// <summary>
        /// Creates a failed budget check result.
        /// </summary>
        public static BudgetCheckResult Failure(
            long estimatedMemoryBytes,
            long availableBudgetBytes,
            string failureReason,
            MemoryBreakdown breakdown)
        {
            return new BudgetCheckResult(
                false,
                estimatedMemoryBytes,
                availableBudgetBytes,
                failureReason,
                breakdown);
        }

        /// <summary>
        /// Gets a summary of the check result.
        /// </summary>
        public string GetSummary()
        {
            var status = CanProceed ? "PASS" : "FAIL";
            var estimatedMB = EstimatedMemoryBytes / 1024.0 / 1024.0;
            var budgetMB = AvailableBudgetBytes / 1024.0 / 1024.0;

            var summary = $"Budget Check [{status}]: Estimated {estimatedMB:F2}MB / Budget {budgetMB:F2}MB";
            
            if (!CanProceed && !string.IsNullOrEmpty(FailureReason))
            {
                summary += $"\nReason: {FailureReason}";
            }

            if (Breakdown != null)
            {
                summary += $"\n{Breakdown.Value.GetSummary()}";
            }

            return summary;
        }
    }

    /// <summary>
    /// Detailed breakdown of memory requirements.
    /// </summary>
    public readonly struct MemoryBreakdown : IEquatable<MemoryBreakdown>
    {
        /// <summary>
        /// Memory for model parameters in bytes.
        /// </summary>
        public readonly long ModelParametersBytes;

        /// <summary>
        /// Memory for activations in bytes.
        /// </summary>
        public readonly long ActivationsBytes;

        /// <summary>
        /// Memory for KV cache in bytes.
        /// </summary>
        public readonly long KVCacheBytes;

        /// <summary>
        /// Memory for gradients in bytes (training only).
        /// </summary>
        public readonly long GradientsBytes;

        /// <summary>
        /// Memory for optimizer state in bytes (training only).
        /// </summary>
        public readonly long OptimizerStateBytes;

        /// <summary>
        /// Additional overhead in bytes.
        /// </summary>
        public readonly long OverheadBytes;

        /// <summary>
        /// Initializes a new instance of the MemoryBreakdown struct.
        /// </summary>
        public MemoryBreakdown(
            long modelParametersBytes,
            long activationsBytes,
            long kvCacheBytes,
            long gradientsBytes,
            long optimizerStateBytes,
            long overheadBytes)
        {
            ModelParametersBytes = modelParametersBytes;
            ActivationsBytes = activationsBytes;
            KVCacheBytes = kvCacheBytes;
            GradientsBytes = gradientsBytes;
            OptimizerStateBytes = optimizerStateBytes;
            OverheadBytes = overheadBytes;
        }

        /// <summary>
        /// Gets the total memory requirement in bytes.
        /// </summary>
        public long TotalBytes =>
            ModelParametersBytes +
            ActivationsBytes +
            KVCacheBytes +
            GradientsBytes +
            OptimizerStateBytes +
            OverheadBytes;

        /// <summary>
        /// Gets a summary of the breakdown.
        /// </summary>
        public readonly string GetSummary()
        {
            return $"Memory Breakdown:\n" +
                   $"  Model Parameters: {ModelParametersBytes / 1024.0 / 1024.0:F2} MB\n" +
                   $"  Activations:      {ActivationsBytes / 1024.0 / 1024.0:F2} MB\n" +
                   $"  KV Cache:         {KVCacheBytes / 1024.0 / 1024.0:F2} MB\n" +
                   $"  Gradients:        {GradientsBytes / 1024.0 / 1024.0:F2} MB\n" +
                   $"  Optimizer State:  {OptimizerStateBytes / 1024.0 / 1024.0:F2} MB\n" +
                   $"  Overhead:         {OverheadBytes / 1024.0 / 1024.0:F2} MB\n" +
                   $"  Total:            {TotalBytes / 1024.0 / 1024.0:F2} MB";
        }

        public bool Equals(MemoryBreakdown other) =>
            ModelParametersBytes == other.ModelParametersBytes &&
            ActivationsBytes == other.ActivationsBytes &&
            KVCacheBytes == other.KVCacheBytes &&
            GradientsBytes == other.GradientsBytes &&
            OptimizerStateBytes == other.OptimizerStateBytes &&
            OverheadBytes == other.OverheadBytes;

        public override bool Equals(object? obj) =>
            obj is MemoryBreakdown other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(
                ModelParametersBytes, ActivationsBytes, KVCacheBytes,
                GradientsBytes, OptimizerStateBytes, OverheadBytes);

        public static bool operator ==(MemoryBreakdown left, MemoryBreakdown right) => left.Equals(right);
        public static bool operator !=(MemoryBreakdown left, MemoryBreakdown right) => !left.Equals(right);
    }
}

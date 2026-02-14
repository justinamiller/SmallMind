namespace SmallMind.Abstractions
{
    /// <summary>
    /// Thrown when a generation budget is exceeded (MaxNewTokens, MaxTimeMs, etc).
    /// Remediation: Increase budget limits or accept partial output.
    /// </summary>
    public class BudgetExceededException : SmallMindException
    {
        /// <summary>
        /// Gets the budget type that was exceeded.
        /// </summary>
        public string BudgetType { get; }

        /// <summary>
        /// Gets the consumed amount.
        /// </summary>
        public long Consumed { get; }

        /// <summary>
        /// Gets the maximum allowed.
        /// </summary>
        public long MaxAllowed { get; }

        /// <summary>
        /// Creates a new BudgetExceededException.
        /// </summary>
        public BudgetExceededException(string budgetType, long consumed, long maxAllowed)
            : base($"{budgetType} budget exceeded: consumed {consumed}, max allowed is {maxAllowed}. " +
                   $"Remediation: increase budget or accept partial results.", "BUDGET_EXCEEDED")
        {
            BudgetType = budgetType;
            Consumed = consumed;
            MaxAllowed = maxAllowed;
        }
    }
}

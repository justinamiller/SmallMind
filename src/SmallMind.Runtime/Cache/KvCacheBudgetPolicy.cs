using System;

namespace SmallMind.Runtime.Cache
{
    /// <summary>
    /// Exception thrown when KV cache budget is exceeded.
    /// </summary>
    public sealed class OutOfBudgetException : Exception
    {
        public long RequestedBytes { get; }
        public long AvailableBytes { get; }
        public long MaxBudgetBytes { get; }

        public OutOfBudgetException(long requestedBytes, long availableBytes, long maxBudgetBytes)
            : base($"KV cache budget exceeded. Requested: {requestedBytes / 1024.0 / 1024.0:F2} MB, " +
                   $"Available: {availableBytes / 1024.0 / 1024.0:F2} MB, " +
                   $"Max Budget: {maxBudgetBytes / 1024.0 / 1024.0:F2} MB")
        {
            RequestedBytes = requestedBytes;
            AvailableBytes = availableBytes;
            MaxBudgetBytes = maxBudgetBytes;
        }
    }

    /// <summary>
    /// Budget policy for KV cache memory management.
    /// Enforces hard memory caps and validates allocation requests before they occur.
    /// </summary>
    public sealed class KvCacheBudgetPolicy
    {
        private readonly long _maxBytesPerSession;
        private readonly int _maxSeqLen;
        private readonly int _nLayers;
        private readonly int _nKvHeads;
        private readonly int _headDim;
        private readonly int _bytesPerElement;

        /// <summary>
        /// Gets the maximum bytes allowed per session.
        /// </summary>
        public long MaxBytesPerSession => _maxBytesPerSession;

        /// <summary>
        /// Gets the maximum sequence length.
        /// </summary>
        public int MaxSeqLen => _maxSeqLen;

        /// <summary>
        /// Creates a budget policy with the given constraints.
        /// </summary>
        /// <param name="maxBytesPerSession">Hard cap on memory per session</param>
        /// <param name="maxSeqLen">Maximum sequence length</param>
        /// <param name="nLayers">Number of transformer layers</param>
        /// <param name="nKvHeads">Number of KV heads (for GQA)</param>
        /// <param name="headDim">Head dimension</param>
        /// <param name="bytesPerElement">Bytes per float (default 4 for float32)</param>
        public KvCacheBudgetPolicy(
            long maxBytesPerSession,
            int maxSeqLen,
            int nLayers,
            int nKvHeads,
            int headDim,
            int bytesPerElement = sizeof(float))
        {
            if (maxBytesPerSession <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxBytesPerSession), "Must be greater than 0");
            if (maxSeqLen <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSeqLen), "Must be greater than 0");
            if (nLayers <= 0)
                throw new ArgumentOutOfRangeException(nameof(nLayers), "Must be greater than 0");
            if (nKvHeads <= 0)
                throw new ArgumentOutOfRangeException(nameof(nKvHeads), "Must be greater than 0");
            if (headDim <= 0)
                throw new ArgumentOutOfRangeException(nameof(headDim), "Must be greater than 0");

            _maxBytesPerSession = maxBytesPerSession;
            _maxSeqLen = maxSeqLen;
            _nLayers = nLayers;
            _nKvHeads = nKvHeads;
            _headDim = headDim;
            _bytesPerElement = bytesPerElement;

            // Validate that the budget can fit the minimum required allocation
            long minRequiredBytes = ComputeRequiredBytes(1);
            if (minRequiredBytes > maxBytesPerSession)
            {
                throw new ArgumentException(
                    $"Budget too small. Need at least {minRequiredBytes / 1024.0 / 1024.0:F2} MB " +
                    $"for 1 token, but budget is {maxBytesPerSession / 1024.0 / 1024.0:F2} MB",
                    nameof(maxBytesPerSession));
            }

            // Validate that maxSeqLen fits within budget
            long maxSeqLenBytes = ComputeRequiredBytes(maxSeqLen);
            if (maxSeqLenBytes > maxBytesPerSession)
            {
                throw new ArgumentException(
                    $"maxSeqLen={maxSeqLen} requires {maxSeqLenBytes / 1024.0 / 1024.0:F2} MB, " +
                    $"which exceeds budget of {maxBytesPerSession / 1024.0 / 1024.0:F2} MB",
                    nameof(maxSeqLen));
            }
        }

        /// <summary>
        /// Computes the bytes required for the given number of tokens.
        /// </summary>
        public long ComputeRequiredBytes(int numTokens)
        {
            if (numTokens <= 0)
                throw new ArgumentOutOfRangeException(nameof(numTokens), "Must be greater than 0");

            // Each token requires storage for K and V across all layers
            // Shape: [nLayers, numTokens, nKvHeads, headDim] for both K and V
            long elementsPerToken = (long)_nKvHeads * _headDim;
            long totalElements = 2L * _nLayers * numTokens * elementsPerToken; // 2x for K and V
            return totalElements * _bytesPerElement;
        }

        /// <summary>
        /// Validates that the requested number of additional tokens fits within the budget.
        /// </summary>
        /// <param name="currentTokens">Current number of tokens in cache</param>
        /// <param name="additionalTokens">Number of tokens to add</param>
        /// <returns>True if the request fits within budget</returns>
        public bool TryReserveTokens(int currentTokens, int additionalTokens)
        {
            if (additionalTokens <= 0)
                return true; // No tokens to reserve

            int totalTokens = currentTokens + additionalTokens;
            if (totalTokens > _maxSeqLen)
                return false; // Exceeds max sequence length

            long requiredBytes = ComputeRequiredBytes(totalTokens);
            return requiredBytes <= _maxBytesPerSession;
        }

        /// <summary>
        /// Validates that the requested number of additional tokens fits within the budget.
        /// Throws OutOfBudgetException if budget is exceeded.
        /// </summary>
        /// <param name="currentTokens">Current number of tokens in cache</param>
        /// <param name="additionalTokens">Number of tokens to add</param>
        public void ValidateReservation(int currentTokens, int additionalTokens)
        {
            if (additionalTokens <= 0)
                return; // No tokens to reserve

            int totalTokens = currentTokens + additionalTokens;
            if (totalTokens > _maxSeqLen)
            {
                throw new OutOfBudgetException(
                    ComputeRequiredBytes(totalTokens),
                    ComputeRequiredBytes(_maxSeqLen),
                    _maxBytesPerSession);
            }

            long requiredBytes = ComputeRequiredBytes(totalTokens);
            if (requiredBytes > _maxBytesPerSession)
            {
                long currentBytes = currentTokens > 0 ? ComputeRequiredBytes(currentTokens) : 0;
                long availableBytes = _maxBytesPerSession - currentBytes;
                throw new OutOfBudgetException(requiredBytes, availableBytes, _maxBytesPerSession);
            }
        }

        /// <summary>
        /// Gets the maximum number of tokens that can fit in the budget.
        /// </summary>
        public int GetMaxTokensForBudget()
        {
            // Binary search to find maximum tokens that fit in budget
            int low = 1;
            int high = _maxSeqLen;
            int result = 1;

            while (low <= high)
            {
                int mid = low + (high - low) / 2;
                long requiredBytes = ComputeRequiredBytes(mid);

                if (requiredBytes <= _maxBytesPerSession)
                {
                    result = mid;
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }

            return result;
        }
    }
}

namespace SmallMind.Abstractions
{
    /// <summary>
    /// Base exception for all SmallMind engine errors.
    /// All public exception types inherit from this.
    /// </summary>
    public class SmallMindException : Exception
    {
        /// <summary>
        /// Gets the error code for programmatic handling.
        /// </summary>
        public string ErrorCode { get; }

        /// <summary>
        /// Creates a new SmallMindException.
        /// </summary>
        public SmallMindException(string message, string errorCode = "SMALLMIND_ERROR")
            : base(message)
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// Creates a new SmallMindException with an inner exception.
        /// </summary>
        public SmallMindException(string message, Exception innerException, string errorCode = "SMALLMIND_ERROR")
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Thrown when attempting to load an unsupported model file format.
    /// Remediation: Ensure model file is .smq format, or enable GGUF import if supported.
    /// </summary>
    public class UnsupportedModelException : SmallMindException
    {
        /// <summary>
        /// Gets the file path that failed to load.
        /// </summary>
        public string FilePath { get; }

        /// <summary>
        /// Gets the file extension that was not supported.
        /// </summary>
        public string Extension { get; }

        /// <summary>
        /// Creates a new UnsupportedModelException.
        /// </summary>
        public UnsupportedModelException(string filePath, string extension, string message)
            : base($"Unsupported model format: {message}", "UNSUPPORTED_MODEL_FORMAT")
        {
            FilePath = filePath;
            Extension = extension;
        }
    }

    /// <summary>
    /// Thrown when a GGUF tensor type is not supported by the engine.
    /// Remediation: Use a different quantization format, or file an issue if this is a common GGUF format.
    /// </summary>
    public class UnsupportedGgufTensorException : SmallMindException
    {
        /// <summary>
        /// Gets the unsupported tensor type value.
        /// </summary>
        public int TensorType { get; }

        /// <summary>
        /// Gets the tensor name that failed.
        /// </summary>
        public string TensorName { get; }

        /// <summary>
        /// Creates a new UnsupportedGgufTensorException.
        /// </summary>
        public UnsupportedGgufTensorException(string tensorName, int tensorType, string message)
            : base($"Unsupported GGUF tensor type: {message}", "UNSUPPORTED_GGUF_TENSOR")
        {
            TensorName = tensorName;
            TensorType = tensorType;
        }
    }

    /// <summary>
    /// Thrown when the context window limit is exceeded.
    /// Remediation: Reduce input length or increase MaxContextTokens in options.
    /// </summary>
    public class ContextLimitExceededException : SmallMindException
    {
        /// <summary>
        /// Gets the total tokens in the conversation.
        /// </summary>
        public int TotalTokens { get; }

        /// <summary>
        /// Gets the context limit.
        /// </summary>
        public int ContextLimit { get; }

        /// <summary>
        /// Gets the system message tokens.
        /// </summary>
        public int SystemTokens { get; }

        /// <summary>
        /// Gets the current message tokens.
        /// </summary>
        public int MessageTokens { get; }

        /// <summary>
        /// Creates a new ContextLimitExceededException.
        /// </summary>
        public ContextLimitExceededException(string message, int totalTokens, int contextLimit, int systemTokens = 0, int messageTokens = 0)
            : base(message, "CONTEXT_LIMIT_EXCEEDED")
        {
            TotalTokens = totalTokens;
            ContextLimit = contextLimit;
            SystemTokens = systemTokens;
            MessageTokens = messageTokens;
        }
    }

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

    /// <summary>
    /// Thrown when RAG retrieval finds insufficient evidence to answer a question.
    /// Remediation: Rephrase query, add more documents to index, or lower confidence threshold.
    /// </summary>
    public class RagInsufficientEvidenceException : SmallMindException
    {
        /// <summary>
        /// Gets the query that failed.
        /// </summary>
        public string Query { get; }

        /// <summary>
        /// Gets the minimum confidence threshold.
        /// </summary>
        public double MinConfidence { get; }

        /// <summary>
        /// Creates a new RagInsufficientEvidenceException.
        /// </summary>
        public RagInsufficientEvidenceException(string query, double minConfidence)
            : base($"Insufficient evidence for query: '{query}'. No results met confidence threshold {minConfidence:F2}. " +
                   $"Remediation: rephrase query, add more documents, or lower threshold.", "RAG_INSUFFICIENT_EVIDENCE")
        {
            Query = query;
            MinConfidence = minConfidence;
        }
    }

    /// <summary>
    /// Thrown when a security violation is detected (malicious input, unauthorized access, etc).
    /// Remediation: Review input for injections, check authorization policies.
    /// </summary>
    public class SecurityViolationException : SmallMindException
    {
        /// <summary>
        /// Gets the violation type.
        /// </summary>
        public string ViolationType { get; }

        /// <summary>
        /// Creates a new SecurityViolationException.
        /// </summary>
        public SecurityViolationException(string violationType, string message)
            : base($"Security violation: {message}", "SECURITY_VIOLATION")
        {
            ViolationType = violationType;
        }
    }

    /// <summary>
    /// Thrown when insufficient memory is available for an operation.
    /// Remediation: Reduce batch size, lower model size, or increase available memory.
    /// </summary>
    public class InsufficientMemoryException : SmallMindException
    {
        /// <summary>
        /// Gets the estimated memory required in bytes.
        /// </summary>
        public long RequiredBytes { get; }

        /// <summary>
        /// Gets the available memory in bytes.
        /// </summary>
        public long AvailableBytes { get; }

        /// <summary>
        /// Creates a new InsufficientMemoryException.
        /// </summary>
        public InsufficientMemoryException(long requiredBytes, long availableBytes)
            : base($"Insufficient memory: operation requires ~{requiredBytes / 1024 / 1024}MB, " +
                   $"but only {availableBytes / 1024 / 1024}MB available (>90% would be used). " +
                   $"Remediation: reduce batch size or lower model size.", "INSUFFICIENT_MEMORY")
        {
            RequiredBytes = requiredBytes;
            AvailableBytes = availableBytes;
        }
    }
}

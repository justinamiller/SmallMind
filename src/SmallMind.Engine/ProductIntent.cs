namespace SmallMind.Engine;

/// <summary>
/// Internal documentation of SmallMind's product intent and feature classification.
/// This class helps maintain clear boundaries between stable runtime features and experimental areas.
/// </summary>
internal static class ProductIntent
{
    /// <summary>
    /// Primary use case: Production-ready, local, CPU-first LLM inference runtime for .NET
    /// </summary>
    public const string PrimaryUseCase = "Local, CPU-first LLM Inference Runtime";

    /// <summary>
    /// Stable runtime invariants that MUST be upheld for the public API contract.
    /// </summary>
    public static class StableRuntimeInvariants
    {
        /// <summary>
        /// Model loading validates format and metadata early, failing fast with actionable errors.
        /// </summary>
        public const string SafeModelLoading = "Safe model loading with early validation";

        /// <summary>
        /// Budgets (MaxNewTokens, MaxContextTokens, TimeoutMs) are enforced consistently across all generation APIs.
        /// </summary>
        public const string BudgetEnforcement = "Consistent budget enforcement (tokens, time, context)";

        /// <summary>
        /// CancellationToken stops generation promptly when requested.
        /// </summary>
        public const string CancellationSupport = "Prompt cancellation with CancellationToken";

        /// <summary>
        /// Deterministic mode (fixed seed) produces identical outputs for same model + prompt + options.
        /// </summary>
        public const string DeterministicGeneration = "Deterministic generation with fixed seed";

        /// <summary>
        /// Thread safety: Engine and ModelHandle are thread-safe for concurrent operations.
        /// ChatSession is single-threaded.
        /// </summary>
        public const string ConcurrencyPolicy = "Engine/ModelHandle thread-safe, ChatSession single-threaded";

        /// <summary>
        /// All public exceptions include actionable error messages with remediation guidance.
        /// </summary>
        public const string ActionableExceptions = "Actionable exceptions with remediation guidance";
    }

    /// <summary>
    /// Feature classification: Stable runtime (guaranteed) vs Experimental (best-effort)
    /// </summary>
    public static class Features
    {
        /// <summary>
        /// Features that are part of the stable runtime guarantee.
        /// </summary>
        public static class StableRuntime
        {
            public const string ModelLoading = "Model loading (.smq, .gguf import)";
            public const string Tokenization = "Tokenization (BPE, Character)";
            public const string TextGeneration = "Text generation (streaming and non-streaming)";
            public const string MultiTurnConversations = "Multi-turn conversations with KV cache";
            public const string ResourceGovernance = "Resource governance (budgets, limits)";
            public const string DeterministicMode = "Deterministic mode for testing";
            public const string RagPipeline = "RAG pipeline (retrieval + generation)";
            public const string Quantization = "Quantized inference (Q8, Q4)";
        }

        /// <summary>
        /// Features that are experimental or best-effort (NOT part of stable guarantee).
        /// </summary>
        public static class Experimental
        {
            public const string Training = "Training large models / fine-tuning";
            public const string ResearchUtilities = "Research utilities and experimental features";
            public const string GpuAcceleration = "GPU acceleration (if present)";
            public const string DistributedServing = "Distributed serving / multi-node";
            public const string AdvancedSampling = "Advanced sampling strategies";
            public const string CustomTokenizers = "Custom tokenization schemes";
            public const string ExperimentalArchitectures = "Experimental model architectures";
        }
    }

    /// <summary>
    /// Mapping of namespaces to stability classification.
    /// </summary>
    public static class NamespaceClassification
    {
        /// <summary>
        /// Stable public API namespaces (semantic versioning applies).
        /// </summary>
        public static readonly string[] StableNamespaces = new[]
        {
            "SmallMind.Abstractions",
            "SmallMind.Engine.SmallMind" // Static factory only
        };

        /// <summary>
        /// Internal implementation namespaces (may change without notice).
        /// </summary>
        public static readonly string[] InternalNamespaces = new[]
        {
            "SmallMind.Core",
            "SmallMind.Runtime",
            "SmallMind.Transformers",
            "SmallMind.Tokenizers",
            "SmallMind.Quantization",
            "SmallMind.Engine" // Internal facade implementations
        };

        /// <summary>
        /// Experimental namespaces (best-effort, may be removed).
        /// Note: Training code is currently in SmallMind.Runtime.Core.Training and SmallMind.Core
        /// but is not exposed through stable public API.
        /// </summary>
        public static readonly string[] ExperimentalNamespaces = new[]
        {
            "SmallMind.Experimental" // Future: move training here
        };
    }

    /// <summary>
    /// Supported model formats and their classification.
    /// </summary>
    public static class ModelFormats
    {
        public static readonly (string Extension, string Status, string Notes)[] Supported = new[]
        {
            (".smq", "Stable", "Native quantized format, recommended for production"),
            (".gguf", "Experimental", "Import via AllowGgufImport=true, converted to SMQ"),
            (".json", "Experimental", "FP32 checkpoint format, primarily for development")
        };

        public static readonly string[] Unsupported = new[]
        {
            ".onnx", ".pt", ".pth", ".safetensors"
        };
    }

    /// <summary>
    /// Supported quantization schemes and their classification.
    /// </summary>
    public static class QuantizationSchemes
    {
        public static readonly (string Scheme, string Status, string Notes)[] Supported = new[]
        {
            ("FP32", "Stable", "Full precision, largest file size"),
            ("Q8", "Stable", "8-bit quantization, ~1% accuracy loss, recommended"),
            ("Q4", "Stable", "4-bit quantization, ~2-3% accuracy loss, good for large models")
        };

        public static readonly string[] Unsupported = new[]
        {
            "Q5", "Q6", "IQ*"
        };
    }

    /// <summary>
    /// Concurrency guarantees for different components.
    /// </summary>
    public static class ConcurrencyGuarantees
    {
        public const string Engine = "Thread-safe: Multiple threads can call LoadModelAsync, GenerateAsync, etc. concurrently";
        public const string ModelHandle = "Thread-safe for reads: Model weights are read-only and shared safely. Concurrent generation allowed.";
        public const string ChatSession = "NOT thread-safe: Each session is designed for single-threaded use. Create separate sessions for concurrent conversations.";
        public const string RagEngine = "Thread-safe: Concurrent queries allowed. Index operations may block.";
        public const string RagIndex = "Read-mostly: Concurrent reads, exclusive writes. Build index once, query many times.";
    }
}

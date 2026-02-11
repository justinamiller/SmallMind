using System;
using System.Collections.Generic;
using System.Threading;

namespace SmallMind
{
    // ============================================================
    // CORE ENGINE INTERFACE
    // ============================================================

    /// <summary>
    /// SmallMind inference engine. This is the main entry point for the public API.
    /// </summary>
    public interface ISmallMindEngine : IDisposable
    {
        /// <summary>
        /// Gets the capabilities of this engine instance.
        /// </summary>
        EngineCapabilities GetCapabilities();

        /// <summary>
        /// Creates a text generation session with the specified options.
        /// </summary>
        ITextGenerationSession CreateTextGenerationSession(TextGenerationOptions options);

        /// <summary>
        /// Creates an embedding session with the specified options.
        /// </summary>
        IEmbeddingSession CreateEmbeddingSession(EmbeddingOptions options);

        /// <summary>
        /// Creates a Level 3 chat client with the specified options.
        /// </summary>
        IChatClient CreateChatClient(ChatClientOptions options);
    }

    // ============================================================
    // ENGINE OPTIONS
    // ============================================================

    /// <summary>
    /// Configuration options for creating a SmallMind engine instance.
    /// </summary>
    public sealed class SmallMindOptions
    {
        /// <summary>
        /// Path to the model file (.smq or .gguf format).
        /// </summary>
        public required string ModelPath { get; init; }

        /// <summary>
        /// Maximum context length in tokens.
        /// Default: 2048
        /// </summary>
        public int MaxContextTokens { get; init; } = 2048;

        /// <summary>
        /// Maximum batch size for parallel requests.
        /// Default: 1 (no batching)
        /// </summary>
        public int MaxBatchSize { get; init; } = 1;

        /// <summary>
        /// Number of CPU threads to use. If null, uses system default.
        /// </summary>
        public int? ThreadCount { get; init; }

        /// <summary>
        /// Enable KV cache optimization for faster inference.
        /// Default: true
        /// </summary>
        public bool EnableKvCache { get; init; } = true;

        /// <summary>
        /// Allow importing GGUF models (requires conversion).
        /// Default: false
        /// </summary>
        public bool AllowGgufImport { get; init; } = false;

        /// <summary>
        /// Directory for caching converted GGUF models.
        /// If null, uses system temp directory.
        /// </summary>
        public string? GgufCacheDirectory { get; init; }

        /// <summary>
        /// Request timeout in milliseconds. If null, no timeout.
        /// </summary>
        public int? RequestTimeoutMs { get; init; }

        /// <summary>
        /// Maximum number of buffered tokens for streaming backpressure control.
        /// If null, no backpressure limit.
        /// Default: null (unbounded)
        /// </summary>
        public int? MaxBufferedTokens { get; init; }

        /// <summary>
        /// Maximum queue depth for batched/multi-session scenarios.
        /// If null, no queue depth limit.
        /// Default: null (unbounded)
        /// </summary>
        public int? MaxQueueDepth { get; init; }

        /// <summary>
        /// Maximum tensor memory budget in bytes.
        /// If null, no memory budget enforcement.
        /// Default: null (unbounded)
        /// </summary>
        public long? MaxTensorBytes { get; init; }

        /// <summary>
        /// Memory budget enforcement mode.
        /// Default: None (no enforcement)
        /// </summary>
        public Abstractions.Telemetry.MemoryBudgetMode MemoryBudgetMode { get; init; } = Abstractions.Telemetry.MemoryBudgetMode.None;

        /// <summary>
        /// Runtime logger for diagnostics and debugging.
        /// If null, uses NullRuntimeLogger (no logging).
        /// Default: null
        /// </summary>
        public Abstractions.Telemetry.IRuntimeLogger? Logger { get; init; }

        /// <summary>
        /// Runtime metrics collector for performance tracking.
        /// If null, uses NullRuntimeMetrics (no metrics).
        /// Default: null
        /// </summary>
        public Abstractions.Telemetry.IRuntimeMetrics? Metrics { get; init; }

        /// <summary>
        /// Diagnostics sink for observability.
        /// </summary>
        public ISmallMindDiagnosticsSink? DiagnosticsSink { get; init; }
    }

    // ============================================================
    // TEXT GENERATION SESSION
    // ============================================================

    /// <summary>
    /// Text generation session for inference.
    /// </summary>
    public interface ITextGenerationSession : IDisposable
    {
        /// <summary>
        /// Generates text from a prompt (blocking, non-streaming).
        /// </summary>
        GenerationResult Generate(TextGenerationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates text from a prompt with streaming tokens.
        /// </summary>
        IAsyncEnumerable<TokenResult> GenerateStreaming(TextGenerationRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for text generation sessions.
    /// </summary>
    public sealed class TextGenerationOptions
    {
        /// <summary>
        /// Sampling temperature (0.0 = deterministic, higher = more random).
        /// Default: 0.7
        /// </summary>
        public float Temperature { get; init; } = 0.7f;

        /// <summary>
        /// Top-p (nucleus) sampling threshold.
        /// Default: 0.9
        /// </summary>
        public float TopP { get; init; } = 0.9f;

        /// <summary>
        /// Top-k sampling (0 = disabled).
        /// Default: 40
        /// </summary>
        public int TopK { get; init; } = 40;

        /// <summary>
        /// Maximum number of output tokens to generate.
        /// Default: 256
        /// </summary>
        public int MaxOutputTokens { get; init; } = 256;

        /// <summary>
        /// Stop sequences that halt generation.
        /// </summary>
        public IReadOnlyList<string> StopSequences { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// Request for text generation.
    /// </summary>
    public sealed class TextGenerationRequest
    {
        /// <summary>
        /// The input prompt.
        /// </summary>
        public required ReadOnlyMemory<char> Prompt { get; init; }

        /// <summary>
        /// Override max output tokens for this request.
        /// </summary>
        public int? MaxOutputTokensOverride { get; init; }

        /// <summary>
        /// Override stop sequences for this request.
        /// </summary>
        public IReadOnlyList<string> StopSequencesOverride { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Random seed for deterministic generation (optional).
        /// </summary>
        public int? Seed { get; init; }
    }

    /// <summary>
    /// Result of a text generation request.
    /// </summary>
    public sealed class GenerationResult
    {
        /// <summary>
        /// The generated text.
        /// </summary>
        public required string Text { get; init; }

        /// <summary>
        /// Token usage statistics.
        /// </summary>
        public required Usage Usage { get; init; }

        /// <summary>
        /// Timing statistics.
        /// </summary>
        public required GenerationTimings Timings { get; init; }

        /// <summary>
        /// Why generation stopped.
        /// </summary>
        public required FinishReason FinishReason { get; init; }

        /// <summary>
        /// Optional warnings (e.g., truncation).
        /// </summary>
        public IReadOnlyList<string>? Warnings { get; init; }
    }

    /// <summary>
    /// Streaming token result.
    /// </summary>
    public sealed class TokenResult
    {
        /// <summary>
        /// The token text.
        /// </summary>
        public required string TokenText { get; init; }

        /// <summary>
        /// The token ID.
        /// </summary>
        public required int TokenId { get; init; }

        /// <summary>
        /// Whether this is a special token (e.g., EOS).
        /// </summary>
        public required bool IsSpecial { get; init; }

        /// <summary>
        /// Partial usage statistics (updated incrementally).
        /// </summary>
        public required UsagePartial Usage { get; init; }

        /// <summary>
        /// Partial timing statistics.
        /// </summary>
        public required GenerationTimingsPartial Timings { get; init; }
    }

    /// <summary>
    /// Token usage statistics.
    /// </summary>
    public sealed class Usage
    {
        /// <summary>
        /// Number of prompt tokens.
        /// </summary>
        public required int PromptTokens { get; init; }

        /// <summary>
        /// Number of completion tokens generated.
        /// </summary>
        public required int CompletionTokens { get; init; }

        /// <summary>
        /// Total tokens (prompt + completion).
        /// </summary>
        public int TotalTokens => PromptTokens + CompletionTokens;
    }

    /// <summary>
    /// Partial usage statistics for streaming.
    /// </summary>
    public sealed class UsagePartial
    {
        /// <summary>
        /// Number of prompt tokens (may not be available during streaming).
        /// </summary>
        public required int PromptTokens { get; init; }

        /// <summary>
        /// Number of completion tokens generated so far.
        /// </summary>
        public required int CompletionTokensSoFar { get; init; }
    }

    /// <summary>
    /// Generation timing statistics.
    /// </summary>
    public sealed class GenerationTimings
    {
        /// <summary>
        /// Time to first token in milliseconds.
        /// </summary>
        public double? TimeToFirstTokenMs { get; }

        /// <summary>
        /// Total elapsed time in milliseconds.
        /// </summary>
        public double TotalMs { get; }

        /// <summary>
        /// Total elapsed time in milliseconds (alias for TotalMs).
        /// </summary>
        public double ElapsedMs => TotalMs;

        /// <summary>
        /// Tokens per second.
        /// </summary>
        public double TokensPerSecond { get; }

        public GenerationTimings(double? timeToFirstTokenMs, double totalMs, int tokenCount)
        {
            TimeToFirstTokenMs = timeToFirstTokenMs;
            TotalMs = totalMs;
            TokensPerSecond = totalMs > 0 ? tokenCount / (totalMs / 1000.0) : 0;
        }
    }

    /// <summary>
    /// Partial timing statistics for streaming.
    /// </summary>
    public sealed class GenerationTimingsPartial
    {
        /// <summary>
        /// Time to first token in milliseconds (null until first token).
        /// </summary>
        public double? TimeToFirstTokenMs { get; init; }

        /// <summary>
        /// Elapsed time so far in milliseconds.
        /// </summary>
        public double ElapsedMs { get; init; }
    }

    /// <summary>
    /// Reason why generation finished.
    /// </summary>
    public enum FinishReason
    {
        /// <summary>
        /// Generation completed naturally (e.g., EOS token).
        /// </summary>
        Completed,

        /// <summary>
        /// Stop sequence was encountered.
        /// </summary>
        StopSequence,

        /// <summary>
        /// Maximum token limit reached.
        /// </summary>
        Length,

        /// <summary>
        /// Request was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// Request timed out.
        /// </summary>
        Timeout,

        /// <summary>
        /// An error occurred.
        /// </summary>
        Error
    }

    // ============================================================
    // EMBEDDING SESSION (PLACEHOLDER)
    // ============================================================

    /// <summary>
    /// Embedding session (not yet implemented).
    /// </summary>
    public interface IEmbeddingSession : IDisposable
    {
        /// <summary>
        /// Embeds text (not yet implemented).
        /// </summary>
        EmbeddingResult Embed(EmbeddingRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for embedding sessions (not yet implemented).
    /// </summary>
    public sealed class EmbeddingOptions
    {
        /// <summary>
        /// Whether to normalize embeddings (not yet implemented).
        /// </summary>
        public bool Normalize { get; init; } = true;
    }

    /// <summary>
    /// Request for embedding (not yet implemented).
    /// </summary>
    public sealed class EmbeddingRequest
    {
        /// <summary>
        /// The input text to embed.
        /// </summary>
        public required ReadOnlyMemory<char> Input { get; init; }
    }

    /// <summary>
    /// Result of an embedding request (not yet implemented).
    /// </summary>
    public sealed class EmbeddingResult
    {
        /// <summary>
        /// The embedding vector.
        /// </summary>
        public required float[] Embedding { get; init; }

        /// <summary>
        /// The embedding vector (alias for Embedding).
        /// </summary>
        public float[] Vector => Embedding;

        /// <summary>
        /// Token usage statistics.
        /// </summary>
        public required Usage Usage { get; init; }
    }

    // ============================================================
    // LEVEL 3 CHAT CLIENT API
    // ============================================================

    /// <summary>
    /// Level 3 chat client interface for product-grade chat sessions.
    /// Supports messages-first design, context policies, tools, and RAG.
    /// </summary>
    public interface IChatClient : IDisposable
    {
        /// <summary>
        /// Sends a chat request and returns the response.
        /// </summary>
        Abstractions.ChatResponse SendChat(Abstractions.ChatRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds a system message to the conversation context.
        /// </summary>
        void AddSystemMessage(string content);

        /// <summary>
        /// Gets the session information (ID, turn count, cache stats).
        /// </summary>
        Abstractions.SessionInfo GetSessionInfo();
    }

    /// <summary>
    /// Options for creating a chat client.
    /// </summary>
    public sealed class ChatClientOptions
    {
        /// <summary>
        /// Gets or sets the session ID. If null, a new ID is generated.
        /// </summary>
        public string? SessionId { get; init; }

        /// <summary>
        /// Gets or sets whether to enable KV cache.
        /// Default: true.
        /// </summary>
        public bool EnableKvCache { get; init; } = true;

        /// <summary>
        /// Gets or sets the maximum KV cache size in tokens.
        /// If null, uses the model's max context.
        /// </summary>
        public int? MaxKvCacheTokens { get; init; }

        /// <summary>
        /// Gets or sets the default context policy for requests.
        /// </summary>
        public Abstractions.IContextPolicy? DefaultContextPolicy { get; init; }

        /// <summary>
        /// Gets or sets the default telemetry implementation.
        /// </summary>
        public Abstractions.IChatTelemetry? DefaultTelemetry { get; init; }

        /// <summary>
        /// Gets or sets whether to enable RAG.
        /// Default: false.
        /// </summary>
        public bool EnableRag { get; init; } = false;
    }

    // ============================================================
    // EXCEPTIONS
    // ============================================================

    /// <summary>
    /// Error codes for SmallMind exceptions.
    /// </summary>
    public enum SmallMindErrorCode
    {
        /// <summary>
        /// Invalid engine or session options.
        /// </summary>
        InvalidOptions,

        /// <summary>
        /// Model loading failed.
        /// </summary>
        ModelLoadFailed,

        /// <summary>
        /// Model format not supported.
        /// </summary>
        UnsupportedModelFormat,

        /// <summary>
        /// Context length exceeded.
        /// </summary>
        ContextOverflow,

        /// <summary>
        /// Request was cancelled or timed out.
        /// </summary>
        RequestCancelled,

        /// <summary>
        /// Inference failed.
        /// </summary>
        InferenceFailed,

        /// <summary>
        /// Internal error.
        /// </summary>
        InternalError
    }

    /// <summary>
    /// Base exception for all SmallMind public API exceptions.
    /// </summary>
    public abstract class SmallMindException : Exception
    {
        /// <summary>
        /// Gets the error code.
        /// </summary>
        public SmallMindErrorCode Code { get; }

        protected SmallMindException(SmallMindErrorCode code, string message) : base(message)
        {
            Code = code;
        }

        protected SmallMindException(SmallMindErrorCode code, string message, Exception innerException) : base(message, innerException)
        {
            Code = code;
        }
    }

    /// <summary>
    /// Thrown when engine options are invalid.
    /// </summary>
    public sealed class InvalidOptionsException : SmallMindException
    {
        public string OptionName { get; }

        public InvalidOptionsException(string optionName, string message)
            : base(SmallMindErrorCode.InvalidOptions, $"Invalid option '{optionName}': {message}")
        {
            OptionName = optionName;
        }
    }

    /// <summary>
    /// Thrown when model loading fails.
    /// </summary>
    public sealed class ModelLoadFailedException : SmallMindException
    {
        public string ModelPath { get; }

        public ModelLoadFailedException(string modelPath, string message, Exception? innerException = null)
            : base(SmallMindErrorCode.ModelLoadFailed, $"Failed to load model '{modelPath}': {message}", innerException!)
        {
            ModelPath = modelPath;
        }
    }

    /// <summary>
    /// Thrown when model format is not supported.
    /// </summary>
    public sealed class UnsupportedModelFormatException : SmallMindException
    {
        public string Extension { get; }

        public UnsupportedModelFormatException(string extension, string message)
            : base(SmallMindErrorCode.UnsupportedModelFormat, message)
        {
            Extension = extension;
        }
    }

    /// <summary>
    /// Thrown when context limit is exceeded.
    /// </summary>
    public sealed class ContextOverflowException : SmallMindException
    {
        public int RequestedLength { get; }
        public int MaxLength { get; }

        public ContextOverflowException(int requestedLength, int maxLength)
            : base(SmallMindErrorCode.ContextOverflow, $"Context overflow: requested {requestedLength} tokens, but max is {maxLength}")
        {
            RequestedLength = requestedLength;
            MaxLength = maxLength;
        }
    }

    /// <summary>
    /// Thrown when a request is cancelled or times out.
    /// </summary>
    public sealed class RequestCancelledException : SmallMindException
    {
        public RequestCancelledException(string message)
            : base(SmallMindErrorCode.RequestCancelled, message)
        {
        }
    }

    /// <summary>
    /// Thrown when inference fails.
    /// </summary>
    public sealed class InferenceFailedException : SmallMindException
    {
        public InferenceFailedException(string message, Exception? innerException = null)
            : base(SmallMindErrorCode.InferenceFailed, message, innerException!)
        {
        }
    }

    /// <summary>
    /// Thrown when an internal error occurs.
    /// </summary>
    public sealed class InternalErrorException : SmallMindException
    {
        public InternalErrorException(string message, Exception? innerException = null)
            : base(SmallMindErrorCode.InternalError, message, innerException!)
        {
        }
    }
}

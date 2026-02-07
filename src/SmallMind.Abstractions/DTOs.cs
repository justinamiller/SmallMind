using System;

namespace SmallMind.Abstractions
{
    /// <summary>
    /// Options for model loading.
    /// </summary>
    public sealed class ModelLoadRequest
    {
        /// <summary>
        /// Gets or sets the path to the model file (.smq or .gguf).
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether to allow GGUF import (if .gguf file is provided).
        /// When true, GGUF files will be automatically converted to SMQ format.
        /// Default: false.
        /// </summary>
        public bool AllowGgufImport { get; set; }

        /// <summary>
        /// Gets or sets the directory for caching imported GGUF models.
        /// If null, uses a default cache directory.
        /// </summary>
        public string? ImportCacheDirectory { get; set; }

        /// <summary>
        /// Gets or sets the number of threads for model operations.
        /// If 0 or negative, uses system default (typically processor count).
        /// Default: 0 (auto).
        /// </summary>
        public int Threads { get; set; }

        /// <summary>
        /// Gets or sets the maximum memory budget in bytes for the model.
        /// If null, no limit is enforced.
        /// </summary>
        public long? MaxMemoryBytes { get; set; }

        /// <summary>
        /// Gets or sets whether to enforce strict memory budgeting.
        /// When true, operations exceeding budget are rejected before execution.
        /// Default: false (advisory limits only).
        /// </summary>
        public bool EnableStrictMemoryBudget { get; set; } = false;

        /// <summary>
        /// Gets or sets the safety margin for strict budgets (0.0 to 1.0).
        /// Actual limit is MaxMemoryBytes * (1 - SafetyMargin).
        /// Default: 0.1 (10% safety margin).
        /// </summary>
        public double StrictBudgetSafetyMargin { get; set; } = 0.1;

        /// <summary>
        /// Gets or sets whether to pre-allocate memory when strict budgeting is enabled.
        /// When true, memory is allocated upfront for predictable performance.
        /// Default: true.
        /// </summary>
        public bool PreAllocateMemory { get; set; } = true;
    }

    /// <summary>
    /// Information about a loaded model.
    /// </summary>
    public sealed class ModelInfo
    {
        /// <summary>
        /// Gets the model name or identifier.
        /// </summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>
        /// Gets the vocabulary size.
        /// </summary>
        public int VocabSize { get; init; }

        /// <summary>
        /// Gets the maximum context length supported.
        /// </summary>
        public int MaxContextLength { get; init; }

        /// <summary>
        /// Gets the quantization schemes used (if any).
        /// </summary>
        public string[] QuantizationSchemes { get; init; } = Array.Empty<string>();

        /// <summary>
        /// Gets the engine version that loaded this model.
        /// </summary>
        public string EngineVersion { get; init; } = string.Empty;

        /// <summary>
        /// Gets the build hash for traceability.
        /// </summary>
        public string BuildHash { get; init; } = string.Empty;
    }

    /// <summary>
    /// Generation mode: deterministic vs exploratory.
    /// </summary>
    public enum GenerationMode
    {
        /// <summary>
        /// Deterministic generation: same seed + prompt = same output.
        /// Use for testing, debugging, or reproducible results.
        /// </summary>
        Deterministic,

        /// <summary>
        /// Exploratory generation: randomized sampling for creative outputs.
        /// Use for production inference with varied results.
        /// </summary>
        Exploratory
    }

    /// <summary>
    /// Request for text generation.
    /// </summary>
    public sealed class GenerationRequest
    {
        /// <summary>
        /// Gets or sets the input prompt.
        /// </summary>
        public string Prompt { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the generation options.
        /// </summary>
        public GenerationOptions Options { get; set; } = new GenerationOptions();
    }

    /// <summary>
    /// Options for text generation.
    /// </summary>
    public sealed class GenerationOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of new tokens to generate.
        /// Default: 100.
        /// </summary>
        public int MaxNewTokens { get; set; } = 100;

        /// <summary>
        /// Gets or sets the maximum total context tokens (input + generated).
        /// Default: 4096.
        /// </summary>
        public int MaxContextTokens { get; set; } = 4096;

        /// <summary>
        /// Gets or sets the timeout in milliseconds.
        /// 0 means no timeout.
        /// Default: 0.
        /// </summary>
        public int TimeoutMs { get; set; }

        /// <summary>
        /// Gets or sets the generation mode.
        /// Default: Exploratory.
        /// </summary>
        public GenerationMode Mode { get; set; } = GenerationMode.Exploratory;

        /// <summary>
        /// Gets or sets the random seed for deterministic generation.
        /// Only used when Mode is Deterministic.
        /// Default: 42.
        /// </summary>
        public uint Seed { get; set; } = 42;

        /// <summary>
        /// Gets or sets the temperature for sampling (higher = more random).
        /// Default: 0.8.
        /// </summary>
        public double Temperature { get; set; } = 0.8;

        /// <summary>
        /// Gets or sets the top-k value for sampling (0 to disable).
        /// Default: 40.
        /// </summary>
        public int TopK { get; set; } = 40;

        /// <summary>
        /// Gets or sets the top-p (nucleus sampling) value.
        /// Default: 0.95.
        /// </summary>
        public double TopP { get; set; } = 0.95;

        /// <summary>
        /// Gets or sets stop sequences to end generation.
        /// </summary>
        public string[]? Stop { get; set; }

        /// <summary>
        /// Gets or sets output constraints (e.g., JSON schema).
        /// </summary>
        public OutputConstraints? Constraints { get; set; }
    }

    /// <summary>
    /// Output constraints for structured generation.
    /// </summary>
    public sealed class OutputConstraints
    {
        /// <summary>
        /// Gets or sets the JSON schema for output validation.
        /// </summary>
        public string? JsonSchema { get; set; }

        /// <summary>
        /// Gets or sets the regex pattern for output validation.
        /// </summary>
        public string? RegexPattern { get; set; }
    }

    /// <summary>
    /// Result of text generation.
    /// </summary>
    public sealed class GenerationResult
    {
        /// <summary>
        /// Gets the generated text.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets the number of tokens generated.
        /// </summary>
        public int GeneratedTokens { get; init; }

        /// <summary>
        /// Gets whether generation was stopped by a budget limit.
        /// </summary>
        public bool StoppedByBudget { get; init; }

        /// <summary>
        /// Gets the reason generation stopped.
        /// </summary>
        public string? StopReason { get; init; }

        /// <summary>
        /// Gets the citations (for RAG).
        /// </summary>
        public RagCitation[]? Citations { get; init; }
    }

    /// <summary>
    /// Token event kind for streaming.
    /// </summary>
    public enum TokenEventKind
    {
        /// <summary>
        /// Generation started.
        /// </summary>
        Started,

        /// <summary>
        /// A token was generated.
        /// </summary>
        Token,

        /// <summary>
        /// Generation completed successfully.
        /// </summary>
        Completed,

        /// <summary>
        /// An error occurred during generation.
        /// </summary>
        Error,

        /// <summary>
        /// Generation was cancelled by user request or timeout.
        /// </summary>
        Cancelled
    }

    /// <summary>
    /// Event emitted during streaming generation.
    /// </summary>
    public readonly struct TokenEvent : IEquatable<TokenEvent>
    {
        /// <summary>
        /// Gets the event kind.
        /// </summary>
        public readonly TokenEventKind Kind;

        /// <summary>
        /// Gets the generated text for this token.
        /// </summary>
        public readonly ReadOnlyMemory<char> Text;

        /// <summary>
        /// Gets the token ID.
        /// </summary>
        public readonly int TokenId;

        /// <summary>
        /// Gets the total number of tokens generated so far.
        /// </summary>
        public readonly int GeneratedTokens;

        /// <summary>
        /// Gets whether this is the final event.
        /// </summary>
        public readonly bool IsFinal;

        /// <summary>
        /// Gets the error message (only when Kind is Error).
        /// </summary>
        public readonly string? Error;

        /// <summary>
        /// Initializes a new instance of the TokenEvent struct.
        /// </summary>
        public TokenEvent(
            TokenEventKind kind,
            ReadOnlyMemory<char> text,
            int tokenId,
            int generatedTokens,
            bool isFinal,
            string? error = null)
        {
            Kind = kind;
            Text = text;
            TokenId = tokenId;
            GeneratedTokens = generatedTokens;
            IsFinal = isFinal;
            Error = error;
        }

        /// <summary>
        /// Determines whether the specified TokenEvent is equal to the current TokenEvent.
        /// </summary>
        /// <param name="other">The TokenEvent to compare with the current TokenEvent.</param>
        /// <returns>true if the specified TokenEvent is equal to the current TokenEvent; otherwise, false.</returns>
        public bool Equals(TokenEvent other) =>
            Kind == other.Kind &&
            Text.Span.SequenceEqual(other.Text.Span) &&
            TokenId == other.TokenId &&
            GeneratedTokens == other.GeneratedTokens &&
            IsFinal == other.IsFinal &&
            Error == other.Error;

        /// <summary>
        /// Determines whether the specified object is equal to the current TokenEvent.
        /// </summary>
        /// <param name="obj">The object to compare with the current TokenEvent.</param>
        /// <returns>true if the specified object is equal to the current TokenEvent; otherwise, false.</returns>
        public override bool Equals(object? obj) =>
            obj is TokenEvent other && Equals(other);

        /// <summary>
        /// Returns the hash code for this TokenEvent.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode() =>
            HashCode.Combine(Kind, TokenId, GeneratedTokens, IsFinal, Error);

        /// <summary>
        /// Determines whether two specified TokenEvent instances are equal.
        /// </summary>
        /// <param name="left">The first TokenEvent to compare.</param>
        /// <param name="right">The second TokenEvent to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(TokenEvent left, TokenEvent right) => left.Equals(right);
        
        /// <summary>
        /// Determines whether two specified TokenEvent instances are not equal.
        /// </summary>
        /// <param name="left">The first TokenEvent to compare.</param>
        /// <param name="right">The second TokenEvent to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(TokenEvent left, TokenEvent right) => !left.Equals(right);
    }

    /// <summary>
    /// Chat role.
    /// </summary>
    public enum ChatRole
    {
        /// <summary>
        /// System role (instructions/context).
        /// </summary>
        System,

        /// <summary>
        /// User role (user input).
        /// </summary>
        User,

        /// <summary>
        /// Assistant role (model output).
        /// </summary>
        Assistant
    }

    /// <summary>
    /// A message in a chat conversation.
    /// </summary>
    public sealed class ChatMessage
    {
        /// <summary>
        /// Gets or sets the role.
        /// </summary>
        public ChatRole Role { get; set; }

        /// <summary>
        /// Gets or sets the content.
        /// </summary>
        public string Content { get; set; } = string.Empty;
    }

    /// <summary>
    /// Options for creating a chat session.
    /// </summary>
    public sealed class SessionOptions
    {
        /// <summary>
        /// Gets or sets a caller-supplied session ID for tracking.
        /// If null, the engine will generate one.
        /// </summary>
        public string? SessionId { get; set; }

        /// <summary>
        /// Gets or sets whether to enable KV cache for this session.
        /// Default: true.
        /// </summary>
        public bool EnableKvCache { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum KV cache size in tokens.
        /// If null, uses the model's max context length.
        /// </summary>
        public int? MaxKvCacheTokens { get; set; }
    }

    /// <summary>
    /// Information about a chat session.
    /// </summary>
    public readonly struct SessionInfo : IEquatable<SessionInfo>
    {
        /// <summary>
        /// Gets the session ID.
        /// </summary>
        public readonly string SessionId;

        /// <summary>
        /// Gets when the session was created.
        /// </summary>
        public readonly DateTimeOffset CreatedAt;

        /// <summary>
        /// Gets the number of turns in this session.
        /// </summary>
        public readonly int TurnCount;

        /// <summary>
        /// Gets the current KV cache size in tokens.
        /// </summary>
        public readonly int KvCacheTokens;

        /// <summary>
        /// Initializes a new instance of the SessionInfo struct.
        /// </summary>
        public SessionInfo(string sessionId, DateTimeOffset createdAt, int turnCount, int kvCacheTokens)
        {
            SessionId = sessionId ?? string.Empty;
            CreatedAt = createdAt;
            TurnCount = turnCount;
            KvCacheTokens = kvCacheTokens;
        }

        /// <summary>
        /// Determines whether the specified SessionInfo is equal to the current SessionInfo.
        /// </summary>
        /// <param name="other">The SessionInfo to compare with the current SessionInfo.</param>
        /// <returns>true if the specified SessionInfo is equal to the current SessionInfo; otherwise, false.</returns>
        public bool Equals(SessionInfo other) =>
            SessionId == other.SessionId &&
            CreatedAt == other.CreatedAt &&
            TurnCount == other.TurnCount &&
            KvCacheTokens == other.KvCacheTokens;

        /// <summary>
        /// Determines whether the specified object is equal to the current SessionInfo.
        /// </summary>
        /// <param name="obj">The object to compare with the current SessionInfo.</param>
        /// <returns>true if the specified object is equal to the current SessionInfo; otherwise, false.</returns>
        public override bool Equals(object? obj) =>
            obj is SessionInfo other && Equals(other);

        /// <summary>
        /// Returns the hash code for this SessionInfo.
        /// </summary>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode() =>
            HashCode.Combine(SessionId, CreatedAt, TurnCount, KvCacheTokens);

        /// <summary>
        /// Determines whether two specified SessionInfo instances are equal.
        /// </summary>
        /// <param name="left">The first SessionInfo to compare.</param>
        /// <param name="right">The second SessionInfo to compare.</param>
        /// <returns>true if left and right are equal; otherwise, false.</returns>
        public static bool operator ==(SessionInfo left, SessionInfo right) => left.Equals(right);
        
        /// <summary>
        /// Determines whether two specified SessionInfo instances are not equal.
        /// </summary>
        /// <param name="left">The first SessionInfo to compare.</param>
        /// <param name="right">The second SessionInfo to compare.</param>
        /// <returns>true if left and right are not equal; otherwise, false.</returns>
        public static bool operator !=(SessionInfo left, SessionInfo right) => !left.Equals(right);
    }

    /// <summary>
    /// Engine capabilities (for discovery).
    /// </summary>
    public sealed class EngineCapabilities
    {
        /// <summary>
        /// Gets whether the engine supports quantized inference.
        /// </summary>
        public bool SupportsQuantizedInference { get; init; }

        /// <summary>
        /// Gets whether the engine supports GGUF import.
        /// </summary>
        public bool SupportsGgufImport { get; init; }

        /// <summary>
        /// Gets whether the engine supports RAG.
        /// </summary>
        public bool SupportsRag { get; init; }

        /// <summary>
        /// Gets whether the engine supports KV cache.
        /// </summary>
        public bool SupportsKvCache { get; init; }

        /// <summary>
        /// Gets whether the engine supports batching.
        /// </summary>
        public bool SupportsBatching { get; init; }

        /// <summary>
        /// Gets whether the engine supports deterministic mode.
        /// </summary>
        public bool SupportsDeterministicMode { get; init; }

        /// <summary>
        /// Gets whether the engine supports streaming.
        /// </summary>
        public bool SupportsStreaming { get; init; }
    }
}

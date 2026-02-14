using SmallMind.Abstractions.Telemetry;

namespace SmallMind.Abstractions
{
    // ============================================================================
    // LEVEL 3 CHAT MODELS - Messages-first design for product-grade chat runtime
    // ============================================================================

    /// <summary>
    /// Response format options for structured output.
    /// </summary>
    public enum ResponseFormatType
    {
        /// <summary>
        /// Plain text response (default).
        /// </summary>
        Text,

        /// <summary>
        /// JSON object response (any valid JSON).
        /// </summary>
        JsonObject,

        /// <summary>
        /// JSON response conforming to a specific schema.
        /// </summary>
        JsonSchema
    }

    /// <summary>
    /// Specifies the desired response format for structured output.
    /// </summary>
    public readonly struct ResponseFormat : IEquatable<ResponseFormat>
    {
        /// <summary>
        /// Gets or sets the response format type.
        /// </summary>
        public required ResponseFormatType Type { get; init; }

        /// <summary>
        /// Gets or sets the JSON schema definition (required when Type is JsonSchema).
        /// Schema must be a valid JSON Schema subset supported by SmallMind.
        /// </summary>
        public string? Schema { get; init; }

        /// <summary>
        /// Creates a text format (default).
        /// </summary>
        public static ResponseFormat Text() => new ResponseFormat { Type = ResponseFormatType.Text };

        /// <summary>
        /// Creates a JSON object format (any valid JSON).
        /// </summary>
        public static ResponseFormat JsonObject() => new ResponseFormat { Type = ResponseFormatType.JsonObject };

        /// <summary>
        /// Creates a JSON schema format with the specified schema.
        /// </summary>
        public static ResponseFormat JsonSchema(string schema) => new ResponseFormat
        {
            Type = ResponseFormatType.JsonSchema,
            Schema = schema
        };

        /// <summary>
        /// Determines whether the specified ResponseFormat is equal to the current ResponseFormat.
        /// </summary>
        public bool Equals(ResponseFormat other) =>
            Type == other.Type &&
            Schema == other.Schema;

        /// <summary>
        /// Determines whether the specified object is equal to the current ResponseFormat.
        /// </summary>
        public override bool Equals(object? obj) =>
            obj is ResponseFormat other && Equals(other);

        /// <summary>
        /// Returns the hash code for this ResponseFormat.
        /// </summary>
        public override int GetHashCode() =>
            HashCode.Combine(Type, Schema);

        /// <summary>
        /// Determines whether two specified ResponseFormat instances are equal.
        /// </summary>
        public static bool operator ==(ResponseFormat left, ResponseFormat right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified ResponseFormat instances are not equal.
        /// </summary>
        public static bool operator !=(ResponseFormat left, ResponseFormat right) => !left.Equals(right);
    }

    /// <summary>
    /// Tool definition for function calling.
    /// </summary>
    public sealed class ToolDefinition
    {
        /// <summary>
        /// Gets or sets the tool name (must be unique in the request).
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tool description (helps the model decide when to use it).
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the JSON schema for the tool's input parameters.
        /// Must be a valid JSON Schema subset supported by SmallMind.
        /// </summary>
        public string? ParametersSchema { get; set; }
    }

    /// <summary>
    /// Represents a tool call request from the model.
    /// </summary>
    public readonly struct ToolCall : IEquatable<ToolCall>
    {
        /// <summary>
        /// Gets or sets the unique ID for this tool call (for correlation).
        /// </summary>
        public required string Id { get; init; }

        /// <summary>
        /// Gets or sets the tool name to call.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// Gets or sets the arguments as a JSON string.
        /// </summary>
        public required string ArgumentsJson { get; init; }

        /// <summary>
        /// Determines whether the specified ToolCall is equal to the current ToolCall.
        /// </summary>
        public bool Equals(ToolCall other) =>
            Id == other.Id &&
            Name == other.Name &&
            ArgumentsJson == other.ArgumentsJson;

        /// <summary>
        /// Determines whether the specified object is equal to the current ToolCall.
        /// </summary>
        public override bool Equals(object? obj) =>
            obj is ToolCall other && Equals(other);

        /// <summary>
        /// Returns the hash code for this ToolCall.
        /// </summary>
        public override int GetHashCode() =>
            HashCode.Combine(Id, Name, ArgumentsJson);

        /// <summary>
        /// Determines whether two specified ToolCall instances are equal.
        /// </summary>
        public static bool operator ==(ToolCall left, ToolCall right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified ToolCall instances are not equal.
        /// </summary>
        public static bool operator !=(ToolCall left, ToolCall right) => !left.Equals(right);
    }

    /// <summary>
    /// Represents the result of a tool execution.
    /// </summary>
    public readonly struct ToolResult : IEquatable<ToolResult>
    {
        /// <summary>
        /// Gets or sets the tool call ID this result corresponds to.
        /// </summary>
        public required string ToolCallId { get; init; }

        /// <summary>
        /// Gets or sets the result content (can be text, JSON, etc).
        /// </summary>
        public required string Content { get; init; }

        /// <summary>
        /// Gets or sets whether the tool execution resulted in an error.
        /// </summary>
        public required bool IsError { get; init; }

        /// <summary>
        /// Determines whether the specified ToolResult is equal to the current ToolResult.
        /// </summary>
        public bool Equals(ToolResult other) =>
            ToolCallId == other.ToolCallId &&
            Content == other.Content &&
            IsError == other.IsError;

        /// <summary>
        /// Determines whether the specified object is equal to the current ToolResult.
        /// </summary>
        public override bool Equals(object? obj) =>
            obj is ToolResult other && Equals(other);

        /// <summary>
        /// Returns the hash code for this ToolResult.
        /// </summary>
        public override int GetHashCode() =>
            HashCode.Combine(ToolCallId, Content, IsError);

        /// <summary>
        /// Determines whether two specified ToolResult instances are equal.
        /// </summary>
        public static bool operator ==(ToolResult left, ToolResult right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified ToolResult instances are not equal.
        /// </summary>
        public static bool operator !=(ToolResult left, ToolResult right) => !left.Equals(right);
    }

    /// <summary>
    /// Enhanced chat message with Tool role and metadata support.
    /// </summary>
    public sealed class ChatMessageV3
    {
        /// <summary>
        /// Gets or sets the role.
        /// </summary>
        public ChatRole Role { get; set; }

        /// <summary>
        /// Gets or sets the message content.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional name (for user or tool messages).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets tool calls (only for Assistant role when requesting tool execution).
        /// </summary>
        public IReadOnlyList<ToolCall>? ToolCalls { get; set; }

        /// <summary>
        /// Gets or sets the tool call ID (only for Tool role messages).
        /// </summary>
        public string? ToolCallId { get; set; }

        /// <summary>
        /// Gets or sets optional metadata for this message.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; set; }
    }

    /// <summary>
    /// Request for chat completion with messages-first design.
    /// </summary>
    public sealed class ChatRequest
    {
        /// <summary>
        /// Gets or sets the list of messages in the conversation.
        /// Messages are processed in order.
        /// </summary>
        public IReadOnlyList<ChatMessageV3> Messages { get; set; } = Array.Empty<ChatMessageV3>();

        /// <summary>
        /// Gets or sets the generation options.
        /// </summary>
        public GenerationOptions? Options { get; set; }

        /// <summary>
        /// Gets or sets available tools for function calling.
        /// </summary>
        public IReadOnlyList<ToolDefinition>? Tools { get; set; }

        /// <summary>
        /// Gets or sets the desired response format.
        /// </summary>
        public ResponseFormat? ResponseFormat { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of tool calls allowed in this request.
        /// Default: 10. Set to 0 to disable tool calling.
        /// </summary>
        public int MaxToolCalls { get; set; } = 10;

        /// <summary>
        /// Gets or sets optional retrieved context chunks (for RAG).
        /// </summary>
        public IReadOnlyList<RetrievedChunk>? RetrievedContext { get; set; }

        /// <summary>
        /// Gets or sets the context policy to apply for this request.
        /// If null, uses the session's default policy or no policy.
        /// </summary>
        public IContextPolicy? ContextPolicy { get; set; }
    }

    /// <summary>
    /// Response from chat completion.
    /// </summary>
    public sealed class ChatResponse
    {
        /// <summary>
        /// Gets the assistant's message.
        /// </summary>
        public ChatMessageV3 Message { get; init; } = new ChatMessageV3 { Role = ChatRole.Assistant };

        /// <summary>
        /// Gets the finish reason.
        /// </summary>
        public string FinishReason { get; init; } = string.Empty;

        /// <summary>
        /// Gets the usage statistics.
        /// </summary>
        public required UsageStats Usage { get; init; }

        /// <summary>
        /// Gets citations (for RAG responses).
        /// </summary>
        public IReadOnlyList<Citation>? Citations { get; init; }

        /// <summary>
        /// Gets warnings from the generation process.
        /// </summary>
        public IReadOnlyList<string>? Warnings { get; init; }
    }

    /// <summary>
    /// Token usage statistics for a chat completion.
    /// </summary>
    public readonly struct UsageStats : IEquatable<UsageStats>
    {
        /// <summary>
        /// Gets the number of tokens in the prompt.
        /// </summary>
        public required int PromptTokens { get; init; }

        /// <summary>
        /// Gets the number of tokens generated.
        /// </summary>
        public required int CompletionTokens { get; init; }

        /// <summary>
        /// Gets the total tokens (prompt + completion).
        /// </summary>
        public int TotalTokens => PromptTokens + CompletionTokens;

        /// <summary>
        /// Gets the time to first token in milliseconds (TTFT).
        /// </summary>
        public required double TimeToFirstTokenMs { get; init; }

        /// <summary>
        /// Gets the tokens per second rate.
        /// </summary>
        public required double TokensPerSecond { get; init; }

        /// <summary>
        /// Determines whether the specified UsageStats is equal to the current UsageStats.
        /// </summary>
        public bool Equals(UsageStats other) =>
            PromptTokens == other.PromptTokens &&
            CompletionTokens == other.CompletionTokens &&
            TimeToFirstTokenMs == other.TimeToFirstTokenMs &&
            TokensPerSecond == other.TokensPerSecond;

        /// <summary>
        /// Determines whether the specified object is equal to the current UsageStats.
        /// </summary>
        public override bool Equals(object? obj) =>
            obj is UsageStats other && Equals(other);

        /// <summary>
        /// Returns the hash code for this UsageStats.
        /// </summary>
        public override int GetHashCode() =>
            HashCode.Combine(PromptTokens, CompletionTokens, TimeToFirstTokenMs, TokensPerSecond);

        /// <summary>
        /// Determines whether two specified UsageStats instances are equal.
        /// </summary>
        public static bool operator ==(UsageStats left, UsageStats right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified UsageStats instances are not equal.
        /// </summary>
        public static bool operator !=(UsageStats left, UsageStats right) => !left.Equals(right);
    }

    /// <summary>
    /// Retrieved context chunk for RAG.
    /// </summary>
    public readonly struct RetrievedChunk : IEquatable<RetrievedChunk>
    {
        /// <summary>
        /// Gets or sets the chunk content.
        /// </summary>
        public required string Content { get; init; }

        /// <summary>
        /// Gets or sets the source identifier.
        /// </summary>
        public required string Source { get; init; }

        /// <summary>
        /// Gets or sets the relevance score (0.0 to 1.0).
        /// </summary>
        public required float Score { get; init; }

        /// <summary>
        /// Gets or sets optional metadata.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; init; }

        /// <summary>
        /// Determines whether the specified RetrievedChunk is equal to the current RetrievedChunk.
        /// </summary>
        public bool Equals(RetrievedChunk other)
        {
            if (Content != other.Content || Source != other.Source || Score != other.Score)
                return false;

            // Compare metadata dictionaries
            if (Metadata == null && other.Metadata == null)
                return true;
            if (Metadata == null || other.Metadata == null)
                return false;
            if (Metadata.Count != other.Metadata.Count)
                return false;

            foreach (var kvp in Metadata)
            {
                if (!other.Metadata.TryGetValue(kvp.Key, out var otherValue) || kvp.Value != otherValue)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current RetrievedChunk.
        /// </summary>
        public override bool Equals(object? obj) =>
            obj is RetrievedChunk other && Equals(other);

        /// <summary>
        /// Returns the hash code for this RetrievedChunk.
        /// </summary>
        public override int GetHashCode()
        {
            var hash = HashCode.Combine(Content, Source, Score);
            if (Metadata != null)
            {
                // Use count only to avoid non-deterministic ordering issues
                hash = HashCode.Combine(hash, Metadata.Count);
            }
            return hash;
        }

        /// <summary>
        /// Determines whether two specified RetrievedChunk instances are equal.
        /// </summary>
        public static bool operator ==(RetrievedChunk left, RetrievedChunk right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specified RetrievedChunk instances are not equal.
        /// </summary>
        public static bool operator !=(RetrievedChunk left, RetrievedChunk right) => !left.Equals(right);
    }

    // ============================================================================
    // CONTEXT POLICY INTERFACES
    // ============================================================================

    /// <summary>
    /// Policy for managing conversation context when it exceeds the model's limit.
    /// </summary>
    public interface IContextPolicy
    {
        /// <summary>
        /// Applies the context policy to fit messages within the token budget.
        /// </summary>
        /// <param name="messages">Input messages.</param>
        /// <param name="maxTokens">Maximum tokens allowed.</param>
        /// <param name="tokenizer">Tokenizer for counting tokens.</param>
        /// <returns>Filtered messages that fit within the budget.</returns>
        IReadOnlyList<ChatMessageV3> Apply(
            IReadOnlyList<ChatMessageV3> messages,
            int maxTokens,
            ITokenCounter tokenizer);

        /// <summary>
        /// Gets whether this policy is deterministic (same input = same output).
        /// </summary>
        bool IsDeterministic { get; }
    }

    /// <summary>
    /// Token counter interface for context budgeting.
    /// </summary>
    public interface ITokenCounter
    {
        /// <summary>
        /// Counts tokens in the given text.
        /// </summary>
        int CountTokens(string text);
    }

    /// <summary>
    /// Optional summarization hook for context management.
    /// </summary>
    public interface ISummarizer
    {
        /// <summary>
        /// Summarizes old conversation turns to compress context.
        /// </summary>
        /// <param name="messages">Messages to summarize.</param>
        /// <returns>Summary text.</returns>
        string Summarize(IReadOnlyList<ChatMessageV3> messages);
    }

    // ============================================================================
    // TELEMETRY INTERFACES
    // ============================================================================

    /// <summary>
    /// Telemetry hook for observability in chat sessions.
    /// </summary>
    public interface IChatTelemetry
    {
        /// <summary>
        /// Gets a default no-op implementation of IChatTelemetry.
        /// </summary>
        static IChatTelemetry Default => NoOpTelemetry.Instance;

        /// <summary>
        /// Called when a request starts.
        /// </summary>
        void OnRequestStart(string sessionId, int messageCount);

        /// <summary>
        /// Called when the first token is generated.
        /// </summary>
        void OnFirstToken(string sessionId, double elapsedMs);

        /// <summary>
        /// Called when a request completes.
        /// </summary>
        void OnRequestComplete(string sessionId, UsageStats usage);

        /// <summary>
        /// Called when context policy is applied.
        /// </summary>
        void OnContextPolicyApplied(string sessionId, string policyName, int originalTokens, int finalTokens);

        /// <summary>
        /// Called when KV cache is hit or missed.
        /// </summary>
        void OnKvCacheAccess(string sessionId, bool hit, int cachedTokens);

        /// <summary>
        /// Called when a tool is invoked.
        /// </summary>
        void OnToolCall(string sessionId, string toolName, double elapsedMs);

        /// <summary>
        /// Called when a session's KV cache budget is exceeded.
        /// </summary>
        void OnKvCacheBudgetExceeded(string sessionId, long currentBytes, long maxBytes);

        /// <summary>
        /// Called when a KV cache entry is evicted.
        /// </summary>
        void OnKvCacheEviction(string evictedSessionId, string reason, long freedBytes);
    }

    /// <summary>
    /// No-op telemetry implementation (default).
    /// </summary>
    public sealed class NoOpTelemetry : IChatTelemetry
    {
        /// <summary>
        /// Singleton instance of NoOpTelemetry.
        /// </summary>
        public static NoOpTelemetry Instance { get; } = new NoOpTelemetry();

        private NoOpTelemetry() { }

        public void OnRequestStart(string sessionId, int messageCount) { }
        public void OnFirstToken(string sessionId, double elapsedMs) { }
        public void OnRequestComplete(string sessionId, UsageStats usage) { }
        public void OnContextPolicyApplied(string sessionId, string policyName, int originalTokens, int finalTokens) { }
        public void OnKvCacheAccess(string sessionId, bool hit, int cachedTokens) { }
        public void OnToolCall(string sessionId, string toolName, double elapsedMs) { }
        public void OnKvCacheBudgetExceeded(string sessionId, long currentBytes, long maxBytes) { }
        public void OnKvCacheEviction(string evictedSessionId, string reason, long freedBytes) { }
    }

    /// <summary>
    /// Console logger implementation for telemetry.
    /// </summary>
    public sealed class ConsoleTelemetry : IChatTelemetry
    {
        private readonly IRuntimeLogger _logger;

        public ConsoleTelemetry(IRuntimeLogger? logger = null)
        {
            _logger = logger ?? NullRuntimeLogger.Instance;
        }

        public void OnRequestStart(string sessionId, int messageCount)
        {
            _logger.Info($"[{sessionId}] Request started with {messageCount} messages");
        }

        public void OnFirstToken(string sessionId, double elapsedMs)
        {
            _logger.Info($"[{sessionId}] First token: {elapsedMs:F2}ms (TTFT)");
        }

        public void OnRequestComplete(string sessionId, UsageStats usage)
        {
            _logger.Info($"[{sessionId}] Completed: {usage.PromptTokens} prompt + {usage.CompletionTokens} completion = {usage.TotalTokens} total");
            _logger.Info($"[{sessionId}] Performance: {usage.TokensPerSecond:F2} tok/s");
        }

        public void OnContextPolicyApplied(string sessionId, string policyName, int originalTokens, int finalTokens)
        {
            _logger.Info($"[{sessionId}] Context policy '{policyName}': {originalTokens} → {finalTokens} tokens");
        }

        public void OnKvCacheAccess(string sessionId, bool hit, int cachedTokens)
        {
            _logger.Info($"[{sessionId}] KV cache {(hit ? "HIT" : "MISS")}: {cachedTokens} cached tokens");
        }

        public void OnToolCall(string sessionId, string toolName, double elapsedMs)
        {
            _logger.Info($"[{sessionId}] Tool call '{toolName}': {elapsedMs:F2}ms");
        }

        public void OnKvCacheBudgetExceeded(string sessionId, long currentBytes, long maxBytes)
        {
            _logger.Info($"[{sessionId}] KV cache budget EXCEEDED: {currentBytes / 1024 / 1024}MB / {maxBytes / 1024 / 1024}MB");
        }

        public void OnKvCacheEviction(string evictedSessionId, string reason, long freedBytes)
        {
            _logger.Info($"[EVICTION] Session '{evictedSessionId}' evicted ({reason}): freed {freedBytes / 1024 / 1024}MB");
        }
    }

    // ============================================================================
    // TOOL EXECUTOR INTERFACE
    // ============================================================================

    /// <summary>
    /// Interface for executing tools during function calling.
    /// </summary>
    public interface IToolExecutor
    {
        /// <summary>
        /// Executes a tool and returns the result.
        /// </summary>
        /// <param name="toolCall">The tool call to execute.</param>
        /// <returns>The execution result.</returns>
        ToolResult Execute(ToolCall toolCall);
    }

    // ============================================================================
    // RAG RETRIEVAL PROVIDER INTERFACE
    // ============================================================================

    /// <summary>
    /// Interface for retrieving context chunks for RAG.
    /// </summary>
    public interface IRetrievalProvider
    {
        /// <summary>
        /// Gets a default no-op implementation of IRetrievalProvider.
        /// </summary>
        static IRetrievalProvider Default => NoOpRetrievalProvider.Instance;

        /// <summary>
        /// Retrieves relevant chunks for the given query.
        /// </summary>
        /// <param name="query">The user's query.</param>
        /// <param name="topK">Number of top chunks to retrieve.</param>
        /// <returns>Retrieved chunks ordered by relevance.</returns>
        IReadOnlyList<RetrievedChunk> Retrieve(string query, int topK = 5);
    }

    /// <summary>
    /// No-op retrieval provider (returns empty results).
    /// </summary>
    public sealed class NoOpRetrievalProvider : IRetrievalProvider
    {
        /// <summary>
        /// Singleton instance of NoOpRetrievalProvider.
        /// </summary>
        public static NoOpRetrievalProvider Instance { get; } = new NoOpRetrievalProvider();

        private NoOpRetrievalProvider() { }

        public IReadOnlyList<RetrievedChunk> Retrieve(string query, int topK = 5)
        {
            return Array.Empty<RetrievedChunk>();
        }
    }
}

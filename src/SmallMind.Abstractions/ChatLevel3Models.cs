using System;
using System.Collections.Generic;

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
    public sealed class ResponseFormat
    {
        /// <summary>
        /// Gets or sets the response format type.
        /// </summary>
        public ResponseFormatType Type { get; set; } = ResponseFormatType.Text;

        /// <summary>
        /// Gets or sets the JSON schema definition (required when Type is JsonSchema).
        /// Schema must be a valid JSON Schema subset supported by SmallMind.
        /// </summary>
        public string? Schema { get; set; }

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
    public sealed class ToolCall
    {
        /// <summary>
        /// Gets or sets the unique ID for this tool call (for correlation).
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tool name to call.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the arguments as a JSON string.
        /// </summary>
        public string ArgumentsJson { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the result of a tool execution.
    /// </summary>
    public sealed class ToolResult
    {
        /// <summary>
        /// Gets or sets the tool call ID this result corresponds to.
        /// </summary>
        public string ToolCallId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the result content (can be text, JSON, etc).
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the tool execution resulted in an error.
        /// </summary>
        public bool IsError { get; set; }
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
        public UsageStats Usage { get; init; } = new UsageStats();

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
    public sealed class UsageStats
    {
        /// <summary>
        /// Gets the number of tokens in the prompt.
        /// </summary>
        public int PromptTokens { get; init; }

        /// <summary>
        /// Gets the number of tokens generated.
        /// </summary>
        public int CompletionTokens { get; init; }

        /// <summary>
        /// Gets the total tokens (prompt + completion).
        /// </summary>
        public int TotalTokens => PromptTokens + CompletionTokens;

        /// <summary>
        /// Gets the time to first token in milliseconds (TTFT).
        /// </summary>
        public double TimeToFirstTokenMs { get; init; }

        /// <summary>
        /// Gets the tokens per second rate.
        /// </summary>
        public double TokensPerSecond { get; init; }
    }

    /// <summary>
    /// Retrieved context chunk for RAG.
    /// </summary>
    public sealed class RetrievedChunk
    {
        /// <summary>
        /// Gets or sets the chunk content.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the source identifier.
        /// </summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the relevance score (0.0 to 1.0).
        /// </summary>
        public float Score { get; set; }

        /// <summary>
        /// Gets or sets optional metadata.
        /// </summary>
        public IReadOnlyDictionary<string, string>? Metadata { get; set; }
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
        public void OnRequestStart(string sessionId, int messageCount)
        {
            Console.WriteLine($"[{sessionId}] Request started with {messageCount} messages");
        }

        public void OnFirstToken(string sessionId, double elapsedMs)
        {
            Console.WriteLine($"[{sessionId}] First token: {elapsedMs:F2}ms (TTFT)");
        }

        public void OnRequestComplete(string sessionId, UsageStats usage)
        {
            Console.WriteLine($"[{sessionId}] Completed: {usage.PromptTokens} prompt + {usage.CompletionTokens} completion = {usage.TotalTokens} total");
            Console.WriteLine($"[{sessionId}] Performance: {usage.TokensPerSecond:F2} tok/s");
        }

        public void OnContextPolicyApplied(string sessionId, string policyName, int originalTokens, int finalTokens)
        {
            Console.WriteLine($"[{sessionId}] Context policy '{policyName}': {originalTokens} → {finalTokens} tokens");
        }

        public void OnKvCacheAccess(string sessionId, bool hit, int cachedTokens)
        {
            Console.WriteLine($"[{sessionId}] KV cache {(hit ? "HIT" : "MISS")}: {cachedTokens} cached tokens");
        }

        public void OnToolCall(string sessionId, string toolName, double elapsedMs)
        {
            Console.WriteLine($"[{sessionId}] Tool call '{toolName}': {elapsedMs:F2}ms");
        }

        public void OnKvCacheBudgetExceeded(string sessionId, long currentBytes, long maxBytes)
        {
            Console.WriteLine($"[{sessionId}] KV cache budget EXCEEDED: {currentBytes / 1024 / 1024}MB / {maxBytes / 1024 / 1024}MB");
        }

        public void OnKvCacheEviction(string evictedSessionId, string reason, long freedBytes)
        {
            Console.WriteLine($"[EVICTION] Session '{evictedSessionId}' evicted ({reason}): freed {freedBytes / 1024 / 1024}MB");
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

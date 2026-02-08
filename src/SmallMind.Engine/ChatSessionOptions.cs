using SmallMind.Abstractions;
using SmallMind.Runtime.Cache;

namespace SmallMind.Engine
{
    /// <summary>
    /// Chat template types for different model architectures.
    /// </summary>
    public enum ChatTemplateType
    {
        /// <summary>No template formatting.</summary>
        None,
        /// <summary>ChatML format (e.g., Vicuna, OpenChat).</summary>
        ChatML,
        /// <summary>Llama 2 format.</summary>
        Llama2,
        /// <summary>Llama 3 format.</summary>
        Llama3,
        /// <summary>Mistral format.</summary>
        Mistral,
        /// <summary>Phi format.</summary>
        Phi,
        /// <summary>Auto-detect from model metadata.</summary>
        Auto
    }

    /// <summary>
    /// Strategy for handling context window overflow.
    /// </summary>
    public enum ContextOverflowStrategy
    {
        /// <summary>Remove oldest non-system turns to fit context.</summary>
        TruncateOldest,
        /// <summary>Keep system + last N turns that fit.</summary>
        SlidingWindow,
        /// <summary>Throw exception on overflow.</summary>
        Error
    }

    /// <summary>
    /// Extended session options for chat sessions.
    /// Wraps base SessionOptions with chat-specific configuration.
    /// </summary>
    public sealed class ChatSessionOptions
    {
        /// <summary>
        /// Gets or sets the base session options.
        /// </summary>
        public SessionOptions BaseOptions { get; set; } = new SessionOptions();

        /// <summary>
        /// Gets or sets a caller-supplied session ID for tracking.
        /// If null, the engine will generate one.
        /// </summary>
        public string? SessionId 
        { 
            get => BaseOptions.SessionId;
            set => BaseOptions.SessionId = value;
        }

        /// <summary>
        /// Gets or sets whether to enable KV cache for this session.
        /// Default: true.
        /// </summary>
        public bool EnableKvCache
        {
            get => BaseOptions.EnableKvCache;
            set => BaseOptions.EnableKvCache = value;
        }

        /// <summary>
        /// Gets or sets the maximum KV cache size in tokens.
        /// If null, uses the model's max context length.
        /// </summary>
        public int? MaxKvCacheTokens
        {
            get => BaseOptions.MaxKvCacheTokens;
            set => BaseOptions.MaxKvCacheTokens = value;
        }

        /// <summary>
        /// Gets or sets the chat template type.
        /// Default: Auto (auto-detect from model).
        /// </summary>
        public ChatTemplateType ChatTemplateType { get; set; } = ChatTemplateType.Auto;

        /// <summary>
        /// Gets or sets the maximum number of conversation turns to keep in history.
        /// Default: 50.
        /// </summary>
        public int MaxHistoryTurns { get; set; } = 50;

        /// <summary>
        /// Gets or sets the strategy for handling context window overflow.
        /// Default: TruncateOldest.
        /// </summary>
        public ContextOverflowStrategy ContextOverflowStrategy { get; set; } = ContextOverflowStrategy.TruncateOldest;

        /// <summary>
        /// Gets or sets whether to enable RAG (Retrieval Augmented Generation).
        /// Default: false.
        /// </summary>
        public bool EnableRag { get; set; } = false;

        /// <summary>
        /// Gets or sets the optional KV cache store.
        /// If null, a default LruKvCacheStore will be created.
        /// </summary>
        public IKvCacheStore? KvCacheStore { get; set; }

        /// <summary>
        /// Gets or sets optional RAG configuration.
        /// Only used when EnableRag is true.
        /// </summary>
        public RagOptions? RagOptions { get; set; }
    }

    /// <summary>
    /// Options for RAG (Retrieval Augmented Generation).
    /// Placeholder for future RAG configuration.
    /// </summary>
    public sealed class RagOptions
    {
        /// <summary>
        /// Gets or sets the number of documents to retrieve.
        /// Default: 5.
        /// </summary>
        public int TopK { get; set; } = 5;

        /// <summary>
        /// Gets or sets the minimum relevance score threshold (0.0 to 1.0).
        /// Default: 0.5.
        /// </summary>
        public float MinRelevanceScore { get; set; } = 0.5f;

        /// <summary>
        /// Gets or sets whether to include citation metadata in responses.
        /// Default: true.
        /// </summary>
        public bool IncludeCitations { get; set; } = true;
    }
}

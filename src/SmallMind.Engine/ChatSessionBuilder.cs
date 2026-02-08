using System;
using SmallMind.Abstractions;
using SmallMind.Rag.Pipeline;
using SmallMind.Runtime.Cache;

namespace SmallMind.Engine
{
    /// <summary>
    /// Fluent builder for configuring and creating chat sessions.
    /// </summary>
    internal sealed class ChatSessionBuilder
    {
        private readonly ModelHandle _modelHandle;
        private readonly SmallMindOptions _engineOptions;
        private ChatSessionOptions _sessionOptions;
        private IKvCacheStore? _kvCacheStore;
        private RagPipeline? _ragPipeline;

        internal ChatSessionBuilder(ModelHandle modelHandle, SmallMindOptions engineOptions)
        {
            _modelHandle = modelHandle ?? throw new ArgumentNullException(nameof(modelHandle));
            _engineOptions = engineOptions ?? throw new ArgumentNullException(nameof(engineOptions));
            _sessionOptions = new ChatSessionOptions();
        }

        /// <summary>
        /// Enables automatic template detection from model metadata.
        /// </summary>
        public ChatSessionBuilder WithAutoTemplate()
        {
            _sessionOptions.ChatTemplateType = ChatTemplateType.Auto;
            return this;
        }

        /// <summary>
        /// Configures sliding window context overflow strategy.
        /// </summary>
        public ChatSessionBuilder WithSlidingWindowContext()
        {
            _sessionOptions.ContextOverflowStrategy = ContextOverflowStrategy.SlidingWindow;
            return this;
        }

        /// <summary>
        /// Enables KV cache with optional custom store.
        /// </summary>
        public ChatSessionBuilder WithKvCache(IKvCacheStore? customStore = null)
        {
            _sessionOptions.EnableKvCache = true;
            _kvCacheStore = customStore;
            return this;
        }

        /// <summary>
        /// Sets the maximum number of conversation turns to keep in history.
        /// </summary>
        public ChatSessionBuilder WithMaxHistoryTurns(int maxTurns)
        {
            _sessionOptions.MaxHistoryTurns = maxTurns;
            return this;
        }

        /// <summary>
        /// Enables RAG with the specified pipeline.
        /// </summary>
        public ChatSessionBuilder WithRag(RagPipeline pipeline)
        {
            _sessionOptions.EnableRag = true;
            _ragPipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
            return this;
        }

        /// <summary>
        /// Configures the session with a custom action.
        /// </summary>
        public ChatSessionBuilder Configure(Action<ChatSessionOptions> configure)
        {
            configure?.Invoke(_sessionOptions);
            return this;
        }

        /// <summary>
        /// Builds and returns the configured chat session.
        /// </summary>
        public IChatSession Build()
        {
            if (_kvCacheStore != null)
            {
                _sessionOptions.KvCacheStore = _kvCacheStore;
            }

            return new ChatSession(_modelHandle, _sessionOptions, _engineOptions, _ragPipeline);
        }
    }
}

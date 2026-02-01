using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Abstractions
{
    /// <summary>
    /// Main entry point for the SmallMind inference engine.
    /// Provides model loading, text generation, and chat capabilities.
    /// </summary>
    public interface ISmallMindEngine : IDisposable
    {
        /// <summary>
        /// Gets the engine capabilities.
        /// </summary>
        EngineCapabilities Capabilities { get; }

        /// <summary>
        /// Gets the RAG engine (if supported).
        /// Returns null if RAG is not supported.
        /// </summary>
        IRagEngine? Rag { get; }

        /// <summary>
        /// Loads a model from the specified path.
        /// Supports .smq format natively, and .gguf if AllowGgufImport is enabled.
        /// </summary>
        /// <param name="request">Model load request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A handle to the loaded model.</returns>
        /// <exception cref="UnsupportedModelException">Thrown when the model format is not supported.</exception>
        /// <exception cref="SmallMindException">Thrown when model loading fails.</exception>
        ValueTask<IModelHandle> LoadModelAsync(ModelLoadRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates a chat session for multi-turn conversations.
        /// </summary>
        /// <param name="model">Model handle to use for this session.</param>
        /// <param name="options">Session options.</param>
        /// <returns>A chat session.</returns>
        IChatSession CreateChatSession(IModelHandle model, SessionOptions options);

        /// <summary>
        /// Generates text from a prompt (non-streaming).
        /// </summary>
        /// <param name="model">Model handle to use for generation.</param>
        /// <param name="request">Generation request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Generation result.</returns>
        /// <exception cref="ContextLimitExceededException">Thrown when context limit is exceeded.</exception>
        /// <exception cref="BudgetExceededException">Thrown when a budget is exceeded.</exception>
        ValueTask<GenerationResult> GenerateAsync(
            IModelHandle model,
            GenerationRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates text from a prompt with streaming output.
        /// </summary>
        /// <param name="model">Model handle to use for generation.</param>
        /// <param name="request">Generation request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of token events.</returns>
        /// <exception cref="ContextLimitExceededException">Thrown when context limit is exceeded.</exception>
        /// <exception cref="BudgetExceededException">Thrown when a budget is exceeded.</exception>
        IAsyncEnumerable<TokenEvent> GenerateStreamingAsync(
            IModelHandle model,
            GenerationRequest request,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Handle to a loaded model.
    /// Dispose when done to release resources.
    /// </summary>
    public interface IModelHandle : IDisposable
    {
        /// <summary>
        /// Gets information about the model.
        /// </summary>
        ModelInfo Info { get; }
    }

    /// <summary>
    /// A chat session for multi-turn conversations with KV cache.
    /// </summary>
    public interface IChatSession : IDisposable
    {
        /// <summary>
        /// Gets information about the session.
        /// </summary>
        SessionInfo Info { get; }

        /// <summary>
        /// Adds a system prompt to the session.
        /// This is typically done once at the start.
        /// </summary>
        /// <param name="systemPrompt">System prompt text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        ValueTask AddSystemAsync(string systemPrompt, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message and gets a response (non-streaming).
        /// </summary>
        /// <param name="message">Chat message to send.</param>
        /// <param name="options">Generation options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Generation result.</returns>
        ValueTask<GenerationResult> SendAsync(
            ChatMessage message,
            GenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a message and gets a streaming response.
        /// </summary>
        /// <param name="message">Chat message to send.</param>
        /// <param name="options">Generation options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of token events.</returns>
        IAsyncEnumerable<TokenEvent> SendStreamingAsync(
            ChatMessage message,
            GenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Resets the session (clears conversation and KV cache).
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// RAG (Retrieval-Augmented Generation) engine.
    /// </summary>
    public interface IRagEngine
    {
        /// <summary>
        /// Builds a RAG index from source documents.
        /// </summary>
        /// <param name="request">Build request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A RAG index.</returns>
        ValueTask<IRagIndex> BuildIndexAsync(RagBuildRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Asks a question using RAG (non-streaming).
        /// </summary>
        /// <param name="model">Model handle for generation.</param>
        /// <param name="request">Ask request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>RAG answer with citations.</returns>
        /// <exception cref="RagInsufficientEvidenceException">Thrown when insufficient evidence is found.</exception>
        ValueTask<RagAnswer> AskAsync(
            IModelHandle model,
            RagAskRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Asks a question using RAG with streaming output.
        /// </summary>
        /// <param name="model">Model handle for generation.</param>
        /// <param name="request">Ask request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of token events.</returns>
        /// <exception cref="RagInsufficientEvidenceException">Thrown when insufficient evidence is found.</exception>
        IAsyncEnumerable<TokenEvent> AskStreamingAsync(
            IModelHandle model,
            RagAskRequest request,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// A RAG index for document retrieval.
    /// </summary>
    public interface IRagIndex : IDisposable
    {
        /// <summary>
        /// Gets information about the index.
        /// </summary>
        RagIndexInfo Info { get; }

        /// <summary>
        /// Saves the index to disk.
        /// </summary>
        /// <param name="directory">Directory to save to.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        ValueTask SaveAsync(string directory, CancellationToken cancellationToken = default);
    }
}

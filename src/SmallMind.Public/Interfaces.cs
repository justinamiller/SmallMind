using System;
using System.Collections.Generic;
using System.Threading;

namespace SmallMind.Public
{
    /// <summary>
    /// Main SmallMind inference engine interface.
    /// This is the stable public contract for SmallMind.
    /// Thread-safe for concurrent operations.
    /// </summary>
    public interface ISmallMindEngine : IDisposable
    {
        /// <summary>
        /// Gets the engine capabilities.
        /// Use this to discover what features are supported before attempting to use them.
        /// </summary>
        /// <returns>Engine capabilities.</returns>
        EngineCapabilities GetCapabilities();

        /// <summary>
        /// Creates a new text generation session.
        /// Sessions provide isolated context and can maintain KV cache state.
        /// Thread-safety: Multiple sessions can be used concurrently.
        /// </summary>
        /// <param name="options">Session options.</param>
        /// <returns>A new text generation session.</returns>
        /// <exception cref="InvalidOptionsException">Thrown when options are invalid.</exception>
        ITextGenerationSession CreateTextGenerationSession(TextGenerationOptions options);

        /// <summary>
        /// Creates a new embedding session.
        /// Sessions provide isolated context for embedding generation.
        /// Thread-safety: Multiple sessions can be used concurrently.
        /// </summary>
        /// <param name="options">Session options.</param>
        /// <returns>A new embedding session.</returns>
        /// <exception cref="InvalidOptionsException">Thrown when options are invalid.</exception>
        /// <exception cref="InternalErrorException">Thrown when embeddings are not supported.</exception>
        IEmbeddingSession CreateEmbeddingSession(EmbeddingOptions options);
    }

    /// <summary>
    /// Text generation session.
    /// Sessions are NOT thread-safe - use one per thread or synchronize access.
    /// </summary>
    public interface ITextGenerationSession : IDisposable
    {
        /// <summary>
        /// Generates text from a prompt (non-streaming).
        /// Blocks until generation is complete or cancelled.
        /// </summary>
        /// <param name="request">Generation request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Generation result.</returns>
        /// <exception cref="ContextOverflowException">Thrown when context limit is exceeded.</exception>
        /// <exception cref="InferenceFailedException">Thrown when inference fails.</exception>
        /// <exception cref="RequestCancelledException">Thrown when request is cancelled.</exception>
        GenerationResult Generate(TextGenerationRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates text from a prompt with streaming output.
        /// Returns tokens as they are generated.
        /// The async enumerable is cancellation-safe and will not leak threads.
        /// </summary>
        /// <param name="request">Generation request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Async enumerable of token results.</returns>
        /// <exception cref="ContextOverflowException">Thrown when context limit is exceeded.</exception>
        /// <exception cref="InferenceFailedException">Thrown when inference fails.</exception>
        /// <exception cref="RequestCancelledException">Thrown when request is cancelled.</exception>
        IAsyncEnumerable<TokenResult> GenerateStreaming(TextGenerationRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Embedding session.
    /// Sessions are NOT thread-safe - use one per thread or synchronize access.
    /// </summary>
    public interface IEmbeddingSession : IDisposable
    {
        /// <summary>
        /// Generates an embedding vector from input text.
        /// </summary>
        /// <param name="request">Embedding request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Embedding result.</returns>
        /// <exception cref="EmbeddingFailedException">Thrown when embedding generation fails.</exception>
        /// <exception cref="RequestCancelledException">Thrown when request is cancelled.</exception>
        EmbeddingResult Embed(EmbeddingRequest request, CancellationToken cancellationToken = default);
    }
}

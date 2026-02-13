using System.Threading.Channels;
using SmallMind.Runtime.Cache;

namespace SmallMind.Runtime.Batching
{
    /// <summary>
    /// Represents a single inference request in the batching queue.
    /// Contains prompt tokens, options, and response channel for streaming results.
    /// </summary>
    internal sealed class InferenceRequest : IDisposable
    {
        /// <summary>
        /// Gets the session ID for this request.
        /// </summary>
        public SessionId SessionId { get; }

        /// <summary>
        /// Gets the prompt tokens (owned buffer - caller should not modify after submission).
        /// </summary>
        public int[] PromptTokens { get; }

        /// <summary>
        /// Gets the inference options for this request.
        /// </summary>
        public ProductionInferenceOptions Options { get; }

        /// <summary>
        /// Gets the cancellation token for this request.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        /// <summary>
        /// Gets the channel writer for streaming generated tokens back to the caller.
        /// </summary>
        public ChannelWriter<GeneratedToken> ResponseWriter { get; }

        /// <summary>
        /// Gets the channel reader for receiving generated tokens.
        /// </summary>
        public ChannelReader<GeneratedToken> ResponseReader { get; }

        /// <summary>
        /// Gets or sets the current position in generation (number of tokens generated so far).
        /// Updated by the batch processor during generation.
        /// </summary>
        public int CurrentPosition { get; set; }

        /// <summary>
        /// Gets or sets the total number of tokens generated (not including prompt).
        /// </summary>
        public int GeneratedTokenCount { get; set; }

        /// <summary>
        /// Gets whether this request has been cancelled.
        /// </summary>
        public bool IsCancelled => CancellationToken.IsCancellationRequested;

        /// <summary>
        /// Gets whether this request has completed generation.
        /// </summary>
        public bool IsComplete { get; private set; }

        private readonly Channel<GeneratedToken> _responseChannel;
        private bool _disposed;

        /// <summary>
        /// Creates a new inference request.
        /// </summary>
        /// <param name="sessionId">Session ID</param>
        /// <param name="promptTokens">Prompt tokens (ownership transferred)</param>
        /// <param name="options">Inference options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public InferenceRequest(
            SessionId sessionId,
            int[] promptTokens,
            ProductionInferenceOptions options,
            CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            PromptTokens = promptTokens ?? throw new ArgumentNullException(nameof(promptTokens));
            Options = options ?? throw new ArgumentNullException(nameof(options));
            CancellationToken = cancellationToken;

            // Create unbounded channel for streaming responses
            _responseChannel = Channel.CreateUnbounded<GeneratedToken>(new UnboundedChannelOptions
            {
                SingleWriter = true,
                SingleReader = true
            });

            ResponseWriter = _responseChannel.Writer;
            ResponseReader = _responseChannel.Reader;

            CurrentPosition = 0;
            GeneratedTokenCount = 0;
        }

        /// <summary>
        /// Marks this request as complete and closes the response channel.
        /// </summary>
        public void MarkComplete()
        {
            if (!IsComplete)
            {
                IsComplete = true;
                ResponseWriter.TryComplete();
            }
        }

        /// <summary>
        /// Marks this request as failed with an error.
        /// </summary>
        public void MarkFailed(Exception exception)
        {
            if (!IsComplete)
            {
                IsComplete = true;
                ResponseWriter.TryComplete(exception);
            }
        }

        /// <summary>
        /// Checks if this request is compatible with another for batching.
        /// Requests are compatible if they have the same model configuration.
        /// </summary>
        public bool IsCompatibleWith(InferenceRequest other)
        {
            if (other == null)
                return false;

            // For now, consider all requests compatible
            // In a real system, you'd check:
            // - Same model
            // - Compatible sampling parameters (temperature, top-k, etc.)
            return true;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                MarkComplete();
                _disposed = true;
            }
        }
    }
}

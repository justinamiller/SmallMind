using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Abstractions;
using SmallMind.Runtime;
using PublicGenerationOptions = SmallMind.Abstractions.GenerationOptions;

namespace SmallMind.Engine
{
    /// <summary>
    /// Internal implementation of IChatSession.
    /// Maintains conversation context and KV cache across multiple turns.
    /// </summary>
    internal sealed class ChatSession : IChatSession
    {
        private readonly ModelHandle _modelHandle;
        private readonly SessionOptions _options;
        private readonly SmallMindOptions _engineOptions;
        private readonly List<ChatMessage> _conversationHistory;
        private readonly string _sessionId;
        private readonly DateTimeOffset _createdAt;
        private int _turnCount;
        private bool _disposed;

        public ChatSession(
            ModelHandle modelHandle,
            SessionOptions options,
            SmallMindOptions engineOptions)
        {
            _modelHandle = modelHandle ?? throw new ArgumentNullException(nameof(modelHandle));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _engineOptions = engineOptions ?? throw new ArgumentNullException(nameof(engineOptions));
            _conversationHistory = new List<ChatMessage>();
            _sessionId = options.SessionId ?? Guid.NewGuid().ToString("N");
            _createdAt = DateTimeOffset.UtcNow;
        }

        public SessionInfo Info => new SessionInfo
        {
            SessionId = _sessionId,
            CreatedAt = _createdAt,
            TurnCount = _turnCount,
            KvCacheTokens = 0 // TODO: Track actual KV cache size
        };

        public ValueTask AddSystemAsync(string systemPrompt, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(systemPrompt))
            {
                throw new ArgumentException("System prompt cannot be empty", nameof(systemPrompt));
            }

            _conversationHistory.Add(new ChatMessage
            {
                Role = ChatRole.System,
                Content = systemPrompt
            });

            return ValueTask.CompletedTask;
        }

        public async ValueTask<GenerationResult> SendAsync(
            ChatMessage message,
            PublicGenerationOptions options,
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // Add user message to history
            _conversationHistory.Add(message);

            // Build full conversation prompt
            var prompt = BuildConversationPrompt();

            // Generate response
            var session = _modelHandle.CreateInferenceSession(options, _engineOptions);
            try
            {
                var response = await session.GenerateAsync(
                    prompt,
                    metrics: null,
                    cancellationToken: cancellationToken);

                // Add assistant response to history
                _conversationHistory.Add(new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Content = response
                });

                _turnCount++;

                return new GenerationResult
                {
                    Text = response,
                    GeneratedTokens = options.MaxNewTokens, // Approximate
                    StoppedByBudget = false,
                    StopReason = "completed"
                };
            }
            finally
            {
                session.Dispose();
            }
        }

        public async IAsyncEnumerable<TokenEvent> SendStreamingAsync(
            ChatMessage message,
            PublicGenerationOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            // Add user message to history
            _conversationHistory.Add(message);

            // Build full conversation prompt
            var prompt = BuildConversationPrompt();

            // Generate streaming response
            var session = _modelHandle.CreateInferenceSession(options, _engineOptions);
            try
            {
                var responseBuilder = new StringBuilder();
                int tokenCount = 0;

                // Emit started event
                yield return new TokenEvent
                {
                    Kind = TokenEventKind.Started,
                    Text = ReadOnlyMemory<char>.Empty,
                    TokenId = -1,
                    GeneratedTokens = 0,
                    IsFinal = false
                };

                bool isLast = false;
                await foreach (var token in session.GenerateStreamAsync(
                    prompt,
                    metrics: null,
                    cancellationToken: cancellationToken))
                {
                    tokenCount++;
                    responseBuilder.Append(token.Text);
                    isLast = (tokenCount >= options.MaxNewTokens);

                    yield return new TokenEvent
                    {
                        Kind = TokenEventKind.Token,
                        Text = token.Text.AsMemory(),
                        TokenId = token.TokenId,
                        GeneratedTokens = tokenCount,
                        IsFinal = isLast
                    };

                    if (isLast)
                    {
                        break;
                    }
                }

                // Add complete response to history
                _conversationHistory.Add(new ChatMessage
                {
                    Role = ChatRole.Assistant,
                    Content = responseBuilder.ToString()
                });

                _turnCount++;

                // Emit completed event
                yield return new TokenEvent
                {
                    Kind = TokenEventKind.Completed,
                    Text = ReadOnlyMemory<char>.Empty,
                    TokenId = -1,
                    GeneratedTokens = tokenCount,
                    IsFinal = true
                };
            }
            finally
            {
                session.Dispose();
            }
        }

        public void Reset()
        {
            ThrowIfDisposed();

            _conversationHistory.Clear();
            _turnCount = 0;
        }

        private string BuildConversationPrompt()
        {
            var sb = new StringBuilder();

            for (int i = 0; i < _conversationHistory.Count; i++)
            {
                var msg = _conversationHistory[i];

                switch (msg.Role)
                {
                    case ChatRole.System:
                        sb.AppendLine($"System: {msg.Content}");
                        break;
                    case ChatRole.User:
                        sb.AppendLine($"User: {msg.Content}");
                        break;
                    case ChatRole.Assistant:
                        sb.AppendLine($"Assistant: {msg.Content}");
                        break;
                }
            }

            // Add assistant prefix for next response
            sb.Append("Assistant:");

            return sb.ToString();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChatSession));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }
    }
}

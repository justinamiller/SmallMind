using System;
using System.Threading;
using SmallMind.Abstractions;

namespace SmallMind
{
    /// <summary>
    /// Level 3 chat client implementation.
    /// Wraps ChatSession with a clean public API.
    /// </summary>
    internal sealed class ChatClient : IChatClient
    {
        private readonly IChatSession _session;
        private readonly IChatTelemetry _telemetry;
        private bool _disposed;

        public ChatClient(IChatSession session, IChatTelemetry? telemetry)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _telemetry = telemetry ?? IChatTelemetry.Default;
        }

        public ChatResponse SendChat(ChatRequest request, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChatClient));

            if (request == null)
                throw new ArgumentNullException(nameof(request));

            try
            {
                // Use Task.Run to avoid SynchronizationContext deadlocks in ASP.NET/UI contexts
                // Since generation is CPU-bound, this is safe and prevents deadlocks
                return Task.Run(async () => 
                    await _session.SendAsync(request, _telemetry, cancellationToken)
                        .ConfigureAwait(false)
                ).GetAwaiter().GetResult();
            }
            catch (Exception ex) when (ex is not ObjectDisposedException)
            {
                throw new InferenceFailedException($"Chat request failed: {ex.Message}", ex);
            }
        }

        public void AddSystemMessage(string content)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChatClient));

            if (string.IsNullOrWhiteSpace(content))
                throw new ArgumentException("System message cannot be empty", nameof(content));

            // Use Task.Run to avoid SynchronizationContext deadlocks
            Task.Run(async () => 
                await _session.AddSystemAsync(content).ConfigureAwait(false)
            ).GetAwaiter().GetResult();
        }

        public SessionInfo GetSessionInfo()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ChatClient));

            return _session.Info;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _session?.Dispose();
            _disposed = true;
        }
    }
}

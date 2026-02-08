using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Abstractions;

namespace SmallMind.Engine
{
    /// <summary>
    /// In-memory session store implementation.
    /// Moved from SmallMind.Chat to SmallMind.Engine for unified chat pipeline.
    /// </summary>
    internal sealed class InMemorySessionStore : ISessionStore
    {
        private readonly ConcurrentDictionary<string, ChatSessionData> _sessions;

        /// <summary>
        /// Create a new in-memory session store.
        /// </summary>
        public InMemorySessionStore()
        {
            _sessions = new ConcurrentDictionary<string, ChatSessionData>();
        }

        /// <summary>
        /// Get a session by ID.
        /// </summary>
        public Task<ChatSessionData?> GetAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

            cancellationToken.ThrowIfCancellationRequested();

            _sessions.TryGetValue(sessionId, out var session);
            return Task.FromResult(session);
        }

        /// <summary>
        /// Create or update a session.
        /// </summary>
        public Task UpsertAsync(ChatSessionData session, CancellationToken cancellationToken = default)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (string.IsNullOrEmpty(session.SessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(session));

            cancellationToken.ThrowIfCancellationRequested();

            session.LastUpdatedAt = DateTime.UtcNow;
            _sessions[session.SessionId] = session;

            return Task.CompletedTask;
        }

        /// <summary>
        /// Delete a session.
        /// </summary>
        public Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

            cancellationToken.ThrowIfCancellationRequested();

            _sessions.TryRemove(sessionId, out _);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Check if a session exists.
        /// </summary>
        public Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentException("Session ID cannot be null or empty", nameof(sessionId));

            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(_sessions.ContainsKey(sessionId));
        }

        /// <summary>
        /// Get the number of sessions currently in the store.
        /// </summary>
        public int Count => _sessions.Count;

        /// <summary>
        /// Clear all sessions from the store.
        /// </summary>
        public void Clear()
        {
            _sessions.Clear();
        }
    }
}

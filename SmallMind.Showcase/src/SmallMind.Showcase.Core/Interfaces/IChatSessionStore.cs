using SmallMind.Showcase.Core.Models;

namespace SmallMind.Showcase.Core.Interfaces;

/// <summary>
/// Persistent storage for chat sessions.
/// </summary>
public interface IChatSessionStore
{
    /// <summary>
    /// Gets all stored sessions.
    /// </summary>
    Task<List<ChatSession>> GetAllSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific session by ID.
    /// </summary>
    Task<ChatSession?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new session.
    /// </summary>
    Task<ChatSession> CreateSessionAsync(ChatSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing session.
    /// </summary>
    Task UpdateSessionAsync(ChatSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a session.
    /// </summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default);
}

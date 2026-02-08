using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Abstractions
{
    /// <summary>
    /// Interface for session storage implementations.
    /// </summary>
    public interface ISessionStore
    {
        /// <summary>
        /// Get a session by ID.
        /// </summary>
        /// <param name="sessionId">Session ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The session, or null if not found.</returns>
        Task<ChatSessionData?> GetAsync(string sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Create or update a session.
        /// </summary>
        /// <param name="session">Session to upsert.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task UpsertAsync(ChatSessionData session, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete a session.
        /// </summary>
        /// <param name="sessionId">Session ID to delete.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task DeleteAsync(string sessionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a session exists.
        /// </summary>
        /// <param name="sessionId">Session ID to check.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if the session exists.</returns>
        Task<bool> ExistsAsync(string sessionId, CancellationToken cancellationToken = default);
    }
}

using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Chat
{
    /// <summary>
    /// Interface for chat completion services.
    /// </summary>
    public interface IChatCompletionService
    {
        /// <summary>
        /// Process a user message and generate a response.
        /// </summary>
        /// <param name="sessionId">Session ID.</param>
        /// <param name="message">User message.</param>
        /// <param name="options">Chat options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Chat response.</returns>
        Task<ChatResponse> AskAsync(
            string sessionId,
            string message,
            ChatOptions options,
            CancellationToken cancellationToken = default);
    }
}

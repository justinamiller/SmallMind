using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Domain
{
    /// <summary>
    /// Interface for domain-bounded reasoning operations.
    /// Provides safe, constrained text generation with policy enforcement.
    /// </summary>
    public interface IDomainReasoner
    {
        /// <summary>
        /// Processes a domain question and returns an answer asynchronously.
        /// </summary>
        /// <param name="question">The domain question to answer.</param>
        /// <param name="domain">The domain profile defining constraints and policies.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>A domain answer containing the result or rejection reason.</returns>
        Task<DomainAnswer> AskAsync(DomainQuestion question, DomainProfile domain, CancellationToken ct = default);

        /// <summary>
        /// Processes a domain question and streams tokens as they are generated.
        /// </summary>
        /// <param name="question">The domain question to answer.</param>
        /// <param name="domain">The domain profile defining constraints and policies.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>An async enumerable of domain tokens.</returns>
        IAsyncEnumerable<DomainToken> AskStreamAsync(DomainQuestion question, DomainProfile domain, CancellationToken ct = default);
    }
}

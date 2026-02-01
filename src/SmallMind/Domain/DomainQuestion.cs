using System.Collections.Generic;
using SmallMind.Validation;

namespace SmallMind.Domain
{
    /// <summary>
    /// Represents a question or request for domain-bounded reasoning.
    /// </summary>
    public class DomainQuestion
    {
        /// <summary>
        /// Gets or sets the query text.
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets optional context text to help with the query.
        /// This context is subject to token limits and validation.
        /// </summary>
        public string? Context { get; set; }

        /// <summary>
        /// Gets or sets an optional request ID for tracking and logging.
        /// If not provided, one will be generated automatically.
        /// </summary>
        public string? RequestId { get; set; }

        /// <summary>
        /// Gets or sets optional tags for audit and logging purposes.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }

        /// <summary>
        /// Validates the domain question.
        /// </summary>
        public void Validate()
        {
            Guard.NotNullOrWhiteSpace(Query, nameof(Query));

            // Generate request ID if not provided
            if (string.IsNullOrEmpty(RequestId))
            {
                RequestId = System.Guid.NewGuid().ToString();
            }
        }

        /// <summary>
        /// Creates a new DomainQuestion with the specified query.
        /// </summary>
        /// <param name="query">The query text.</param>
        /// <returns>A new domain question.</returns>
        public static DomainQuestion Create(string query)
        {
            Guard.NotNullOrWhiteSpace(query, nameof(query));

            return new DomainQuestion
            {
                Query = query,
                RequestId = System.Guid.NewGuid().ToString()
            };
        }

        /// <summary>
        /// Creates a new DomainQuestion with query and context.
        /// </summary>
        /// <param name="query">The query text.</param>
        /// <param name="context">Optional context text.</param>
        /// <returns>A new domain question.</returns>
        public static DomainQuestion CreateWithContext(string query, string context)
        {
            Guard.NotNullOrWhiteSpace(query, nameof(query));

            return new DomainQuestion
            {
                Query = query,
                Context = context,
                RequestId = System.Guid.NewGuid().ToString()
            };
        }
    }
}

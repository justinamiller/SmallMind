using System.Collections.Generic;
using System.Threading;

namespace SmallMind.Retrieval
{
    /// <summary>
    /// Configuration options for retrieval operations.
    /// </summary>
    public class RetrievalOptions
    {
        /// <summary>
        /// Number of top chunks to retrieve.
        /// </summary>
        public int TopK { get; set; } = 5;

        /// <summary>
        /// Maximum number of chunks to return per document.
        /// </summary>
        public int MaxChunksPerDocument { get; set; } = 2;

        /// <summary>
        /// Whether to use deterministic ordering (stable sort with tie-breakers).
        /// </summary>
        public bool Deterministic { get; set; } = true;

        /// <summary>
        /// Whether to include text snippets in the results.
        /// </summary>
        public bool IncludeSnippets { get; set; } = true;

        /// <summary>
        /// Maximum characters for each snippet.
        /// </summary>
        public int MaxSnippetChars { get; set; } = 280;
    }

    /// <summary>
    /// Result from a retrieval operation.
    /// </summary>
    public class RetrievalResult
    {
        /// <summary>
        /// The query that was used for retrieval.
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Retrieved chunks ordered by relevance.
        /// </summary>
        public List<RetrievedChunkWithCitation> Chunks { get; set; } = new List<RetrievedChunkWithCitation>();

        /// <summary>
        /// Total number of candidate chunks that were considered.
        /// </summary>
        public int TotalCandidates { get; set; }

        /// <summary>
        /// Any warnings or informational messages.
        /// </summary>
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// A retrieved chunk with citation information.
    /// </summary>
    public class RetrievedChunkWithCitation
    {
        /// <summary>
        /// Document ID.
        /// </summary>
        public string DocumentId { get; set; } = string.Empty;

        /// <summary>
        /// Chunk ID.
        /// </summary>
        public string ChunkId { get; set; } = string.Empty;

        /// <summary>
        /// Relevance score.
        /// </summary>
        public double Score { get; set; }

        /// <summary>
        /// The text or snippet.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        /// <summary>
        /// Citation information.
        /// </summary>
        public Citation Citation { get; set; } = new Citation();

        /// <summary>
        /// Optional metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>
    /// Citation information for a chunk.
    /// </summary>
    public class Citation
    {
        /// <summary>
        /// Document title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Source URI.
        /// </summary>
        public string? SourceUri { get; set; }

        /// <summary>
        /// Start offset in the original document.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// End offset in the original document.
        /// </summary>
        public int EndOffset { get; set; }
    }

    /// <summary>
    /// Interface for retrieval index implementations.
    /// </summary>
    public interface IRetrievalIndex
    {
        /// <summary>
        /// Add or update a document in the index.
        /// </summary>
        /// <param name="document">Document to upsert.</param>
        void Upsert(Document document);

        /// <summary>
        /// Add or update multiple documents in the index.
        /// </summary>
        /// <param name="documents">Documents to upsert.</param>
        void Upsert(IEnumerable<Document> documents);

        /// <summary>
        /// Search the index for relevant chunks.
        /// </summary>
        /// <param name="query">Query text.</param>
        /// <param name="options">Retrieval options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Retrieval results.</returns>
        RetrievalResult Search(string query, RetrievalOptions options, CancellationToken cancellationToken = default);
    }
}

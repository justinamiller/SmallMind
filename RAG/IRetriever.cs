using System.Collections.Generic;

namespace TinyLLM.RAG
{
    /// <summary>
    /// Interface for retriever implementations.
    /// Retrieves relevant chunks for a query.
    /// </summary>
    public interface IRetriever
    {
        /// <summary>
        /// Retrieve the top K most relevant chunks for a query.
        /// </summary>
        /// <param name="query">Query text</param>
        /// <param name="k">Number of chunks to retrieve (null to use default)</param>
        /// <returns>List of retrieved chunks with scores</returns>
        List<RetrievedChunk> Retrieve(string query, int? k = null);

        /// <summary>
        /// Retrieve chunks with a minimum score threshold.
        /// </summary>
        /// <param name="query">Query text</param>
        /// <param name="minScore">Minimum similarity score</param>
        /// <param name="maxResults">Maximum number of results</param>
        /// <returns>List of retrieved chunks above threshold</returns>
        List<RetrievedChunk> RetrieveWithThreshold(string query, float minScore, int maxResults = 10);
    }
}

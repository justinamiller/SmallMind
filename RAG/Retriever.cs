using System;
using System.Collections.Generic;
using TinyLLM.Embeddings;
using TinyLLM.Indexing;

namespace TinyLLM.RAG
{
    /// <summary>
    /// Chunk of text with relevance score for retrieval.
    /// </summary>
    public class RetrievedChunk
    {
        public string Text { get; set; } = "";
        public float Score { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public string Id { get; set; } = "";
    }

    /// <summary>
    /// Retriever for finding relevant chunks from the vector index.
    /// </summary>
    public class Retriever : IRetriever
    {
        private readonly VectorIndex _vectorIndex;
        private readonly int _defaultK;

        /// <summary>
        /// Create a new retriever.
        /// </summary>
        /// <param name="vectorIndex">Vector index to search</param>
        /// <param name="defaultK">Default number of chunks to retrieve</param>
        public Retriever(VectorIndex vectorIndex, int defaultK = 5)
        {
            _vectorIndex = vectorIndex ?? throw new ArgumentNullException(nameof(vectorIndex));
            _defaultK = defaultK;
        }

        /// <summary>
        /// Retrieve the top K most relevant chunks for a query.
        /// </summary>
        /// <param name="query">Query text</param>
        /// <param name="k">Number of chunks to retrieve (null to use default)</param>
        /// <returns>List of retrieved chunks with scores</returns>
        public List<RetrievedChunk> Retrieve(string query, int? k = null)
        {
            int numChunks = k ?? _defaultK;
            var searchResults = _vectorIndex.Search(query, numChunks);
            
            var chunks = new List<RetrievedChunk>(searchResults.Count);
            for (int i = 0; i < searchResults.Count; i++)
            {
                var result = searchResults[i];
                chunks.Add(new RetrievedChunk
                {
                    Id = result.Id,
                    Text = result.Text,
                    Score = result.Score,
                    Metadata = result.Metadata
                });
            }

            return chunks;
        }

        /// <summary>
        /// Retrieve chunks with a minimum score threshold.
        /// </summary>
        public List<RetrievedChunk> RetrieveWithThreshold(string query, float minScore, int maxResults = 10)
        {
            var searchResults = _vectorIndex.Search(query, maxResults);
            // Pre-size based on searchResults.Count (upper bound for filtered results)
            var chunks = new List<RetrievedChunk>(capacity: searchResults.Count);

            for (int i = 0; i < searchResults.Count; i++)
            {
                if (searchResults[i].Score >= minScore)
                {
                    chunks.Add(new RetrievedChunk
                    {
                        Id = searchResults[i].Id,
                        Text = searchResults[i].Text,
                        Score = searchResults[i].Score,
                        Metadata = searchResults[i].Metadata
                    });
                }
            }

            return chunks;
        }
    }
}

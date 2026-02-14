namespace SmallMind.Abstractions
{
    /// <summary>
    /// Thrown when RAG retrieval finds insufficient evidence to answer a question.
    /// Remediation: Rephrase query, add more documents to index, or lower confidence threshold.
    /// </summary>
    public class RagInsufficientEvidenceException : SmallMindException
    {
        /// <summary>
        /// Gets the query that failed.
        /// </summary>
        public string Query { get; }

        /// <summary>
        /// Gets the minimum confidence threshold.
        /// </summary>
        public double MinConfidence { get; }

        /// <summary>
        /// Creates a new RagInsufficientEvidenceException.
        /// </summary>
        public RagInsufficientEvidenceException(string query, double minConfidence)
            : base($"Insufficient evidence for query: '{query}'. No results met confidence threshold {minConfidence:F2}. " +
                   $"Remediation: rephrase query, add more documents, or lower threshold.", "RAG_INSUFFICIENT_EVIDENCE")
        {
            Query = query;
            MinConfidence = minConfidence;
        }
    }
}

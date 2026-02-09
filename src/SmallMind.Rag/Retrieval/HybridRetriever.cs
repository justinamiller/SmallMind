using SmallMind.Rag.Common;
using SmallMind.Rag.Indexing.Sparse;

namespace SmallMind.Rag.Retrieval;

/// <summary>
/// Hybrid retriever that combines BM25 sparse retrieval with dense vector retrieval.
/// </summary>
internal sealed class HybridRetriever
{
    private readonly Bm25Retriever _bm25;
    private readonly DenseRetriever _dense;
    private readonly float _sparseWeight;
    private readonly float _denseWeight;

    /// <summary>
    /// Initializes a new hybrid retriever.
    /// </summary>
    /// <param name="bm25">BM25 sparse retriever.</param>
    /// <param name="dense">Dense vector retriever.</param>
    /// <param name="sparseWeight">Weight for BM25 scores (default 0.5).</param>
    /// <param name="denseWeight">Weight for dense scores (default 0.5).</param>
    public HybridRetriever(
        Bm25Retriever bm25, 
        DenseRetriever dense, 
        float sparseWeight = 0.5f, 
        float denseWeight = 0.5f)
    {
        _bm25 = bm25 ?? throw new ArgumentNullException(nameof(bm25));
        _dense = dense ?? throw new ArgumentNullException(nameof(dense));

        if (sparseWeight < 0f || denseWeight < 0f)
            throw new ArgumentException("Weights must be non-negative");

        float totalWeight = sparseWeight + denseWeight;
        if (totalWeight == 0f)
            throw new ArgumentException("At least one weight must be positive");

        _sparseWeight = sparseWeight / totalWeight;
        _denseWeight = denseWeight / totalWeight;
    }

    /// <summary>
    /// Retrieves the most relevant chunks using hybrid search.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="topK">Number of top results to return.</param>
    /// <param name="chunkStore">Dictionary of all chunks by ID.</param>
    /// <param name="candidateK">Number of candidates to retrieve from each method (default 20).</param>
    /// <returns>List of retrieved chunks sorted by combined relevance.</returns>
    public List<RetrievedChunk> Retrieve(
        string query, 
        int topK, 
        Dictionary<string, Chunk> chunkStore, 
        int candidateK = 20)
    {
        if (string.IsNullOrEmpty(query))
            return new List<RetrievedChunk>();
        if (chunkStore == null)
            throw new ArgumentNullException(nameof(chunkStore));
        if (topK <= 0)
            throw new ArgumentException("topK must be positive", nameof(topK));
        if (candidateK <= 0)
            throw new ArgumentException("candidateK must be positive", nameof(candidateK));

        // Retrieve candidates from both methods
        var bm25Results = _bm25.Retrieve(query, candidateK, chunkStore);
        var denseResults = _dense.Retrieve(query, candidateK, chunkStore);

        // Normalize scores
        var normalizedBm25 = NormalizeScores(bm25Results);
        var normalizedDense = NormalizeScores(denseResults);

        // Merge results by chunk ID
        var combinedScores = new Dictionary<string, (Chunk chunk, float score)>();

        // Add BM25 results
        for (int i = 0; i < bm25Results.Count; i++)
        {
            var result = bm25Results[i];
            string chunkId = result.ChunkId;

            if (chunkStore.TryGetValue(chunkId, out var chunk) && 
                normalizedBm25.TryGetValue(chunkId, out float normScore))
            {
                float combinedScore = _sparseWeight * normScore;
                combinedScores[chunkId] = (chunk, combinedScore);
            }
        }

        // Add or update with dense results
        for (int i = 0; i < denseResults.Count; i++)
        {
            var result = denseResults[i];
            string chunkId = result.ChunkId;

            if (chunkStore.TryGetValue(chunkId, out var chunk) &&
                normalizedDense.TryGetValue(chunkId, out float normScore))
            {
                float denseContribution = _denseWeight * normScore;

                if (combinedScores.TryGetValue(chunkId, out var existing))
                {
                    combinedScores[chunkId] = (existing.chunk, existing.score + denseContribution);
                }
                else
                {
                    combinedScores[chunkId] = (chunk, denseContribution);
                }
            }
        }

        // Convert to list and sort by combined score
        var mergedResults = new List<(Chunk chunk, float score)>(combinedScores.Count);
        foreach (var kvp in combinedScores)
        {
            mergedResults.Add(kvp.Value);
        }

        mergedResults.Sort((a, b) => b.score.CompareTo(a.score));

        // Build final result list with ranks
        int returnCount = Math.Min(topK, mergedResults.Count);
        var finalResults = new List<RetrievedChunk>(returnCount);

        for (int i = 0; i < returnCount; i++)
        {
            var (chunk, score) = mergedResults[i];

            string excerpt = TextHelper.TruncateWithEllipsis(chunk.Text, RetrievalConstants.MaxExcerptLength);

            finalResults.Add(new RetrievedChunk(
                chunkId: chunk.ChunkId,
                docId: chunk.DocId,
                score: score,
                rank: i + 1,
                excerpt: excerpt
            ));
        }

        return finalResults;
    }

    /// <summary>
    /// Normalizes scores to [0, 1] range by dividing by the maximum score.
    /// </summary>
    /// <param name="results">Results to normalize.</param>
    /// <returns>Dictionary mapping chunk ID to normalized score.</returns>
    private Dictionary<string, float> NormalizeScores(List<RetrievedChunk> results)
    {
        var normalized = new Dictionary<string, float>();

        if (results.Count == 0)
            return normalized;

        // Find max score
        float maxScore = float.NegativeInfinity;
        for (int i = 0; i < results.Count; i++)
        {
            if (results[i].Score > maxScore)
                maxScore = results[i].Score;
        }

        if (maxScore <= 0f)
            maxScore = 1f;

        // Normalize
        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            normalized[result.ChunkId] = result.Score / maxScore;
        }

        return normalized;
    }
}

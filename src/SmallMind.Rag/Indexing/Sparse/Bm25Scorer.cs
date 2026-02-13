namespace SmallMind.Rag.Indexing.Sparse;

/// <summary>
/// Static BM25 scoring implementation for ranking document relevance.
/// Implements the Okapi BM25 algorithm with configurable parameters.
/// </summary>
internal static class Bm25Scorer
{
    /// <summary>
    /// Computes the BM25 relevance score for a chunk given query terms.
    /// </summary>
    /// <param name="queryTerms">The list of tokenized query terms.</param>
    /// <param name="chunkId">The chunk identifier to score.</param>
    /// <param name="index">The inverted index containing term statistics.</param>
    /// <param name="k1">BM25 term frequency saturation parameter (default: 1.2).</param>
    /// <param name="b">BM25 length normalization parameter (default: 0.75).</param>
    /// <returns>The BM25 score for the chunk. Higher scores indicate greater relevance.</returns>
    /// <remarks>
    /// BM25 formula: score = Σ IDF(t) * ((tf*(k1+1)) / (tf + k1*(1 - b + b*(dl/avgdl))))
    /// where IDF(t) = log((N - df + 0.5) / (df + 0.5) + 1.0)
    /// 
    /// N = total number of chunks
    /// df = document frequency (number of chunks containing term)
    /// tf = term frequency (number of times term appears in chunk)
    /// dl = document length (number of tokens in chunk)
    /// avgdl = average document length across all chunks
    /// </remarks>
    public static double ComputeScore(
        List<string> queryTerms,
        string chunkId,
        InvertedIndex index,
        double k1 = 1.2,
        double b = 0.75)
    {
        if (queryTerms == null || queryTerms.Count == 0)
            return 0.0;

        if (string.IsNullOrEmpty(chunkId))
            return 0.0;

        if (index == null)
            throw new ArgumentNullException(nameof(index));

        int N = index.TotalChunks;

        if (N == 0)
            return 0.0;

        int docLength = index.GetDocLength(chunkId);

        if (docLength == 0)
            return 0.0;

        double avgDocLength = index.AvgDocLength;

        if (avgDocLength <= 0.0)
            avgDocLength = 1.0;

        double score = 0.0;

        // Iterate over query terms and accumulate BM25 score
        for (int i = 0; i < queryTerms.Count; i++)
        {
            string term = queryTerms[i];

            int df = index.GetDocumentFrequency(term);

            if (df == 0)
                continue;

            int tf = index.GetTermFrequency(term, chunkId);

            if (tf == 0)
                continue;

            // Compute IDF: log((N - df + 0.5) / (df + 0.5) + 1.0)
            double idf = Math.Log((N - df + 0.5) / (df + 0.5) + 1.0);

            // Compute term score: (tf * (k1 + 1)) / (tf + k1 * (1 - b + b * (dl / avgdl)))
            double lengthNorm = 1.0 - b + b * (docLength / avgDocLength);
            double termScore = (tf * (k1 + 1.0)) / (tf + k1 * lengthNorm);

            score += idf * termScore;
        }

        return score;
    }
}

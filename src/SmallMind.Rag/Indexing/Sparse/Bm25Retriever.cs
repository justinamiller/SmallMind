using System;
using System.Collections.Generic;
using SmallMind.Rag.Common;

namespace SmallMind.Rag.Indexing.Sparse;

/// <summary>
/// BM25-based retriever for ranking and retrieving relevant chunks.
/// Combines tokenization, scoring, and ranking to return top-K results.
/// </summary>
public sealed class Bm25Retriever
{
    private readonly InvertedIndex _index;
    private readonly double _k1;
    private readonly double _b;

    /// <summary>
    /// Initializes a new instance of the <see cref="Bm25Retriever"/> class.
    /// </summary>
    /// <param name="index">The inverted index to use for retrieval.</param>
    /// <param name="k1">BM25 term frequency saturation parameter (default: 1.2).</param>
    /// <param name="b">BM25 length normalization parameter (default: 0.75).</param>
    public Bm25Retriever(InvertedIndex index, double k1 = 1.2, double b = 0.75)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _k1 = k1;
        _b = b;
    }

    /// <summary>
    /// Retrieves the top-K most relevant chunks for a given query.
    /// </summary>
    /// <param name="query">The query string to search for.</param>
    /// <param name="topK">The number of top results to return.</param>
    /// <param name="chunkStore">Dictionary mapping chunk IDs to Chunk objects for metadata retrieval.</param>
    /// <returns>A list of retrieved chunks sorted by relevance (descending score).</returns>
    /// <remarks>
    /// The method tokenizes the query, scores all candidate chunks using BM25,
    /// and returns the top-K results with ranking information.
    /// </remarks>
    public List<RetrievedChunk> Retrieve(
        string query,
        int topK,
        Dictionary<string, Chunk> chunkStore)
    {
        if (string.IsNullOrEmpty(query))
            return new List<RetrievedChunk>();

        if (topK <= 0)
            return new List<RetrievedChunk>();

        if (chunkStore == null)
            throw new ArgumentNullException(nameof(chunkStore));

        // Tokenize query
        List<string> queryTerms = RagTokenizer.Tokenize(query);
        
        if (queryTerms.Count == 0)
            return new List<RetrievedChunk>();

        // Collect candidate chunks (chunks that contain at least one query term)
        var candidates = new HashSet<string>();
        
        for (int i = 0; i < queryTerms.Count; i++)
        {
            string term = queryTerms[i];
            
            // Get all chunks containing this term directly from the index
            foreach (string chunkId in _index.GetChunkIdsForTerm(term))
            {
                candidates.Add(chunkId);
            }
        }

        if (candidates.Count == 0)
            return new List<RetrievedChunk>();

        // Score all candidates
        var scoredChunks = new List<ScoredChunk>(candidates.Count);
        
        foreach (string chunkId in candidates)
        {
            double score = Bm25Scorer.ComputeScore(queryTerms, chunkId, _index, _k1, _b);
            
            if (score > 0.0)
            {
                scoredChunks.Add(new ScoredChunk(chunkId, score));
            }
        }

        // Sort by score descending (manual sorting, no LINQ)
        scoredChunks.Sort((a, b) =>
        {
            // Sort descending: b compared to a
            if (b.Score > a.Score) return 1;
            if (b.Score < a.Score) return -1;
            return 0;
        });

        // Take top-K and convert to RetrievedChunk
        int resultCount = Math.Min(topK, scoredChunks.Count);
        var results = new List<RetrievedChunk>(resultCount);

        for (int i = 0; i < resultCount; i++)
        {
            ScoredChunk sc = scoredChunks[i];
            
            // Get chunk metadata from store
            if (!chunkStore.TryGetValue(sc.ChunkId, out Chunk? chunk))
                continue;

            // Create excerpt (first 200 chars of text)
            string excerpt = TextHelper.TruncateWithEllipsis(chunk.Text, RetrievalConstants.MaxExcerptLength);

            var retrievedChunk = new RetrievedChunk(
                chunkId: sc.ChunkId,
                docId: chunk.DocId,
                score: (float)sc.Score,
                rank: i + 1,
                excerpt: excerpt
            );

            results.Add(retrievedChunk);
        }

        return results;
    }

    /// <summary>
    /// Internal structure for holding chunk ID and score pairs during ranking.
    /// </summary>
    private readonly struct ScoredChunk
    {
        public readonly string ChunkId;
        public readonly double Score;

        public ScoredChunk(string chunkId, double score)
        {
            ChunkId = chunkId;
            Score = score;
        }
    }
}

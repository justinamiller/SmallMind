using SmallMind.Rag.Common;
using SmallMind.Rag.Indexing.Sparse;

namespace SmallMind.Rag.Retrieval;

/// <summary>
/// Dense retriever using vector embeddings for semantic search.
/// </summary>
public sealed class DenseRetriever
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbedder _embedder;

    /// <summary>
    /// Initializes a new dense retriever.
    /// </summary>
    /// <param name="vectorStore">The vector store for indexing and search.</param>
    /// <param name="embedder">The embedder for generating vectors.</param>
    public DenseRetriever(IVectorStore vectorStore, IEmbedder embedder)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _embedder = embedder ?? throw new ArgumentNullException(nameof(embedder));

        if (vectorStore.Dimension != embedder.EmbeddingDim)
            throw new ArgumentException($"Vector store dimension {vectorStore.Dimension} does not match embedder dimension {embedder.EmbeddingDim}");
    }

    /// <summary>
    /// Retrieves the most relevant chunks for a query using dense embeddings.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="topK">Number of top results to return.</param>
    /// <param name="chunkStore">Dictionary of all chunks by ID.</param>
    /// <returns>List of retrieved chunks sorted by relevance.</returns>
    public List<RetrievedChunk> Retrieve(string query, int topK, Dictionary<string, Chunk> chunkStore)
    {
        if (string.IsNullOrEmpty(query))
            return new List<RetrievedChunk>();
        if (chunkStore == null)
            throw new ArgumentNullException(nameof(chunkStore));
        if (topK <= 0)
            throw new ArgumentException("topK must be positive", nameof(topK));

        var queryVector = _embedder.Embed(query);
        var searchResults = _vectorStore.Search(queryVector, topK);

        var retrievedChunks = new List<RetrievedChunk>(searchResults.Count);

        for (int i = 0; i < searchResults.Count; i++)
        {
            var (chunkId, score) = searchResults[i];

            if (chunkStore.TryGetValue(chunkId, out var chunk))
            {
                string excerpt = TextHelper.TruncateWithEllipsis(chunk.Text, RetrievalConstants.MaxExcerptLength);

                var retrieved = new RetrievedChunk(
                    chunkId: chunk.ChunkId,
                    docId: chunk.DocId,
                    score: score,
                    rank: i + 1,
                    excerpt: excerpt
                );

                retrievedChunks.Add(retrieved);
            }
        }

        return retrievedChunks;
    }

    /// <summary>
    /// Builds the vector index from a collection of chunks.
    /// </summary>
    /// <param name="chunks">Dictionary of chunks to index.</param>
    public void BuildIndex(Dictionary<string, Chunk> chunks)
    {
        if (chunks == null)
            throw new ArgumentNullException(nameof(chunks));

        _vectorStore.Clear();

        foreach (var kvp in chunks)
        {
            string chunkId = kvp.Key;
            Chunk chunk = kvp.Value;

            var vector = _embedder.Embed(chunk.Text);
            _vectorStore.AddVector(chunkId, vector);
        }
    }
}

namespace SmallMind.Rag;

/// <summary>
/// Represents a retrieved chunk with relevance scoring information.
/// Immutable value type for performance in retrieval operations.
/// </summary>
internal readonly struct RetrievedChunk
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetrievedChunk"/> struct.
    /// </summary>
    /// <param name="chunkId">The chunk identifier.</param>
    /// <param name="docId">The parent document identifier.</param>
    /// <param name="score">The relevance score (typically 0.0 to 1.0).</param>
    /// <param name="rank">The ranking position in retrieval results (1-based).</param>
    /// <param name="excerpt">Optional excerpt of the chunk content for display.</param>
    public RetrievedChunk(
        string chunkId,
        string docId,
        float score,
        int rank,
        string excerpt = "")
    {
        ChunkId = chunkId ?? throw new ArgumentNullException(nameof(chunkId));
        DocId = docId ?? throw new ArgumentNullException(nameof(docId));
        Score = score;
        Rank = rank;
        Excerpt = excerpt ?? string.Empty;
    }

    /// <summary>
    /// Gets the chunk identifier.
    /// </summary>
    public string ChunkId { get; }

    /// <summary>
    /// Gets the parent document identifier.
    /// </summary>
    public string DocId { get; }

    /// <summary>
    /// Gets the relevance score (typically 0.0 to 1.0, higher is more relevant).
    /// </summary>
    public float Score { get; }

    /// <summary>
    /// Gets the ranking position in retrieval results (1-based, where 1 is the most relevant).
    /// </summary>
    public int Rank { get; }

    /// <summary>
    /// Gets an optional excerpt of the chunk content for display purposes.
    /// </summary>
    public string Excerpt { get; }
}

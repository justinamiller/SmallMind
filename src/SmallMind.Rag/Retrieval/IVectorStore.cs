namespace SmallMind.Rag.Retrieval;

/// <summary>
/// Interface for storing and searching vectors for dense retrieval.
/// </summary>
internal interface IVectorStore
{
    /// <summary>
    /// Adds a vector to the store.
    /// </summary>
    /// <param name="chunkId">Unique identifier for the chunk.</param>
    /// <param name="vector">The embedding vector.</param>
    void AddVector(string chunkId, float[] vector);

    /// <summary>
    /// Removes a vector from the store.
    /// </summary>
    /// <param name="chunkId">Unique identifier for the chunk to remove.</param>
    void RemoveVector(string chunkId);

    /// <summary>
    /// Searches for the most similar vectors to the query vector.
    /// </summary>
    /// <param name="queryVector">The query embedding vector.</param>
    /// <param name="topK">Number of top results to return.</param>
    /// <returns>List of chunk IDs and their similarity scores, sorted by score descending.</returns>
    List<(string chunkId, float score)> Search(float[] queryVector, int topK);

    /// <summary>
    /// Gets the number of vectors in the store.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the dimension of vectors in the store.
    /// </summary>
    int Dimension { get; }

    /// <summary>
    /// Saves the vector store to disk.
    /// </summary>
    /// <param name="path">File path to save to.</param>
    void Save(string path);

    /// <summary>
    /// Loads the vector store from disk.
    /// </summary>
    /// <param name="path">File path to load from.</param>
    void Load(string path);

    /// <summary>
    /// Clears all vectors from the store.
    /// </summary>
    void Clear();
}

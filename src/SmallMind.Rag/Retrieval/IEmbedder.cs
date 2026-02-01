namespace SmallMind.Rag.Retrieval;

/// <summary>
/// Interface for generating embeddings from text.
/// </summary>
public interface IEmbedder
{
    /// <summary>
    /// Gets the dimension of the embedding vectors.
    /// </summary>
    int EmbeddingDim { get; }

    /// <summary>
    /// Generates an embedding vector for the given text.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <returns>An embedding vector.</returns>
    float[] Embed(string text);

    /// <summary>
    /// Generates embedding vectors for a batch of texts.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <returns>A list of embedding vectors.</returns>
    List<float[]> EmbedBatch(List<string> texts);
}

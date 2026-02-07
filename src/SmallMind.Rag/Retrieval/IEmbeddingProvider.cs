using System;
using System.Collections.Generic;

namespace SmallMind.Rag.Retrieval
{
    /// <summary>
    /// Abstraction for embedding providers.
    /// Converts text chunks into vector representations for semantic search.
    /// </summary>
    public interface IEmbeddingProvider
    {
        /// <summary>
        /// Gets the dimensionality of the embedding vectors produced by this provider.
        /// </summary>
        int EmbeddingDimension { get; }

        /// <summary>
        /// Generate an embedding vector for a single text chunk.
        /// </summary>
        /// <param name="text">The text to embed</param>
        /// <returns>An array of floats representing the embedding vector</returns>
        float[] Embed(string text);

        /// <summary>
        /// Generate embedding vectors for multiple text chunks.
        /// Default implementation calls Embed for each chunk.
        /// </summary>
        /// <param name="texts">The texts to embed</param>
        /// <returns>A list of embedding vectors</returns>
        List<float[]> EmbedBatch(List<string> texts)
        {
            var embeddings = new List<float[]>(texts.Count);
            for (int i = 0; i < texts.Count; i++)
            {
                embeddings.Add(Embed(texts[i]));
            }
            return embeddings;
        }
    }
}

using System.Runtime.CompilerServices;
using SmallMind.Rag.Indexing.Sparse;

namespace SmallMind.Rag.Retrieval;

/// <summary>
/// Deterministic feature hashing embedder using the hashing trick.
/// Provides a simple baseline for embeddings without external models.
/// </summary>
public sealed class FeatureHashingEmbedder : IEmbedder
{
    private readonly int _dimension;
    private readonly int _seed;

    /// <summary>
    /// Initializes a new feature hashing embedder.
    /// </summary>
    /// <param name="dimension">The dimension of the embedding vectors.</param>
    /// <param name="seed">Random seed for hash stability.</param>
    public FeatureHashingEmbedder(int dimension = 256, int seed = 42)
    {
        if (dimension <= 0)
            throw new ArgumentException("Dimension must be positive", nameof(dimension));

        _dimension = dimension;
        _seed = seed;
    }

    /// <inheritdoc/>
    public int EmbeddingDim => _dimension;

    /// <inheritdoc/>
    public float[] Embed(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new float[_dimension];

        var vector = new float[_dimension];
        var tokens = RagTokenizer.Tokenize(text);

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];
            int hash = HashToken(token);
            int index = Math.Abs(hash) % _dimension;
            float sign = GetSign(hash);

            vector[index] += sign;
        }

        L2Normalize(vector);
        return vector;
    }

    /// <inheritdoc/>
    public List<float[]> EmbedBatch(List<string> texts)
    {
        var embeddings = new List<float[]>(texts.Count);
        
        for (int i = 0; i < texts.Count; i++)
        {
            embeddings.Add(Embed(texts[i]));
        }

        return embeddings;
    }

    /// <summary>
    /// Computes a hash for a token with the seed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int HashToken(string token)
    {
        return token.GetHashCode() ^ _seed;
    }

    /// <summary>
    /// Gets the sign (+1 or -1) from the hash value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetSign(int hash)
    {
        return ((hash >> 1) & 1) == 0 ? 1.0f : -1.0f;
    }

    /// <summary>
    /// L2 normalizes the vector in-place.
    /// </summary>
    private void L2Normalize(float[] vector)
    {
        float sumSquares = 0f;

        for (int i = 0; i < vector.Length; i++)
        {
            sumSquares += vector[i] * vector[i];
        }

        if (sumSquares == 0f)
            return;

        float norm = MathF.Sqrt(sumSquares);
        float invNorm = 1f / norm;

        for (int i = 0; i < vector.Length; i++)
        {
            vector[i] *= invNorm;
        }
    }
}

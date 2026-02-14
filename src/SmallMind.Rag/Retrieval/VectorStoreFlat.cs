using System.Numerics;
using System.Runtime.CompilerServices;
using SmallMind.Core.Simd;

namespace SmallMind.Rag.Retrieval;

/// <summary>
/// Simple flat vector store that performs brute-force similarity search.
/// Optimized with SIMD for cosine similarity computation.
/// </summary>
internal sealed class VectorStoreFlat : IVectorStore
{
    private const int FILE_VERSION = 1;

    private readonly List<(string chunkId, float[] vector)> _vectors;
    private readonly int _dimension;

    /// <summary>
    /// Initializes a new flat vector store.
    /// </summary>
    /// <param name="dimension">The dimension of vectors to store.</param>
    public VectorStoreFlat(int dimension)
    {
        if (dimension <= 0)
            throw new ArgumentException("Dimension must be positive", nameof(dimension));

        _dimension = dimension;
        _vectors = new List<(string, float[])>();
    }

    /// <inheritdoc/>
    public int Count => _vectors.Count;

    /// <inheritdoc/>
    public int Dimension => _dimension;

    /// <inheritdoc/>
    public void AddVector(string chunkId, float[] vector)
    {
        if (vector == null)
            throw new ArgumentNullException(nameof(vector));
        if (vector.Length != _dimension)
            throw new ArgumentException($"Vector dimension {vector.Length} does not match store dimension {_dimension}");
        if (string.IsNullOrEmpty(chunkId))
            throw new ArgumentException("ChunkId cannot be null or empty", nameof(chunkId));

        _vectors.Add((chunkId, vector));
    }

    /// <inheritdoc/>
    public void RemoveVector(string chunkId)
    {
        for (int i = _vectors.Count - 1; i >= 0; i--)
        {
            if (_vectors[i].chunkId == chunkId)
            {
                _vectors.RemoveAt(i);
            }
        }
    }

    /// <inheritdoc/>
    public List<(string chunkId, float score)> Search(float[] queryVector, int topK)
    {
        if (queryVector == null)
            throw new ArgumentNullException(nameof(queryVector));
        if (queryVector.Length != _dimension)
            throw new ArgumentException($"Query vector dimension {queryVector.Length} does not match store dimension {_dimension}");
        if (topK <= 0)
            throw new ArgumentException("topK must be positive", nameof(topK));

        if (_vectors.Count == 0)
            return new List<(string, float)>();

        float queryNorm = ComputeNorm(queryVector);
        if (queryNorm < 1e-9f)
            return new List<(string, float)>();

        var results = new List<(string chunkId, float score)>(_vectors.Count);

        for (int i = 0; i < _vectors.Count; i++)
        {
            var (chunkId, vector) = _vectors[i];
            float similarity = CosineSimilarity(queryVector, vector, queryNorm);
            results.Add((chunkId, similarity));
        }

        // Sort by score descending
        results.Sort((a, b) => b.score.CompareTo(a.score));

        // Return top K
        int returnCount = Math.Min(topK, results.Count);
        var topResults = new List<(string, float)>(returnCount);
        for (int i = 0; i < returnCount; i++)
        {
            topResults.Add(results[i]);
        }

        return topResults;
    }

    /// <inheritdoc/>
    public void Clear()
    {
        _vectors.Clear();
    }

    /// <inheritdoc/>
    public void Save(string path)
    {
        using var writer = new BinaryWriter(File.Open(path, FileMode.Create));

        writer.Write(FILE_VERSION);
        writer.Write(_dimension);
        writer.Write(_vectors.Count);

        for (int i = 0; i < _vectors.Count; i++)
        {
            var (chunkId, vector) = _vectors[i];
            writer.Write(chunkId);

            for (int j = 0; j < vector.Length; j++)
            {
                writer.Write(vector[j]);
            }
        }
    }

    /// <inheritdoc/>
    public void Load(string path)
    {
        _vectors.Clear();

        using var reader = new BinaryReader(File.Open(path, FileMode.Open));

        int version = reader.ReadInt32();
        if (version != FILE_VERSION)
            throw new InvalidOperationException($"Unsupported file version {version}");

        int dimension = reader.ReadInt32();
        if (dimension != _dimension)
            throw new InvalidOperationException($"File dimension {dimension} does not match store dimension {_dimension}");

        int count = reader.ReadInt32();

        for (int i = 0; i < count; i++)
        {
            string chunkId = reader.ReadString();
            float[] vector = new float[_dimension];

            for (int j = 0; j < _dimension; j++)
            {
                vector[j] = reader.ReadSingle();
            }

            _vectors.Add((chunkId, vector));
        }
    }

    /// <summary>
    /// Computes cosine similarity between two vectors using SIMD optimization.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float CosineSimilarity(float[] a, float[] b, float normA)
    {
        float dotProduct = MatMulOps.DotProduct(a.AsSpan(), b.AsSpan());
        float normB = ComputeNorm(b);

        if (normB < 1e-9f)
            return 0f;

        return dotProduct / (normA * normB);
    }

    /// <summary>
    /// Computes L2 norm using SIMD when available.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ComputeNorm(float[] vector)
    {
        int vectorSize = Vector<float>.Count;
        int length = vector.Length;
        int i = 0;

        Vector<float> sumVec = Vector<float>.Zero;

        // Process SIMD-width chunks
        for (; i <= length - vectorSize; i += vectorSize)
        {
            var v = new Vector<float>(vector, i);
            sumVec += v * v;
        }

        // Sum the vector elements
        float sum = 0f;
        for (int j = 0; j < vectorSize; j++)
        {
            sum += sumVec[j];
        }

        // Handle remainder
        for (; i < length; i++)
        {
            sum += vector[i] * vector[i];
        }

        return MathF.Sqrt(sum);
    }
}

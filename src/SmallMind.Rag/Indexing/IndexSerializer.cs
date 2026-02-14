using SmallMind.Core.Validation;
using SmallMind.Rag.Indexing.Sparse;

namespace SmallMind.Rag.Indexing;

/// <summary>
/// Handles binary serialization and deserialization of the RAG index to disk.
/// Stores chunks, sparse inverted index, and manifest as separate files.
/// </summary>
internal static class IndexSerializer
{
    private const string ManifestFileName = "manifest.json";
    private const string ChunksFileName = "chunks.bin";
    private const string SparseIndexFileName = "sparse.bin";

    /// <summary>
    /// Saves the complete index to disk including chunks, inverted index, and manifest.
    /// </summary>
    /// <param name="indexDir">The directory to save index files to.</param>
    /// <param name="index">The inverted index to save.</param>
    /// <param name="chunks">The chunk dictionary to save.</param>
    /// <param name="manifest">The index manifest to save.</param>
    /// <exception cref="IOException">Thrown when files cannot be written.</exception>
    public static void SaveIndex(
        string indexDir,
        InvertedIndex index,
        Dictionary<string, Chunk> chunks,
        IndexManifest manifest)
    {
        if (indexDir == null)
            throw new ArgumentNullException(nameof(indexDir));
        if (index == null)
            throw new ArgumentNullException(nameof(index));
        if (chunks == null)
            throw new ArgumentNullException(nameof(chunks));
        if (manifest == null)
            throw new ArgumentNullException(nameof(manifest));

        if (!Directory.Exists(indexDir))
        {
            Directory.CreateDirectory(indexDir);
        }

        // Validate file names are safe (literal constants, but CodeQL may still flag them)
        Guard.SafeFileName(ManifestFileName, nameof(ManifestFileName));
        Guard.SafeFileName(ChunksFileName, nameof(ChunksFileName));
        Guard.SafeFileName(SparseIndexFileName, nameof(SparseIndexFileName));

        string manifestPath = Path.Combine(indexDir, ManifestFileName);
        string chunksPath = Path.Combine(indexDir, ChunksFileName);
        string sparsePath = Path.Combine(indexDir, SparseIndexFileName);

        try
        {
            SaveChunks(chunksPath, chunks);
            SaveSparseIndex(sparsePath, index);
            manifest.SaveToFile(manifestPath);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to save index to '{indexDir}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads the complete index from disk including chunks, inverted index, and manifest.
    /// </summary>
    /// <param name="indexDir">The directory to load index files from.</param>
    /// <returns>A tuple containing the loaded inverted index, chunks, and manifest.</returns>
    /// <exception cref="IOException">Thrown when files cannot be read or are incompatible.</exception>
    public static (InvertedIndex, Dictionary<string, Chunk>, IndexManifest) LoadIndex(string indexDir)
    {
        if (indexDir == null)
            throw new ArgumentNullException(nameof(indexDir));

        if (!Directory.Exists(indexDir))
        {
            return (new InvertedIndex(), new Dictionary<string, Chunk>(), new IndexManifest());
        }

        // Validate file names are safe (literal constants, but CodeQL may still flag them)
        Guard.SafeFileName(ManifestFileName, nameof(ManifestFileName));
        Guard.SafeFileName(ChunksFileName, nameof(ChunksFileName));
        Guard.SafeFileName(SparseIndexFileName, nameof(SparseIndexFileName));

        string manifestPath = Path.Combine(indexDir, ManifestFileName);
        string chunksPath = Path.Combine(indexDir, ChunksFileName);
        string sparsePath = Path.Combine(indexDir, SparseIndexFileName);

        if (!File.Exists(manifestPath) || !File.Exists(chunksPath) || !File.Exists(sparsePath))
        {
            return (new InvertedIndex(), new Dictionary<string, Chunk>(), new IndexManifest());
        }

        try
        {
            IndexManifest manifest = IndexManifest.LoadFromFile(manifestPath);

            if (manifest.Version != 1)
            {
                throw new IOException($"Incompatible index version: {manifest.Version}. Expected version 1.");
            }

            Dictionary<string, Chunk> chunks = LoadChunks(chunksPath);
            InvertedIndex index = LoadSparseIndex(sparsePath);

            return (index, chunks, manifest);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to load index from '{indexDir}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Saves chunks to a binary file.
    /// </summary>
    private static void SaveChunks(string path, Dictionary<string, Chunk> chunks)
    {
        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new BinaryWriter(fs);

        writer.Write(chunks.Count);

        foreach (var kvp in chunks)
        {
            Chunk chunk = kvp.Value;

            writer.Write(chunk.ChunkId);
            writer.Write(chunk.DocId);
            writer.Write(chunk.SourceUri);
            writer.Write(chunk.Title);
            writer.Write(chunk.Text);
            writer.Write(chunk.CharStart);
            writer.Write(chunk.CharEnd);
            writer.Write(chunk.SecurityLabel);

            writer.Write(chunk.Tags.Length);
            for (int i = 0; i < chunk.Tags.Length; i++)
            {
                writer.Write(chunk.Tags[i]);
            }

            writer.Write(chunk.CreatedUtc.Ticks);
            writer.Write(chunk.Version);
        }
    }

    /// <summary>
    /// Loads chunks from a binary file.
    /// </summary>
    private static Dictionary<string, Chunk> LoadChunks(string path)
    {
        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new BinaryReader(fs);

        int count = reader.ReadInt32();
        var chunks = new Dictionary<string, Chunk>(count);

        for (int i = 0; i < count; i++)
        {
            string chunkId = reader.ReadString();
            string docId = reader.ReadString();
            string sourceUri = reader.ReadString();
            string title = reader.ReadString();
            string text = reader.ReadString();
            int charStart = reader.ReadInt32();
            int charEnd = reader.ReadInt32();
            string securityLabel = reader.ReadString();

            int tagsCount = reader.ReadInt32();
            string[] tags = new string[tagsCount];
            for (int j = 0; j < tagsCount; j++)
            {
                tags[j] = reader.ReadString();
            }

            long createdTicks = reader.ReadInt64();
            int version = reader.ReadInt32();

            var chunk = new Chunk
            {
                ChunkId = chunkId,
                DocId = docId,
                SourceUri = sourceUri,
                Title = title,
                Text = text,
                CharStart = charStart,
                CharEnd = charEnd,
                SecurityLabel = securityLabel,
                Tags = tags,
                CreatedUtc = new DateTime(createdTicks, DateTimeKind.Utc),
                Version = version
            };

            chunks[chunkId] = chunk;
        }

        return chunks;
    }

    /// <summary>
    /// Saves the sparse inverted index to a binary file.
    /// </summary>
    private static void SaveSparseIndex(string path, InvertedIndex index)
    {
        using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using BinaryWriter writer = new BinaryWriter(fs);

        writer.Write(index.TotalChunks);
        writer.Write(index.AvgDocLength);

        var docLengths = new List<(string chunkId, int length)>();
        foreach (var entry in index.GetAllDocLengths())
        {
            docLengths.Add(entry);
        }

        writer.Write(docLengths.Count);
        for (int i = 0; i < docLengths.Count; i++)
        {
            writer.Write(docLengths[i].chunkId);
            writer.Write(docLengths[i].length);
        }

        var terms = new List<string>();
        foreach (string term in index.GetAllTerms())
        {
            terms.Add(term);
        }

        writer.Write(terms.Count);
        for (int i = 0; i < terms.Count; i++)
        {
            string term = terms[i];
            writer.Write(term);

            int df = index.GetDocumentFrequency(term);
            writer.Write(df);

            var postings = new List<(string chunkId, int frequency)>();
            foreach (var posting in index.GetPostingsForTerm(term))
            {
                postings.Add(posting);
            }

            writer.Write(postings.Count);
            for (int j = 0; j < postings.Count; j++)
            {
                writer.Write(postings[j].chunkId);
                writer.Write(postings[j].frequency);
            }
        }
    }

    /// <summary>
    /// Loads the sparse inverted index from a binary file.
    /// </summary>
    private static InvertedIndex LoadSparseIndex(string path)
    {
        using FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using BinaryReader reader = new BinaryReader(fs);

        int totalChunks = reader.ReadInt32();
        double avgDocLength = reader.ReadDouble();

        var index = new InvertedIndex();
        index.SetState(totalChunks, avgDocLength);

        int docLengthsCount = reader.ReadInt32();
        for (int i = 0; i < docLengthsCount; i++)
        {
            string chunkId = reader.ReadString();
            int length = reader.ReadInt32();
            index.AddDocLength(chunkId, length);
        }

        int vocabSize = reader.ReadInt32();
        for (int i = 0; i < vocabSize; i++)
        {
            string term = reader.ReadString();
            int df = reader.ReadInt32();

            int postingsCount = reader.ReadInt32();
            for (int j = 0; j < postingsCount; j++)
            {
                string chunkId = reader.ReadString();
                int frequency = reader.ReadInt32();
                index.AddPosting(term, chunkId, frequency);
            }
        }

        return index;
    }
}

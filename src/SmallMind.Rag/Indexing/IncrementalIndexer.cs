using System;
using System.Collections.Generic;
using System.IO;
using SmallMind.Rag.Indexing.Sparse;
using SmallMind.Rag.Ingestion;

namespace SmallMind.Rag.Indexing;

/// <summary>
/// Manages incremental indexing of documents for the RAG system.
/// Supports building indexes from scratch and updating existing indexes with new or changed documents.
/// </summary>
internal sealed class IncrementalIndexer
{
    private readonly string _indexDir;
    private readonly DocumentIngestor _ingestor;
    private readonly Chunker _chunker;
    private readonly RagOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="IncrementalIndexer"/> class.
    /// </summary>
    /// <param name="indexDir">The directory where index files are stored.</param>
    /// <param name="ingestor">The document ingestor for processing files.</param>
    /// <param name="chunker">The chunker for splitting documents into chunks.</param>
    /// <param name="options">RAG configuration options.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public IncrementalIndexer(
        string indexDir,
        DocumentIngestor ingestor,
        Chunker chunker,
        RagOptions options)
    {
        _indexDir = indexDir ?? throw new ArgumentNullException(nameof(indexDir));
        _ingestor = ingestor ?? throw new ArgumentNullException(nameof(ingestor));
        _chunker = chunker ?? throw new ArgumentNullException(nameof(chunker));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Rebuilds the index from scratch using the provided documents.
    /// Deletes any existing index and creates a new one.
    /// </summary>
    /// <param name="docs">The list of document records to index.</param>
    /// <param name="docContents">A dictionary mapping document IDs to their text content.</param>
    /// <exception cref="ArgumentNullException">Thrown when docs or docContents is null.</exception>
    /// <exception cref="IOException">Thrown when index files cannot be written.</exception>
    public void RebuildIndex(List<DocumentRecord> docs, Dictionary<string, string> docContents)
    {
        if (docs == null)
            throw new ArgumentNullException(nameof(docs));
        if (docContents == null)
            throw new ArgumentNullException(nameof(docContents));

        var index = new InvertedIndex();
        var chunks = new Dictionary<string, Chunk>();
        var manifest = new IndexManifest
        {
            ChunkingOptions = _options.Chunking
        };

        for (int i = 0; i < docs.Count; i++)
        {
            DocumentRecord doc = docs[i];

            if (!docContents.TryGetValue(doc.DocId, out string? content))
            {
                Console.WriteLine($"[WARNING] Content not found for document '{doc.DocId}', skipping.");
                continue;
            }

            ProcessDocument(doc, content, index, chunks);
            manifest.UpdateDocument(doc);
        }

        manifest.TotalDocuments = manifest.DocumentHashes.Count;
        manifest.TotalChunks = chunks.Count;

        IndexSerializer.SaveIndex(_indexDir, index, chunks, manifest);

        Console.WriteLine($"[INFO] Rebuilt index: {manifest.TotalDocuments} documents, {manifest.TotalChunks} chunks.");
    }

    /// <summary>
    /// Updates the existing index with new or changed documents.
    /// Loads the current index, identifies changes, and saves the updated index.
    /// </summary>
    /// <param name="newDocs">The list of document records to add or update.</param>
    /// <param name="docContents">A dictionary mapping document IDs to their text content.</param>
    /// <exception cref="ArgumentNullException">Thrown when newDocs or docContents is null.</exception>
    /// <exception cref="IOException">Thrown when index files cannot be read or written.</exception>
    public void UpdateIndex(List<DocumentRecord> newDocs, Dictionary<string, string> docContents)
    {
        if (newDocs == null)
            throw new ArgumentNullException(nameof(newDocs));
        if (docContents == null)
            throw new ArgumentNullException(nameof(docContents));

        (InvertedIndex index, Dictionary<string, Chunk> chunks, IndexManifest manifest) = 
            IndexSerializer.LoadIndex(_indexDir);

        if (manifest.ChunkingOptions != null)
        {
            bool optionsChanged = 
                manifest.ChunkingOptions.MaxChunkSize != _options.Chunking.MaxChunkSize ||
                manifest.ChunkingOptions.OverlapSize != _options.Chunking.OverlapSize ||
                manifest.ChunkingOptions.MinChunkSize != _options.Chunking.MinChunkSize;

            if (optionsChanged)
            {
                Console.WriteLine("[WARNING] Chunking options changed. Consider rebuilding the entire index.");
            }
        }

        int addedCount = 0;
        int updatedCount = 0;

        for (int i = 0; i < newDocs.Count; i++)
        {
            DocumentRecord doc = newDocs[i];

            if (!docContents.TryGetValue(doc.DocId, out string? content))
            {
                Console.WriteLine($"[WARNING] Content not found for document '{doc.DocId}', skipping.");
                continue;
            }

            bool isNew = !manifest.DocumentHashes.ContainsKey(doc.DocId);
            bool hasChanged = manifest.HasDocumentChanged(doc);

            if (isNew || hasChanged)
            {
                if (!isNew)
                {
                    RemoveDocumentChunks(doc.DocId, index, chunks);
                    updatedCount++;
                }
                else
                {
                    addedCount++;
                }

                ProcessDocument(doc, content, index, chunks);
                manifest.UpdateDocument(doc);
            }
        }

        manifest.TotalDocuments = manifest.DocumentHashes.Count;
        manifest.TotalChunks = chunks.Count;

        IndexSerializer.SaveIndex(_indexDir, index, chunks, manifest);

        Console.WriteLine($"[INFO] Updated index: {addedCount} added, {updatedCount} updated. Total: {manifest.TotalDocuments} documents, {manifest.TotalChunks} chunks.");
    }

    /// <summary>
    /// Processes a single document by chunking it and adding chunks to the index.
    /// </summary>
    private void ProcessDocument(
        DocumentRecord doc,
        string content,
        InvertedIndex index,
        Dictionary<string, Chunk> chunks)
    {
        List<Chunk> docChunks = _chunker.ChunkDocument(doc, content, _options.Chunking);

        for (int i = 0; i < docChunks.Count; i++)
        {
            Chunk chunk = docChunks[i];
            chunks[chunk.ChunkId] = chunk;
            index.AddChunk(chunk.ChunkId, chunk.Text);
        }
    }

    /// <summary>
    /// Removes all chunks associated with a document from the index and chunk dictionary.
    /// </summary>
    private void RemoveDocumentChunks(
        string docId,
        InvertedIndex index,
        Dictionary<string, Chunk> chunks)
    {
        var chunksToRemove = new List<string>();

        foreach (var kvp in chunks)
        {
            if (kvp.Value.DocId == docId)
            {
                chunksToRemove.Add(kvp.Key);
            }
        }

        for (int i = 0; i < chunksToRemove.Count; i++)
        {
            string chunkId = chunksToRemove[i];
            index.RemoveChunk(chunkId);
            chunks.Remove(chunkId);
        }
    }
}

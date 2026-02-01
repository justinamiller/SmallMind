using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace SmallMind.Rag.Indexing;

/// <summary>
/// Tracks metadata for the RAG index including version, creation time, and document hashes.
/// Used to support incremental updates and version checking.
/// </summary>
public sealed class IndexManifest
{
    /// <summary>
    /// Gets or sets the index format version number.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Gets or sets the UTC timestamp when the index was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC timestamp when the index was last modified.
    /// </summary>
    public DateTime LastModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the total number of documents in the index.
    /// </summary>
    public int TotalDocuments { get; set; }

    /// <summary>
    /// Gets or sets the total number of chunks in the index.
    /// </summary>
    public int TotalChunks { get; set; }

    /// <summary>
    /// Gets or sets the mapping of document IDs to content hashes.
    /// Used to detect document changes for incremental updates.
    /// </summary>
    public Dictionary<string, string> DocumentHashes { get; set; } = new();

    /// <summary>
    /// Gets or sets the chunking options used when creating the index.
    /// </summary>
    public RagOptions.ChunkingOptions? ChunkingOptions { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexManifest"/> class.
    /// Sets creation and modification times to the current UTC time.
    /// </summary>
    public IndexManifest()
    {
        DateTime now = DateTime.UtcNow;
        CreatedUtc = now;
        LastModifiedUtc = now;
    }

    /// <summary>
    /// Checks if a document has changed by comparing its content hash with the stored hash.
    /// </summary>
    /// <param name="doc">The document record to check.</param>
    /// <returns>True if the document is new or has changed; false if unchanged.</returns>
    public bool HasDocumentChanged(DocumentRecord doc)
    {
        if (!DocumentHashes.TryGetValue(doc.DocId, out string? storedHash))
            return true; // New document

        return storedHash != doc.ContentHash;
    }

    /// <summary>
    /// Updates or adds a document's content hash in the manifest.
    /// </summary>
    /// <param name="doc">The document record to update.</param>
    public void UpdateDocument(DocumentRecord doc)
    {
        DocumentHashes[doc.DocId] = doc.ContentHash;
        LastModifiedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes a document from the manifest.
    /// </summary>
    /// <param name="docId">The document ID to remove.</param>
    public void RemoveDocument(string docId)
    {
        if (DocumentHashes.Remove(docId))
        {
            LastModifiedUtc = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Saves the manifest to a JSON file.
    /// </summary>
    /// <param name="path">The file path to save to.</param>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    public void SaveToFile(string path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        try
        {
            string directory = Path.GetDirectoryName(path) ?? string.Empty;
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(path, json);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to save manifest to '{path}': {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Loads a manifest from a JSON file.
    /// </summary>
    /// <param name="path">The file path to load from.</param>
    /// <returns>The loaded manifest.</returns>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be read or contains invalid JSON.</exception>
    public static IndexManifest LoadFromFile(string path)
    {
        if (path == null)
            throw new ArgumentNullException(nameof(path));

        if (!File.Exists(path))
            throw new FileNotFoundException($"Manifest file not found: {path}");

        try
        {
            string json = File.ReadAllText(path);
            IndexManifest? manifest = JsonSerializer.Deserialize<IndexManifest>(json);

            if (manifest == null)
                throw new IOException("Failed to deserialize manifest: result was null");

            return manifest;
        }
        catch (JsonException ex)
        {
            throw new IOException($"Failed to parse manifest JSON from '{path}': {ex.Message}", ex);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to load manifest from '{path}': {ex.Message}", ex);
        }
    }
}

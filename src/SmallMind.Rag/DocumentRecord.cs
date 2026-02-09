using System;

namespace SmallMind.Rag;

/// <summary>
/// Represents metadata for a document in the RAG index.
/// Immutable value type for performance and thread-safety.
/// </summary>
internal readonly struct DocumentRecord
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentRecord"/> struct.
    /// </summary>
    /// <param name="docId">The stable document identifier based on path and content hash.</param>
    /// <param name="title">The document title (typically the filename).</param>
    /// <param name="sourceUri">The full path or URI to the source document.</param>
    /// <param name="lastWriteTimeUtc">The UTC timestamp of the last write to the document.</param>
    /// <param name="contentHash">The SHA256 hash of the document content as a hex string.</param>
    /// <param name="sizeBytes">The size of the document in bytes.</param>
    /// <param name="version">The document version number.</param>
    public DocumentRecord(
        string docId,
        string title,
        string sourceUri,
        DateTime lastWriteTimeUtc,
        string contentHash,
        long sizeBytes,
        int version = 1)
    {
        DocId = docId ?? throw new ArgumentNullException(nameof(docId));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        SourceUri = sourceUri ?? throw new ArgumentNullException(nameof(sourceUri));
        LastWriteTimeUtc = lastWriteTimeUtc;
        ContentHash = contentHash ?? throw new ArgumentNullException(nameof(contentHash));
        SizeBytes = sizeBytes;
        Version = version;
    }

    /// <summary>
    /// Gets the stable document identifier based on path and content hash.
    /// </summary>
    public string DocId { get; }

    /// <summary>
    /// Gets the document title (typically the filename).
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the full path or URI to the source document.
    /// </summary>
    public string SourceUri { get; }

    /// <summary>
    /// Gets the UTC timestamp of the last write to the document.
    /// </summary>
    public DateTime LastWriteTimeUtc { get; }

    /// <summary>
    /// Gets the SHA256 hash of the document content as a hex string.
    /// </summary>
    public string ContentHash { get; }

    /// <summary>
    /// Gets the size of the document in bytes.
    /// </summary>
    public long SizeBytes { get; }

    /// <summary>
    /// Gets the document version number.
    /// </summary>
    public int Version { get; }
}

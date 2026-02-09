using System;
using System.Security.Cryptography;
using System.Text;
using SmallMind.Rag.Common;

namespace SmallMind.Rag;

/// <summary>
/// Represents a text chunk extracted from a document for indexing and retrieval.
/// </summary>
internal sealed class Chunk
{
    /// <summary>
    /// Gets or sets the stable chunk identifier computed from document ID, text, and offsets.
    /// </summary>
    public string ChunkId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the parent document identifier.
    /// </summary>
    public string DocId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the source URI of the parent document.
    /// </summary>
    public string SourceUri { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the title of the parent document.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the text content of this chunk.
    /// </summary>
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the starting character offset in the original document.
    /// </summary>
    public int CharStart { get; set; }

    /// <summary>
    /// Gets or sets the ending character offset in the original document.
    /// </summary>
    public int CharEnd { get; set; }

    /// <summary>
    /// Gets or sets the security label for access control (empty if security is disabled).
    /// </summary>
    public string SecurityLabel { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the array of tags associated with this chunk.
    /// </summary>
    public string[] Tags { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Gets or sets the UTC timestamp when this chunk was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the chunk version number.
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Computes a stable chunk identifier from document ID, normalized text, and character offsets.
    /// Uses SHA256 hash to ensure uniqueness and stability across runs.
    /// </summary>
    /// <param name="docId">The parent document identifier.</param>
    /// <param name="text">The chunk text content.</param>
    /// <param name="charStart">The starting character offset.</param>
    /// <param name="charEnd">The ending character offset.</param>
    /// <returns>A hex string representation of the SHA256 hash.</returns>
    public static string ComputeChunkId(string docId, string text, int charStart, int charEnd)
    {
        if (docId == null) throw new ArgumentNullException(nameof(docId));
        if (text == null) throw new ArgumentNullException(nameof(text));

        // Normalize text: trim and collapse whitespace for stable hashing
        string normalized = TextHelper.NormalizeWhitespace(text);

        // Combine components into a stable string
        string combined = $"{docId}|{normalized}|{charStart}|{charEnd}";

        // Compute SHA256 hash
        Span<byte> hashBytes = stackalloc byte[32]; // SHA256 produces 32 bytes
        SHA256.HashData(Encoding.UTF8.GetBytes(combined), hashBytes);

        // Convert to hex string without allocations
        return Convert.ToHexString(hashBytes);
    }

}

using System;

namespace SmallMind.Rag;

/// <summary>
/// Configuration options for the RAG (Retrieval-Augmented Generation) system.
/// </summary>
public sealed class RagOptions
{
    /// <summary>
    /// Gets or sets the directory path where the index is stored.
    /// </summary>
    public string IndexDirectory { get; set; } = "./index";

    /// <summary>
    /// Gets or sets the chunking configuration options.
    /// </summary>
    public ChunkingOptions Chunking { get; set; } = new();

    /// <summary>
    /// Gets or sets the retrieval configuration options.
    /// </summary>
    public RetrievalOptions Retrieval { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether security features (labels, access control) are enabled.
    /// </summary>
    public bool SecurityEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether telemetry and diagnostics are enabled.
    /// </summary>
    public bool TelemetryEnabled { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether operations should be deterministic (for reproducibility).
    /// </summary>
    public bool Deterministic { get; set; } = true;

    /// <summary>
    /// Gets or sets the random seed for deterministic operations. If null, a random seed is used.
    /// </summary>
    public int? Seed { get; set; } = null;

    /// <summary>
    /// Configuration options for text chunking.
    /// </summary>
    public sealed class ChunkingOptions
    {
        /// <summary>
        /// Gets or sets the maximum size of each chunk in characters.
        /// </summary>
        public int MaxChunkSize { get; set; } = 512;

        /// <summary>
        /// Gets or sets the number of overlapping characters between consecutive chunks.
        /// </summary>
        public int OverlapSize { get; set; } = 64;

        /// <summary>
        /// Gets or sets the minimum size of a chunk in characters. Smaller chunks are discarded.
        /// </summary>
        public int MinChunkSize { get; set; } = 32;
    }

    /// <summary>
    /// Configuration options for retrieval.
    /// </summary>
    public sealed class RetrievalOptions
    {
        /// <summary>
        /// Gets or sets the maximum number of chunks to retrieve per query.
        /// </summary>
        public int TopK { get; set; } = 5;

        /// <summary>
        /// Gets or sets the minimum similarity score threshold (0.0 to 1.0). Chunks below this score are filtered.
        /// </summary>
        public float MinScore { get; set; } = 0.0f;

        /// <summary>
        /// Gets or sets a value indicating whether to re-rank results after initial retrieval.
        /// </summary>
        public bool EnableReranking { get; set; } = false;
    }
}

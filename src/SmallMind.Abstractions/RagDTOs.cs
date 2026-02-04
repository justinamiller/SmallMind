using System;

namespace SmallMind.Abstractions
{
    /// <summary>
    /// Request for building a RAG index.
    /// </summary>
    public sealed class RagBuildRequest
    {
        /// <summary>
        /// Gets or sets the directory or file paths to ingest.
        /// </summary>
        public string[] SourcePaths { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Gets or sets the index directory where the index will be saved.
        /// </summary>
        public string IndexDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the chunk size in characters.
        /// Default: 512.
        /// </summary>
        public int ChunkSize { get; set; } = 512;

        /// <summary>
        /// Gets or sets the chunk overlap in characters.
        /// Default: 128.
        /// </summary>
        public int ChunkOverlap { get; set; } = 128;

        /// <summary>
        /// Gets or sets whether to use dense (vector) retrieval.
        /// If false, uses sparse (BM25) retrieval only.
        /// Default: false.
        /// </summary>
        public bool UseDenseRetrieval { get; set; }
    }

    /// <summary>
    /// Request for asking a question with RAG.
    /// </summary>
    public sealed class RagAskRequest
    {
        /// <summary>
        /// Gets or sets the query/question.
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the RAG index to search.
        /// </summary>
        public IRagIndex Index { get; set; } = null!;

        /// <summary>
        /// Gets or sets the number of chunks to retrieve.
        /// Default: 5.
        /// </summary>
        public int TopK { get; set; } = 5;

        /// <summary>
        /// Gets or sets the minimum confidence threshold (0.0 to 1.0).
        /// Chunks below this threshold are filtered out.
        /// Default: 0.0 (no filtering).
        /// </summary>
        public double MinConfidence { get; set; }

        /// <summary>
        /// Gets or sets the generation options for the answer.
        /// </summary>
        public GenerationOptions GenerationOptions { get; set; } = new GenerationOptions();
    }

    /// <summary>
    /// Answer from a RAG query.
    /// </summary>
    public sealed class RagAnswer
    {
        /// <summary>
        /// Gets the generated answer text.
        /// </summary>
        public string Answer { get; init; } = string.Empty;

        /// <summary>
        /// Gets the citations supporting the answer.
        /// </summary>
        public RagCitation[] Citations { get; init; } = Array.Empty<RagCitation>();

        /// <summary>
        /// Gets the number of tokens generated.
        /// </summary>
        public int GeneratedTokens { get; init; }

        /// <summary>
        /// Gets whether generation was stopped by a budget limit.
        /// </summary>
        public bool StoppedByBudget { get; init; }
    }

    /// <summary>
    /// A citation from a source document.
    /// </summary>
    public readonly struct RagCitation : IEquatable<RagCitation>
    {
        /// <summary>
        /// Gets the source URI or path.
        /// </summary>
        public readonly string SourceUri;

        /// <summary>
        /// Gets the character range in the source (start, end).
        /// </summary>
        public readonly (int Start, int End) CharRange;

        /// <summary>
        /// Gets the line range in the source (start, end).
        /// </summary>
        public readonly (int Start, int End)? LineRange;

        /// <summary>
        /// Gets a snippet of the cited text.
        /// </summary>
        public readonly string Snippet;

        /// <summary>
        /// Gets the confidence score (0.0 to 1.0).
        /// </summary>
        public readonly double Confidence;

        /// <summary>
        /// Initializes a new instance of the RagCitation struct.
        /// </summary>
        [System.Text.Json.Serialization.JsonConstructor]
        public RagCitation(
            string sourceUri,
            (int Start, int End) charRange,
            (int Start, int End)? lineRange,
            string snippet,
            double confidence)
        {
            SourceUri = sourceUri ?? string.Empty;
            CharRange = charRange;
            LineRange = lineRange;
            Snippet = snippet ?? string.Empty;
            Confidence = confidence;
        }

        public bool Equals(RagCitation other) =>
            SourceUri == other.SourceUri &&
            CharRange == other.CharRange &&
            LineRange == other.LineRange &&
            Snippet == other.Snippet &&
            Math.Abs(Confidence - other.Confidence) < 1e-9;

        public override bool Equals(object? obj) =>
            obj is RagCitation other && Equals(other);

        public override int GetHashCode() =>
            HashCode.Combine(SourceUri, CharRange, LineRange, Snippet, Confidence);

        public static bool operator ==(RagCitation left, RagCitation right) => left.Equals(right);
        public static bool operator !=(RagCitation left, RagCitation right) => !left.Equals(right);
    }

    /// <summary>
    /// Information about a RAG index.
    /// </summary>
    public sealed class RagIndexInfo
    {
        /// <summary>
        /// Gets the index directory.
        /// </summary>
        public string IndexDirectory { get; init; } = string.Empty;

        /// <summary>
        /// Gets the number of chunks in the index.
        /// </summary>
        public int ChunkCount { get; init; }

        /// <summary>
        /// Gets the number of unique source documents.
        /// </summary>
        public int DocumentCount { get; init; }

        /// <summary>
        /// Gets whether the index uses dense (vector) retrieval.
        /// </summary>
        public bool UsesDenseRetrieval { get; init; }

        /// <summary>
        /// Gets when the index was created.
        /// </summary>
        public DateTimeOffset CreatedAt { get; init; }
    }
}

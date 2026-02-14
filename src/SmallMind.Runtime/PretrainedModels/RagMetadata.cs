using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// RAG-specific metadata.
    /// </summary>
    internal class RagMetadata
    {
        /// <summary>
        /// Whether RAG is enabled for this pack.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        /// <summary>
        /// Number of documents in the RAG corpus.
        /// </summary>
        [JsonPropertyName("document_count")]
        public int DocumentCount { get; set; }

        /// <summary>
        /// Type of index used (e.g., "semantic").
        /// </summary>
        [JsonPropertyName("index_type")]
        public string IndexType { get; set; } = string.Empty;
    }
}

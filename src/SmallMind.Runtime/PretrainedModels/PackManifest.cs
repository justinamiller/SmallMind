using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Represents metadata for a pretrained data pack.
    /// </summary>
    internal class PackManifest
    {
        /// <summary>
        /// Unique identifier for this pack (e.g., "sm.pretrained.sentiment.v1").
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Pack type (e.g., "task-pack").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Domain of the pack (e.g., "sentiment", "finance").
        /// </summary>
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Intended use cases for this pack.
        /// </summary>
        [JsonPropertyName("intended_use")]
        public List<string> IntendedUse { get; set; } = new();

        /// <summary>
        /// Supported languages.
        /// </summary>
        [JsonPropertyName("language")]
        public List<string> Language { get; set; } = new();

        /// <summary>
        /// Source information.
        /// </summary>
        [JsonPropertyName("source")]
        public PackSource Source { get; set; } = new();

        /// <summary>
        /// Recommended settings for using this pack.
        /// </summary>
        [JsonPropertyName("recommended_settings")]
        public RecommendedSettings RecommendedSettings { get; set; } = new();

        /// <summary>
        /// Task-specific metadata.
        /// </summary>
        [JsonPropertyName("task")]
        public TaskMetadata? Task { get; set; }

        /// <summary>
        /// Statistical information about the pack.
        /// </summary>
        [JsonPropertyName("statistics")]
        public PackStatistics? Statistics { get; set; }

        /// <summary>
        /// RAG-specific metadata (if applicable).
        /// </summary>
        [JsonPropertyName("rag")]
        public RagMetadata? Rag { get; set; }
    }

    /// <summary>
    /// Source information for a pack.
    /// </summary>
    internal class PackSource
    {
        /// <summary>
        /// Origin of the data (e.g., "synthetic", "public-domain").
        /// </summary>
        [JsonPropertyName("origin")]
        public string Origin { get; set; } = string.Empty;

        /// <summary>
        /// License type (e.g., "MIT").
        /// </summary>
        [JsonPropertyName("license")]
        public string License { get; set; } = string.Empty;

        /// <summary>
        /// Additional notes about the source.
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Recommended settings for using a pack.
    /// </summary>
    internal class RecommendedSettings
    {
        /// <summary>
        /// Recommended context token length.
        /// </summary>
        [JsonPropertyName("context_tokens")]
        public int ContextTokens { get; set; }

        /// <summary>
        /// Whether deterministic mode is recommended.
        /// </summary>
        [JsonPropertyName("deterministic")]
        public bool Deterministic { get; set; }
    }

    /// <summary>
    /// Task-specific metadata.
    /// </summary>
    internal class TaskMetadata
    {
        /// <summary>
        /// Task type (e.g., "sentiment_analysis", "text_classification").
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Labels used in this task.
        /// </summary>
        [JsonPropertyName("labels")]
        public List<string> Labels { get; set; } = new();

        /// <summary>
        /// Description of the task.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Statistical information about a pack.
    /// </summary>
    internal class PackStatistics
    {
        /// <summary>
        /// Total number of samples.
        /// </summary>
        [JsonPropertyName("total_samples")]
        public int TotalSamples { get; set; }

        /// <summary>
        /// Distribution of labels.
        /// </summary>
        [JsonPropertyName("label_distribution")]
        public Dictionary<string, int> LabelDistribution { get; set; } = new();
    }

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

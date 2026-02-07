using System;
using System.Collections.Generic;
using SmallMind.Abstractions;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Configuration for transformer models, particularly Llama-based architectures.
    /// </summary>
    public sealed class ModelConfig
    {
        /// <summary>
        /// Model architecture name (e.g., "llama", "gpt2").
        /// </summary>
        public string Architecture { get; set; } = "llama";

        /// <summary>
        /// Vocabulary size (number of unique tokens).
        /// </summary>
        public int VocabSize { get; set; }

        /// <summary>
        /// Context length (maximum sequence length).
        /// </summary>
        public int ContextLength { get; set; }

        /// <summary>
        /// Embedding dimension (model width).
        /// </summary>
        public int EmbeddingLength { get; set; }

        /// <summary>
        /// Number of transformer blocks/layers.
        /// </summary>
        public int BlockCount { get; set; }

        /// <summary>
        /// Number of attention heads per layer.
        /// </summary>
        public int HeadCount { get; set; }

        /// <summary>
        /// Number of KV attention heads per layer (for GQA).
        /// If equal to HeadCount, uses standard multi-head attention.
        /// If less than HeadCount, uses grouped-query attention.
        /// </summary>
        public int HeadCountKv { get; set; }

        /// <summary>
        /// Feed-forward hidden dimension (typically 4x EmbeddingLength for standard, ~2.66x for SwiGLU).
        /// </summary>
        public int FeedForwardLength { get; set; }

        /// <summary>
        /// RoPE frequency base (theta).
        /// </summary>
        public double RopeFreqBase { get; set; } = 10000.0;

        /// <summary>
        /// RMSNorm epsilon value.
        /// </summary>
        public double RmsNormEps { get; set; } = 1e-5;

        /// <summary>
        /// Whether to use RoPE (Rotary Position Embeddings).
        /// </summary>
        public bool UseRope { get; set; } = true;

        /// <summary>
        /// Whether to use RMSNorm instead of LayerNorm.
        /// </summary>
        public bool UseRmsNorm { get; set; } = true;

        /// <summary>
        /// Whether to use SwiGLU activation (gated MLP) instead of standard MLP.
        /// </summary>
        public bool UseSwiGlu { get; set; } = true;

        /// <summary>
        /// Dropout rate (typically 0.0 for inference).
        /// </summary>
        public double Dropout { get; set; } = 0.0;

        /// <summary>
        /// Creates a ModelConfig from GGUF metadata.
        /// </summary>
        public static ModelConfig FromGgufMetadata(Dictionary<string, object> metadata)
        {
            if (metadata == null || metadata.Count == 0)
                throw new ArgumentException("Metadata cannot be null or empty", nameof(metadata));

            var config = new ModelConfig();

            // Extract architecture
            if (metadata.TryGetValue("general.architecture", out var archObj))
            {
                config.Architecture = archObj?.ToString() ?? "llama";
            }

            // Validate architecture
            if (config.Architecture != "llama")
            {
                throw new UnsupportedModelException(
                    "",
                    config.Architecture,
                    $"Unsupported architecture: {config.Architecture}. Only 'llama' architecture is currently supported.");
            }

            // Extract Llama-specific parameters
            config.VocabSize = ExtractInt(metadata, "llama.vocab_size") 
                ?? throw new MissingMetadataException("llama.vocab_size");

            config.ContextLength = ExtractInt(metadata, "llama.context_length")
                ?? throw new MissingMetadataException("llama.context_length");

            config.EmbeddingLength = ExtractInt(metadata, "llama.embedding_length")
                ?? throw new MissingMetadataException("llama.embedding_length");

            config.BlockCount = ExtractInt(metadata, "llama.block_count")
                ?? throw new MissingMetadataException("llama.block_count");

            config.HeadCount = ExtractInt(metadata, "llama.attention.head_count")
                ?? throw new MissingMetadataException("llama.attention.head_count");

            // Head count KV (for GQA) - defaults to HeadCount if not specified
            config.HeadCountKv = ExtractInt(metadata, "llama.attention.head_count_kv")
                ?? config.HeadCount;

            config.FeedForwardLength = ExtractInt(metadata, "llama.feed_forward_length")
                ?? (config.EmbeddingLength * 4); // Default to 4x if not specified

            // RoPE frequency base
            config.RopeFreqBase = ExtractDouble(metadata, "llama.rope.freq_base")
                ?? 10000.0;

            // RMSNorm epsilon
            config.RmsNormEps = ExtractDouble(metadata, "llama.attention.layer_norm_rms_epsilon")
                ?? 1e-5;

            // Llama always uses RoPE, RMSNorm, and SwiGLU
            config.UseRope = true;
            config.UseRmsNorm = true;
            config.UseSwiGlu = true;

            // Validate configuration
            ValidateConfig(config);

            return config;
        }

        private static int? ExtractInt(Dictionary<string, object> metadata, string key)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                if (value is int intVal)
                    return intVal;
                if (value is uint uintVal)
                    return (int)uintVal;
                if (value is long longVal)
                    return (int)longVal;
                if (value is ulong ulongVal)
                    return (int)ulongVal;
            }
            return null;
        }

        private static double? ExtractDouble(Dictionary<string, object> metadata, string key)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                if (value is double dblVal)
                    return dblVal;
                if (value is float fltVal)
                    return fltVal;
                if (value is int intVal)
                    return intVal;
            }
            return null;
        }

        private static void ValidateConfig(ModelConfig config)
        {
            if (config.VocabSize <= 0)
                throw new ArgumentException($"Invalid vocab size: {config.VocabSize}");

            if (config.EmbeddingLength <= 0)
                throw new ArgumentException($"Invalid embedding length: {config.EmbeddingLength}");

            if (config.BlockCount <= 0)
                throw new ArgumentException($"Invalid block count: {config.BlockCount}");

            if (config.HeadCount <= 0)
                throw new ArgumentException($"Invalid head count: {config.HeadCount}");

            if (config.HeadCountKv <= 0 || config.HeadCountKv > config.HeadCount)
                throw new ArgumentException(
                    $"Invalid KV head count: {config.HeadCountKv}. Must be between 1 and {config.HeadCount}");

            if (config.EmbeddingLength % config.HeadCount != 0)
                throw new ArgumentException(
                    $"Embedding length {config.EmbeddingLength} must be divisible by head count {config.HeadCount}");

            if (config.HeadCount % config.HeadCountKv != 0)
                throw new ArgumentException(
                    $"Head count {config.HeadCount} must be divisible by KV head count {config.HeadCountKv}");
        }

        /// <summary>
        /// Creates a ModelConfig for GPT-style models (backward compatibility).
        /// </summary>
        public static ModelConfig ForGpt(int vocabSize, int contextLength, int embeddingLength, int blockCount, int headCount, double dropout = 0.0)
        {
            return new ModelConfig
            {
                Architecture = "gpt2",
                VocabSize = vocabSize,
                ContextLength = contextLength,
                EmbeddingLength = embeddingLength,
                BlockCount = blockCount,
                HeadCount = headCount,
                HeadCountKv = headCount, // Standard MHA
                FeedForwardLength = embeddingLength * 4,
                UseRope = false,
                UseRmsNorm = false,
                UseSwiGlu = false,
                Dropout = dropout
            };
        }
    }

    /// <summary>
    /// Exception thrown when required metadata is missing from GGUF file.
    /// </summary>
    public class MissingMetadataException : Exception
    {
        public string MetadataKey { get; }

        public MissingMetadataException(string metadataKey)
            : base($"Required metadata key not found: {metadataKey}")
        {
            MetadataKey = metadataKey;
        }
    }
}

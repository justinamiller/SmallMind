using SmallMind.Abstractions;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Unified configuration for transformer models supporting multiple architectures.
    /// Supports: Llama, Mistral, Phi, GPT-2.
    /// </summary>
    internal sealed class ModelConfig
    {
        /// <summary>
        /// Model architecture name (e.g., "llama", "mistral", "phi3", "gpt2").
        /// </summary>
        public string Architecture { get; set; } = "llama";

        /// <summary>
        /// Model name (optional, for documentation).
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Vocabulary size (number of unique tokens).
        /// </summary>
        public int VocabSize { get; set; }

        /// <summary>
        /// Maximum sequence length (context window).
        /// </summary>
        public int ContextLength { get; set; }

        /// <summary>
        /// Embedding dimension (model width / hidden size).
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
        /// Feed-forward intermediate dimension (typically 4x EmbeddingLength for standard, ~2.66x for SwiGLU).
        /// </summary>
        public int FeedForwardLength { get; set; }

        /// <summary>
        /// Head dimension (computed as EmbeddingLength / HeadCount).
        /// </summary>
        public int HeadDim => EmbeddingLength / HeadCount;

        /// <summary>
        /// RoPE frequency base (theta). Default is 10000.0 for Llama.
        /// </summary>
        public double RopeFreqBase { get; set; } = 10000.0;

        /// <summary>
        /// RoPE scaling type (null, "linear", "yarn", etc.). Null means no scaling.
        /// </summary>
        public string? RopeScalingType { get; set; }

        /// <summary>
        /// RoPE scaling factor (for extending context length). 1.0 means no scaling.
        /// </summary>
        public double RopeScalingFactor { get; set; } = 1.0;

        /// <summary>
        /// Normalization epsilon value (for RMSNorm or LayerNorm).
        /// </summary>
        public double NormEps { get; set; } = 1e-5;

        /// <summary>
        /// Normalization type: "rms" for RMSNorm, "layer" for LayerNorm.
        /// </summary>
        public string NormType { get; set; } = "rms";

        /// <summary>
        /// Whether to use RoPE (Rotary Position Embeddings). False means learned positional embeddings.
        /// </summary>
        public bool UseRope { get; set; } = true;

        /// <summary>
        /// MLP activation type: "gelu", "swiglu", "geglu".
        /// </summary>
        public string MlpType { get; set; } = "swiglu";

        /// <summary>
        /// Whether to use bias in linear layers.
        /// </summary>
        public bool UseBias { get; set; } = false;

        /// <summary>
        /// BOS (beginning-of-sequence) token ID.
        /// </summary>
        public int BosTokenId { get; set; } = -1;

        /// <summary>
        /// EOS (end-of-sequence) token ID.
        /// </summary>
        public int EosTokenId { get; set; } = -1;

        /// <summary>
        /// PAD (padding) token ID.
        /// </summary>
        public int PadTokenId { get; set; } = -1;

        /// <summary>
        /// Mistral: Sliding window attention size (0 = disabled).
        /// </summary>
        public int SlidingWindowSize { get; set; } = 0;

        /// <summary>
        /// Dropout rate (typically 0.0 for inference).
        /// </summary>
        public double Dropout { get; set; } = 0.0;

        /// <summary>
        /// Convenience property: whether to use RMSNorm (derived from NormType).
        /// </summary>
        public bool UseRmsNorm => NormType == "rms";

        /// <summary>
        /// Convenience property: whether to use SwiGLU (derived from MlpType).
        /// </summary>
        public bool UseSwiGlu => MlpType == "swiglu";

        /// <summary>
        /// Creates a ModelConfig from GGUF metadata.
        /// Supports: llama, mistral, phi3 (and compatible architectures).
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

            // Normalize architecture name (mistral and phi use llama format)
            var archPrefix = config.Architecture.ToLowerInvariant();

            // Mistral and Phi variants use the same keys as Llama
            if (archPrefix.StartsWith("mistral") || archPrefix.StartsWith("phi"))
            {
                archPrefix = "llama";
            }

            // Validate architecture
            var supportedArchs = new[] { "llama", "gpt2" };
            if (!supportedArchs.Contains(archPrefix) && !config.Architecture.StartsWith("mistral") && !config.Architecture.StartsWith("phi"))
            {
                throw new UnsupportedModelException(
                    "gguf-metadata",
                    config.Architecture,
                    $"Unsupported architecture: {config.Architecture}. Supported: llama, mistral, phi, gpt2");
            }

            // Extract general metadata
            config.Name = GetMetadataValue(metadata, "general.name", null);

            // Extract architecture-specific parameters using archPrefix for key lookups
            // Note: ?? operator ensures InferVocabSizeFromTokenizer is only called if ExtractInt returns null
            config.VocabSize = ExtractInt(metadata, $"{archPrefix}.vocab_size")
                ?? InferVocabSizeFromTokenizer(metadata)
                ?? throw new MissingMetadataException($"{archPrefix}.vocab_size");

            config.ContextLength = ExtractInt(metadata, $"{archPrefix}.context_length")
                ?? throw new MissingMetadataException($"{archPrefix}.context_length");

            config.EmbeddingLength = ExtractInt(metadata, $"{archPrefix}.embedding_length")
                ?? throw new MissingMetadataException($"{archPrefix}.embedding_length");

            config.BlockCount = ExtractInt(metadata, $"{archPrefix}.block_count")
                ?? throw new MissingMetadataException($"{archPrefix}.block_count");

            config.HeadCount = ExtractInt(metadata, $"{archPrefix}.attention.head_count")
                ?? throw new MissingMetadataException($"{archPrefix}.attention.head_count");

            // Head count KV (for GQA) - defaults to HeadCount if not specified
            config.HeadCountKv = ExtractInt(metadata, $"{archPrefix}.attention.head_count_kv")
                ?? config.HeadCount;

            config.FeedForwardLength = ExtractInt(metadata, $"{archPrefix}.feed_forward_length")
                ?? (config.EmbeddingLength * 4); // Default to 4x if not specified

            // RoPE configuration
            config.RopeFreqBase = ExtractDouble(metadata, $"{archPrefix}.rope.freq_base")
                ?? 10000.0;

            config.RopeScalingType = GetMetadataValue(metadata, $"{archPrefix}.rope.scaling.type", null);
            config.RopeScalingFactor = ExtractDouble(metadata, $"{archPrefix}.rope.scaling.factor")
                ?? 1.0;

            // Normalization epsilon
            config.NormEps = ExtractDouble(metadata, $"{archPrefix}.attention.layer_norm_rms_epsilon")
                ?? ExtractDouble(metadata, $"{archPrefix}.attention.layer_norm_epsilon")
                ?? 1e-5;

            // Special token IDs
            config.BosTokenId = ExtractInt(metadata, "tokenizer.ggml.bos_token_id") ?? -1;
            config.EosTokenId = ExtractInt(metadata, "tokenizer.ggml.eos_token_id") ?? -1;
            config.PadTokenId = ExtractInt(metadata, "tokenizer.ggml.padding_token_id") ?? -1;

            // Mistral-specific: sliding window attention
            if (config.Architecture.StartsWith("mistral"))
            {
                config.SlidingWindowSize = ExtractInt(metadata, $"{archPrefix}.attention.sliding_window") ?? 0;
            }

            // Set architecture defaults
            if (archPrefix == "llama" || config.Architecture.StartsWith("mistral") || config.Architecture.StartsWith("phi"))
            {
                config.UseRope = true;
                config.NormType = "rms";
                config.MlpType = "swiglu";
                config.UseBias = false;
            }
            else if (archPrefix == "gpt2")
            {
                config.UseRope = false;
                config.NormType = "layer";
                config.MlpType = "gelu";
                config.UseBias = true;
            }

            // Validate configuration
            ValidateConfig(config);

            return config;
        }

        private static string? GetMetadataValue(Dictionary<string, object> metadata, string key, string? defaultValue)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                return value?.ToString() ?? defaultValue;
            }
            return defaultValue;
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

        private static int? InferVocabSizeFromTokenizer(Dictionary<string, object> metadata)
        {
            if (metadata.TryGetValue("tokenizer.ggml.tokens", out var tokensObj) && tokensObj != null)
            {
                // Handle different possible types for tokens array
                if (tokensObj is object[] objArray)
                {
                    return objArray.Length;
                }
                else if (tokensObj is string[] strArray)
                {
                    return strArray.Length;
                }
                else if (tokensObj is System.Collections.ICollection collection)
                {
                    return collection.Count;
                }
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
                NormType = "layer",
                MlpType = "gelu",
                UseBias = true,
                Dropout = dropout
            };
        }

        /// <summary>
        /// Creates a preset ModelConfig for Llama-style architecture.
        /// </summary>
        public static ModelConfig ForLlama(int vocabSize, int contextLength, int embeddingLength, int blockCount, int headCount, int? headCountKv = null)
        {
            return new ModelConfig
            {
                Architecture = "llama",
                VocabSize = vocabSize,
                ContextLength = contextLength,
                EmbeddingLength = embeddingLength,
                BlockCount = blockCount,
                HeadCount = headCount,
                HeadCountKv = headCountKv ?? headCount,
                FeedForwardLength = (int)(embeddingLength * 2.66667 * 2), // ~2.66x * 2 for up and gate projections
                UseRope = true,
                NormType = "rms",
                MlpType = "swiglu",
                UseBias = false,
                RopeFreqBase = 10000.0,
                NormEps = 1e-5
            };
        }

        /// <summary>
        /// Creates a preset ModelConfig for Mistral architecture.
        /// </summary>
        public static ModelConfig ForMistral(int vocabSize, int contextLength, int embeddingLength, int blockCount, int headCount, int headCountKv, int slidingWindow = 4096)
        {
            return new ModelConfig
            {
                Architecture = "mistral",
                VocabSize = vocabSize,
                ContextLength = contextLength,
                EmbeddingLength = embeddingLength,
                BlockCount = blockCount,
                HeadCount = headCount,
                HeadCountKv = headCountKv,
                FeedForwardLength = (int)(embeddingLength * 2.66667 * 2),
                UseRope = true,
                NormType = "rms",
                MlpType = "swiglu",
                UseBias = false,
                RopeFreqBase = 10000.0,
                NormEps = 1e-5,
                SlidingWindowSize = slidingWindow
            };
        }

        /// <summary>
        /// Creates a preset ModelConfig for Phi-3 architecture.
        /// </summary>
        public static ModelConfig ForPhi3(int vocabSize, int contextLength, int embeddingLength, int blockCount, int headCount, int headCountKv)
        {
            return new ModelConfig
            {
                Architecture = "phi3",
                VocabSize = vocabSize,
                ContextLength = contextLength,
                EmbeddingLength = embeddingLength,
                BlockCount = blockCount,
                HeadCount = headCount,
                HeadCountKv = headCountKv,
                FeedForwardLength = (int)(embeddingLength * 2.66667 * 2),
                UseRope = true,
                NormType = "rms",
                MlpType = "swiglu",
                UseBias = false,
                RopeFreqBase = 10000.0,
                NormEps = 1e-5
            };
        }
    }

    /// <summary>
    /// Exception thrown when required metadata is missing from GGUF file.
    /// </summary>
    internal class MissingMetadataException : Exception
    {
        public string MetadataKey { get; }

        public MissingMetadataException(string metadataKey)
            : base($"Required metadata key not found: {metadataKey}")
        {
            MetadataKey = metadataKey;
        }
    }
}

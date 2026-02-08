using System;
using System.Collections.Generic;
using System.IO;
using SmallMind.Quantization.IO.Gguf;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Loads models from GGUF format files.
    /// Extracts ModelConfig and tokenizer from GGUF metadata.
    /// Constructs TransformerModel with appropriate architecture (Llama, Mistral, Phi, GPT-2).
    /// Phase 3 implementation for maturity 3/5.
    /// </summary>
    public sealed class GgufModelLoader
    {
        /// <summary>
        /// Load a model from a GGUF file.
        /// Reads metadata, extracts config and tokenizer, builds TransformerModel.
        /// </summary>
        /// <param name="ggufPath">Path to GGUF file</param>
        /// <param name="seed">Random seed for model initialization</param>
        /// <returns>Tuple of (model, tokenizer, config)</returns>
        public static (TransformerModel model, ITokenizer tokenizer, ModelConfig config) LoadFromGguf(
            string ggufPath, 
            int seed = 42)
        {
            if (string.IsNullOrEmpty(ggufPath))
                throw new ArgumentNullException(nameof(ggufPath));
            if (!File.Exists(ggufPath))
                throw new FileNotFoundException($"GGUF file not found: {ggufPath}");

            GgufModelInfo modelInfo;
            using (var stream = File.OpenRead(ggufPath))
            using (var reader = new GgufReader(stream))
            {
                modelInfo = reader.ReadModelInfo();
            }

            // Extract ModelConfig from metadata
            var config = ModelConfig.FromGgufMetadata(modelInfo.Metadata);

            // Extract tokenizer from metadata
            var tokenizer = GgufTokenizerExtractor.ExtractTokenizer(modelInfo.Metadata);
            if (tokenizer == null)
            {
                throw new NotSupportedException(
                    "Failed to extract tokenizer from GGUF file. " +
                    "Ensure the file contains tokenizer metadata (tokenizer.ggml.*).");
            }

            // Build TransformerModel from config
            var model = BuildModelFromConfig(config, seed);

            // Note: Weights are NOT loaded yet - this would require reading tensors from GGUF
            // For now, this creates a model with random weights matching the architecture
            // Full weight loading requires tensor deserialization (Phase 3+ enhancement)

            return (model, tokenizer, config);
        }

        /// <summary>
        /// Build a TransformerModel from ModelConfig.
        /// Creates model with appropriate architecture based on config.
        /// Weights are randomly initialized (weight loading is Phase 3+ enhancement).
        /// </summary>
        private static TransformerModel BuildModelFromConfig(ModelConfig config, int seed)
        {
            // For Phase 3 initial implementation:
            // Build model structure matching the config architecture
            // Note: This uses the existing GPT-2 style constructor
            // Full Llama/Mistral architecture support requires TransformerModel enhancements

            // Validate configuration
            if (config.VocabSize <= 0 || config.ContextLength <= 0 || 
                config.EmbeddingLength <= 0 || config.BlockCount <= 0 || config.HeadCount <= 0)
            {
                throw new ArgumentException("Invalid model configuration: all dimensions must be positive");
            }

            // Create model using existing constructor
            // This creates a GPT-2 style model structure
            // TODO (Phase 3+): Enhance TransformerModel to support RoPE, RMSNorm, SwiGLU based on config
            var model = new TransformerModel(
                vocabSize: config.VocabSize,
                blockSize: config.ContextLength,
                nEmbd: config.EmbeddingLength,
                nLayer: config.BlockCount,
                nHead: config.HeadCount,
                dropout: config.Dropout,
                seed: seed
            );

            return model;
        }

        /// <summary>
        /// Load just the model configuration from a GGUF file without building the model.
        /// Useful for inspecting model metadata.
        /// </summary>
        /// <param name="ggufPath">Path to GGUF file</param>
        /// <returns>ModelConfig extracted from metadata</returns>
        public static ModelConfig LoadConfigFromGguf(string ggufPath)
        {
            if (string.IsNullOrEmpty(ggufPath))
                throw new ArgumentNullException(nameof(ggufPath));
            if (!File.Exists(ggufPath))
                throw new FileNotFoundException($"GGUF file not found: {ggufPath}");

            using var stream = File.OpenRead(ggufPath);
            using var reader = new GgufReader(stream);
            var modelInfo = reader.ReadModelInfo();

            return ModelConfig.FromGgufMetadata(modelInfo.Metadata);
        }

        /// <summary>
        /// Load just the tokenizer from a GGUF file.
        /// </summary>
        /// <param name="ggufPath">Path to GGUF file</param>
        /// <returns>Tokenizer extracted from metadata</returns>
        public static ITokenizer LoadTokenizerFromGguf(string ggufPath)
        {
            if (string.IsNullOrEmpty(ggufPath))
                throw new ArgumentNullException(nameof(ggufPath));
            if (!File.Exists(ggufPath))
                throw new FileNotFoundException($"GGUF file not found: {ggufPath}");

            using var stream = File.OpenRead(ggufPath);
            using var reader = new GgufReader(stream);
            var modelInfo = reader.ReadModelInfo();

            var tokenizer = GgufTokenizerExtractor.ExtractTokenizer(modelInfo.Metadata);
            if (tokenizer == null)
            {
                throw new NotSupportedException(
                    "Failed to extract tokenizer from GGUF file. " +
                    "Ensure the file contains tokenizer metadata.");
            }

            return tokenizer;
        }
    }
}

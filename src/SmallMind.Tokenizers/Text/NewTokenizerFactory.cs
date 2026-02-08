using System;
using System.IO;
using System.Text.Json;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Factory for creating tokenizer instances from configuration.
    /// </summary>
    public static class NewTokenizerFactory
    {
        /// <summary>
        /// Creates a tokenizer from a configuration object.
        /// </summary>
        public static ITokenizer Create(TokenizerConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            return config.Kind switch
            {
                TokenizerKind.Char => CreateCharTokenizer(config),
                TokenizerKind.ByteBpe => CreateByteLevelBpeTokenizer(config),
                TokenizerKind.Bpe => CreateBpeTokenizer(config),
                TokenizerKind.Unigram => CreateUnigramTokenizer(config),
                TokenizerKind.WordPiece => CreateWordPieceTokenizer(config),
                TokenizerKind.ByteFallback => CreateByteFallbackTokenizer(config),
                _ => throw new TokenizationException($"Unknown tokenizer kind: {config.Kind}")
            };
        }

        /// <summary>
        /// Creates a tokenizer from a JSON configuration file.
        /// </summary>
        public static ITokenizer CreateFromFile(string configPath)
        {
            if (!File.Exists(configPath))
                throw new TokenizationException($"Configuration file not found: {configPath}");

            string json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<TokenizerConfig>(json);
            
            if (config == null)
                throw new TokenizationException($"Failed to parse configuration file: {configPath}");

            return Create(config);
        }

        /// <summary>
        /// Creates a tokenizer from an environment variable.
        /// Variable should contain either a JSON config string or a path to a config file.
        /// </summary>
        public static ITokenizer? CreateFromEnvironment(string variableName = "SMALLMIND_TOKENIZER_CONFIG")
        {
            string? value = Environment.GetEnvironmentVariable(variableName);
            
            if (string.IsNullOrWhiteSpace(value))
                return null;

            // Check if it's a file path
            if (File.Exists(value))
            {
                return CreateFromFile(value);
            }

            // Try parsing as JSON
            try
            {
                var config = JsonSerializer.Deserialize<TokenizerConfig>(value);
                if (config != null)
                {
                    return Create(config);
                }
            }
            catch
            {
                // Not valid JSON, ignore
            }

            return null;
        }

        private static ITokenizer CreateCharTokenizer(TokenizerConfig config)
        {
            if (string.IsNullOrEmpty(config.TrainingText))
                throw new TokenizationException("TrainingText is required for CharTokenizer");

            return new CharTokenizer(config.TrainingText);
        }

        private static ITokenizer CreateByteLevelBpeTokenizer(TokenizerConfig config)
        {
            // Option 1: Load from saved vocabulary file
            if (!string.IsNullOrEmpty(config.VocabPath) && File.Exists(config.VocabPath))
            {
                var tokenizer = new ByteLevelBpeTokenizer();
                tokenizer.LoadVocabulary(config.VocabPath);
                return tokenizer;
            }

            // Option 2: Train from training text
            if (!string.IsNullOrEmpty(config.TrainingText))
            {
                int vocabSize = 1024; // Default
                if (config.Options != null && config.Options.TryGetValue("vocabSize", out object? vocabSizeObj))
                {
                    if (vocabSizeObj is int vs)
                        vocabSize = vs;
                    else if (vocabSizeObj is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Number)
                        vocabSize = je.GetInt32();
                }

                var tokenizer = new ByteLevelBpeTokenizer();
                tokenizer.Train(config.TrainingText, vocabSize);
                return tokenizer;
            }

            throw new TokenizationException(
                "ByteLevelBpeTokenizer requires either VocabPath (to load) or TrainingText (to train).\n" +
                "Provide one of these in the TokenizerConfig.");
        }

        private static ITokenizer CreateBpeTokenizer(TokenizerConfig config)
        {
            // Use existing BpeTokenizer which needs an assets directory
            if (string.IsNullOrEmpty(config.VocabPath))
                throw new TokenizationException("VocabPath is required for Bpe tokenizer");

            // Extract directory from VocabPath
            string? assetsDir = Path.GetDirectoryName(config.VocabPath);
            if (string.IsNullOrEmpty(assetsDir))
                throw new TokenizationException("Could not determine assets directory from VocabPath");

            return new BpeTokenizer(assetsDir);
        }

        private static ITokenizer CreateUnigramTokenizer(TokenizerConfig config)
        {
            if (string.IsNullOrEmpty(config.ModelPath))
                throw new TokenizationException("ModelPath is required for Unigram tokenizer");

            string unkToken = config.SpecialTokens?.Unk ?? "<unk>";
            return new UnigramTokenizer(config.ModelPath, unkToken, config.SpecialTokens);
        }

        private static ITokenizer CreateWordPieceTokenizer(TokenizerConfig config)
        {
            if (string.IsNullOrEmpty(config.VocabPath))
                throw new TokenizationException("VocabPath is required for WordPiece tokenizer");

            string unkToken = config.SpecialTokens?.Unk ?? "[UNK]";
            int maxChars = 200;
            
            if (config.Options != null && config.Options.TryGetValue("maxInputCharsPerWord", out object? maxObj))
            {
                if (maxObj is int maxInt)
                    maxChars = maxInt;
                else if (maxObj is JsonElement elem && elem.ValueKind == JsonValueKind.Number)
                    maxChars = elem.GetInt32();
            }

            return new WordPieceTokenizer(config.VocabPath, unkToken, maxChars, config.SpecialTokens);
        }

        private static ITokenizer CreateByteFallbackTokenizer(TokenizerConfig config)
        {
            if (config.InnerTokenizer == null)
                throw new TokenizationException("InnerTokenizer configuration is required for ByteFallback tokenizer");

            ITokenizer innerTokenizer = Create(config.InnerTokenizer);
            return new ByteFallbackTokenizer(innerTokenizer);
        }
    }
}

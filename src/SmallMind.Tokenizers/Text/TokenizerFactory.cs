using System;
using System.IO;
using SmallMind.Abstractions.Telemetry;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Factory for creating tokenizer instances based on configuration and asset availability.
    /// </summary>
    internal static class TokenizerFactory
    {
        /// <summary>
        /// Creates a tokenizer instance based on the provided options.
        /// </summary>
        /// <param name="options">Tokenizer configuration options</param>
        /// <param name="trainingText">Training text for CharTokenizer (required for Char mode or Auto fallback)</param>
        /// <param name="logger">Optional logger</param>
        /// <returns>ITokenizer instance</returns>
        public static ITokenizer Create(TokenizerOptions options, string? trainingText = null, IRuntimeLogger? logger = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            switch (options.Mode)
            {
                case TokenizerMode.Char:
                    return CreateCharTokenizer(trainingText, logger);

                case TokenizerMode.Bpe:
                    return CreateBpeTokenizer(options, trainingText, logger);

                case TokenizerMode.Auto:
                    return CreateAutoTokenizer(options, trainingText, logger);

                default:
                    throw new TokenizationException($"Unknown tokenizer mode: {options.Mode}");
            }
        }

        /// <summary>
        /// Creates a CharTokenizer instance.
        /// </summary>
        private static ITokenizer CreateCharTokenizer(string? trainingText, IRuntimeLogger? logger = null)
        {
            if (string.IsNullOrEmpty(trainingText))
            {
                throw new TokenizationException(
                    "Training text is required for CharTokenizer.\n" +
                    "Provide training text when creating a CharTokenizer or using Auto mode without BPE assets.");
            }

            return new CharTokenizer(trainingText, logger);
        }

        /// <summary>
        /// Creates a BpeTokenizer instance.
        /// </summary>
        private static ITokenizer CreateBpeTokenizer(TokenizerOptions options, string? trainingText, IRuntimeLogger? logger = null)
        {
            string? assetsPath = FindTokenizerAssets(options);

            if (assetsPath == null)
            {
                if (options.Strict)
                {
                    throw new TokenizationException(
                        $"BPE tokenizer assets not found for tokenizer '{options.TokenizerName}'.\n" +
                        $"Searched locations:\n" +
                        (options.TokenizerPath != null ? $"  - {options.TokenizerPath}\n" : "") +
                        $"  - {Path.Combine("assets", "tokenizers", options.TokenizerName)}\n" +
                        $"  - {Path.Combine(AppContext.BaseDirectory, "assets", "tokenizers", options.TokenizerName)}\n" +
                        $"\n" +
                        $"Expected files: vocab.json and merges.txt\n" +
                        $"\n" +
                        $"To fix this issue:\n" +
                        $"  1. Create the assets directory\n" +
                        $"  2. Add vocab.json (JSON object mapping tokens to IDs)\n" +
                        $"  3. Add merges.txt (one merge pair per line: 'token1 token2')\n" +
                        $"  OR\n" +
                        $"  Set TokenizerMode to Char or Auto with Strict=false to use fallback.");
                }

                // Fallback to CharTokenizer
                (logger ?? NullRuntimeLogger.Instance).Info($"BPE assets not found for '{options.TokenizerName}'. Falling back to CharTokenizer.");
                return CreateCharTokenizer(trainingText, logger);
            }

            try
            {
                return new BpeTokenizer(assetsPath, logger);
            }
            catch (TokenizationException) when (!options.Strict)
            {
                // Fallback to CharTokenizer on BPE load failure (non-strict mode)
                (logger ?? NullRuntimeLogger.Instance).Info($"Failed to load BPE tokenizer. Falling back to CharTokenizer.");
                return CreateCharTokenizer(trainingText, logger);
            }
        }

        /// <summary>
        /// Creates a tokenizer in Auto mode: tries BPE if assets exist, otherwise falls back to CharTokenizer.
        /// </summary>
        private static ITokenizer CreateAutoTokenizer(TokenizerOptions options, string? trainingText, IRuntimeLogger? logger = null)
        {
            // Try BPE first (will fallback to Char if not found since Strict is already false in Auto mode)
            return CreateBpeTokenizer(options, trainingText, logger);
        }

        /// <summary>
        /// Finds tokenizer assets directory based on options.
        /// Returns null if not found.
        /// </summary>
        private static string? FindTokenizerAssets(TokenizerOptions options)
        {
            // 1. Check explicit TokenizerPath
            if (!string.IsNullOrEmpty(options.TokenizerPath))
            {
                if (Directory.Exists(options.TokenizerPath) && 
                    HasTokenizerAssets(options.TokenizerPath))
                {
                    return options.TokenizerPath;
                }
            }

            // 2. Check ./assets/tokenizers/<name>/
            string relativeAssetsPath = Path.Combine("assets", "tokenizers", options.TokenizerName);
            if (Directory.Exists(relativeAssetsPath) && HasTokenizerAssets(relativeAssetsPath))
            {
                return Path.GetFullPath(relativeAssetsPath);
            }

            // 3. Check <AppContext.BaseDirectory>/assets/tokenizers/<name>/
            string appBaseAssetsPath = Path.Combine(
                AppContext.BaseDirectory, 
                "assets", 
                "tokenizers", 
                options.TokenizerName);
            if (Directory.Exists(appBaseAssetsPath) && HasTokenizerAssets(appBaseAssetsPath))
            {
                return appBaseAssetsPath;
            }

            return null;
        }

        /// <summary>
        /// Checks if a directory contains the required tokenizer assets (vocab.json and merges.txt).
        /// </summary>
        private static bool HasTokenizerAssets(string path)
        {
            string vocabPath = Path.Combine(path, "vocab.json");
            string mergesPath = Path.Combine(path, "merges.txt");
            return File.Exists(vocabPath) && File.Exists(mergesPath);
        }

        /// <summary>
        /// Create a character-level tokenizer from training text.
        /// </summary>
        /// <param name="trainingText">Text to build vocabulary from</param>
        /// <returns>Character-level tokenizer</returns>
        public static ITokenizer CreateCharLevel(string trainingText)
        {
            if (string.IsNullOrEmpty(trainingText))
            {
                throw new ArgumentException("Training text is required for character-level tokenizer", nameof(trainingText));
            }

            return new CharTokenizer(trainingText);
        }

        /// <summary>
        /// Create a byte-level BPE tokenizer and train it on the provided text.
        /// </summary>
        /// <param name="trainingText">Text to train BPE on</param>
        /// <param name="vocabSize">Target vocabulary size (default: 1024, min: 260)</param>
        /// <param name="minFrequency">Minimum pair frequency for merging (default: 2)</param>
        /// <returns>Trained byte-level BPE tokenizer</returns>
        public static ITokenizer CreateByteLevelBpe(string trainingText, int vocabSize = 1024, int minFrequency = 2)
        {
            if (string.IsNullOrEmpty(trainingText))
            {
                throw new ArgumentException("Training text is required for BPE tokenizer", nameof(trainingText));
            }

            if (vocabSize < 260)
            {
                throw new ArgumentException("Vocabulary size must be at least 260 (256 bytes + 4 special tokens)", nameof(vocabSize));
            }

            var tokenizer = new ByteLevelBpeTokenizer();
            tokenizer.Train(trainingText, vocabSize, minFrequency);
            return tokenizer;
        }

        /// <summary>
        /// Load a tokenizer from a saved file and auto-detect its type.
        /// </summary>
        /// <param name="path">Path to the tokenizer file</param>
        /// <returns>Loaded tokenizer instance</returns>
        public static ITokenizer Load(string path)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Tokenizer file not found: {path}");
            }

            // Try to detect tokenizer type from file content
            string content = File.ReadAllText(path);

            // Check for ByteLevelBpe format
            if (content.Contains("\"version\"") && content.Contains("smallmind-bpe-v1"))
            {
                var tokenizer = new ByteLevelBpeTokenizer();
                tokenizer.LoadVocabulary(path);
                return tokenizer;
            }

            // Add other tokenizer type detection here as needed
            
            throw new NotSupportedException(
                $"Unable to auto-detect tokenizer type from file: {path}\n" +
                "The file format is not recognized.");
        }
    }
}

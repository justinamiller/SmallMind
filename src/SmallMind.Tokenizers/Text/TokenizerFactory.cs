using System;
using System.IO;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Factory for creating tokenizer instances based on configuration and asset availability.
    /// </summary>
    public static class TokenizerFactory
    {
        /// <summary>
        /// Creates a tokenizer instance based on the provided options.
        /// </summary>
        /// <param name="options">Tokenizer configuration options</param>
        /// <param name="trainingText">Training text for CharTokenizer (required for Char mode or Auto fallback)</param>
        /// <returns>ITokenizer instance</returns>
        public static ITokenizer Create(TokenizerOptions options, string? trainingText = null)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            switch (options.Mode)
            {
                case TokenizerMode.Char:
                    return CreateCharTokenizer(trainingText);

                case TokenizerMode.Bpe:
                    return CreateBpeTokenizer(options, trainingText);

                case TokenizerMode.Auto:
                    return CreateAutoTokenizer(options, trainingText);

                default:
                    throw new TokenizationException($"Unknown tokenizer mode: {options.Mode}");
            }
        }

        /// <summary>
        /// Creates a CharTokenizer instance.
        /// </summary>
        private static ITokenizer CreateCharTokenizer(string? trainingText)
        {
            if (string.IsNullOrEmpty(trainingText))
            {
                throw new TokenizationException(
                    "Training text is required for CharTokenizer.\n" +
                    "Provide training text when creating a CharTokenizer or using Auto mode without BPE assets.");
            }

            return new CharTokenizer(trainingText);
        }

        /// <summary>
        /// Creates a BpeTokenizer instance.
        /// </summary>
        private static ITokenizer CreateBpeTokenizer(TokenizerOptions options, string? trainingText)
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
                Console.WriteLine($"BPE assets not found for '{options.TokenizerName}'. Falling back to CharTokenizer.");
                return CreateCharTokenizer(trainingText);
            }

            try
            {
                return new BpeTokenizer(assetsPath);
            }
            catch (TokenizationException) when (!options.Strict)
            {
                // Fallback to CharTokenizer on BPE load failure (non-strict mode)
                Console.WriteLine($"Failed to load BPE tokenizer. Falling back to CharTokenizer.");
                return CreateCharTokenizer(trainingText);
            }
        }

        /// <summary>
        /// Creates a tokenizer in Auto mode: tries BPE if assets exist, otherwise falls back to CharTokenizer.
        /// </summary>
        private static ITokenizer CreateAutoTokenizer(TokenizerOptions options, string? trainingText)
        {
            // Try BPE first (will fallback to Char if not found since Strict is already false in Auto mode)
            return CreateBpeTokenizer(options, trainingText);
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
    }
}

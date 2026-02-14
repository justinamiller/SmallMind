using SmallMind.Abstractions.Telemetry;

namespace SmallMind.Tokenizers.Gguf
{
    /// <summary>
    /// Factory for creating GGUF-based tokenizers.
    /// Analyzes GGUF metadata to determine the appropriate tokenizer type
    /// and provides fallback strategies when metadata is incomplete.
    /// </summary>
    internal static class GgufTokenizerFactory
    {
        /// <summary>
        /// Creates a tokenizer from GGUF metadata with diagnostic logging.
        /// </summary>
        /// <param name="metadata">GGUF metadata dictionary</param>
        /// <param name="logger">Optional logger for diagnostics</param>
        /// <returns>Configured tokenizer instance, or null if tokenizer cannot be created</returns>
        public static (ITokenizer? tokenizer, TokenizerDiagnostics diagnostics) CreateTokenizer(
            Dictionary<string, object> metadata,
            IRuntimeLogger? logger = null)
        {
            logger ??= NullRuntimeLogger.Instance;
            var diagnostics = new TokenizerDiagnostics();

            if (metadata == null || metadata.Count == 0)
            {
                diagnostics.AddIssue(RuntimeDegradeReason.TokenizerGgufMetadataMissing,
                    "GGUF metadata is null or empty");
                logger.Warn("GGUF metadata is null or empty - cannot create tokenizer");
                return (null, diagnostics);
            }

            // Check tokenizer model type
            if (!metadata.TryGetValue("tokenizer.ggml.model", out var modelObj))
            {
                diagnostics.AddIssue(RuntimeDegradeReason.TokenizerGgufMetadataMissing,
                    "tokenizer.ggml.model not found in GGUF metadata");
                logger.Warn("tokenizer.ggml.model not found in GGUF metadata");
                return (null, diagnostics);
            }

            string tokenizerModel = modelObj?.ToString() ?? "";
            logger.Info($"GGUF tokenizer model type: {tokenizerModel}");

            // Extract tokens (vocabulary)
            if (!metadata.TryGetValue("tokenizer.ggml.tokens", out var tokensObj) ||
                tokensObj is not object[] tokensArray)
            {
                diagnostics.AddIssue(RuntimeDegradeReason.TokenizerVocabPartial,
                    "tokenizer.ggml.tokens not found or invalid");
                logger.Warn("tokenizer.ggml.tokens not found or invalid in GGUF metadata");
                return (null, diagnostics);
            }

            logger.Info($"Found {tokensArray.Length} tokens in vocabulary");

            // Build vocabulary: map token string -> ID
            var vocab = new Dictionary<string, int>();
            var reverseVocab = new List<string>();
            for (int i = 0; i < tokensArray.Length; i++)
            {
                if (tokensArray[i] is string token)
                {
                    vocab[token] = i;
                    reverseVocab.Add(token);
                }
            }

            if (vocab.Count == 0)
            {
                diagnostics.AddIssue(RuntimeDegradeReason.TokenizerVocabPartial,
                    "No valid tokens found in vocabulary");
                logger.Warn("No valid tokens found in vocabulary");
                return (null, diagnostics);
            }

            // Extract merges (if available)
            var merges = new List<(string, string)>();
            bool hasMerges = false;
            if (metadata.TryGetValue("tokenizer.ggml.merges", out var mergesObj) &&
                mergesObj is object[] mergesArray && mergesArray.Length > 0)
            {
                foreach (var mergeObj in mergesArray)
                {
                    if (mergeObj is string mergeStr)
                    {
                        var parts = mergeStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 2)
                        {
                            merges.Add((parts[0], parts[1]));
                        }
                    }
                }
                hasMerges = merges.Count > 0;
                logger.Info($"Found {merges.Count} BPE merges");
            }
            else
            {
                diagnostics.AddIssue(RuntimeDegradeReason.TokenizerMergesMissing,
                    "tokenizer.ggml.merges not found or empty");
                logger.Warn("tokenizer.ggml.merges not found or empty");
            }

            // Extract special token IDs
            var specialTokens = ExtractSpecialTokens(metadata, vocab, logger);

            // Determine which tokenizer to create
            ITokenizer? tokenizer;

            if (tokenizerModel == "gpt2" || tokenizerModel == "llama")
            {
                if (hasMerges)
                {
                    // Create BPE tokenizer with merges
                    tokenizer = new GgufBpeTokenizer(vocab, reverseVocab, merges, specialTokens);
                    logger.Info("Created GGUF BPE tokenizer with merges");
                }
                else
                {
                    // Fallback to token-table-only tokenizer
                    tokenizer = new GgufTokenTableTokenizer(vocab, reverseVocab, specialTokens);
                    diagnostics.AddIssue(RuntimeDegradeReason.TokenizerFallbackTokenTableOnly,
                        "Using token-table-only tokenizer (no BPE merges available)");
                    logger.Warn("Falling back to token-table-only tokenizer (no BPE merges)");
                }
            }
            else
            {
                // Unknown tokenizer model - try token table fallback
                tokenizer = new GgufTokenTableTokenizer(vocab, reverseVocab, specialTokens);
                diagnostics.AddIssue(RuntimeDegradeReason.TokenizerFallbackTokenTableOnly,
                    $"Unknown tokenizer model '{tokenizerModel}' - using token-table fallback");
                logger.Warn($"Unknown tokenizer model '{tokenizerModel}' - using token-table fallback");
            }

            diagnostics.TokenizerType = tokenizer.GetType().Name;
            diagnostics.VocabSize = vocab.Count;
            diagnostics.HasMerges = hasMerges;
            diagnostics.MergeCount = merges.Count;

            return (tokenizer, diagnostics);
        }

        private static SpecialTokens ExtractSpecialTokens(
            Dictionary<string, object> metadata,
            Dictionary<string, int> vocab,
            IRuntimeLogger logger)
        {
            var specialTokens = new SpecialTokens();

            // Try to get special token IDs from metadata
            if (metadata.TryGetValue("tokenizer.ggml.bos_token_id", out var bosObj) &&
                bosObj is uint or int)
            {
                specialTokens.BosTokenId = Convert.ToInt32(bosObj);
            }
            else
            {
                // Try to find by name
                specialTokens.BosTokenId = FindTokenId(vocab, "<s>", "<|startoftext|>", "<bos>");
            }

            if (metadata.TryGetValue("tokenizer.ggml.eos_token_id", out var eosObj) &&
                eosObj is uint or int)
            {
                specialTokens.EosTokenId = Convert.ToInt32(eosObj);
            }
            else
            {
                specialTokens.EosTokenId = FindTokenId(vocab, "</s>", "<|endoftext|>", "<eos>", "<|im_end|>");
            }

            if (metadata.TryGetValue("tokenizer.ggml.unknown_token_id", out var unkObj) &&
                unkObj is uint or int)
            {
                specialTokens.UnkTokenId = Convert.ToInt32(unkObj);
            }
            else
            {
                specialTokens.UnkTokenId = FindTokenId(vocab, "<unk>", "[UNK]");
            }

            if (metadata.TryGetValue("tokenizer.ggml.padding_token_id", out var padObj) &&
                padObj is uint or int)
            {
                specialTokens.PadTokenId = Convert.ToInt32(padObj);
            }
            else
            {
                specialTokens.PadTokenId = FindTokenId(vocab, "<pad>", "[PAD]");
            }

            logger.Debug($"Special tokens: BOS={specialTokens.BosTokenId}, EOS={specialTokens.EosTokenId}, " +
                        $"UNK={specialTokens.UnkTokenId}, PAD={specialTokens.PadTokenId}");

            return specialTokens;
        }

        private static int FindTokenId(Dictionary<string, int> vocab, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (vocab.TryGetValue(candidate, out var id))
                {
                    return id;
                }
            }
            return -1;
        }
    }

    /// <summary>
    /// Special token IDs extracted from GGUF metadata.
    /// </summary>
    internal sealed class SpecialTokens
    {
        public int BosTokenId { get; set; } = -1;
        public int EosTokenId { get; set; } = -1;
        public int UnkTokenId { get; set; } = -1;
        public int PadTokenId { get; set; } = -1;
    }

    /// <summary>
    /// Diagnostics information about tokenizer creation.
    /// </summary>
    public sealed class TokenizerDiagnostics
    {
        public List<(RuntimeDegradeReason reason, string message)> Issues { get; } = new();
        public string? TokenizerType { get; set; }
        public int VocabSize { get; set; }
        public bool HasMerges { get; set; }
        public int MergeCount { get; set; }

        public void AddIssue(RuntimeDegradeReason reason, string message)
        {
            Issues.Add((reason, message));
        }

        public bool HasIssues => Issues.Count > 0;
    }
}

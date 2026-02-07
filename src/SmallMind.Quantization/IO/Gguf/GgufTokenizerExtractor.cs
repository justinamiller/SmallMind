using System;
using System.Collections.Generic;
using System.Linq;
using SmallMind.Tokenizers;

namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// Extracts tokenizer configuration from GGUF metadata.
    /// </summary>
    public static class GgufTokenizerExtractor
    {
        /// <summary>
        /// Extract a tokenizer from GGUF metadata.
        /// Supports GPT-2 style BPE tokenizers (used by SmolLM2, Llama, etc.).
        /// </summary>
        /// <param name="metadata">GGUF metadata dictionary</param>
        /// <returns>Configured tokenizer instance, or null if tokenizer metadata is missing</returns>
        public static ITokenizer? ExtractTokenizer(Dictionary<string, object> metadata)
        {
            if (metadata == null || metadata.Count == 0)
                return null;

            // Check tokenizer model type
            if (!metadata.TryGetValue("tokenizer.ggml.model", out var modelObj))
                return null;

            string tokenizerModel = modelObj?.ToString() ?? "";

            // Currently support BPE-based tokenizers (GPT-2 style, used by Llama/SmolLM2)
            if (tokenizerModel == "gpt2" || tokenizerModel == "llama")
            {
                return ExtractBpeTokenizer(metadata);
            }

            // Unsupported or unknown tokenizer type
            return null;
        }

        private static ITokenizer? ExtractBpeTokenizer(Dictionary<string, object> metadata)
        {
            // Extract required fields for BPE tokenizer
            if (!metadata.TryGetValue("tokenizer.ggml.tokens", out var tokensObj))
                return null;

            // Tokens should be a string array
            if (tokensObj is not object[] tokensArray)
                return null;

            // Build vocabulary: map token string -> ID
            var vocab = new Dictionary<string, int>();
            for (int i = 0; i < tokensArray.Length; i++)
            {
                if (tokensArray[i] is string token)
                {
                    vocab[token] = i;
                }
            }

            if (vocab.Count == 0)
                return null;

            // Extract merges (if available)
            var merges = new List<(string, string)>();
            if (metadata.TryGetValue("tokenizer.ggml.merges", out var mergesObj) && mergesObj is object[] mergesArray)
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
            }

            // Extract special token IDs
            int bosTokenId = ExtractTokenId(metadata, "tokenizer.ggml.bos_token_id");
            int eosTokenId = ExtractTokenId(metadata, "tokenizer.ggml.eos_token_id");
            int unkTokenId = ExtractTokenId(metadata, "tokenizer.ggml.unknown_token_id");
            int padTokenId = ExtractTokenId(metadata, "tokenizer.ggml.padding_token_id");

            // If we don't have explicit special token IDs, try to find them by name
            if (bosTokenId == -1)
            {
                bosTokenId = FindTokenId(vocab, "<s>", "<|startoftext|>", "<bos>");
            }
            if (eosTokenId == -1)
            {
                eosTokenId = FindTokenId(vocab, "</s>", "<|endoftext|>", "<eos>", "<|im_end|>");
            }
            if (unkTokenId == -1)
            {
                unkTokenId = FindTokenId(vocab, "<unk>", "[UNK]");
            }
            if (padTokenId == -1)
            {
                padTokenId = FindTokenId(vocab, "<pad>", "[PAD]");
            }

            // Create BPE tokenizer with in-memory vocab and merges
            return new BpeTokenizer(vocab, merges, bosTokenId, eosTokenId, unkTokenId);
        }

        private static int ExtractTokenId(Dictionary<string, object> metadata, string key)
        {
            if (metadata.TryGetValue(key, out var value))
            {
                // Handle different numeric types
                if (value is int intVal)
                    return intVal;
                if (value is uint uintVal)
                    return (int)uintVal;
                if (value is long longVal)
                    return (int)longVal;
                if (value is ulong ulongVal)
                    return (int)ulongVal;
                
                // Try to parse as string
                if (value is string strVal && int.TryParse(strVal, out int parsed))
                    return parsed;
            }
            return -1;
        }

        private static int FindTokenId(Dictionary<string, int> vocab, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (vocab.TryGetValue(candidate, out int id))
                    return id;
            }
            return -1;
        }

        /// <summary>
        /// Extract and preserve tokenizer metadata for SMQ storage.
        /// </summary>
        public static Dictionary<string, object> PreserveTokenizerMetadata(Dictionary<string, object> ggufMetadata)
        {
            var tokenizerMetadata = new Dictionary<string, object>();

            var relevantKeys = new[]
            {
                "tokenizer.ggml.model",
                "tokenizer.ggml.tokens",
                "tokenizer.ggml.scores",
                "tokenizer.ggml.merges",
                "tokenizer.ggml.token_type",
                "tokenizer.ggml.bos_token_id",
                "tokenizer.ggml.eos_token_id",
                "tokenizer.ggml.unknown_token_id",
                "tokenizer.ggml.padding_token_id",
                "tokenizer.ggml.add_bos_token",
                "tokenizer.ggml.add_eos_token"
            };

            foreach (var key in relevantKeys)
            {
                if (ggufMetadata.TryGetValue(key, out var value))
                {
                    tokenizerMetadata[key] = value;
                }
            }

            return tokenizerMetadata;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using SmallMind.Tokenizers;

namespace SmallMind.Quantization.IO.Gguf
{
    /// <summary>
    /// Extracts tokenizer configuration from GGUF metadata.
    /// </summary>
    internal static class GgufTokenizerExtractor
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

            // Determine if this is byte-level BPE by checking token format
            // Byte-level BPE tokens often contain UTF-8 byte sequences or special char representations
            bool isByteLevelBpe = DetectByteLevelBpe(vocab);

            // Create GGUF BPE tokenizer (supports both character and byte-level)
            return new SmallMind.Tokenizers.Text.GgufBpeTokenizer(
                vocab, 
                merges, 
                bosTokenId, 
                eosTokenId, 
                unkTokenId,
                isByteLevelBpe);
        }

        /// <summary>
        /// Detect if this is byte-level BPE by examining token patterns.
        /// Byte-level BPE often has tokens with UTF-8 byte sequences.
        /// </summary>
        private static bool DetectByteLevelBpe(Dictionary<string, int> vocab)
        {
            // Check for common byte-level BPE patterns:
            // 1. Tokens starting with "Ġ" (GPT-2 space marker)
            // 2. High Unicode characters (used for byte mapping)
            // 3. Byte-level tokens like "Ċ", "ĉ", etc.
            
            int byteLevelIndicators = 0;
            int sampleSize = Math.Min(1000, vocab.Count);
            
            foreach (var kvp in vocab.Take(sampleSize))
            {
                string token = kvp.Key;
                if (string.IsNullOrEmpty(token))
                    continue;

                // Check for GPT-2 style space marker
                if (token.StartsWith("Ġ"))
                    byteLevelIndicators++;
                
                // Check for high Unicode (often used in byte-level mapping)
                foreach (char c in token)
                {
                    if (c >= 256 && c < 512)
                    {
                        byteLevelIndicators++;
                        break;
                    }
                }

                if (byteLevelIndicators > 10) // Found enough indicators
                    return true;
            }

            return byteLevelIndicators > 5; // Threshold for detection
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

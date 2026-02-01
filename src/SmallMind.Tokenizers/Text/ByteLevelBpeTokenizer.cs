using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Byte-level BPE tokenizer (GPT-2 style).
    /// Operates on UTF-8 bytes with reversible byte-to-token mapping.
    /// </summary>
    public class ByteLevelBpeTokenizer : ITokenizer
    {
        private readonly Dictionary<int, int> _byteToToken;
        private readonly Dictionary<int, byte> _tokenToByte;
        private readonly Dictionary<string, int> _vocab;
        private readonly Dictionary<int, string> _inverseVocab;
        private readonly Dictionary<(int, int), int> _mergeRanks;
        
        public int VocabSize => _vocab.Count;
        public TokenizerInfo Info { get; }

        /// <summary>
        /// Creates a new ByteLevelBpeTokenizer from vocab and merges files.
        /// </summary>
        /// <param name="vocabPath">Path to vocab.json file</param>
        /// <param name="mergesPath">Path to merges.txt file</param>
        /// <param name="specialTokens">Optional special tokens configuration</param>
        public ByteLevelBpeTokenizer(string vocabPath, string mergesPath, SpecialTokensConfig? specialTokens = null)
        {
            if (!File.Exists(vocabPath))
                throw new TokenizationException($"Vocabulary file not found: {vocabPath}");
            if (!File.Exists(mergesPath))
                throw new TokenizationException($"Merges file not found: {mergesPath}");

            // Initialize byte-to-token mapping (reversible encoding)
            _byteToToken = new Dictionary<int, int>(256);
            _tokenToByte = new Dictionary<int, byte>(256);
            InitializeByteMappings();

            // Load vocabulary
            string vocabJson = File.ReadAllText(vocabPath);
            _vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson)
                ?? throw new TokenizationException($"Failed to parse vocab.json: {vocabPath}");

            _inverseVocab = new Dictionary<int, string>(_vocab.Count);
            foreach (var kvp in _vocab)
            {
                _inverseVocab[kvp.Value] = kvp.Key;
            }

            // Load merges
            _mergeRanks = new Dictionary<(int, int), int>();
            string[] lines = File.ReadAllLines(mergesPath);
            int rank = 0;
            
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                    continue;

                // Convert merge tokens to byte sequences
                if (_vocab.TryGetValue(parts[0], out int tok1) && _vocab.TryGetValue(parts[1], out int tok2))
                {
                    _mergeRanks[(tok1, tok2)] = rank++;
                }
            }

            // Setup Info with special tokens
            int bosId = specialTokens?.Bos != null && _vocab.TryGetValue(specialTokens.Bos, out int bId) ? bId : -1;
            int eosId = specialTokens?.Eos != null && _vocab.TryGetValue(specialTokens.Eos, out int eId) ? eId : -1;
            int padId = specialTokens?.Pad != null && _vocab.TryGetValue(specialTokens.Pad, out int pId) ? pId : -1;
            int unkId = specialTokens?.Unk != null && _vocab.TryGetValue(specialTokens.Unk, out int uId) ? uId : -1;

            Info = new TokenizerInfo(
                name: "ByteLevelBpe",
                vocabSize: _vocab.Count,
                bosTokenId: bosId,
                eosTokenId: eosId,
                padTokenId: padId,
                unkTokenId: unkId,
                supportsByteFallback: true
            );
        }

        /// <summary>
        /// Initialize reversible byte-to-unicode mapping (GPT-2 style).
        /// This avoids whitespace and control characters in the vocabulary.
        /// </summary>
        private void InitializeByteMappings()
        {
            // GPT-2 style byte encoding: maps bytes to printable unicode range
            // This creates a reversible mapping without using control characters
            int n = 0;
            
            for (int b = 0; b < 256; b++)
            {
                // Printable ASCII
                if ((b >= 33 && b <= 126) || (b >= 161 && b <= 172) || (b >= 174 && b <= 255))
                {
                    _byteToToken[b] = b;
                    _tokenToByte[b] = (byte)b;
                }
                else
                {
                    // Map to high unicode range (256+)
                    int mapped = 256 + n;
                    _byteToToken[b] = mapped;
                    _tokenToByte[mapped] = (byte)b;
                    n++;
                }
            }
        }

        /// <summary>
        /// Encode a string into token IDs.
        /// </summary>
        public List<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<int>();

            byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
            int[] tokens = ArrayPool<int>.Shared.Rent(utf8Bytes.Length * 2);
            
            try
            {
                int count = Encode(utf8Bytes, tokens);
                var result = new List<int>(count);
                for (int i = 0; i < count; i++)
                {
                    result.Add(tokens[i]);
                }
                return result;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(tokens);
            }
        }

        /// <summary>
        /// Encode UTF-8 bytes into token IDs (fast path).
        /// </summary>
        public int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
        {
            if (utf8.Length == 0)
                return 0;

            // Convert bytes to initial tokens using byte mapping
            int[] symbols = ArrayPool<int>.Shared.Rent(utf8.Length);
            
            try
            {
                for (int i = 0; i < utf8.Length; i++)
                {
                    symbols[i] = _byteToToken[utf8[i]];
                }

                int symCount = utf8.Length;

                // Apply BPE merges
                while (symCount > 1)
                {
                    (int, int)? bestPair = null;
                    int bestRank = int.MaxValue;
                    int bestIndex = -1;

                    // Find best merge pair
                    for (int i = 0; i < symCount - 1; i++)
                    {
                        var pair = (symbols[i], symbols[i + 1]);
                        if (_mergeRanks.TryGetValue(pair, out int rank) && rank < bestRank)
                        {
                            bestPair = pair;
                            bestRank = rank;
                            bestIndex = i;
                        }
                    }

                    if (bestPair == null)
                        break;

                    // Apply merge: combine pair into single token
                    // Need to look up merged token in vocab
                    string tok1 = _inverseVocab.GetValueOrDefault(bestPair.Value.Item1, "");
                    string tok2 = _inverseVocab.GetValueOrDefault(bestPair.Value.Item2, "");
                    string merged = tok1 + tok2;

                    if (_vocab.TryGetValue(merged, out int mergedId))
                    {
                        symbols[bestIndex] = mergedId;
                        // Shift remaining symbols left
                        for (int i = bestIndex + 1; i < symCount - 1; i++)
                        {
                            symbols[i] = symbols[i + 1];
                        }
                        symCount--;
                    }
                    else
                    {
                        break; // Can't merge
                    }
                }

                // Copy to output
                int count = Math.Min(symCount, tokensOut.Length);
                for (int i = 0; i < count; i++)
                {
                    tokensOut[i] = symbols[i];
                }
                return count;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(symbols);
            }
        }

        /// <summary>
        /// Decode token IDs back into UTF-8 bytes.
        /// </summary>
        public int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out)
        {
            if (tokens.Length == 0)
                return 0;

            // Decode tokens to byte sequence
            int byteCount = 0;
            
            foreach (int tokenId in tokens)
            {
                if (!_inverseVocab.TryGetValue(tokenId, out string? tokenStr))
                    continue;

                // Token string represents a sequence of byte-mapped characters
                // Decode each character back to original byte
                foreach (char c in tokenStr)
                {
                    if (byteCount >= utf8Out.Length)
                        return byteCount;

                    int code = c;
                    if (_tokenToByte.TryGetValue(code, out byte b))
                    {
                        utf8Out[byteCount++] = b;
                    }
                    else if (code < 256)
                    {
                        utf8Out[byteCount++] = (byte)code;
                    }
                }
            }

            return byteCount;
        }

        /// <summary>
        /// Decode token IDs to string.
        /// </summary>
        public string Decode(List<int> tokens)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(tokens.Count * 4);
            try
            {
                int byteCount = Decode(tokens.ToArray().AsSpan(), buffer);
                return Encoding.UTF8.GetString(buffer, 0, byteCount);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Decode token IDs to string (convenience).
        /// </summary>
        public string DecodeToString(ReadOnlySpan<int> tokens)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(tokens.Length * 4);
            try
            {
                int byteCount = Decode(tokens, buffer);
                return Encoding.UTF8.GetString(buffer, 0, byteCount);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
    }
}

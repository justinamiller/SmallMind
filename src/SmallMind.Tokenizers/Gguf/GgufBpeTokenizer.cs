using System;
using System.Collections.Generic;
using System.Text;

namespace SmallMind.Tokenizers.Gguf
{
    /// <summary>
    /// GGUF BPE tokenizer with merge rules.
    /// Implements Byte Pair Encoding using GGUF-provided vocabulary and merges.
    /// </summary>
    internal sealed class GgufBpeTokenizer : ITokenizer
    {
        private const int ByteTokenLength = 6; // Length of byte tokens (e.g., "<0x20>")
        
        private readonly Dictionary<string, int> _vocab;
        private readonly List<string> _reverseVocab;
        private readonly List<(string, string)> _merges;
        private readonly Dictionary<(string, string), int> _mergeRanks;
        private readonly SpecialTokens _specialTokens;
        private readonly string[] _byteTokenCache; // Pre-computed byte tokens for performance

        public GgufBpeTokenizer(
            Dictionary<string, int> vocab,
            List<string> reverseVocab,
            List<(string, string)> merges,
            SpecialTokens specialTokens)
        {
            _vocab = vocab ?? throw new ArgumentNullException(nameof(vocab));
            _reverseVocab = reverseVocab ?? throw new ArgumentNullException(nameof(reverseVocab));
            _merges = merges ?? throw new ArgumentNullException(nameof(merges));
            _specialTokens = specialTokens ?? throw new ArgumentNullException(nameof(specialTokens));

            // Build merge ranks for efficient lookup
            _mergeRanks = new Dictionary<(string, string), int>();
            for (int i = 0; i < _merges.Count; i++)
            {
                _mergeRanks[_merges[i]] = i;
            }
            
            // Pre-compute byte tokens to avoid allocations in hot path
            _byteTokenCache = new string[256];
            for (int b = 0; b < 256; b++)
            {
                _byteTokenCache[b] = $"<0x{b:X2}>";
            }
        }

        public int VocabSize => _vocab.Count;

        public TokenizerInfo Info => new TokenizerInfo(
            name: "GgufBpe",
            vocabSize: VocabSize,
            bosTokenId: _specialTokens.BosTokenId,
            eosTokenId: _specialTokens.EosTokenId,
            padTokenId: _specialTokens.PadTokenId,
            unkTokenId: _specialTokens.UnkTokenId,
            supportsByteFallback: true,
            addBos: _specialTokens.BosTokenId >= 0
        );

        public List<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<int>();

            // Convert text to byte tokens first
            var bytes = Encoding.UTF8.GetBytes(text);
            var tokens = new List<string>();
            
            foreach (byte b in bytes)
            {
                // Use pre-computed byte token string (no allocation)
                string byteToken = _byteTokenCache[b];
                if (_vocab.ContainsKey(byteToken))
                {
                    tokens.Add(byteToken);
                }
                else
                {
                    // Fallback to character
                    tokens.Add(((char)b).ToString());
                }
            }

            // Apply BPE merges
            while (tokens.Count > 1)
            {
                var bestPair = FindBestPair(tokens);
                if (bestPair == null)
                    break;

                var (left, right, rank) = bestPair.Value;
                string merged = left + right;
                
                // Only merge if the result is in vocabulary
                if (!_vocab.ContainsKey(merged))
                    break;

                tokens = MergePair(tokens, left, right, merged);
            }

            // Convert token strings to IDs
            var result = new List<int>();
            foreach (var token in tokens)
            {
                if (_vocab.TryGetValue(token, out int id))
                {
                    result.Add(id);
                }
                else if (_specialTokens.UnkTokenId != -1)
                {
                    result.Add(_specialTokens.UnkTokenId);
                }
            }

            return result;
        }

        private (string left, string right, int rank)? FindBestPair(List<string> tokens)
        {
            (string, string, int)? best = null;
            int bestRank = int.MaxValue;

            for (int i = 0; i < tokens.Count - 1; i++)
            {
                var pair = (tokens[i], tokens[i + 1]);
                if (_mergeRanks.TryGetValue(pair, out int rank))
                {
                    if (rank < bestRank)
                    {
                        best = (pair.Item1, pair.Item2, rank);
                        bestRank = rank;
                    }
                }
            }

            return best;
        }

        private List<string> MergePair(List<string> tokens, string left, string right, string merged)
        {
            var result = new List<string>();
            int i = 0;
            while (i < tokens.Count)
            {
                if (i < tokens.Count - 1 && tokens[i] == left && tokens[i + 1] == right)
                {
                    result.Add(merged);
                    i += 2;
                }
                else
                {
                    result.Add(tokens[i]);
                    i++;
                }
            }
            return result;
        }

        public int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
        {
            // Convert UTF-8 to string for simple implementation
            string text = Encoding.UTF8.GetString(utf8);
            var tokens = Encode(text);
            
            int count = Math.Min(tokens.Count, tokensOut.Length);
            for (int i = 0; i < count; i++)
            {
                tokensOut[i] = tokens[i];
            }
            
            return count;
        }

        public string Decode(List<int> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var tokenId in tokens)
            {
                if (tokenId >= 0 && tokenId < _reverseVocab.Count)
                {
                    string tokenStr = _reverseVocab[tokenId];
                    
                    // Handle byte tokens (e.g., <0x20> for space)
                    if (IsByteToken(tokenStr, out byte byteValue))
                    {
                        sb.Append((char)byteValue);
                        continue;
                    }
                    
                    sb.Append(tokenStr);
                }
            }

            return sb.ToString();
        }
        
        private static bool IsByteToken(string tokenStr, out byte byteValue)
        {
            // Check if token is in byte format: <0xXX> where XX is hex
            if (tokenStr.Length == ByteTokenLength && 
                tokenStr.StartsWith("<0x") && 
                tokenStr.EndsWith(">"))
            {
                return byte.TryParse(
                    tokenStr.Substring(3, 2), 
                    System.Globalization.NumberStyles.HexNumber, 
                    null, 
                    out byteValue);
            }
            
            byteValue = 0;
            return false;
        }

        public int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out)
        {
            // Convert tokens to string first
            var tokenList = new List<int>(tokens.Length);
            foreach (var token in tokens)
            {
                tokenList.Add(token);
            }
            
            string text = Decode(tokenList);
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(text);
            
            int count = Math.Min(utf8Bytes.Length, utf8Out.Length);
            utf8Bytes.AsSpan(0, count).CopyTo(utf8Out);
            
            return count;
        }

        public string DecodeToString(ReadOnlySpan<int> tokens)
        {
            var tokenList = new List<int>(tokens.Length);
            foreach (var token in tokens)
            {
                tokenList.Add(token);
            }
            return Decode(tokenList);
        }

        public string DecodeSingleToken(int tokenId)
        {
            if (tokenId >= 0 && tokenId < _reverseVocab.Count)
            {
                string tokenStr = _reverseVocab[tokenId];
                
                // Handle byte tokens
                if (IsByteToken(tokenStr, out byte byteValue))
                {
                    return ((char)byteValue).ToString();
                }
                
                return tokenStr;
            }
            
            return string.Empty;
        }
    }
}

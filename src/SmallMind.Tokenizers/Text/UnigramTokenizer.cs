using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Unigram Language Model tokenizer (SentencePiece-style).
    /// Uses Viterbi algorithm for best segmentation based on token scores.
    /// </summary>
    internal class UnigramTokenizer : ITokenizer
    {
        private readonly List<(string token, float score, int id)> _pieces;
        private readonly FrozenDictionary<int, string> _idToToken;
        private readonly TrieNode _trie;
        private readonly int _unkTokenId;

        public int VocabSize => _pieces.Count;
        public TokenizerInfo Info { get; }

        /// <summary>
        /// Creates a new UnigramTokenizer from a model file.
        /// Model file format: each line contains "token\tscore\tid"
        /// </summary>
        /// <param name="modelPath">Path to Unigram model file</param>
        /// <param name="unkToken">Unknown token string (default: "<unk>")</param>
        /// <param name="specialTokens">Optional special tokens configuration</param>
        public UnigramTokenizer(
            string modelPath,
            string unkToken = "<unk>",
            SpecialTokensConfig? specialTokens = null)
        {
            if (!File.Exists(modelPath))
                throw new TokenizationException($"Model file not found: {modelPath}");

            _pieces = new List<(string, float, int)>();
            var idToTokenDict = new Dictionary<int, string>();
            _trie = new TrieNode();

            // Load model file
            string[] lines = File.ReadAllLines(modelPath);
            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = line.Split('\t');
                if (parts.Length < 3)
                    continue;

                string token = parts[0];
                if (!float.TryParse(parts[1], out float score))
                    continue;
                if (!int.TryParse(parts[2], out int id))
                    continue;

                _pieces.Add((token, score, id));
                idToTokenDict[id] = token;
                
                // Add to trie for fast prefix matching
                AddToTrie(token, id, score);
            }

            // Convert to FrozenDictionary for faster lookups
            _idToToken = idToTokenDict.ToFrozenDictionary();

            // Find UNK token
            _unkTokenId = -1;
            for (int i = 0; i < _pieces.Count; i++)
            {
                if (_pieces[i].token == unkToken)
                {
                    _unkTokenId = _pieces[i].id;
                    break;
                }
            }

            if (_unkTokenId == -1)
                throw new TokenizationException($"Unknown token '{unkToken}' not found in model");

            // Setup Info
            int bosId = -1, eosId = -1, padId = -1;
            
            if (specialTokens != null)
            {
                for (int i = 0; i < _pieces.Count; i++)
                {
                    if (specialTokens.Bos != null && _pieces[i].token == specialTokens.Bos)
                        bosId = _pieces[i].id;
                    if (specialTokens.Eos != null && _pieces[i].token == specialTokens.Eos)
                        eosId = _pieces[i].id;
                    if (specialTokens.Pad != null && _pieces[i].token == specialTokens.Pad)
                        padId = _pieces[i].id;
                }
            }

            Info = new TokenizerInfo(
                name: "Unigram",
                vocabSize: _pieces.Count,
                bosTokenId: bosId,
                eosTokenId: eosId,
                padTokenId: padId,
                unkTokenId: _unkTokenId,
                supportsByteFallback: false
            );
        }

        private void AddToTrie(string token, int id, float score)
        {
            TrieNode node = _trie;
            foreach (char c in token)
            {
                if (!node.Children.TryGetValue(c, out TrieNode? child))
                {
                    child = new TrieNode();
                    node.Children[c] = child;
                }
                node = child;
            }
            node.IsEndOfToken = true;
            node.TokenId = id;
            node.Score = score;
        }

        /// <summary>
        /// Encode a string using Viterbi algorithm for best segmentation.
        /// </summary>
        public List<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<int>();

            int n = text.Length;
            
            // Viterbi: dp[i] = (best score to position i, best last token id)
            var dp = new (float score, int lastTokenId, int prevPos)[n + 1];
            dp[0] = (0f, -1, -1);
            
            for (int i = 1; i <= n; i++)
            {
                dp[i] = (float.NegativeInfinity, -1, -1);
            }

            // Fill DP table
            for (int i = 0; i < n; i++)
            {
                if (float.IsNegativeInfinity(dp[i].score))
                    continue;

                // Try all possible tokens starting at position i
                TrieNode node = _trie;
                for (int j = i; j < n && j < i + 100; j++) // Limit max token length
                {
                    char c = text[j];
                    if (!node.Children.TryGetValue(c, out TrieNode? child))
                        break;
                    
                    node = child;
                    
                    if (node.IsEndOfToken)
                    {
                        float newScore = dp[i].score + node.Score;
                        if (newScore > dp[j + 1].score)
                        {
                            dp[j + 1] = (newScore, node.TokenId, i);
                        }
                    }
                }

                // Fallback: single character as UNK
                if (dp[i + 1].score == float.NegativeInfinity)
                {
                    dp[i + 1] = (dp[i].score - 10f, _unkTokenId, i);
                }
            }

            // Backtrack to get tokens
            var result = new List<int>(text.Length / 3);
            int pos = n;
            
            while (pos > 0)
            {
                if (dp[pos].lastTokenId != -1)
                {
                    result.Add(dp[pos].lastTokenId);
                }
                pos = dp[pos].prevPos;
            }

            result.Reverse();
            return result;
        }

        /// <summary>
        /// Encode UTF-8 bytes into token IDs.
        /// </summary>
        public int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
        {
            string text = Encoding.UTF8.GetString(utf8);
            List<int> tokens = Encode(text);
            
            int count = Math.Min(tokens.Count, tokensOut.Length);
            for (int i = 0; i < count; i++)
            {
                tokensOut[i] = tokens[i];
            }
            return count;
        }

        /// <summary>
        /// Decode token IDs back into UTF-8 bytes.
        /// </summary>
        public int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out)
        {
            var sb = new StringBuilder();
            
            foreach (int tokenId in tokens)
            {
                if (_idToToken.TryGetValue(tokenId, out string? token))
                {
                    sb.Append(token);
                }
            }

            string text = sb.ToString();
            int bytesWritten = Encoding.UTF8.GetBytes(text.AsSpan(), utf8Out);
            return bytesWritten;
        }

        /// <summary>
        /// Decode token IDs to string.
        /// </summary>
        public string Decode(List<int> tokens)
        {
            if (tokens == null || tokens.Count == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (int tokenId in tokens)
            {
                if (_idToToken.TryGetValue(tokenId, out string? token))
                {
                    sb.Append(token);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Decode token IDs to string (convenience).
        /// </summary>
        public string DecodeToString(ReadOnlySpan<int> tokens)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(tokens.Length * 10);
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

        private class TrieNode
        {
            public Dictionary<char, TrieNode> Children { get; } = new Dictionary<char, TrieNode>();
            public bool IsEndOfToken { get; set; }
            public int TokenId { get; set; }
            public float Score { get; set; }
        }
    }
}

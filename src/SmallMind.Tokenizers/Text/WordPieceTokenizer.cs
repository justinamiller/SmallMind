using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// WordPiece tokenizer (BERT-style with ## continuation markers).
    /// Uses greedy longest-match-first algorithm.
    /// </summary>
    public class WordPieceTokenizer : ITokenizer
    {
        private readonly FrozenDictionary<string, int> _vocab;
        private readonly FrozenDictionary<int, string> _inverseVocab;
        private readonly int _unkTokenId;
        private readonly string _unkToken;
        private readonly int _maxInputCharsPerWord;

        public int VocabSize => _vocab.Count;
        public TokenizerInfo Info { get; }

        /// <summary>
        /// Creates a new WordPieceTokenizer from a vocabulary file.
        /// </summary>
        /// <param name="vocabPath">Path to vocabulary file (one token per line)</param>
        /// <param name="unkToken">Unknown token string (default: "[UNK]")</param>
        /// <param name="maxInputCharsPerWord">Maximum characters per word (default: 200)</param>
        /// <param name="specialTokens">Optional special tokens configuration</param>
        public WordPieceTokenizer(
            string vocabPath, 
            string unkToken = "[UNK]",
            int maxInputCharsPerWord = 200,
            SpecialTokensConfig? specialTokens = null)
        {
            if (!File.Exists(vocabPath))
                throw new TokenizationException($"Vocabulary file not found: {vocabPath}");

            _unkToken = unkToken;
            _maxInputCharsPerWord = maxInputCharsPerWord;

            // Load vocabulary from file (one token per line)
            var vocabDict = new Dictionary<string, int>();
            var inverseDict = new Dictionary<int, string>();

            string[] lines = File.ReadAllLines(vocabPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string token = lines[i].Trim();
                if (!string.IsNullOrEmpty(token))
                {
                    vocabDict[token] = i;
                    inverseDict[i] = token;
                }
            }

            // Convert to FrozenDictionary for faster lookups
            _vocab = vocabDict.ToFrozenDictionary();
            _inverseVocab = inverseDict.ToFrozenDictionary();

            _unkTokenId = _vocab.TryGetValue(_unkToken, out int id) ? id : -1;
            if (_unkTokenId == -1)
                throw new TokenizationException($"Unknown token '{_unkToken}' not found in vocabulary");

            // Setup Info
            int bosId = specialTokens?.Bos != null && _vocab.TryGetValue(specialTokens.Bos, out int bId) ? bId : -1;
            int eosId = specialTokens?.Eos != null && _vocab.TryGetValue(specialTokens.Eos, out int eId) ? eId : -1;
            int padId = specialTokens?.Pad != null && _vocab.TryGetValue(specialTokens.Pad, out int pId) ? pId : -1;

            Info = new TokenizerInfo(
                name: "WordPiece",
                vocabSize: _vocab.Count,
                bosTokenId: bosId,
                eosTokenId: eosId,
                padTokenId: padId,
                unkTokenId: _unkTokenId,
                supportsByteFallback: false
            );
        }

        /// <summary>
        /// Encode a string into token IDs.
        /// </summary>
        public List<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
                return new List<int>();

            var result = new List<int>();
            
            // Split on whitespace
            string[] words = text.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string word in words)
            {
                if (word.Length > _maxInputCharsPerWord)
                {
                    result.Add(_unkTokenId);
                    continue;
                }

                // Greedy longest-match-first
                bool isBad = false;
                int start = 0;
                
                // Pre-allocate buffers outside loop to avoid potential stack overflow
                // Initialize prefix buffer once (never changes)
                Span<char> prefixBuffer = stackalloc char[2];
                prefixBuffer[0] = '#';
                prefixBuffer[1] = '#';
                
                // Max word length buffer for prefix concatenation (reasonable upper bound)
                const int MaxTokenLength = 256;
                Span<char> workBuffer = stackalloc char[MaxTokenLength];

                while (start < word.Length)
                {
                    int end = word.Length;
                    int subTokenId = -1;

                    // Find longest match
                    while (start < end)
                    {
                        ReadOnlySpan<char> substr = word.AsSpan(start, end - start);
                        
                        // For continuation tokens (not first token), try with ## prefix
                        if (start > 0)
                        {
                            // Use pre-allocated work buffer for ## prefix concatenation
                            int totalLen = substr.Length + 2;
                            if (totalLen <= workBuffer.Length)
                            {
                                Span<char> withPrefix = workBuffer.Slice(0, totalLen);
                                prefixBuffer.CopyTo(withPrefix);
                                substr.CopyTo(withPrefix.Slice(2));
                                
                                string substrWithPrefix = new string(withPrefix);
                                if (_vocab.TryGetValue(substrWithPrefix, out int tokenId))
                                {
                                    subTokenId = tokenId;
                                    break;
                                }
                            }
                            else
                            {
                                // Fallback for very long tokens (rare)
                                string substrWithPrefix = "##" + substr.ToString();
                                if (_vocab.TryGetValue(substrWithPrefix, out int tokenId))
                                {
                                    subTokenId = tokenId;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            // First token - no prefix needed
                            string substrStr = substr.ToString();
                            if (_vocab.TryGetValue(substrStr, out int tokenId))
                            {
                                subTokenId = tokenId;
                                break;
                            }
                        }
                        
                        end--;
                    }

                    if (subTokenId == -1)
                    {
                        isBad = true;
                        break;
                    }

                    result.Add(subTokenId);
                    start = end;
                }

                if (isBad)
                {
                    result.Add(_unkTokenId);
                }
            }

            return result;
        }

        /// <summary>
        /// Encode UTF-8 bytes into token IDs (fast path).
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
            if (tokens.Length == 0)
                return 0;

            var sb = new StringBuilder();
            
            foreach (int tokenId in tokens)
            {
                if (_inverseVocab.TryGetValue(tokenId, out string? token))
                {
                    // Remove ## prefix for continuation tokens
                    if (token.StartsWith("##"))
                    {
                        sb.Append(token.Substring(2));
                    }
                    else
                    {
                        // Add space before new word (except first)
                        if (sb.Length > 0)
                            sb.Append(' ');
                        sb.Append(token);
                    }
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
                if (_inverseVocab.TryGetValue(tokenId, out string? token))
                {
                    // Remove ## prefix for continuation tokens
                    if (token.StartsWith("##"))
                    {
                        sb.Append(token.Substring(2));
                    }
                    else
                    {
                        // Add space before new word (except first)
                        if (sb.Length > 0)
                            sb.Append(' ');
                        sb.Append(token);
                    }
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
    }
}

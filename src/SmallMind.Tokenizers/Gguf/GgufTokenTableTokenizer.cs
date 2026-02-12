using System;
using System.Collections.Generic;
using System.Text;

namespace SmallMind.Tokenizers.Gguf
{
    /// <summary>
    /// Token-table-only tokenizer for GGUF models.
    /// Uses direct vocabulary lookup without BPE merges.
    /// Provides deterministic fallback when merges are unavailable.
    /// </summary>
    internal sealed class GgufTokenTableTokenizer : ITokenizer
    {
        private const int MaxTokenLength = 50; // Maximum length to search for matching tokens
        private const int ByteTokenLength = 6; // Length of byte tokens (e.g., "<0x20>")
        
        private readonly Dictionary<string, int> _vocab;
        private readonly List<string> _reverseVocab;
        private readonly SpecialTokens _specialTokens;

        public GgufTokenTableTokenizer(
            Dictionary<string, int> vocab,
            List<string> reverseVocab,
            SpecialTokens specialTokens)
        {
            _vocab = vocab ?? throw new ArgumentNullException(nameof(vocab));
            _reverseVocab = reverseVocab ?? throw new ArgumentNullException(nameof(reverseVocab));
            _specialTokens = specialTokens ?? throw new ArgumentNullException(nameof(specialTokens));
        }

        public int VocabSize => _vocab.Count;

        public TokenizerInfo Info => new TokenizerInfo(
            name: "GgufTokenTable",
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

            var tokens = new List<int>();
            
            // Simple greedy tokenization: longest match first
            int pos = 0;
            while (pos < text.Length)
            {
                int longestMatchLen = 0;
                int matchedTokenId = -1;

                // Try to find the longest matching token starting at current position
                for (int len = Math.Min(text.Length - pos, MaxTokenLength); len > 0; len--)
                {
                    string candidate = text.Substring(pos, len);
                    if (_vocab.TryGetValue(candidate, out int tokenId))
                    {
                        longestMatchLen = len;
                        matchedTokenId = tokenId;
                        break;
                    }
                }

                if (matchedTokenId != -1)
                {
                    tokens.Add(matchedTokenId);
                    pos += longestMatchLen;
                }
                else
                {
                    // No match found - use unknown token or byte fallback
                    if (_specialTokens.UnkTokenId != -1)
                    {
                        tokens.Add(_specialTokens.UnkTokenId);
                    }
                    else
                    {
                        // Try to encode as byte tokens
                        byte b = (byte)text[pos];
                        string byteToken = $"<0x{b:X2}>";
                        if (_vocab.TryGetValue(byteToken, out int byteTokenId))
                        {
                            tokens.Add(byteTokenId);
                        }
                    }
                    pos++;
                }
            }

            return tokens;
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

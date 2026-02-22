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

            // SentencePiece-style GGUF vocab commonly stores word-leading tokens with ▁ prefix.
            // Tokenize by words, then greedily match pieces (first piece prefixed with ▁).
            int pos = 0;
            bool atWordStart = true;

            while (pos < text.Length)
            {
                char c = text[pos];

                if (char.IsWhiteSpace(c))
                {
                    atWordStart = true;
                    pos++;
                    continue;
                }

                // Read one word span
                int wordStart = pos;
                while (pos < text.Length && !char.IsWhiteSpace(text[pos]))
                {
                    pos++;
                }

                string word = text.Substring(wordStart, pos - wordStart);
                EncodeWord(word, atWordStart, tokens);
                atWordStart = false;
            }

            return tokens;
        }

        private void EncodeWord(string word, bool atWordStart, List<int> output)
        {
            int cursor = 0;
            bool firstPiece = atWordStart;

            while (cursor < word.Length)
            {
                int maxLen = Math.Min(word.Length - cursor, MaxTokenLength);
                int matchedTokenId = -1;
                int matchedLen = 0;

                for (int len = maxLen; len > 0; len--)
                {
                    string piece = word.Substring(cursor, len);
                    string candidate = firstPiece ? $"▁{piece}" : piece;

                    if (_vocab.TryGetValue(candidate, out int tokenId))
                    {
                        matchedTokenId = tokenId;
                        matchedLen = len;
                        break;
                    }
                }

                if (matchedTokenId != -1)
                {
                    output.Add(matchedTokenId);
                    cursor += matchedLen;
                    firstPiece = false;
                    continue;
                }

                // Fallback: unknown token or byte token for first char.
                if (_specialTokens.UnkTokenId != -1)
                {
                    output.Add(_specialTokens.UnkTokenId);
                }
                else
                {
                    byte b = (byte)word[cursor];
                    string byteToken = $"<0x{b:X2}>";
                    if (_vocab.TryGetValue(byteToken, out int byteTokenId))
                    {
                        output.Add(byteTokenId);
                    }
                }

                cursor++;
                firstPiece = false;
            }
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
                    if (GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue))
                    {
                        sb.Append((char)byteValue);
                        continue;
                    }

                    // SentencePiece-style token boundary marker: ▁ means a word-leading space.
                    // Normalize to plain text so downstream coherence checks and UX are correct.
                    sb.Append(tokenStr.Replace('▁', ' '));
                }
            }

            return sb.ToString();
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
                if (GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue))
                {
                    return ((char)byteValue).ToString();
                }

                return tokenStr;
            }

            return string.Empty;
        }
    }
}

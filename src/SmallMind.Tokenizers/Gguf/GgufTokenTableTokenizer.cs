using System.Text;

namespace SmallMind.Tokenizers.Gguf
{
    /// <summary>
    /// Token-table-only tokenizer for GGUF models.
    /// Uses direct vocabulary lookup without BPE merges.
    /// Supports SentencePiece-style vocabularies (LLaMA family) where word-boundary spaces
    /// are represented by ▁ (U+2581) prefixed tokens.
    /// </summary>
    internal sealed class GgufTokenTableTokenizer : ITokenizer
    {
        private const int MaxTokenLength = 50; // Maximum length to search for matching tokens

        private readonly Dictionary<string, int> _vocab;
        private readonly List<string> _reverseVocab;
        private readonly SpecialTokens _specialTokens;
        private readonly bool _isSentencePiece; // True when vocab uses ▁ (U+2581) word-boundary markers

        public GgufTokenTableTokenizer(
            Dictionary<string, int> vocab,
            List<string> reverseVocab,
            SpecialTokens specialTokens)
        {
            _vocab = vocab ?? throw new ArgumentNullException(nameof(vocab));
            _reverseVocab = reverseVocab ?? throw new ArgumentNullException(nameof(reverseVocab));
            _specialTokens = specialTokens ?? throw new ArgumentNullException(nameof(specialTokens));

            // Detect SentencePiece vocabulary: any token starting with ▁ (U+2581) signals
            // that this vocab uses word-boundary space markers (LLaMA/Mistral family).
            foreach (var token in vocab.Keys)
            {
                if (token.Length > 0 && token[0] == '\u2581')
                {
                    _isSentencePiece = true;
                    break;
                }
            }
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

            // SentencePiece normalization: replace spaces with ▁ (U+2581) and
            // prepend ▁ at the start to mark the first word boundary.
            // e.g. "The capital of France" → "▁The▁capital▁of▁France"
            string normalized = text;
            if (_isSentencePiece)
            {
                normalized = text.Replace(' ', '\u2581');
                if (!normalized.StartsWith('\u2581'))
                    normalized = "\u2581" + normalized;
            }

            // Pre-seed list with BOS so we avoid O(n) Insert(0) later
            bool shouldAddBos = _specialTokens.BosTokenId >= 0;
            var tokens = new List<int>();
            if (shouldAddBos)
                tokens.Add(_specialTokens.BosTokenId);

            // Greedy longest-match tokenization
            int pos = 0;
            while (pos < normalized.Length)
            {
                int longestMatchLen = 0;
                int matchedTokenId = -1;

                // Try to find the longest matching token starting at current position
                for (int len = Math.Min(normalized.Length - pos, MaxTokenLength); len > 0; len--)
                {
                    string candidate = normalized.Substring(pos, len);
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
                        byte b = (byte)normalized[pos];
                        string byteToken = $"<0x{b:X2}>";
                        if (_vocab.TryGetValue(byteToken, out int byteTokenId))
                        {
                            tokens.Add(byteTokenId);
                        }
                    }
                    pos++;
                }
            }

            // If encoding produced nothing (empty text after normalization), don't return bare BOS
            if (shouldAddBos && tokens.Count == 1)
                tokens.Clear();

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

            // Skip BOS token at start so the decoded output length matches the original prompt
            // length. This is required for ValidateCoherence's output.Substring(prompt.Length)
            // to work correctly (mirrors GgufBpeTokenizer.Decode behaviour).
            int startIdx = (_specialTokens.BosTokenId >= 0 && tokens[0] == _specialTokens.BosTokenId) ? 1 : 0;

            var sb = new StringBuilder();
            for (int tokenIndex = startIdx; tokenIndex < tokens.Count; tokenIndex++)
            {
                int tokenId = tokens[tokenIndex];

                // Skip EOS token so "</s>" does not appear in the decoded output.
                if (_specialTokens.EosTokenId >= 0 && tokenId == _specialTokens.EosTokenId)
                    continue;

                if (tokenId >= 0 && tokenId < _reverseVocab.Count)
                {
                    string tokenStr = _reverseVocab[tokenId];

                    // Handle byte tokens (e.g., <0x20> for space)
                    if (GgufTokenizerHelpers.IsByteToken(tokenStr, out byte byteValue))
                    {
                        sb.Append((char)byteValue);
                        continue;
                    }

                    // SentencePiece space: replace "▁" (U+2581) with regular space " "
                    if (tokenStr.Contains('\u2581'))
                        sb.Append(tokenStr.Replace('\u2581', ' '));
                    else
                        sb.Append(tokenStr);
                }
            }

            // SentencePiece models prepend a word-boundary space to the first real token
            // (e.g. "▁What" → " What"). Strip that leading space ONLY when a BOS token was
            // present and has been skipped, because in that case the ▁ represents the sentence
            // start boundary (not a real space in the original text). This keeps
            // output.Substring(prompt.Length) aligned with the prompt in ValidateCoherence.
            // Use a start offset into the StringBuilder instead of Remove(0,1) to avoid O(n)
            // internal buffer shifting.
            int resultStart = (_isSentencePiece && startIdx > 0 && sb.Length > 0 && sb[0] == ' ') ? 1 : 0;
            return resultStart > 0 ? sb.ToString(resultStart, sb.Length - resultStart) : sb.ToString();
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

                // SentencePiece space: replace "▁" (U+2581) with regular space " "
                if (tokenStr.Contains('\u2581'))
                    return tokenStr.Replace('\u2581', ' ');

                return tokenStr;
            }

            return string.Empty;
        }
    }
}

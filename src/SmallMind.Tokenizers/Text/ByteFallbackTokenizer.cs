using System.Buffers;
using System.Text;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Byte fallback wrapper tokenizer.
    /// Wraps an inner tokenizer and falls back to byte tokens for unknown sequences.
    /// Ensures no UNK tokens by encoding unknowns as individual bytes.
    /// </summary>
    internal class ByteFallbackTokenizer : ITokenizer
    {
        private readonly ITokenizer _innerTokenizer;
        private readonly int _byteTokenStart;
        private readonly Dictionary<int, byte> _tokenToByte;
        private readonly Dictionary<byte, int> _byteToToken;

        public int VocabSize { get; }
        public TokenizerInfo Info { get; }

        /// <summary>
        /// Creates a ByteFallbackTokenizer wrapping an inner tokenizer.
        /// </summary>
        /// <param name="innerTokenizer">The tokenizer to wrap</param>
        public ByteFallbackTokenizer(ITokenizer innerTokenizer)
        {
            _innerTokenizer = innerTokenizer ?? throw new ArgumentNullException(nameof(innerTokenizer));

            // Byte tokens start after inner vocab
            _byteTokenStart = _innerTokenizer.VocabSize;

            // Extended vocab includes inner tokens + 256 byte tokens
            VocabSize = _byteTokenStart + 256;

            // Initialize byte token mappings
            _tokenToByte = new Dictionary<int, byte>(256);
            _byteToToken = new Dictionary<byte, int>(256);

            for (int i = 0; i < 256; i++)
            {
                int tokenId = _byteTokenStart + i;
                byte b = (byte)i;
                _tokenToByte[tokenId] = b;
                _byteToToken[b] = tokenId;
            }

            // Create Info
            var innerInfo = _innerTokenizer.Info;
            Info = new TokenizerInfo(
                name: $"ByteFallback({innerInfo.Name})",
                vocabSize: VocabSize,
                bosTokenId: innerInfo.BosTokenId,
                eosTokenId: innerInfo.EosTokenId,
                padTokenId: innerInfo.PadTokenId,
                unkTokenId: -1, // No UNK token - we use byte fallback
                supportsByteFallback: true
            );
        }

        /// <summary>
        /// Encode a string into token IDs with byte fallback.
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
        /// Encode UTF-8 bytes with byte fallback for unknown sequences.
        /// </summary>
        public int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
        {
            if (utf8.Length == 0)
                return 0;

            // Try encoding with inner tokenizer first
            int[] innerTokens = ArrayPool<int>.Shared.Rent(utf8.Length * 2);

            try
            {
                int innerCount = _innerTokenizer.Encode(utf8, innerTokens);

                // Check if we need byte fallback
                // If the inner tokenizer doesn't support byte fallback and has UNK tokens, OR
                // if it skipped characters (e.g., CharTokenizer), fall back to bytes
                int unkId = _innerTokenizer.Info.UnkTokenId;
                bool needsFallback = false;

                // Check for UNK tokens
                if (unkId >= 0)
                {
                    for (int i = 0; i < innerCount; i++)
                    {
                        if (innerTokens[i] == unkId)
                        {
                            needsFallback = true;
                            break;
                        }
                    }
                }

                // Also check if inner tokenizer might have skipped bytes
                // Decode and compare byte counts to detect loss
                if (!needsFallback && innerCount > 0)
                {
                    byte[] decoded = ArrayPool<byte>.Shared.Rent(utf8.Length * 2);
                    try
                    {
                        int decodedCount = _innerTokenizer.Decode(innerTokens.AsSpan(0, innerCount), decoded);
                        // If decoded bytes don't match input length, we lost information
                        if (decodedCount != utf8.Length)
                        {
                            needsFallback = true;
                        }
                        else
                        {
                            // Compare actual bytes
                            for (int i = 0; i < utf8.Length; i++)
                            {
                                if (decoded[i] != utf8[i])
                                {
                                    needsFallback = true;
                                    break;
                                }
                            }
                        }
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(decoded);
                    }
                }

                // If no fallback needed, use inner tokenizer result
                if (!needsFallback && innerCount > 0)
                {
                    int count = Math.Min(innerCount, tokensOut.Length);
                    for (int i = 0; i < count; i++)
                    {
                        tokensOut[i] = innerTokens[i];
                    }
                    return count;
                }

                // Otherwise, fall back to byte-level encoding
                return EncodeByteFallback(utf8, tokensOut);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(innerTokens);
            }
        }

        private int EncodeByteFallback(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
        {
            // Encode as byte tokens
            int count = Math.Min(utf8.Length, tokensOut.Length);
            for (int i = 0; i < count; i++)
            {
                tokensOut[i] = _byteToToken[utf8[i]];
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

            // Separate inner tokens from byte tokens
            List<int> innerTokens = new List<int>(16);
            int byteCount = 0;

            foreach (int tokenId in tokens)
            {
                if (tokenId >= _byteTokenStart && tokenId < VocabSize)
                {
                    // Byte token - decode directly
                    if (byteCount < utf8Out.Length)
                    {
                        // Flush any pending inner tokens first
                        if (innerTokens.Count > 0)
                        {
                            byteCount += DecodeInnerTokens(innerTokens, utf8Out.Slice(byteCount));
                            innerTokens.Clear();
                        }

                        utf8Out[byteCount++] = _tokenToByte[tokenId];
                    }
                }
                else
                {
                    // Inner token - accumulate
                    innerTokens.Add(tokenId);
                }
            }

            // Flush remaining inner tokens
            if (innerTokens.Count > 0)
            {
                byteCount += DecodeInnerTokens(innerTokens, utf8Out.Slice(byteCount));
            }

            return byteCount;
        }

        private int DecodeInnerTokens(List<int> tokens, Span<byte> utf8Out)
        {
            int[] tokenArray = ArrayPool<int>.Shared.Rent(tokens.Count);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(tokens.Count * 10);
            try
            {
                for (int i = 0; i < tokens.Count; i++)
                {
                    tokenArray[i] = tokens[i];
                }

                int count = _innerTokenizer.Decode(tokenArray.AsSpan(0, tokens.Count), buffer);
                int toCopy = Math.Min(count, utf8Out.Length);
                buffer.AsSpan(0, toCopy).CopyTo(utf8Out);
                return toCopy;
            }
            finally
            {
                ArrayPool<int>.Shared.Return(tokenArray);
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Decode token IDs to string.
        /// </summary>
        public string Decode(List<int> tokens)
        {
            int[] tokenArray = ArrayPool<int>.Shared.Rent(tokens.Count);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(tokens.Count * 4);
            try
            {
                for (int i = 0; i < tokens.Count; i++)
                {
                    tokenArray[i] = tokens[i];
                }

                int byteCount = Decode(tokenArray.AsSpan(0, tokens.Count), buffer);
                return Encoding.UTF8.GetString(buffer, 0, byteCount);
            }
            finally
            {
                ArrayPool<int>.Shared.Return(tokenArray);
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

        /// <summary>
        /// Fast-path decode for a single token ID. Avoids List allocation.
        /// </summary>
        internal string DecodeSingleToken(int tokenId)
        {
            if (tokenId >= _byteTokenStart && tokenId < VocabSize)
            {
                // Byte token - decode directly to single byte
                byte b = _tokenToByte[tokenId];
                Span<byte> bytes = stackalloc byte[1];
                bytes[0] = b;
                return Encoding.UTF8.GetString(bytes);
            }
            else
            {
                // Inner token - use inner tokenizer's decode
                Span<int> tokenSpan = stackalloc int[1];
                tokenSpan[0] = tokenId;

                byte[] buffer = ArrayPool<byte>.Shared.Rent(10);
                try
                {
                    int byteCount = _innerTokenizer.Decode(tokenSpan, buffer);
                    return Encoding.UTF8.GetString(buffer, 0, byteCount);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
    }
}

using System.Text;
using System.Text.RegularExpressions;

namespace SmallMind.Tokenizers.Text
{
    /// <summary>
    /// BPE tokenizer for models loaded from GGUF files.
    /// Supports both character-level and byte-level BPE (GPT-2 style).
    /// Extracts vocabulary and merges directly from GGUF metadata.
    /// </summary>
    internal class GgufBpeTokenizer : ITokenizer
    {
        private readonly Dictionary<string, int> _vocab;
        private readonly Dictionary<int, string> _inverseVocab;
        private readonly Dictionary<(string, string), int> _mergeRanks;
        private readonly bool _isByteLevelBpe;

        // Pre-tokenization regex (GPT-2 style): uses GeneratedRegex for optimal performance
        // Matches contractions, letters, numbers, punctuation, and whitespace sequences
        // Removed static readonly field - now using centralized RegexPatterns.Gpt2PreTokenize()

        // Byte-level BPE mapping (GPT-2 style)
        private readonly Dictionary<byte, string>? _byteToChar;
        private readonly Dictionary<string, byte>? _charToByte;

        public int VocabSize => _vocab.Count;
        public TokenizerInfo Info { get; }

        /// <summary>
        /// Creates a GGUF BPE tokenizer from in-memory vocabulary and merges.
        /// </summary>
        /// <param name="vocab">Vocabulary mapping tokens to IDs</param>
        /// <param name="merges">List of merge pairs in order of priority</param>
        /// <param name="bosTokenId">BOS token ID</param>
        /// <param name="eosTokenId">EOS token ID</param>
        /// <param name="unkTokenId">Unknown token ID</param>
        /// <param name="isByteLevelBpe">Whether to use byte-level BPE (GPT-2 style)</param>
        public GgufBpeTokenizer(
            Dictionary<string, int> vocab,
            List<(string, string)> merges,
            int bosTokenId = -1,
            int eosTokenId = -1,
            int unkTokenId = -1,
            bool isByteLevelBpe = false)
        {
            if (vocab == null || vocab.Count == 0)
                throw new ArgumentException("Vocabulary cannot be null or empty", nameof(vocab));
            if (merges == null)
                throw new ArgumentNullException(nameof(merges));

            _vocab = vocab;
            _isByteLevelBpe = isByteLevelBpe;

            // Build inverse vocabulary
            _inverseVocab = new Dictionary<int, string>(vocab.Count);
            foreach (var kvp in vocab)
            {
                _inverseVocab[kvp.Value] = kvp.Key;
            }

            // Build merge ranks
            _mergeRanks = new Dictionary<(string, string), int>();
            for (int i = 0; i < merges.Count; i++)
            {
                _mergeRanks[merges[i]] = i;
            }

            // Initialize byte-level mapping if needed
            if (_isByteLevelBpe)
            {
                _byteToChar = BuildByteToCharMap();
                _charToByte = BuildCharToByteMap(_byteToChar);
            }

            // Pre-tokenization regex now uses static field (no per-instance compilation)

            Info = new TokenizerInfo(
                name: "GgufBpeTokenizer",
                vocabSize: _vocab.Count,
                bosTokenId: bosTokenId,
                eosTokenId: eosTokenId,
                unkTokenId: unkTokenId,
                supportsByteFallback: _isByteLevelBpe
            );
        }

        /// <summary>
        /// Build GPT-2 style byte-to-Unicode mapping.
        /// Maps bytes 0-255 to Unicode characters in a reversible way.
        /// </summary>
        private Dictionary<byte, string> BuildByteToCharMap()
        {
            var map = new Dictionary<byte, string>();

            // Direct mappings for printable ASCII, except space
            var directBytes = new List<byte>();
            for (byte b = (byte)'!'; b <= (byte)'~'; b++)
            {
                directBytes.Add(b);
            }
            directBytes.Add((byte)'¡'); // Add ¡ as direct mapping
            for (byte b = (byte)'¢'; b <= (byte)'¬'; b++)
            {
                directBytes.Add(b);
            }
            for (byte b = (byte)'®'; b <= (byte)'ÿ'; b++)
            {
                directBytes.Add(b);
            }

            // Build the map
            int n = 0;
            for (int b = 0; b < 256; b++)
            {
                byte byteVal = (byte)b;
                if (directBytes.Contains(byteVal))
                {
                    map[byteVal] = ((char)byteVal).ToString();
                }
                else
                {
                    // Map to high Unicode range
                    map[byteVal] = ((char)(256 + n)).ToString();
                    n++;
                }
            }

            return map;
        }

        /// <summary>
        /// Build reverse mapping from Unicode characters to bytes.
        /// </summary>
        private Dictionary<string, byte> BuildCharToByteMap(Dictionary<byte, string> byteToChar)
        {
            var map = new Dictionary<string, byte>();
            foreach (var kvp in byteToChar)
            {
                map[kvp.Value] = kvp.Key;
            }
            return map;
        }

        /// <summary>
        /// Encode text into token IDs using BPE.
        /// </summary>
        public List<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<int>();
            }

            var result = new List<int>();

            // Pre-tokenize text using GeneratedRegex
            var matches = RegexPatterns.Gpt2PreTokenize().Matches(text);

            foreach (Match match in matches)
            {
                string word = match.Value;

                // Convert to byte-level tokens if needed
                List<string> tokens;
                if (_isByteLevelBpe && _byteToChar != null)
                {
                    // Byte-level: convert to bytes, then to special chars - optimized for loop
                    var bytes = Encoding.UTF8.GetBytes(word);
                    tokens = new List<string>(bytes.Length);
                    for (int i = 0; i < bytes.Length; i++)
                    {
                        tokens.Add(_byteToChar[bytes[i]]);
                    }
                }
                else
                {
                    // Character-level - optimized for loop
                    tokens = new List<string>(word.Length);
                    for (int i = 0; i < word.Length; i++)
                    {
                        tokens.Add(word[i].ToString());
                    }
                }

                // Apply BPE merges - O(N) forward-scan algorithm (no RemoveAt)
                // We alternate between tokens and tempTokens to avoid allocations
                List<string> currentTokens = tokens;
                List<string> tempTokens = new List<string>(tokens.Count);

                while (currentTokens.Count > 1)
                {
                    // Find the pair with the lowest merge rank
                    (string, string)? bestPair = null;
                    int bestRank = int.MaxValue;
                    int bestIndex = -1;

                    for (int i = 0; i < currentTokens.Count - 1; i++)
                    {
                        var pair = (currentTokens[i], currentTokens[i + 1]);
                        if (_mergeRanks.TryGetValue(pair, out int rank) && rank < bestRank)
                        {
                            bestPair = pair;
                            bestRank = rank;
                            bestIndex = i;
                        }
                    }

                    if (bestPair == null)
                        break;

                    // Apply the merge using forward scan (O(N) instead of O(N²))
                    tempTokens.Clear();
                    string merged = bestPair.Value.Item1 + bestPair.Value.Item2;

                    for (int i = 0; i < currentTokens.Count; i++)
                    {
                        if (i == bestIndex)
                        {
                            tempTokens.Add(merged);
                            i++; // Skip next token (it's part of the merge)
                        }
                        else
                        {
                            tempTokens.Add(currentTokens[i]);
                        }
                    }

                    // Swap buffers for next iteration
                    (currentTokens, tempTokens) = (tempTokens, currentTokens);
                }

                // Convert tokens to IDs (use currentTokens as it has final result)
                foreach (var token in currentTokens)
                {
                    if (_vocab.TryGetValue(token, out int id))
                    {
                        result.Add(id);
                    }
                    else if (Info.UnkTokenId >= 0)
                    {
                        result.Add(Info.UnkTokenId);
                    }
                    // else: skip unknown tokens without UNK
                }
            }

            // SmolLM2/Llama expects BOS — GGUF tokenizer doesn't auto-add
            if (Info.AddBos && result.Count > 0)
            {
                if (result[0] != Info.BosTokenId)
                {
                    result.Insert(0, Info.BosTokenId);
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
            var tokens = Encode(text);

            int count = Math.Min(tokens.Count, tokensOut.Length);
            for (int i = 0; i < count; i++)
            {
                tokensOut[i] = tokens[i];
            }
            return count;
        }

        /// <summary>
        /// Decode token IDs back to text.
        /// </summary>
        public string Decode(List<int> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return string.Empty;
            }

            // Skip BOS token at start if present
            int startIdx = 0;
            if (tokens.Count > 0 && tokens[0] == Info.BosTokenId)
            {
                startIdx = 1;
            }

            var sb = new StringBuilder();

            for (int i = startIdx; i < tokens.Count; i++)
            {
                int id = tokens[i];
                if (_inverseVocab.TryGetValue(id, out string? token))
                {
                    sb.Append(token);
                }
                // else: skip unknown token IDs
            }

            var decoded = sb.ToString();

            // If byte-level BPE, convert back from special chars to bytes
            if (_isByteLevelBpe && _charToByte != null)
            {
                var bytes = new List<byte>();
                foreach (char c in decoded)
                {
                    string charStr = c.ToString();
                    if (_charToByte.TryGetValue(charStr, out byte b))
                    {
                        bytes.Add(b);
                    }
                    else
                    {
                        // Fallback: try UTF-8 encoding
                        bytes.AddRange(Encoding.UTF8.GetBytes(charStr));
                    }
                }
                return Encoding.UTF8.GetString(bytes.ToArray());
            }

            return decoded;
        }

        /// <summary>
        /// Fast-path decode for a single token ID. Avoids List allocation.
        /// </summary>
        internal string DecodeSingleToken(int tokenId)
        {
            if (_inverseVocab.TryGetValue(tokenId, out string? token))
            {
                // If byte-level BPE, convert back from special chars to bytes
                if (_isByteLevelBpe && _charToByte != null)
                {
                    var bytes = new List<byte>();
                    foreach (char c in token)
                    {
                        string charStr = c.ToString();
                        if (_charToByte.TryGetValue(charStr, out byte b))
                        {
                            bytes.Add(b);
                        }
                        else
                        {
                            bytes.AddRange(Encoding.UTF8.GetBytes(charStr));
                        }
                    }
                    return Encoding.UTF8.GetString(bytes.ToArray());
                }
                return token;
            }
            // Return empty string for unknown token IDs (consistent with Decode behavior)
            return string.Empty;
        }

        /// <summary>
        /// Decode token IDs back into UTF-8 bytes (fast path).
        /// </summary>
        public int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out)
        {
            var tokenList = new List<int>(tokens.Length);
            for (int i = 0; i < tokens.Length; i++)
            {
                tokenList.Add(tokens[i]);
            }

            string text = Decode(tokenList);
            return Encoding.UTF8.GetBytes(text.AsSpan(), utf8Out);
        }

        /// <summary>
        /// Decode token IDs to string (convenience method).
        /// </summary>
        public string DecodeToString(ReadOnlySpan<int> tokens)
        {
            var tokenList = new List<int>(tokens.Length);
            for (int i = 0; i < tokens.Length; i++)
            {
                tokenList.Add(tokens[i]);
            }
            return Decode(tokenList);
        }
    }
}

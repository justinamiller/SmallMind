using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Byte Pair Encoding (BPE) tokenizer implementation.
    /// Loads vocabulary and merge rules from vocab.json and merges.txt files.
    /// </summary>
    internal class BpeTokenizer : ITokenizer
    {
        private readonly FrozenDictionary<string, int> _vocab;
        private readonly FrozenDictionary<int, string> _inverseVocab;
        private readonly List<(string, string)> _merges;
        private readonly FrozenDictionary<(string, string), int> _mergeRanks;
        
        // Pre-tokenization regex: shared across all instances for efficiency
        // Pattern matches sequences of letters, digits, or individual punctuation/whitespace
        private static readonly Regex PreTokenizeRegex = 
            new Regex(@"\w+|[^\w\s]|\s+", RegexOptions.Compiled);
        
        private const string UnknownToken = "[UNK]";
        private const string EndOfTextToken = "[EOT]";
        
        // Reusable buffers to reduce allocations during encoding
        private List<string>? _tokensBuffer;
        
        // Cache for single-character strings (ASCII range)
        private static readonly string[] _charStringCache = new string[128];
        
        static BpeTokenizer()
        {
            // Pre-populate char string cache for ASCII characters
            for (int i = 0; i < 128; i++)
            {
                _charStringCache[i] = ((char)i).ToString();
            }
        }

        public int VocabSize => _vocab.Count;
        
        public TokenizerInfo Info { get; }

        /// <summary>
        /// Creates a new BpeTokenizer by loading assets from the specified directory.
        /// </summary>
        /// <param name="assetsPath">Path to directory containing vocab.json and merges.txt</param>
        public BpeTokenizer(string assetsPath)
        {
            if (string.IsNullOrWhiteSpace(assetsPath))
            {
                throw new TokenizationException("Assets path cannot be null or empty.");
            }

            if (!Directory.Exists(assetsPath))
            {
                throw new TokenizationException(
                    $"Tokenizer assets directory not found: {assetsPath}\n" +
                    $"Expected directory containing vocab.json and merges.txt files.");
            }

            string vocabPath = Path.Combine(assetsPath, "vocab.json");
            string mergesPath = Path.Combine(assetsPath, "merges.txt");

            if (!File.Exists(vocabPath))
            {
                throw new TokenizationException(
                    $"Vocabulary file not found: {vocabPath}\n" +
                    $"Expected JSON file mapping tokens to IDs: {{\"token\": id, ...}}");
            }

            if (!File.Exists(mergesPath))
            {
                throw new TokenizationException(
                    $"Merges file not found: {mergesPath}\n" +
                    $"Expected text file with one merge pair per line: 'token1 token2'");
            }

            try
            {
                // Load vocabulary
                string vocabJson = File.ReadAllText(vocabPath);
                var vocabDict = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson)
                    ?? throw new TokenizationException($"Failed to parse vocab.json: file is empty or invalid");

                // Build inverse vocabulary
                var inverseDict = new Dictionary<int, string>(vocabDict.Count);
                foreach (var kvp in vocabDict)
                {
                    inverseDict[kvp.Value] = kvp.Key;
                }

                // Load merges
                _merges = new List<(string, string)>();
                var mergeDict = new Dictionary<(string, string), int>();
                
                string[] mergeLines = File.ReadAllLines(mergesPath);
                int rank = 0;
                foreach (string line in mergeLines)
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    {
                        continue; // Skip empty lines and comments
                    }

                    string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                    {
                        throw new TokenizationException(
                            $"Invalid merge line in {mergesPath}: '{line}'\n" +
                            $"Expected format: 'token1 token2'");
                    }

                    var mergePair = (parts[0], parts[1]);
                    _merges.Add(mergePair);
                    mergeDict[mergePair] = rank++;
                }

                // Convert to FrozenDictionary for faster lookups
                _vocab = vocabDict.ToFrozenDictionary();
                _inverseVocab = inverseDict.ToFrozenDictionary();
                _mergeRanks = mergeDict.ToFrozenDictionary();

                // Pre-tokenization regex now uses static field (no per-instance compilation)

                int eosId = _vocab.TryGetValue(EndOfTextToken, out int id) ? id : -1;
                int unkId = _vocab.TryGetValue(UnknownToken, out int id2) ? id2 : -1;
                
                Info = new TokenizerInfo(
                    name: "BpeTokenizer",
                    vocabSize: _vocab.Count,
                    eosTokenId: eosId,
                    unkTokenId: unkId,
                    supportsByteFallback: false
                );
            }
            catch (TokenizationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new TokenizationException(
                    $"Failed to load BPE tokenizer from {assetsPath}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Creates a new BpeTokenizer from in-memory vocabulary and merge rules.
        /// Used when loading from GGUF metadata.
        /// </summary>
        /// <param name="vocab">Dictionary mapping tokens to IDs</param>
        /// <param name="merges">List of merge pairs</param>
        /// <param name="bosTokenId">BOS token ID (or -1 if not present)</param>
        /// <param name="eosTokenId">EOS token ID (or -1 if not present)</param>
        /// <param name="unkTokenId">UNK token ID (or -1 if not present)</param>
        public BpeTokenizer(
            Dictionary<string, int> vocab, 
            List<(string, string)> merges,
            int bosTokenId = -1,
            int eosTokenId = -1,
            int unkTokenId = -1)
        {
            if (vocab == null || vocab.Count == 0)
                throw new ArgumentException("Vocabulary cannot be null or empty", nameof(vocab));
            if (merges == null)
                throw new ArgumentNullException(nameof(merges));

            // Build inverse vocabulary
            var inverseDict = new Dictionary<int, string>(vocab.Count);
            foreach (var kvp in vocab)
            {
                inverseDict[kvp.Value] = kvp.Key;
            }

            // Build merge ranks
            var mergeDict = new Dictionary<(string, string), int>();
            for (int i = 0; i < merges.Count; i++)
            {
                mergeDict[merges[i]] = i;
            }

            _vocab = vocab.ToFrozenDictionary();
            _inverseVocab = inverseDict.ToFrozenDictionary();
            _merges = merges;
            _mergeRanks = mergeDict.ToFrozenDictionary();

            // Pre-tokenization regex now uses static field (no per-instance compilation)

            Info = new TokenizerInfo(
                name: "BpeTokenizer",
                vocabSize: _vocab.Count,
                bosTokenId: bosTokenId,
                eosTokenId: eosTokenId,
                unkTokenId: unkTokenId,
                supportsByteFallback: false
            );
        }

        /// <summary>
        /// Encode text into a list of token IDs using BPE algorithm.
        /// </summary>
        public List<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<int>();
            }

            var result = new List<int>(text.Length / 3);

            // Pre-tokenize: split into words and punctuation
            var matches = PreTokenizeRegex.Matches(text);
            
            foreach (Match match in matches)
            {
                string word = match.Value;
                
                // Reuse tokens buffer to reduce allocations
                if (_tokensBuffer == null)
                {
                    _tokensBuffer = new List<string>(word.Length);
                }
                else
                {
                    _tokensBuffer.Clear();
                    if (_tokensBuffer.Capacity < word.Length)
                    {
                        _tokensBuffer.Capacity = word.Length;
                    }
                }
                
                // Convert word to character tokens - use cached strings for ASCII
                foreach (char c in word)
                {
                    string charStr;
                    if (c < 128)
                    {
                        charStr = _charStringCache[c];
                    }
                    else
                    {
                        charStr = c.ToString();
                    }
                    _tokensBuffer.Add(charStr);
                }

                // Apply BPE merges
                while (_tokensBuffer.Count > 1)
                {
                    // Find the pair with the lowest merge rank
                    (string, string)? bestPair = null;
                    int bestRank = int.MaxValue;
                    int bestIndex = -1;

                    for (int i = 0; i < _tokensBuffer.Count - 1; i++)
                    {
                        var pair = (_tokensBuffer[i], _tokensBuffer[i + 1]);
                        if (_mergeRanks.TryGetValue(pair, out int rank) && rank < bestRank)
                        {
                            bestPair = pair;
                            bestRank = rank;
                            bestIndex = i;
                        }
                    }

                    // If no merge found, break
                    if (bestPair == null)
                    {
                        break;
                    }

                    // Apply the merge - use string concatenation (these are already interned in vocab)
                    string merged = bestPair.Value.Item1 + bestPair.Value.Item2;
                    _tokensBuffer[bestIndex] = merged;
                    _tokensBuffer.RemoveAt(bestIndex + 1);
                }

                // Convert tokens to IDs
                foreach (string token in _tokensBuffer)
                {
                    if (_vocab.TryGetValue(token, out int id))
                    {
                        result.Add(id);
                    }
                    else if (_vocab.TryGetValue(UnknownToken, out int unkId))
                    {
                        result.Add(unkId);
#if DEBUG
                        Console.WriteLine($"Warning: Unknown token '{token}' replaced with [UNK]");
#endif
                    }
                    else
                    {
                        throw new TokenizationException(
                            $"Unknown token '{token}' and no [UNK] token in vocabulary.\n" +
                            $"Add '[UNK]' to vocab.json or ensure all input text uses known tokens.");
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Encode UTF-8 bytes into token IDs (fast path).
        /// </summary>
        public int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
        {
            // Decode UTF-8 to string first, then use existing logic
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
        /// Decode token IDs back into UTF-8 bytes (fast path).
        /// </summary>
        public int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out)
        {
            // Use existing Decode to get string, then encode to UTF-8
            var tokenList = new List<int>(tokens.Length);
            foreach (int token in tokens)
            {
                tokenList.Add(token);
            }
            
            string text = Decode(tokenList);
            int bytesWritten = Encoding.UTF8.GetBytes(text.AsSpan(), utf8Out);
            return bytesWritten;
        }

        /// <summary>
        /// Decode token IDs back to string (convenience).
        /// </summary>
        public string DecodeToString(ReadOnlySpan<int> tokens)
        {
            var tokenList = new List<int>(tokens.Length);
            foreach (int token in tokens)
            {
                tokenList.Add(token);
            }
            return Decode(tokenList);
        }

        /// <summary>
        /// Decode token IDs back into text.
        /// </summary>
        public string Decode(List<int> tokens)
        {
            if (tokens == null || tokens.Count == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();
            foreach (int id in tokens)
            {
                if (_inverseVocab.TryGetValue(id, out string? token))
                {
                    sb.Append(token);
                }
                else
                {
                    throw new TokenizationException(
                        $"Invalid token ID during decode: {id}\n" +
                        $"Valid token IDs range from 0 to {_vocab.Count - 1}");
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Fast-path decode for a single token ID. Avoids List allocation.
        /// Used internally in hot loops (e.g., constraint checking).
        /// </summary>
        internal string DecodeSingleToken(int tokenId)
        {
            if (_inverseVocab.TryGetValue(tokenId, out string? token))
            {
                return token;
            }
            else
            {
                throw new TokenizationException(
                    $"Invalid token ID during decode: {tokenId}\n" +
                    $"Valid token IDs range from 0 to {_vocab.Count - 1}");
            }
        }

        /// <summary>
        /// Gets the token ID for the end-of-text token.
        /// Returns -1 if not present in vocabulary.
        /// </summary>
        public int EndOfTextTokenId => _vocab.TryGetValue(EndOfTextToken, out int id) ? id : -1;

        /// <summary>
        /// Gets the token ID for the unknown token.
        /// Returns -1 if not present in vocabulary.
        /// </summary>
        public int UnknownTokenId => _vocab.TryGetValue(UnknownToken, out int id) ? id : -1;
    }
}

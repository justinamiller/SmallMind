using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SmallMind.Text
{
    /// <summary>
    /// Byte Pair Encoding (BPE) tokenizer implementation.
    /// Loads vocabulary and merge rules from vocab.json and merges.txt files.
    /// </summary>
    public class BpeTokenizer : ITokenizer
    {
        private readonly Dictionary<string, int> _vocab;
        private readonly Dictionary<int, string> _inverseVocab;
        private readonly List<(string, string)> _merges;
        private readonly Dictionary<(string, string), int> _mergeRanks;
        private readonly Regex _preTokenizeRegex;
        private const string UnknownToken = "[UNK]";
        private const string EndOfTextToken = "[EOT]";

        public int VocabSize => _vocab.Count;

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
                _vocab = JsonSerializer.Deserialize<Dictionary<string, int>>(vocabJson)
                    ?? throw new TokenizationException($"Failed to parse vocab.json: file is empty or invalid");

                // Build inverse vocabulary
                _inverseVocab = new Dictionary<int, string>(_vocab.Count);
                foreach (var kvp in _vocab)
                {
                    _inverseVocab[kvp.Value] = kvp.Key;
                }

                // Load merges
                _merges = new List<(string, string)>();
                _mergeRanks = new Dictionary<(string, string), int>();
                
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
                    _mergeRanks[mergePair] = rank++;
                }

                // Pre-tokenization regex: split on whitespace and punctuation boundaries
                // This pattern matches sequences of letters, digits, or individual punctuation/whitespace
                _preTokenizeRegex = new Regex(@"\w+|[^\w\s]|\s+", RegexOptions.Compiled);

                Console.WriteLine($"BpeTokenizer: Loaded {_vocab.Count} tokens and {_merges.Count} merge rules from {assetsPath}");
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
        /// Encode text into a list of token IDs using BPE algorithm.
        /// </summary>
        public List<int> Encode(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new List<int>();
            }

            var result = new List<int>();

            // Pre-tokenize: split into words and punctuation
            var matches = _preTokenizeRegex.Matches(text);
            
            foreach (Match match in matches)
            {
                string word = match.Value;
                
                // Convert word to character tokens
                var tokens = new List<string>();
                foreach (char c in word)
                {
                    tokens.Add(c.ToString());
                }

                // Apply BPE merges
                while (tokens.Count > 1)
                {
                    // Find the pair with the lowest merge rank
                    (string, string)? bestPair = null;
                    int bestRank = int.MaxValue;
                    int bestIndex = -1;

                    for (int i = 0; i < tokens.Count - 1; i++)
                    {
                        var pair = (tokens[i], tokens[i + 1]);
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

                    // Apply the merge
                    tokens[bestIndex] = bestPair.Value.Item1 + bestPair.Value.Item2;
                    tokens.RemoveAt(bestIndex + 1);
                }

                // Convert tokens to IDs
                foreach (string token in tokens)
                {
                    if (_vocab.TryGetValue(token, out int id))
                    {
                        result.Add(id);
                    }
                    else if (_vocab.TryGetValue(UnknownToken, out int unkId))
                    {
                        result.Add(unkId);
                        Console.WriteLine($"Warning: Unknown token '{token}' replaced with [UNK]");
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

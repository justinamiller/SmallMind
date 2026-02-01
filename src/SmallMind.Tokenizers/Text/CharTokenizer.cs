using System;
using System.Collections.Generic;
using System.Text;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Character-level tokenizer. Builds a vocabulary from the training text
    /// and provides encode/decode methods to convert between strings and token IDs.
    /// This is the default tokenizer and works with any text without external assets.
    /// </summary>
    public class CharTokenizer : ITokenizer
    {
        private readonly Dictionary<char, int> _charToIdx;
        private readonly Dictionary<int, char> _idxToChar;

        public int VocabSize { get; }

        /// <summary>
        /// Creates a new CharTokenizer from the given training text.
        /// Builds a vocabulary from all unique characters in the text.
        /// </summary>
        /// <param name="text">Training text to extract vocabulary from</param>
        public CharTokenizer(string text)
        {
            // Build vocabulary from unique characters in the text
            var charSet = new HashSet<char>();
            for (int i = 0; i < text.Length; i++)
            {
                charSet.Add(text[i]);
            }
            
            // Convert to sorted array
            var chars = new char[charSet.Count];
            charSet.CopyTo(chars);
            Array.Sort(chars);
            
            // Pre-size dictionaries with exact capacity to avoid rehashing
            _charToIdx = new Dictionary<char, int>(chars.Length);
            _idxToChar = new Dictionary<int, char>(chars.Length);

            for (int i = 0; i < chars.Length; i++)
            {
                _charToIdx[chars[i]] = i;
                _idxToChar[i] = chars[i];
            }

            VocabSize = chars.Length;
            Console.WriteLine($"CharTokenizer: Vocabulary built with {VocabSize} unique characters");
        }

        /// <summary>
        /// Encode a string into a list of token IDs.
        /// Pre-sized list to reduce allocations.
        /// </summary>
        public List<int> Encode(string text)
        {
            // Pre-size to text length (upper bound)
            var result = new List<int>(text.Length);
            foreach (var ch in text)
            {
                if (_charToIdx.TryGetValue(ch, out int idx))
                {
                    result.Add(idx);
                }
                else
                {
                    // Unknown character - skip it
                    Console.WriteLine($"Warning: Unknown character '{ch}' skipped during encoding");
                }
            }
            return result;
        }

        /// <summary>
        /// Decode a list of token IDs back into a string.
        /// </summary>
        public string Decode(List<int> tokens)
        {
            // Pre-size StringBuilder to tokens.Count (1:1 ratio for character-level tokenization)
            // For character-level tokenizer, each token maps to exactly one character
            var sb = new StringBuilder(tokens.Count);
            foreach (var idx in tokens)
            {
                if (_idxToChar.TryGetValue(idx, out char ch))
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }
    }
}

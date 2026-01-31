using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TinyLLM.Text
{
    /// <summary>
    /// Character-level tokenizer. Builds a vocabulary from the training text
    /// and provides encode/decode methods to convert between strings and token IDs.
    /// </summary>
    public class Tokenizer
    {
        private readonly Dictionary<char, int> _charToIdx;
        private readonly Dictionary<int, char> _idxToChar;

        public int VocabSize { get; }

        public Tokenizer(string text)
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
            
            _charToIdx = new Dictionary<char, int>();
            _idxToChar = new Dictionary<int, char>();

            for (int i = 0; i < chars.Length; i++)
            {
                _charToIdx[chars[i]] = i;
                _idxToChar[i] = chars[i];
            }

            VocabSize = chars.Length;
            Console.WriteLine($"Vocabulary built: {VocabSize} unique characters");
        }

        /// <summary>
        /// Encode a string into a list of token IDs.
        /// </summary>
        public List<int> Encode(string text)
        {
            var result = new List<int>();
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
            var sb = new StringBuilder();
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

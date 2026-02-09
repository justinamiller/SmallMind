using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Character-level tokenizer. Builds a vocabulary from the training text
    /// and provides encode/decode methods to convert between strings and token IDs.
    /// This is the default tokenizer and works with any text without external assets.
    /// </summary>
    internal class CharTokenizer : ITokenizer
    {
        private readonly FrozenDictionary<char, int> _charToIdx;
        private readonly FrozenDictionary<int, char> _idxToChar;

        public int VocabSize { get; }
        
        public TokenizerInfo Info { get; }

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
            var charToIdxDict = new Dictionary<char, int>(chars.Length);
            var idxToCharDict = new Dictionary<int, char>(chars.Length);

            for (int i = 0; i < chars.Length; i++)
            {
                charToIdxDict[chars[i]] = i;
                idxToCharDict[i] = chars[i];
            }

            // Convert to FrozenDictionary for faster lookups
            _charToIdx = charToIdxDict.ToFrozenDictionary();
            _idxToChar = idxToCharDict.ToFrozenDictionary();

            VocabSize = chars.Length;
            Info = new TokenizerInfo(
                name: "CharTokenizer",
                vocabSize: VocabSize,
                supportsByteFallback: false
            );
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
        /// Encode UTF-8 bytes into token IDs (fast path).
        /// </summary>
        public int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut)
        {
            // Decode UTF-8 to string first (CharTokenizer operates on chars)
            string text = Encoding.UTF8.GetString(utf8);
            int count = 0;
            
            foreach (char ch in text)
            {
                if (count >= tokensOut.Length)
                    break;
                    
                if (_charToIdx.TryGetValue(ch, out int idx))
                {
                    tokensOut[count++] = idx;
                }
            }
            
            return count;
        }

        /// <summary>
        /// Decode token IDs back into UTF-8 bytes (fast path).
        /// </summary>
        public int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out)
        {
            // Build string first, then encode to UTF-8
            // For small buffers, use stack allocation
            int maxChars = tokens.Length;
            char[]? rentedArray = null;
            Span<char> chars = maxChars <= 256 
                ? stackalloc char[maxChars] 
                : (rentedArray = ArrayPool<char>.Shared.Rent(maxChars)).AsSpan(0, maxChars);
            
            try
            {
                int charCount = 0;
                foreach (int idx in tokens)
                {
                    if (_idxToChar.TryGetValue(idx, out char ch))
                    {
                        chars[charCount++] = ch;
                    }
                }
                
                int bytesWritten = Encoding.UTF8.GetBytes(chars.Slice(0, charCount), utf8Out);
                return bytesWritten;
            }
            finally
            {
                if (rentedArray != null)
                    ArrayPool<char>.Shared.Return(rentedArray);
            }
        }

        /// <summary>
        /// Decode token IDs back to string (convenience).
        /// </summary>
        public string DecodeToString(ReadOnlySpan<int> tokens)
        {
            var sb = new StringBuilder(tokens.Length);
            foreach (int idx in tokens)
            {
                if (_idxToChar.TryGetValue(idx, out char ch))
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
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

        /// <summary>
        /// Fast-path decode for a single token ID. Avoids List allocation.
        /// </summary>
        internal string DecodeSingleToken(int tokenId)
        {
            if (_idxToChar.TryGetValue(tokenId, out char ch))
            {
                return ch.ToString();
            }
            return string.Empty;
        }
    }
}

using System.Collections.Generic;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Interface for tokenizer implementations.
    /// Converts between text and token IDs.
    /// </summary>
    public interface ITokenizer
    {
        /// <summary>
        /// The size of the vocabulary.
        /// </summary>
        int VocabSize { get; }

        /// <summary>
        /// Encode a string into a list of token IDs.
        /// </summary>
        /// <param name="text">Text to encode</param>
        /// <returns>List of token IDs</returns>
        List<int> Encode(string text);

        /// <summary>
        /// Decode a list of token IDs back into a string.
        /// </summary>
        /// <param name="tokens">Token IDs to decode</param>
        /// <returns>Decoded text</returns>
        string Decode(List<int> tokens);
    }
}

using System;
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
        /// Gets metadata about this tokenizer.
        /// </summary>
        TokenizerInfo Info { get; }

        /// <summary>
        /// Encode a string into a list of token IDs (convenience method, may allocate).
        /// </summary>
        /// <param name="text">Text to encode</param>
        /// <returns>List of token IDs</returns>
        List<int> Encode(string text);

        /// <summary>
        /// Encode UTF-8 bytes into token IDs (fast path, minimal allocations).
        /// </summary>
        /// <param name="utf8">UTF-8 encoded bytes to tokenize</param>
        /// <param name="tokensOut">Output buffer for token IDs</param>
        /// <returns>Number of tokens written to tokensOut</returns>
        int Encode(ReadOnlySpan<byte> utf8, Span<int> tokensOut);

        /// <summary>
        /// Decode token IDs back into UTF-8 bytes (fast path, minimal allocations).
        /// </summary>
        /// <param name="tokens">Token IDs to decode</param>
        /// <param name="utf8Out">Output buffer for UTF-8 bytes</param>
        /// <returns>Number of bytes written to utf8Out</returns>
        int Decode(ReadOnlySpan<int> tokens, Span<byte> utf8Out);

        /// <summary>
        /// Decode a list of token IDs back into a string (convenience method).
        /// </summary>
        /// <param name="tokens">Token IDs to decode</param>
        /// <returns>Decoded text</returns>
        string Decode(List<int> tokens);

        /// <summary>
        /// Decode token IDs back into a string (convenience method).
        /// </summary>
        /// <param name="tokens">Token IDs to decode</param>
        /// <returns>Decoded text</returns>
        string DecodeToString(ReadOnlySpan<int> tokens);
    }
}

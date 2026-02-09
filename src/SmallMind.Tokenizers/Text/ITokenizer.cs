using System;
using System.Collections.Generic;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Interface for tokenizer implementations.
    /// Converts between text and token IDs.
    /// </summary>
    internal interface ITokenizer
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
        /// ID of the padding token, or -1 if not supported.
        /// </summary>
        int PadTokenId => Info.PadTokenId;

        /// <summary>
        /// ID of the beginning-of-sequence token, or -1 if not supported.
        /// </summary>
        int BosTokenId => Info.BosTokenId;

        /// <summary>
        /// ID of the end-of-sequence token, or -1 if not supported.
        /// </summary>
        int EosTokenId => Info.EosTokenId;

        /// <summary>
        /// ID of the unknown token, or -1 if not supported.
        /// </summary>
        int UnkTokenId => Info.UnkTokenId;

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

        /// <summary>
        /// Save tokenizer state to a file (if supported).
        /// </summary>
        /// <param name="path">Path to save the tokenizer state</param>
        void Save(string path)
        {
            throw new NotSupportedException($"Save operation not supported by {GetType().Name}");
        }
    }
}

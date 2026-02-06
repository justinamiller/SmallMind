using System.Collections.Frozen;

namespace SmallMind.Tokenizers;

/// <summary>
/// Helper utilities for tokenizer implementations to reduce code duplication.
/// </summary>
internal static class TokenizerHelper
{
    /// <summary>
    /// Resolves a special token to its ID from the vocabulary.
    /// Returns -1 if the token is null or not found in the vocabulary.
    /// </summary>
    /// <param name="specialToken">The special token string (can be null).</param>
    /// <param name="vocab">The vocabulary dictionary mapping tokens to IDs.</param>
    /// <returns>The token ID if found, otherwise -1.</returns>
    public static int ResolveSpecialToken(string? specialToken, IReadOnlyDictionary<string, int> vocab)
    {
        if (specialToken != null && vocab.TryGetValue(specialToken, out int tokenId))
        {
            return tokenId;
        }
        return -1;
    }
}

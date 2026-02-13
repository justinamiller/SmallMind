using System.Text.RegularExpressions;

namespace SmallMind.Tokenizers
{
    /// <summary>
    /// Centralized regex patterns using source-generated regex for optimal performance.
    /// GeneratedRegex provides compile-time regex generation, eliminating runtime compilation overhead.
    /// Available in .NET 7+ (C# 11+).
    /// </summary>
    internal static partial class RegexPatterns
    {
        /// <summary>
        /// BPE pre-tokenization pattern: matches word sequences, punctuation, and whitespace.
        /// Pattern: \w+ (word chars) | [^\w\s] (non-word, non-space) | \s+ (whitespace)
        /// </summary>
        [GeneratedRegex(@"\w+|[^\w\s]|\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
        internal static partial Regex BpePreTokenize();

        /// <summary>
        /// GPT-2 style pre-tokenization pattern with contractions and Unicode categories.
        /// Matches: contractions ('s, 't, etc.), letters with optional space, numbers, punctuation, whitespace.
        /// Pattern handles: 's|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+
        /// Simplified from original to remove redundant alternations.
        /// </summary>
        [GeneratedRegex(@"'s|'t|'re|'ve|'m|'ll|'d| ?\p{L}+| ?\p{N}+| ?[^\s\p{L}\p{N}]+|\s+",
                        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
        internal static partial Regex Gpt2PreTokenize();
    }
}

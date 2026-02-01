using System;
using System.Collections.Generic;
using System.Text;

namespace SmallMind.Rag.Indexing.Sparse;

/// <summary>
/// Static tokenizer for text processing in RAG indexing and retrieval.
/// Implements high-performance tokenization with zero-allocation scanning.
/// </summary>
public static class RagTokenizer
{
    /// <summary>
    /// Tokenizes text into a list of lowercase alphanumeric tokens.
    /// Minimum token length is 2 characters. Punctuation is ignored.
    /// </summary>
    /// <param name="text">The input text to tokenize.</param>
    /// <returns>A list of lowercase tokens extracted from the text.</returns>
    public static List<string> Tokenize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<string>();

        return Tokenize(text.AsSpan());
    }

    /// <summary>
    /// Tokenizes text from a span into a list of lowercase alphanumeric tokens.
    /// Minimum token length is 2 characters. Punctuation is ignored.
    /// Uses ReadOnlySpan to avoid string allocations during scanning.
    /// </summary>
    /// <param name="text">The input text span to tokenize.</param>
    /// <returns>A list of lowercase tokens extracted from the text.</returns>
    public static List<string> Tokenize(ReadOnlySpan<char> text)
    {
        var tokens = new List<string>();
        
        if (text.Length == 0)
            return tokens;

        int length = text.Length;
        int tokenStart = -1;
        
        for (int i = 0; i < length; i++)
        {
            char c = text[i];
            
            if (char.IsLetterOrDigit(c))
            {
                if (tokenStart == -1)
                    tokenStart = i;
            }
            else
            {
                if (tokenStart != -1)
                {
                    int tokenLength = i - tokenStart;
                    if (tokenLength >= 2)
                    {
                        AddLowercaseToken(text.Slice(tokenStart, tokenLength), tokens);
                    }
                    tokenStart = -1;
                }
            }
        }

        // Handle final token if text ends with alphanumeric
        if (tokenStart != -1)
        {
            int tokenLength = length - tokenStart;
            if (tokenLength >= 2)
            {
                AddLowercaseToken(text.Slice(tokenStart, tokenLength), tokens);
            }
        }

        return tokens;
    }

    /// <summary>
    /// Adds a lowercase version of the token to the list.
    /// Converts each character to lowercase invariant culture.
    /// </summary>
    private static void AddLowercaseToken(ReadOnlySpan<char> tokenSpan, List<string> tokens)
    {
        // Use stack allocation for small tokens, heap for large
        Span<char> lower = tokenSpan.Length <= 128 
            ? stackalloc char[tokenSpan.Length] 
            : new char[tokenSpan.Length];

        for (int i = 0; i < tokenSpan.Length; i++)
        {
            lower[i] = char.ToLowerInvariant(tokenSpan[i]);
        }

        tokens.Add(new string(lower));
    }
}

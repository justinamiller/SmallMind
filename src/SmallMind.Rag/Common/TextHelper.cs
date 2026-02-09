namespace SmallMind.Rag.Common;

/// <summary>
/// Text manipulation utilities for RAG operations.
/// </summary>
internal static class TextHelper
{
    /// <summary>
    /// Truncates text to a maximum length and appends ellipsis if truncated.
    /// </summary>
    /// <param name="text">The text to truncate. Can be null or empty.</param>
    /// <param name="maxLength">Maximum length before truncation.</param>
    /// <returns>Original text if within limit, empty string if null/empty, otherwise truncated text with "..." appended.</returns>
    public static string TruncateWithEllipsis(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength) + "...";
    }

    /// <summary>
    /// Normalizes text by trimming and collapsing consecutive whitespace into single spaces.
    /// Useful for preparing text for embedding or comparison.
    /// </summary>
    /// <param name="text">The text to normalize. Can be null or whitespace.</param>
    /// <returns>Normalized text with single spaces, or empty string if input is null/whitespace.</returns>
    public static string NormalizeWhitespace(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        // Use StringBuilder for efficient string manipulation
        var sb = new System.Text.StringBuilder(text.Length);
        bool lastWasWhitespace = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasWhitespace)
                {
                    sb.Append(' ');
                    lastWasWhitespace = true;
                }
            }
            else
            {
                sb.Append(c);
                lastWasWhitespace = false;
            }
        }

        // Trim trailing whitespace if added
        if (sb.Length > 0 && sb[sb.Length - 1] == ' ')
            sb.Length--;

        return sb.ToString();
    }
}

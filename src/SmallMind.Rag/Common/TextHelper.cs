namespace SmallMind.Rag.Common;

/// <summary>
/// Text manipulation utilities for RAG operations.
/// </summary>
public static class TextHelper
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
}

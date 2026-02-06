namespace SmallMind.Rag.Common;

/// <summary>
/// Constants for retrieval operations to avoid magic numbers.
/// </summary>
public static class RetrievalConstants
{
    /// <summary>
    /// Maximum length for text excerpts before truncation.
    /// Used to generate preview snippets of retrieved chunks.
    /// </summary>
    public const int MaxExcerptLength = 200;
}

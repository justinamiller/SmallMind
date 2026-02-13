using System.Text;

namespace SmallMind.Rag.Prompting;

/// <summary>
/// Static utility class for formatting citations in RAG responses.
/// Provides consistent citation formatting for source references.
/// </summary>
internal static class CitationFormatter
{
    /// <summary>
    /// Formats a single citation with index, title, source URI, and character offsets.
    /// </summary>
    /// <param name="index">The citation index number (1-based).</param>
    /// <param name="title">The document title.</param>
    /// <param name="sourceUri">The source URI or file path.</param>
    /// <param name="charStart">The starting character offset in the source document.</param>
    /// <param name="charEnd">The ending character offset in the source document.</param>
    /// <returns>A formatted citation string in the format: [S{index}] {title} — {sourceUri} — chars {charStart}-{charEnd}</returns>
    public static string FormatCitation(int index, string title, string sourceUri, int charStart, int charEnd)
    {
        if (title == null) throw new ArgumentNullException(nameof(title));
        if (sourceUri == null) throw new ArgumentNullException(nameof(sourceUri));

        return $"[S{index}] {title} — {sourceUri} — chars {charStart}-{charEnd}";
    }

    /// <summary>
    /// Formats a list of citations with each citation on a separate line.
    /// </summary>
    /// <param name="citations">The list of citation tuples containing index, title, sourceUri, charStart, and charEnd.</param>
    /// <returns>A formatted string containing all citations, one per line.</returns>
    public static string FormatCitations(List<(int index, string title, string sourceUri, int charStart, int charEnd)> citations)
    {
        if (citations == null) throw new ArgumentNullException(nameof(citations));
        if (citations.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < citations.Count; i++)
        {
            var citation = citations[i];
            sb.Append(FormatCitation(citation.index, citation.title, citation.sourceUri, citation.charStart, citation.charEnd));

            if (i < citations.Count - 1)
            {
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Extracts citation references (e.g., [S1], [S2]) from an answer text.
    /// Uses simple string scanning without regular expressions.
    /// </summary>
    /// <param name="answer">The answer text to parse for citation references.</param>
    /// <returns>A list of citation numbers found in the answer (e.g., "1", "2", "3").</returns>
    public static List<string> ExtractCitationRefs(string answer)
    {
        if (string.IsNullOrEmpty(answer))
            return new List<string>();

        var refs = new List<string>();
        int i = 0;
        int length = answer.Length;

        while (i < length)
        {
            // Look for '[S'
            if (answer[i] == '[' && i + 2 < length && answer[i + 1] == 'S')
            {
                // Found potential citation start
                int start = i + 2;
                int end = start;

                // Collect digits
                while (end < length && char.IsDigit(answer[end]))
                {
                    end++;
                }

                // Check for closing bracket
                if (end < length && answer[end] == ']' && end > start)
                {
                    string citationNum = answer.Substring(start, end - start);

                    // Avoid duplicates
                    bool found = false;
                    for (int j = 0; j < refs.Count; j++)
                    {
                        if (refs[j] == citationNum)
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        refs.Add(citationNum);
                    }

                    i = end + 1;
                    continue;
                }
            }

            i++;
        }

        return refs;
    }
}

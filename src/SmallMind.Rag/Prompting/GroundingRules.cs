using System.Text;

namespace SmallMind.Rag.Prompting;

/// <summary>
/// Enforces grounding discipline for RAG systems to ensure answers are based on evidence.
/// Provides utilities for checking evidence sufficiency and generating appropriate responses.
/// </summary>
internal static class GroundingRules
{
    private static readonly string[] StopWords = new[]
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "for", "from",
        "has", "he", "in", "is", "it", "its", "of", "on", "that", "the",
        "to", "was", "will", "with", "what", "when", "where", "who", "how",
        "can", "could", "would", "should", "may", "might", "must", "shall",
        "do", "does", "did", "have", "had", "been", "being", "am", "were"
    };

    /// <summary>
    /// Checks if the retrieved chunks provide sufficient evidence to answer the question.
    /// </summary>
    /// <param name="question">The user's question.</param>
    /// <param name="chunks">The list of retrieved chunks.</param>
    /// <param name="minScoreThreshold">The minimum score threshold for sufficient evidence (default: 0.5).</param>
    /// <returns>True if the top chunk score is above the threshold and evidence is sufficient; otherwise, false.</returns>
    public static bool HasSufficientEvidence(string question, List<RetrievedChunk> chunks, float minScoreThreshold = 0.5f)
    {
        if (question == null) throw new ArgumentNullException(nameof(question));
        if (chunks == null) throw new ArgumentNullException(nameof(chunks));

        if (chunks.Count == 0)
            return false;

        // Check if the top chunk score exceeds threshold
        return chunks[0].Score >= minScoreThreshold;
    }

    /// <summary>
    /// Generates a response indicating insufficient evidence to answer the question.
    /// Includes suggested actions based on keywords extracted from the question.
    /// </summary>
    /// <param name="question">The user's question that lacks sufficient evidence.</param>
    /// <returns>A formatted response indicating insufficient evidence and suggested actions.</returns>
    public static string GenerateInsufficientEvidenceResponse(string question)
    {
        if (question == null) throw new ArgumentNullException(nameof(question));

        var keywords = ExtractKeywords(question);
        var sb = new StringBuilder();

        sb.AppendLine("I don't have sufficient evidence in the index to answer this question.");
        sb.AppendLine();
        sb.AppendLine("Suggested actions:");

        if (keywords.Count > 0)
        {
            sb.Append("- Add more documents about ");
            for (int i = 0; i < keywords.Count; i++)
            {
                sb.Append(keywords[i]);
                if (i < keywords.Count - 1)
                {
                    sb.Append(", ");
                }
            }
            sb.AppendLine();
        }

        sb.AppendLine("- Try rephrasing your question");
        sb.Append("- Check if the relevant documents are indexed");

        return sb.ToString();
    }

    /// <summary>
    /// Extracts keywords from a question by removing stop words.
    /// Uses simple tokenization and filtering without external dependencies.
    /// </summary>
    /// <param name="question">The question text to extract keywords from.</param>
    /// <returns>A list of potential keywords for follow-up suggestions.</returns>
    public static List<string> ExtractKeywords(string question)
    {
        if (string.IsNullOrEmpty(question))
            return new List<string>();

        var keywords = new List<string>();
        var tokens = Indexing.Sparse.RagTokenizer.Tokenize(question);

        for (int i = 0; i < tokens.Count; i++)
        {
            string token = tokens[i];

            // Check if token is a stop word
            bool isStopWord = false;
            for (int j = 0; j < StopWords.Length; j++)
            {
                if (token == StopWords[j])
                {
                    isStopWord = true;
                    break;
                }
            }

            if (!isStopWord)
            {
                keywords.Add(token);
            }
        }

        return keywords;
    }

    /// <summary>
    /// Suggests follow-up questions based on retrieved chunks.
    /// Generates simple heuristic-based questions using keywords from chunk content.
    /// </summary>
    /// <param name="question">The original question.</param>
    /// <param name="chunks">The list of retrieved chunks.</param>
    /// <param name="chunkStore">The dictionary of chunk IDs to chunk objects.</param>
    /// <returns>A list of 2-3 suggested follow-up questions.</returns>
    public static List<string> SuggestFollowUpQuestions(
        string question,
        List<RetrievedChunk> chunks,
        Dictionary<string, Chunk> chunkStore)
    {
        if (question == null) throw new ArgumentNullException(nameof(question));
        if (chunks == null) throw new ArgumentNullException(nameof(chunks));
        if (chunkStore == null) throw new ArgumentNullException(nameof(chunkStore));

        var suggestions = new List<string>();

        // Generate up to 3 follow-up questions
        int maxSuggestions = Math.Min(3, chunks.Count);
        var usedKeywords = new List<string>();

        for (int i = 0; i < maxSuggestions; i++)
        {
            var retrieved = chunks[i];

            if (!chunkStore.TryGetValue(retrieved.ChunkId, out var chunk))
                continue;

            // Extract keywords from chunk text
            var keywords = ExtractKeywords(chunk.Text);

            // Find a keyword we haven't used yet
            string? selectedKeyword = null;
            for (int j = 0; j < keywords.Count; j++)
            {
                string keyword = keywords[j];
                bool alreadyUsed = false;

                for (int k = 0; k < usedKeywords.Count; k++)
                {
                    if (usedKeywords[k] == keyword)
                    {
                        alreadyUsed = true;
                        break;
                    }
                }

                if (!alreadyUsed)
                {
                    selectedKeyword = keyword;
                    usedKeywords.Add(keyword);
                    break;
                }
            }

            if (selectedKeyword != null)
            {
                suggestions.Add($"What about {selectedKeyword}?");
            }
        }

        return suggestions;
    }
}

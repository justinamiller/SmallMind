using System;
using System.Collections.Generic;
using System.Text;

namespace SmallMind.Rag.Prompting;

/// <summary>
/// Composes RAG prompts with source citations and grounding instructions.
/// Builds prompts that enforce citation discipline and evidence-based responses.
/// </summary>
internal sealed class PromptComposer
{
    private readonly RagOptions.RetrievalOptions _options;
    private const int MaxContextTokens = 2048;
    private const int CharsPerToken = 4;

    /// <summary>
    /// Initializes a new instance of the <see cref="PromptComposer"/> class.
    /// </summary>
    /// <param name="options">The retrieval options containing configuration for prompt composition.</param>
    public PromptComposer(RagOptions.RetrievalOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Composes a RAG prompt with the question, sources, and instructions for citation discipline.
    /// </summary>
    /// <param name="question">The user's question to answer.</param>
    /// <param name="chunks">The list of retrieved chunks ranked by relevance.</param>
    /// <param name="chunkStore">The dictionary mapping chunk IDs to chunk objects.</param>
    /// <returns>A formatted prompt string ready for the language model.</returns>
    public string ComposePrompt(
        string question,
        List<RetrievedChunk> chunks,
        Dictionary<string, Chunk> chunkStore)
    {
        if (question == null) throw new ArgumentNullException(nameof(question));
        if (chunks == null) throw new ArgumentNullException(nameof(chunks));
        if (chunkStore == null) throw new ArgumentNullException(nameof(chunkStore));

        var sb = new StringBuilder();

        // System instruction
        sb.AppendLine("SYSTEM:");
        sb.AppendLine("Answer ONLY using the provided sources. If the sources don't contain enough information, say \"I don't have sufficient evidence to answer this question.\"");
        sb.AppendLine();

        // User question
        sb.Append("USER QUESTION: ");
        sb.AppendLine(question);
        sb.AppendLine();

        // Sources section
        sb.AppendLine("SOURCES:");

        // Calculate budget
        int maxChars = MaxContextTokens * CharsPerToken;
        int currentChars = sb.Length;

        // Add sources until budget is exceeded
        int sourceIndex = 1;
        for (int i = 0; i < chunks.Count; i++)
        {
            var retrieved = chunks[i];
            
            if (!chunkStore.TryGetValue(retrieved.ChunkId, out var chunk))
                continue;

            string sourceText = FormatSource(sourceIndex, retrieved, chunk);
            int sourceLength = sourceText.Length;

            // Check if adding this source would exceed budget
            if (currentChars + sourceLength > maxChars)
                break;

            sb.Append(sourceText);
            sb.AppendLine();
            
            currentChars += sourceLength + 1; // +1 for newline
            sourceIndex++;
        }

        // Instructions
        sb.AppendLine();
        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("- Answer the question using ONLY information from the sources above");
        sb.AppendLine("- Cite your sources using [S1], [S2], etc. in your answer");
        sb.AppendLine("- If the sources don't support an answer, state that clearly");
        sb.Append("- Suggest follow-up questions or documents that might help");

        return sb.ToString();
    }

    /// <summary>
    /// Formats a single source with citation index, metadata, and content.
    /// </summary>
    /// <param name="index">The source index number (1-based).</param>
    /// <param name="retrieved">The retrieved chunk with scoring information.</param>
    /// <param name="chunk">The full chunk object containing text and metadata.</param>
    /// <returns>A formatted source string with citation header and content.</returns>
    private string FormatSource(int index, RetrievedChunk retrieved, Chunk chunk)
    {
        var sb = new StringBuilder();

        // Citation header
        string citation = CitationFormatter.FormatCitation(
            index,
            chunk.Title,
            chunk.SourceUri,
            chunk.CharStart,
            chunk.CharEnd);

        sb.AppendLine(citation);
        sb.Append(chunk.Text);

        return sb.ToString();
    }
}

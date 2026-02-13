namespace SmallMind.Rag.Ingestion;

/// <summary>
/// Handles intelligent text chunking for document ingestion into the RAG system.
/// Implements markdown-aware splitting with fallback to character window chunking.
/// </summary>
internal sealed class Chunker
{
    /// <summary>
    /// Chunks a document into smaller text segments suitable for embedding and retrieval.
    /// Uses markdown-aware splitting when possible, with fallback to window-based chunking.
    /// </summary>
    /// <param name="doc">The document record to chunk.</param>
    /// <param name="content">The full text content of the document.</param>
    /// <param name="options">Chunking configuration options.</param>
    /// <returns>A list of chunks extracted from the document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when doc, content, or options is null.</exception>
    public List<Chunk> ChunkDocument(DocumentRecord doc, string content, RagOptions.ChunkingOptions options)
    {
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        var chunks = new List<Chunk>();

        if (string.IsNullOrWhiteSpace(content))
            return chunks;

        // Try markdown-aware chunking first
        List<string> segments = TryMarkdownChunking(content, options.MaxChunkSize);

        // If markdown chunking didn't produce good results, fall back to window chunking
        if (segments.Count == 0)
        {
            segments = ApplyWindowChunking(content, options.MaxChunkSize, options.OverlapSize);
        }

        // Convert segments to Chunk objects
        int charOffset = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            string segmentText = segments[i];

            // Skip chunks that are too small
            if (segmentText.Length < options.MinChunkSize)
            {
                charOffset += segmentText.Length;
                continue;
            }

            // Find the actual position in the original content
            int charStart;
            if (charOffset < content.Length)
            {
                charStart = content.IndexOf(segmentText, charOffset, StringComparison.Ordinal);
                if (charStart < 0)
                    charStart = charOffset; // Fallback if exact match not found
            }
            else
            {
                charStart = charOffset; // At or past end, use current offset
            }

            int charEnd = charStart + segmentText.Length;

            var chunk = new Chunk
            {
                ChunkId = Chunk.ComputeChunkId(doc.DocId, segmentText, charStart, charEnd),
                DocId = doc.DocId,
                SourceUri = doc.SourceUri,
                Title = doc.Title,
                Text = segmentText,
                CharStart = charStart,
                CharEnd = charEnd,
                CreatedUtc = DateTime.UtcNow,
                Version = doc.Version
            };

            chunks.Add(chunk);
            charOffset = charEnd;
        }

        return chunks;
    }

    /// <summary>
    /// Attempts to chunk text using markdown structure (headings and paragraphs).
    /// </summary>
    private List<string> TryMarkdownChunking(string text, int maxChunkSize)
    {
        var chunks = new List<string>();

        // Quick check: if no markdown indicators present, return empty to trigger fallback
        if (text.IndexOf('#') < 0)
            return chunks;

        // Split into lines
        List<string> lines = SplitLines(text);

        // Group lines into sections based on headings
        var currentSection = new List<string>();
        int currentSize = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];

            // Check if this is a markdown heading
            bool isHeading = IsMarkdownHeading(line.AsSpan());

            // If we hit a heading and have accumulated content, flush the current section
            if (isHeading && currentSection.Count > 0)
            {
                string sectionText = JoinLines(currentSection);
                if (sectionText.Length > 0)
                {
                    // If section is too large, split it further
                    if (sectionText.Length > maxChunkSize)
                    {
                        List<string> subChunks = SplitLargeSection(sectionText, maxChunkSize);
                        for (int j = 0; j < subChunks.Count; j++)
                        {
                            chunks.Add(subChunks[j]);
                        }
                    }
                    else
                    {
                        chunks.Add(sectionText);
                    }
                }

                currentSection.Clear();
                currentSize = 0;
            }

            // Add line to current section
            currentSection.Add(line);
            currentSize += line.Length + 1; // +1 for newline

            // If section gets too large, flush it
            if (currentSize > maxChunkSize)
            {
                string sectionText = JoinLines(currentSection);
                if (sectionText.Length > 0)
                {
                    List<string> subChunks = SplitLargeSection(sectionText, maxChunkSize);
                    for (int j = 0; j < subChunks.Count; j++)
                    {
                        chunks.Add(subChunks[j]);
                    }
                }

                currentSection.Clear();
                currentSize = 0;
            }
        }

        // Flush remaining section
        if (currentSection.Count > 0)
        {
            string sectionText = JoinLines(currentSection);
            if (sectionText.Length > 0)
            {
                if (sectionText.Length > maxChunkSize)
                {
                    List<string> subChunks = SplitLargeSection(sectionText, maxChunkSize);
                    for (int j = 0; j < subChunks.Count; j++)
                    {
                        chunks.Add(subChunks[j]);
                    }
                }
                else
                {
                    chunks.Add(sectionText);
                }
            }
        }

        return chunks;
    }

    /// <summary>
    /// Splits a large section into smaller chunks based on paragraphs and character limits.
    /// </summary>
    private List<string> SplitLargeSection(string text, int maxChunkSize)
    {
        // First try paragraph-based splitting
        List<string> paragraphs = SplitByParagraphs(text);
        var chunks = new List<string>();
        var currentChunk = new List<string>();
        int currentSize = 0;

        for (int i = 0; i < paragraphs.Count; i++)
        {
            string para = paragraphs[i];

            // If a single paragraph exceeds max size, it needs character-based splitting
            if (para.Length > maxChunkSize)
            {
                // Flush current chunk first
                if (currentChunk.Count > 0)
                {
                    chunks.Add(JoinParagraphs(currentChunk));
                    currentChunk.Clear();
                    currentSize = 0;
                }

                // Split the large paragraph
                List<string> subChunks = SplitByCharacters(para, maxChunkSize);
                for (int j = 0; j < subChunks.Count; j++)
                {
                    chunks.Add(subChunks[j]);
                }
            }
            else if (currentSize + para.Length + 2 > maxChunkSize) // +2 for paragraph separator
            {
                // Flush current chunk
                if (currentChunk.Count > 0)
                {
                    chunks.Add(JoinParagraphs(currentChunk));
                    currentChunk.Clear();
                    currentSize = 0;
                }

                // Start new chunk with this paragraph
                currentChunk.Add(para);
                currentSize = para.Length;
            }
            else
            {
                // Add to current chunk
                currentChunk.Add(para);
                currentSize += para.Length + 2; // +2 for paragraph separator
            }
        }

        // Flush remaining chunk
        if (currentChunk.Count > 0)
        {
            chunks.Add(JoinParagraphs(currentChunk));
        }

        return chunks;
    }

    /// <summary>
    /// Detects if a line is a markdown heading (starts with one or more # characters).
    /// </summary>
    private bool IsMarkdownHeading(ReadOnlySpan<char> line)
    {
        if (line.Length == 0)
            return false;

        // Trim leading whitespace
        int start = 0;
        while (start < line.Length && char.IsWhiteSpace(line[start]))
            start++;

        if (start >= line.Length)
            return false;

        // Check if it starts with #
        if (line[start] != '#')
            return false;

        // Count consecutive # characters
        int hashCount = 0;
        for (int i = start; i < line.Length && line[i] == '#'; i++)
            hashCount++;

        // Valid markdown heading has 1-6 # characters followed by whitespace or end of line
        if (hashCount >= 1 && hashCount <= 6)
        {
            int afterHash = start + hashCount;
            if (afterHash >= line.Length || char.IsWhiteSpace(line[afterHash]))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Splits text into paragraphs based on blank lines (two or more consecutive newlines).
    /// </summary>
    private List<string> SplitByParagraphs(string text)
    {
        var paragraphs = new List<string>();
        int start = 0;
        int length = text.Length;

        while (start < length)
        {
            // Skip leading whitespace/newlines
            while (start < length && char.IsWhiteSpace(text[start]))
                start++;

            if (start >= length)
                break;

            // Find end of paragraph (blank line or end of text)
            int end = start;
            int consecutiveNewlines = 0;

            while (end < length)
            {
                if (text[end] == '\n' || text[end] == '\r')
                {
                    consecutiveNewlines++;
                    if (consecutiveNewlines >= 2)
                        break;
                }
                else if (!char.IsWhiteSpace(text[end]))
                {
                    consecutiveNewlines = 0;
                }

                end++;
            }

            // Extract paragraph and trim
            if (end > start)
            {
                string paragraph = text.Substring(start, end - start).Trim();
                if (paragraph.Length > 0)
                {
                    paragraphs.Add(paragraph);
                }
            }

            start = end;
        }

        return paragraphs;
    }

    /// <summary>
    /// Applies window-based chunking with overlap as a fallback strategy.
    /// </summary>
    private List<string> ApplyWindowChunking(string text, int maxChars, int overlapChars)
    {
        var chunks = new List<string>();
        int length = text.Length;
        int position = 0;

        while (position < length)
        {
            // Calculate chunk end
            int chunkEnd = Math.Min(position + maxChars, length);

            // Try to break at a sentence or word boundary if not at end
            if (chunkEnd < length)
            {
                chunkEnd = FindBestBreakPoint(text, position, chunkEnd);
            }

            // Store the original chunkEnd before trimming
            int originalChunkEnd = chunkEnd;

            // Extract chunk
            string chunk = text.Substring(position, chunkEnd - position).Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            // Move position forward with overlap, but ensure we always make progress
            int newPosition = originalChunkEnd - overlapChars;

            // Ensure we make progress even if overlap is large or chunk was all whitespace
            if (newPosition <= position)
            {
                newPosition = originalChunkEnd;
            }

            position = newPosition;
        }

        return chunks;
    }

    /// <summary>
    /// Splits text by character limits when no better boundary is available.
    /// </summary>
    private List<string> SplitByCharacters(string text, int maxChars)
    {
        var chunks = new List<string>();
        int position = 0;
        int length = text.Length;

        while (position < length)
        {
            int chunkEnd = Math.Min(position + maxChars, length);

            // Try to break at word boundary
            if (chunkEnd < length)
            {
                chunkEnd = FindWordBoundary(text, position, chunkEnd);
            }

            string chunk = text.Substring(position, chunkEnd - position).Trim();
            if (chunk.Length > 0)
            {
                chunks.Add(chunk);
            }

            position = chunkEnd;
        }

        return chunks;
    }

    /// <summary>
    /// Finds the best break point for chunking, preferring sentence then word boundaries.
    /// </summary>
    private int FindBestBreakPoint(string text, int start, int preferredEnd)
    {
        // Look backwards for sentence boundary (. ! ?) followed by whitespace
        for (int i = preferredEnd - 1; i > start + (preferredEnd - start) / 2; i--)
        {
            char c = text[i];
            if ((c == '.' || c == '!' || c == '?') && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
            {
                return i + 1;
            }
        }

        // Fall back to word boundary
        return FindWordBoundary(text, start, preferredEnd);
    }

    /// <summary>
    /// Finds a word boundary by looking backwards for whitespace.
    /// </summary>
    private int FindWordBoundary(string text, int start, int preferredEnd)
    {
        // Look backwards for whitespace
        for (int i = preferredEnd - 1; i > start; i--)
        {
            if (char.IsWhiteSpace(text[i]))
            {
                return i + 1;
            }
        }

        // No whitespace found, use preferred end
        return preferredEnd;
    }

    /// <summary>
    /// Splits text into individual lines.
    /// </summary>
    private List<string> SplitLines(string text)
    {
        var lines = new List<string>();
        int start = 0;
        int length = text.Length;

        for (int i = 0; i < length; i++)
        {
            if (text[i] == '\n')
            {
                // Handle \r\n
                int lineEnd = i;
                if (lineEnd > start && text[lineEnd - 1] == '\r')
                    lineEnd--;

                lines.Add(text.Substring(start, lineEnd - start));
                start = i + 1;
            }
        }

        // Add last line if it doesn't end with newline
        if (start < length)
        {
            lines.Add(text.Substring(start));
        }

        return lines;
    }

    /// <summary>
    /// Joins lines with newline characters.
    /// </summary>
    private string JoinLines(List<string> lines)
    {
        if (lines.Count == 0)
            return string.Empty;

        int totalLength = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            totalLength += lines[i].Length + 1; // +1 for newline
        }

        var result = new char[totalLength - 1]; // -1 because last line doesn't have newline
        int position = 0;

        for (int i = 0; i < lines.Count; i++)
        {
            string line = lines[i];
            line.CopyTo(0, result, position, line.Length);
            position += line.Length;

            if (i < lines.Count - 1)
            {
                result[position] = '\n';
                position++;
            }
        }

        return new string(result);
    }

    /// <summary>
    /// Joins paragraphs with double newline separation.
    /// </summary>
    private string JoinParagraphs(List<string> paragraphs)
    {
        if (paragraphs.Count == 0)
            return string.Empty;

        int totalLength = 0;
        for (int i = 0; i < paragraphs.Count; i++)
        {
            totalLength += paragraphs[i].Length;
            if (i < paragraphs.Count - 1)
                totalLength += 2; // Two newlines between paragraphs
        }

        var result = new char[totalLength];
        int position = 0;

        for (int i = 0; i < paragraphs.Count; i++)
        {
            string para = paragraphs[i];
            para.CopyTo(0, result, position, para.Length);
            position += para.Length;

            if (i < paragraphs.Count - 1)
            {
                result[position] = '\n';
                result[position + 1] = '\n';
                position += 2;
            }
        }

        return new string(result);
    }
}

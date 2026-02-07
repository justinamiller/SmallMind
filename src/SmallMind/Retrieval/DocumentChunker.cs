using System;
using System.Collections.Generic;

namespace SmallMind.Retrieval
{
    /// <summary>
    /// Deterministic text chunker for splitting documents into smaller pieces.
    /// </summary>
    public static class DocumentChunker
    {
        /// <summary>
        /// Chunk a document into smaller pieces using deterministic rules.
        /// </summary>
        /// <param name="document">The document to chunk.</param>
        /// <param name="options">Chunking options.</param>
        /// <returns>List of document chunks.</returns>
        public static List<DocumentChunk> Chunk(Document document, ChunkingOptions options)
        {
            if (document == null)
                throw new ArgumentNullException(nameof(document));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var chunks = new List<DocumentChunk>();
            
            if (string.IsNullOrEmpty(document.Content))
                return chunks;

            var content = document.Content;
            var contentSpan = content.AsSpan();
            int currentOffset = 0;
            int chunkIndex = 0;

            while (currentOffset < content.Length)
            {
                int chunkSize = Math.Min(options.MaxChars, content.Length - currentOffset);
                int actualChunkEnd = currentOffset + chunkSize;

                // If we're not at the end, try to find a better boundary
                if (actualChunkEnd < content.Length)
                {
                    actualChunkEnd = FindBestBoundary(
                        contentSpan,
                        currentOffset,
                        actualChunkEnd,
                        options);
                }

                // Create the chunk
                int startOffset = currentOffset;
                int endOffset = actualChunkEnd;
                int length = endOffset - startOffset;

                // Only create chunk if it meets minimum size (unless it's the last chunk)
                if (length >= options.MinChunkChars || endOffset >= content.Length)
                {
                    var chunkText = content.Substring(startOffset, length);
                    
                    var chunk = new DocumentChunk
                    {
                        ChunkId = $"{document.Id}_chunk_{chunkIndex}",
                        DocumentId = document.Id,
                        StartOffset = startOffset,
                        EndOffset = endOffset,
                        Text = chunkText,
                        Metadata = new Dictionary<string, string>
                        {
                            { "title", document.Title ?? "" },
                            { "source_uri", document.SourceUri ?? "" },
                            { "chunk_index", chunkIndex.ToString() }
                        }
                    };

                    // Copy document tags to chunk metadata
                    foreach (var tag in document.Tags)
                    {
                        chunk.Metadata[$"tag_{tag}"] = "true";
                    }

                    chunks.Add(chunk);
                    chunkIndex++;
                }

                // Move to next chunk with overlap
                currentOffset = endOffset - options.OverlapChars;
                
                // Ensure we make progress
                if (currentOffset <= startOffset)
                {
                    currentOffset = endOffset;
                }

                // Break if we've reached or passed the end
                if (currentOffset >= content.Length)
                {
                    break;
                }
            }

            return chunks;
        }

        /// <summary>
        /// Find the best boundary for splitting text, preferring natural breaks.
        /// </summary>
        private static int FindBestBoundary(
            ReadOnlySpan<char> content,
            int startOffset,
            int proposedEnd,
            ChunkingOptions options)
        {
            int searchStart = Math.Max(startOffset + options.MinChunkChars, proposedEnd - 100);
            int searchEnd = proposedEnd;

            // Try to find paragraph boundary (double newline)
            if (options.PreferParagraphBoundaries)
            {
                for (int i = searchEnd - 1; i >= searchStart; i--)
                {
                    if (i > 0 && content[i] == '\n' && content[i - 1] == '\n')
                    {
                        return i + 1; // Split after the paragraph break
                    }
                }
            }

            // Try to find sentence boundary
            if (options.PreferSentenceBoundaries)
            {
                for (int i = searchEnd - 1; i >= searchStart; i--)
                {
                    char c = content[i];
                    if (c == '.' || c == '!' || c == '?')
                    {
                        // Look ahead to ensure it's a sentence end (followed by space or newline)
                        if (i + 1 < content.Length)
                        {
                            char next = content[i + 1];
                            if (next == ' ' || next == '\n' || next == '\r' || next == '\t')
                            {
                                return i + 1; // Include the punctuation
                            }
                        }
                        else
                        {
                            // End of content
                            return i + 1;
                        }
                    }
                }
            }

            // Try to find word boundary (space, newline)
            for (int i = searchEnd - 1; i >= searchStart; i--)
            {
                char c = content[i];
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t')
                {
                    return i + 1; // Split after the whitespace
                }
            }

            // No good boundary found, use the proposed end
            return proposedEnd;
        }
    }
}

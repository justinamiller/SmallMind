using System;
using System.Collections.Generic;
using System.Text;

namespace SmallMind.Retrieval
{
    /// <summary>
    /// Builds RAG prompts with retrieved context and citations.
    /// Enforces context budgets and ensures citation accuracy.
    /// </summary>
    public static class RagPromptBuilder
    {
        /// <summary>
        /// Build a RAG prompt with retrieved chunks and citations.
        /// </summary>
        /// <param name="userQuery">The user's query or question.</param>
        /// <param name="retrievedChunks">Retrieved chunks to include as context.</param>
        /// <param name="options">Prompt building options.</param>
        /// <returns>Assembled prompt with citations.</returns>
        public static string Build(
            string userQuery,
            List<RetrievedChunkWithCitation> retrievedChunks,
            RagPromptOptions options)
        {
            if (string.IsNullOrEmpty(userQuery))
                throw new ArgumentException("User query cannot be null or empty", nameof(userQuery));
            if (retrievedChunks == null)
                throw new ArgumentNullException(nameof(retrievedChunks));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            var sb = new StringBuilder();
            var usedChunks = new List<RetrievedChunkWithCitation>();
            var citationMap = new Dictionary<string, string>(); // chunkId -> citation

            // Add system instruction if provided
            if (!string.IsNullOrEmpty(options.SystemInstructionTemplate))
            {
                var instruction = options.SystemInstructionTemplate
                    .Replace("{question}", userQuery);
                sb.AppendLine(instruction);
                sb.AppendLine();
            }

            // Add context section
            sb.AppendLine("=== CONTEXT ===");
            sb.AppendLine();

            int contextCharsUsed = sb.Length;
            int maxContextChars = options.MaxContextChars;
            int maxChunks = Math.Min(options.MaxChunksToInclude, retrievedChunks.Count);

            // Include chunks up to budget limits
            for (int i = 0; i < maxChunks; i++)
            {
                var chunk = retrievedChunks[i];
                
                // Generate citation
                var citation = GenerateCitation(chunk, options.CitationFormat);
                citationMap[chunk.ChunkId] = citation;

                // Prepare chunk text with citation
                var chunkText = $"{chunk.Text} {citation}\n\n";
                
                // Check if adding this chunk would exceed the budget
                if (contextCharsUsed + chunkText.Length > maxContextChars)
                {
                    // Try to truncate the chunk to fit
                    int availableChars = maxContextChars - contextCharsUsed - citation.Length - 10; // 10 for formatting
                    
                    if (availableChars > 100) // Only include if we can fit a meaningful portion
                    {
                        var truncatedText = chunk.Text.Substring(0, Math.Min(availableChars, chunk.Text.Length)) + "...";
                        chunkText = $"{truncatedText} {citation}\n\n";
                        
                        sb.Append(chunkText);
                        usedChunks.Add(chunk);
                        contextCharsUsed += chunkText.Length;
                    }
                    
                    break; // Stop adding chunks
                }

                sb.Append(chunkText);
                usedChunks.Add(chunk);
                contextCharsUsed += chunkText.Length;
            }

            // Add question section
            sb.AppendLine("=== QUESTION ===");
            sb.AppendLine(userQuery);
            sb.AppendLine();

            // Add instruction
            sb.AppendLine("=== INSTRUCTION ===");
            sb.AppendLine("Based on the context provided above, answer the question. ");
            sb.AppendLine("Only use information from the context. If the context doesn't contain enough information, say so.");
            sb.AppendLine("Include citations to the relevant sources when making claims.");
            sb.AppendLine();

            // Add sources section if enabled
            if (options.IncludeSourcesSection && usedChunks.Count > 0)
            {
                sb.AppendLine("=== SOURCES ===");
                
                // Group chunks by document to avoid duplicate sources
                var documentSources = new Dictionary<string, (string? title, string? uri)>();
                
                foreach (var chunk in usedChunks)
                {
                    if (!documentSources.ContainsKey(chunk.DocumentId))
                    {
                        documentSources[chunk.DocumentId] = (
                            chunk.Citation.Title,
                            chunk.Citation.SourceUri
                        );
                    }
                }

                // List sources
                foreach (var kvp in documentSources)
                {
                    var docId = kvp.Key;
                    var (title, uri) = kvp.Value;
                    
                    if (!string.IsNullOrEmpty(title) || !string.IsNullOrEmpty(uri))
                    {
                        sb.Append($"- ");
                        
                        if (!string.IsNullOrEmpty(title))
                        {
                            sb.Append(title);
                        }
                        
                        if (!string.IsNullOrEmpty(uri))
                        {
                            if (!string.IsNullOrEmpty(title))
                            {
                                sb.Append($" ({uri})");
                            }
                            else
                            {
                                sb.Append(uri);
                            }
                        }
                        
                        sb.AppendLine();
                    }
                }
                
                sb.AppendLine();
            }

            sb.AppendLine("=== ANSWER ===");

            return sb.ToString();
        }

        /// <summary>
        /// Generate a citation string for a chunk.
        /// </summary>
        private static string GenerateCitation(RetrievedChunkWithCitation chunk, string format)
        {
            var citation = format
                .Replace("{title}", chunk.Citation.Title ?? "Unknown")
                .Replace("{chunkId}", chunk.ChunkId)
                .Replace("{documentId}", chunk.DocumentId)
                .Replace("{sourceUri}", chunk.Citation.SourceUri ?? "");

            return citation;
        }

        /// <summary>
        /// Build a minimal RAG prompt without system templates (for simpler use cases).
        /// </summary>
        public static string BuildSimple(
            string userQuery,
            List<RetrievedChunkWithCitation> retrievedChunks,
            int maxContextChars = 2000)
        {
            var options = new RagPromptOptions
            {
                MaxContextChars = maxContextChars,
                SystemInstructionTemplate = null,
                IncludeSourcesSection = true
            };

            return Build(userQuery, retrievedChunks, options);
        }
    }
}

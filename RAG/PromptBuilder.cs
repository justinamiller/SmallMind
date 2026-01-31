using System;
using System.Collections.Generic;
using System.Text;

namespace TinyLLM.RAG
{
    /// <summary>
    /// Builds prompts for RAG (Retrieval-Augmented Generation).
    /// Formats the system message and context in a structured way.
    /// </summary>
    public class PromptBuilder : IPromptBuilder
    {
        private readonly string _systemMessage;

        /// <summary>
        /// Default system message for RAG.
        /// </summary>
        public const string DEFAULT_SYSTEM_MESSAGE = 
            "Answer the question based only on the provided context. " +
            "If the answer is not in the context, say 'I don't know based on the provided context.'";

        /// <summary>
        /// Create a new prompt builder.
        /// </summary>
        /// <param name="systemMessage">System message to use (null for default)</param>
        public PromptBuilder(string? systemMessage = null)
        {
            _systemMessage = systemMessage ?? DEFAULT_SYSTEM_MESSAGE;
        }

        /// <summary>
        /// Build a prompt with context from retrieved chunks.
        /// </summary>
        /// <param name="question">User's question</param>
        /// <param name="chunks">Retrieved chunks to use as context</param>
        /// <returns>Complete prompt for the LLM</returns>
        public string BuildPrompt(string question, List<RetrievedChunk> chunks)
        {
            var sb = new StringBuilder();

            // System message
            sb.AppendLine(_systemMessage);
            sb.AppendLine();

            // Context section
            if (chunks != null && chunks.Count > 0)
            {
                sb.AppendLine("SOURCES:");
                sb.AppendLine("---");
                
                for (int i = 0; i < chunks.Count; i++)
                {
                    var chunk = chunks[i];
                    sb.AppendLine($"[{i + 1}] (Score: {chunk.Score:F3})");
                    sb.AppendLine(chunk.Text);
                    sb.AppendLine();
                }
                
                sb.AppendLine("---");
                sb.AppendLine();
            }

            // Question
            sb.AppendLine($"Question: {question}");
            sb.AppendLine();
            sb.Append("Answer:");

            return sb.ToString();
        }

        /// <summary>
        /// Build a prompt with simple text context.
        /// </summary>
        public string BuildPromptWithContext(string question, string context)
        {
            var sb = new StringBuilder();

            sb.AppendLine(_systemMessage);
            sb.AppendLine();
            sb.AppendLine("Context:");
            sb.AppendLine(context);
            sb.AppendLine();
            sb.AppendLine($"Question: {question}");
            sb.AppendLine();
            sb.Append("Answer:");

            return sb.ToString();
        }

        /// <summary>
        /// Build a simple prompt without context (useful for testing).
        /// </summary>
        public string BuildSimplePrompt(string question)
        {
            return $"Question: {question}\n\nAnswer:";
        }
    }
}

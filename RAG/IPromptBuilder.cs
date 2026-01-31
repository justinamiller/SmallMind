using System.Collections.Generic;

namespace TinyLLM.RAG
{
    /// <summary>
    /// Interface for prompt builder implementations.
    /// Builds prompts for RAG (Retrieval-Augmented Generation).
    /// </summary>
    public interface IPromptBuilder
    {
        /// <summary>
        /// Build a prompt with context from retrieved chunks.
        /// </summary>
        /// <param name="question">User's question</param>
        /// <param name="chunks">Retrieved chunks to use as context</param>
        /// <returns>Complete prompt for the LLM</returns>
        string BuildPrompt(string question, List<RetrievedChunk> chunks);

        /// <summary>
        /// Build a prompt with simple text context.
        /// </summary>
        /// <param name="question">User's question</param>
        /// <param name="context">Context text</param>
        /// <returns>Complete prompt for the LLM</returns>
        string BuildPromptWithContext(string question, string context);

        /// <summary>
        /// Build a simple prompt without context.
        /// </summary>
        /// <param name="question">User's question</param>
        /// <returns>Simple prompt</returns>
        string BuildSimplePrompt(string question);
    }
}

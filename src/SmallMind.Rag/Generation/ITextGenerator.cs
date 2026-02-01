using System;

namespace SmallMind.Rag.Generation
{
    /// <summary>
    /// Interface for text generation from prompts.
    /// Allows plugging in different LLM backends (SmallMind, OpenAI, etc.)
    /// </summary>
    public interface ITextGenerator
    {
        /// <summary>
        /// Generates text from the given prompt.
        /// </summary>
        /// <param name="prompt">The input prompt to generate from.</param>
        /// <param name="maxTokens">Maximum number of tokens to generate.</param>
        /// <param name="temperature">Sampling temperature (0.0 = deterministic, higher = more random).</param>
        /// <param name="seed">Optional random seed for deterministic generation.</param>
        /// <returns>The generated text.</returns>
        string Generate(string prompt, int maxTokens = 200, double temperature = 0.7, int? seed = null);
    }
}

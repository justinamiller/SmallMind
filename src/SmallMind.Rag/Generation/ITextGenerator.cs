using System;
using System.Collections.Generic;
using System.Threading;

namespace SmallMind.Rag.Generation
{
    /// <summary>
    /// Interface for text generation from prompts.
    /// Allows plugging in different LLM backends (SmallMind, OpenAI, etc.)
    /// </summary>
    internal interface ITextGenerator
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

        /// <summary>
        /// Generates text from the given prompt with streaming support.
        /// Yields token IDs as they are generated.
        /// </summary>
        /// <param name="prompt">The input prompt to generate from.</param>
        /// <param name="options">Generation options.</param>
        /// <param name="cancellationToken">Cancellation token to stop generation.</param>
        /// <returns>Async enumerable of generated token IDs.</returns>
        IAsyncEnumerable<int> GenerateStreamAsync(
            string prompt,
            GenerationOptions options,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Options for text generation.
    /// </summary>
    internal sealed class GenerationOptions
    {
        /// <summary>
        /// Maximum number of tokens to generate.
        /// </summary>
        public int MaxTokens { get; set; } = 200;

        /// <summary>
        /// Sampling temperature (0.0 = deterministic, higher = more random).
        /// </summary>
        public double Temperature { get; set; } = 0.7;

        /// <summary>
        /// Optional random seed for deterministic generation.
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Top-K sampling parameter (0 to disable).
        /// </summary>
        public int TopK { get; set; } = 40;

        /// <summary>
        /// Top-P (nucleus) sampling parameter (1.0 to disable).
        /// </summary>
        public double TopP { get; set; } = 0.95;
    }
}

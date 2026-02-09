using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmallMind.Runtime
{
    /// <summary>
    /// Options for controlling text generation behavior.
    /// </summary>
    internal class GenerationOptions
    {
        /// <summary>
        /// Maximum number of tokens to generate. Default: 100.
        /// </summary>
        public int MaxTokens { get; set; } = 100;

        /// <summary>
        /// Sampling temperature (higher = more random). Default: 1.0.
        /// Values typically range from 0.1 to 2.0.
        /// </summary>
        public float Temperature { get; set; } = 1.0f;

        /// <summary>
        /// Top-K sampling: only sample from top K most likely tokens. 
        /// 0 means disabled (sample from all tokens). Default: 0.
        /// </summary>
        public int TopK { get; set; } = 0;

        /// <summary>
        /// Random seed for deterministic generation. 
        /// Same seed with same prompt and options produces identical output.
        /// Null means non-deterministic. Default: null.
        /// </summary>
        public int? Seed { get; set; }

        /// <summary>
        /// Maximum duration in milliseconds for generation.
        /// 0 means no timeout. Default: 0.
        /// </summary>
        public int MaxDurationMs { get; set; } = 0;
    }

    /// <summary>
    /// Interface for text generation from language models.
    /// </summary>
    internal interface ITextGenerator
    {
        /// <summary>
        /// Generate text asynchronously from a prompt.
        /// </summary>
        /// <param name="prompt">Input text prompt to continue from</param>
        /// <param name="options">Generation options (temperature, max tokens, etc.)</param>
        /// <param name="cancellationToken">Cancellation token for aborting generation</param>
        /// <returns>Generated text continuation</returns>
        Task<string> GenerateAsync(
            string prompt,
            GenerationOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Generate text as a stream of tokens for real-time display.
        /// </summary>
        /// <param name="prompt">Input text prompt to continue from</param>
        /// <param name="options">Generation options (temperature, max tokens, etc.)</param>
        /// <param name="cancellationToken">Cancellation token for aborting generation</param>
        /// <returns>Async enumerable of token IDs</returns>
        IAsyncEnumerable<int> GenerateStreamAsync(
            string prompt,
            GenerationOptions options,
            CancellationToken cancellationToken = default);
    }
}

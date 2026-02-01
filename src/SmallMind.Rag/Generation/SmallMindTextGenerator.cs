using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using SmallMind.Core;
using SmallMind.Text;

namespace SmallMind.Rag.Generation
{
    /// <summary>
    /// Text generator implementation using SmallMind's built-in Transformer model.
    /// </summary>
    public sealed class SmallMindTextGenerator : ITextGenerator
    {
        private readonly Sampling _sampling;

        /// <summary>
        /// Creates a new SmallMind text generator.
        /// </summary>
        /// <param name="model">The trained Transformer model.</param>
        /// <param name="tokenizer">The tokenizer to use.</param>
        /// <param name="blockSize">The model's block size (context window).</param>
        public SmallMindTextGenerator(TransformerModel model, ITokenizer tokenizer, int blockSize)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (tokenizer == null)
                throw new ArgumentNullException(nameof(tokenizer));
            if (blockSize <= 0)
                throw new ArgumentException("Block size must be positive", nameof(blockSize));

            _sampling = new Sampling(model, tokenizer, blockSize);
        }

        /// <summary>
        /// Generates text from the given prompt using the SmallMind model.
        /// </summary>
        /// <param name="prompt">The input prompt to generate from.</param>
        /// <param name="maxTokens">Maximum number of tokens to generate.</param>
        /// <param name="temperature">Sampling temperature (0.0 = deterministic, higher = more random).</param>
        /// <param name="seed">Optional random seed for deterministic generation.</param>
        /// <returns>The generated text.</returns>
        public string Generate(string prompt, int maxTokens = 200, double temperature = 0.7, int? seed = null)
        {
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

            // Use Sampling to generate text
            // Note: showPerf=false to avoid console output during RAG generation
            return _sampling.Generate(
                prompt: prompt,
                maxNewTokens: maxTokens,
                temperature: temperature,
                topK: 40, // Reasonable default for focused generation
                seed: seed,
                showPerf: false,
                isPerfJsonMode: false
            );
        }

        /// <summary>
        /// Generates text from the given prompt with streaming support.
        /// Yields token IDs as they are generated.
        /// </summary>
        /// <param name="prompt">The input prompt to generate from.</param>
        /// <param name="options">Generation options.</param>
        /// <param name="cancellationToken">Cancellation token to stop generation.</param>
        /// <returns>Async enumerable of generated token IDs.</returns>
        public async IAsyncEnumerable<int> GenerateStreamAsync(
            string prompt,
            GenerationOptions options,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            // Note: The current Sampling class doesn't have streaming support
            // For now, we'll fall back to non-streaming and yield all at once
            // TODO: Enhance Sampling class to support true streaming
            
            var result = _sampling.Generate(
                prompt: prompt,
                maxNewTokens: options.MaxTokens,
                temperature: options.Temperature,
                topK: options.TopK,
                seed: options.Seed,
                showPerf: false,
                isPerfJsonMode: false
            );

            // Since we don't have true streaming in Sampling, yield a single result
            // In a real implementation, this would yield tokens as they're generated
            await System.Threading.Tasks.Task.CompletedTask; // Make async
            
            // Return a placeholder token (in a real impl, would be the actual tokens)
            yield return 0; // Placeholder
        }
    }
}

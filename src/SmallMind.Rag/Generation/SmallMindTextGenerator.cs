using System;
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
    }
}

using System;
using SmallMind.Core.Validation;

namespace SmallMind.Transformers
{
    /// <summary>
    /// Builder for creating TransformerModel instances with a fluent API.
    /// Provides validation and sensible defaults for model configuration.
    /// </summary>
    public class TransformerModelBuilder
    {
        private int _vocabSize;
        private int _blockSize = 128;
        private int _embedDim = 384;
        private int _numLayers = 6;
        private int _numHeads = 6;
        private double _dropout = 0.1;
        private int _seed = 42;

        /// <summary>
        /// Set the vocabulary size (required)
        /// </summary>
        /// <param name="vocabSize">Number of unique tokens in vocabulary</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder WithVocabSize(int vocabSize)
        {
            Guard.GreaterThan(vocabSize, 0, nameof(vocabSize));
            _vocabSize = vocabSize;
            return this;
        }

        /// <summary>
        /// Set the context window size (default: 128)
        /// </summary>
        /// <param name="blockSize">Maximum sequence length</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder WithBlockSize(int blockSize)
        {
            Guard.GreaterThan(blockSize, 0, nameof(blockSize));
            _blockSize = blockSize;
            return this;
        }

        /// <summary>
        /// Set the embedding dimension (default: 384)
        /// Must be divisible by number of heads
        /// </summary>
        /// <param name="embedDim">Embedding dimension</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder WithEmbedDim(int embedDim)
        {
            Guard.GreaterThan(embedDim, 0, nameof(embedDim));
            _embedDim = embedDim;
            return this;
        }

        /// <summary>
        /// Set the number of transformer layers (default: 6)
        /// </summary>
        /// <param name="numLayers">Number of layers</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder WithNumLayers(int numLayers)
        {
            Guard.GreaterThan(numLayers, 0, nameof(numLayers));
            _numLayers = numLayers;
            return this;
        }

        /// <summary>
        /// Set the number of attention heads per layer (default: 6)
        /// Embedding dimension must be divisible by this value
        /// </summary>
        /// <param name="numHeads">Number of attention heads</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder WithNumHeads(int numHeads)
        {
            Guard.GreaterThan(numHeads, 0, nameof(numHeads));
            _numHeads = numHeads;
            return this;
        }

        /// <summary>
        /// Set the dropout probability (default: 0.1)
        /// </summary>
        /// <param name="dropout">Dropout probability between 0.0 and 1.0</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder WithDropout(double dropout)
        {
            Guard.InRange(dropout, 0.0, 1.0, nameof(dropout));
            _dropout = dropout;
            return this;
        }

        /// <summary>
        /// Set the random seed for reproducibility (default: 42)
        /// </summary>
        /// <param name="seed">Random seed</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder WithSeed(int seed)
        {
            _seed = seed;
            return this;
        }

        /// <summary>
        /// Configure for a tiny model (suitable for quick testing and debugging)
        /// VocabSize: 50, BlockSize: 64, EmbedDim: 128, NumLayers: 2, NumHeads: 4
        /// </summary>
        /// <param name="vocabSize">Vocabulary size (default: 50)</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder UseTinyConfig(int vocabSize = 50)
        {
            _vocabSize = vocabSize;
            _blockSize = 64;
            _embedDim = 128;
            _numLayers = 2;
            _numHeads = 4;
            _dropout = 0.1;
            return this;
        }

        /// <summary>
        /// Configure for a small model (suitable for CPU training on limited data)
        /// VocabSize: required, BlockSize: 128, EmbedDim: 256, NumLayers: 4, NumHeads: 4
        /// </summary>
        /// <param name="vocabSize">Vocabulary size</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder UseSmallConfig(int vocabSize)
        {
            _vocabSize = vocabSize;
            _blockSize = 128;
            _embedDim = 256;
            _numLayers = 4;
            _numHeads = 4;
            _dropout = 0.1;
            return this;
        }

        /// <summary>
        /// Configure for a medium model (default configuration)
        /// VocabSize: required, BlockSize: 128, EmbedDim: 384, NumLayers: 6, NumHeads: 6
        /// </summary>
        /// <param name="vocabSize">Vocabulary size</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder UseMediumConfig(int vocabSize)
        {
            _vocabSize = vocabSize;
            _blockSize = 128;
            _embedDim = 384;
            _numLayers = 6;
            _numHeads = 6;
            _dropout = 0.1;
            return this;
        }

        /// <summary>
        /// Configure for a large model (requires more memory and training time)
        /// VocabSize: required, BlockSize: 256, EmbedDim: 512, NumLayers: 8, NumHeads: 8
        /// </summary>
        /// <param name="vocabSize">Vocabulary size</param>
        /// <returns>Builder instance for chaining</returns>
        public TransformerModelBuilder UseLargeConfig(int vocabSize)
        {
            _vocabSize = vocabSize;
            _blockSize = 256;
            _embedDim = 512;
            _numLayers = 8;
            _numHeads = 8;
            _dropout = 0.1;
            return this;
        }

        /// <summary>
        /// Build and return the configured TransformerModel
        /// </summary>
        /// <returns>Configured TransformerModel instance</returns>
        /// <exception cref="InvalidOperationException">If vocabulary size is not set</exception>
        public TransformerModel Build()
        {
            if (_vocabSize <= 0)
            {
                throw new InvalidOperationException(
                    "Vocabulary size must be set. Use WithVocabSize() or one of the preset configurations.");
            }

            if (_embedDim % _numHeads != 0)
            {
                throw new InvalidOperationException(
                    $"Embedding dimension ({_embedDim}) must be divisible by number of heads ({_numHeads}). " +
                    $"Current configuration would require {_embedDim}/{_numHeads} = {(float)_embedDim / _numHeads} per head.");
            }

            return new TransformerModel(
                vocabSize: _vocabSize,
                blockSize: _blockSize,
                nEmbd: _embedDim,
                nLayer: _numLayers,
                nHead: _numHeads,
                dropout: _dropout,
                seed: _seed
            );
        }

        /// <summary>
        /// Create a new builder instance
        /// </summary>
        /// <returns>New builder instance</returns>
        public static TransformerModelBuilder Create()
        {
            return new TransformerModelBuilder();
        }
    }
}

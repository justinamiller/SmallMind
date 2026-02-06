using System;
using SmallMind.Core.Validation;

namespace SmallMind.Configuration
{
    /// <summary>
    /// Configuration options for SmallMind transformer models.
    /// </summary>
    public sealed class ModelOptions
    {
        /// <summary>
        /// Gets or sets the vocabulary size.
        /// </summary>
        public int VocabSize { get; set; } = 256;
        
        /// <summary>
        /// Gets or sets the maximum sequence length (block size).
        /// </summary>
        public int BlockSize { get; set; } = 128;
        
        /// <summary>
        /// Gets or sets the embedding dimension.
        /// </summary>
        public int EmbeddingDimension { get; set; } = 384;
        
        /// <summary>
        /// Gets or sets the number of transformer layers.
        /// </summary>
        public int NumLayers { get; set; } = 6;
        
        /// <summary>
        /// Gets or sets the number of attention heads.
        /// </summary>
        public int NumHeads { get; set; } = 6;
        
        /// <summary>
        /// Gets or sets the dropout rate.
        /// </summary>
        public double Dropout { get; set; } = 0.1;
        
        /// <summary>
        /// Gets or sets the random seed for reproducibility.
        /// </summary>
        public int Seed { get; set; } = 42;
        
        /// <summary>
        /// Validates the model options.
        /// </summary>
        /// <exception cref="Exceptions.ValidationException">Thrown when options are invalid.</exception>
        public void Validate()
        {
            Guard.GreaterThan(VocabSize, 0);
            Guard.GreaterThan(BlockSize, 0);
            Guard.GreaterThan(EmbeddingDimension, 0);
            Guard.GreaterThan(NumLayers, 0);
            Guard.GreaterThan(NumHeads, 0);
            Guard.InRange(Dropout, 0.0, 1.0);
            
            if (EmbeddingDimension % NumHeads != 0)
            {
                throw new Exceptions.ValidationException(
                    $"EmbeddingDimension ({EmbeddingDimension}) must be divisible by NumHeads ({NumHeads})",
                    nameof(EmbeddingDimension));
            }
        }
    }
}

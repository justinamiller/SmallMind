using System;
using SmallMind.Transformers;

namespace SmallMind.Tests.TestHelpers
{
    /// <summary>
    /// Factory for creating small, deterministic synthetic models for testing.
    /// Creates tiny TransformerModels with predictable weights to enable golden output testing.
    /// </summary>
    internal static class SyntheticModelFactory
    {
        /// <summary>
        /// Creates a tiny deterministic model for golden output tests.
        /// Vocab=256, Context=64, Embedding=64, Layers=2, Heads=4
        /// </summary>
        /// <param name="seed">Random seed for deterministic weight initialization</param>
        /// <returns>A small TransformerModel with deterministic weights</returns>
        public static TransformerModel CreateTinyModel(int seed = 42)
        {
            var config = new ModelConfig
            {
                VocabSize = 256,
                ContextLength = 64,
                EmbeddingLength = 64,
                FeedForwardLength = 256, // 4x embedding for standard transformer
                BlockCount = 2,
                HeadCount = 4,
                HeadCountKv = 4, // MHA (not GQA)
                Architecture = "test-synthetic",
                RopeFreqBase = 10000.0,
                NormEps = 1e-5,
                UseBias = false
            };
            
            return new TransformerModel(config, seed);
        }
        
        /// <summary>
        /// Creates a micro model (single layer, minimal dimensions) for ultra-fast tests.
        /// Vocab=128, Context=32, Embedding=32, Layers=1, Heads=2
        /// </summary>
        /// <param name="seed">Random seed for deterministic weight initialization</param>
        /// <returns>A micro TransformerModel</returns>
        public static TransformerModel CreateMicroModel(int seed = 42)
        {
            var config = new ModelConfig
            {
                VocabSize = 128,
                ContextLength = 32,
                EmbeddingLength = 32,
                FeedForwardLength = 128, // 4x embedding
                BlockCount = 1,
                HeadCount = 2,
                HeadCountKv = 2,
                Architecture = "test-micro",
                RopeFreqBase = 10000.0,
                NormEps = 1e-5,
                UseBias = false
            };
            
            return new TransformerModel(config, seed);
        }
    }
}

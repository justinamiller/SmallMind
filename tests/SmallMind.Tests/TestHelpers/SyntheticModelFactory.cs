using System;
using SmallMind.Transformers;
using SmallMind.Tokenizers;
using SmallMind.Tests.Fixtures;

namespace SmallMind.Tests.TestHelpers
{
    /// <summary>
    /// Factory for creating synthetic, deterministic transformer models for testing.
    /// Produces tiny models that are fast to execute but exercise full code paths.
    /// Used for golden output regression tests and CI validation.
    /// </summary>
    internal sealed class SyntheticModelFactory
    {
        // Synthetic model configuration (optimized for CI speed)
        public const int VocabSize = 256;           // Extended vocab for better coverage
        public const int ContextLength = 64;        // Short context for fast tests
        public const int EmbeddingDim = 64;         // Small embedding dimension
        public const int NumLayers = 2;             // Minimal layers for full stack testing
        public const int NumHeads = 4;              // Multiple heads for attention testing
        public const int HeadDim = EmbeddingDim / NumHeads; // = 16
        public const int KVHeads = 2;               // For GQA testing
        public const int FFNHiddenDim = 256;        // Standard 4x embedding dim
        public const int DeterministicSeed = 42;    // Fixed seed for reproducibility

        // Special tokens
        public const int BOS_TOKEN_ID = 0;
        public const int EOS_TOKEN_ID = 1;
        public const int UNK_TOKEN_ID = 2;
        public const int PAD_TOKEN_ID = 3;

        /// <summary>
        /// Creates a new synthetic transformer model with deterministic initialization.
        /// All weights are initialized from the same seed for reproducibility.
        /// </summary>
        /// <param name="seed">Random seed for weight initialization (default: 42)</param>
        /// <returns>A fully initialized transformer model</returns>
        public static TransformerModel CreateSyntheticModel(int seed = DeterministicSeed)
        {
            var config = new ModelConfig
            {
                VocabSize = VocabSize,
                ContextLength = ContextLength,
                EmbeddingLength = EmbeddingDim,
                BlockCount = NumLayers,
                HeadCount = NumHeads,
                HeadCountKv = KVHeads,
                FeedForwardLength = FFNHiddenDim,
                Architecture = "llama", // Use Llama architecture
                RopeFreqBase = 10000.0,
                NormEps = 1e-5,
                Dropout = 0.0 // No dropout for deterministic behavior
            };

            var model = new TransformerModel(config, seed);
            model.Eval(); // Set to eval mode (disables dropout)
            
            return model;
        }

        /// <summary>
        /// Creates a synthetic character-level tokenizer.
        /// Uses ASCII printable characters + common control chars.
        /// </summary>
        /// <returns>A character-level tokenizer</returns>
        public static ITokenizer CreateSyntheticTokenizer()
        {
            // Build vocabulary from ASCII printable characters
            var vocabChars = new char[VocabSize];
            
            // Special tokens first
            vocabChars[BOS_TOKEN_ID] = '<'; // <BOS>
            vocabChars[EOS_TOKEN_ID] = '>'; // <EOS>
            vocabChars[UNK_TOKEN_ID] = '?'; // <UNK>
            vocabChars[PAD_TOKEN_ID] = '_'; // <PAD>
            
            // Fill rest with ASCII printable (32-126) and wrap around
            for (int i = 4; i < VocabSize; i++)
            {
                vocabChars[i] = (char)(32 + ((i - 4) % 95));
            }
            
            var vocab = new string(vocabChars);
            return new CharTokenizer(vocab);
        }

        /// <summary>
        /// Creates a complete synthetic model setup (model + tokenizer).
        /// </summary>
        /// <param name="seed">Random seed for model initialization</param>
        /// <returns>Tuple of (model, tokenizer)</returns>
        public static (TransformerModel model, ITokenizer tokenizer) CreateComplete(int seed = DeterministicSeed)
        {
            var model = CreateSyntheticModel(seed);
            var tokenizer = CreateSyntheticTokenizer();
            return (model, tokenizer);
        }
    }
}

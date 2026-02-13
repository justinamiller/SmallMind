using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Tests.Fixtures
{
    /// <summary>
    /// Provides tiny, deterministic transformer models for regression testing.
    /// Models are small enough to run quickly in CI (<1 second per test)
    /// but large enough to exercise all code paths.
    /// </summary>
    internal sealed class TinyModelFixture
    {
        // Model configuration constants
        public const int VocabSize = 128;
        public const int ModelDim = 64;
        public const int NumLayers = 2;
        public const int NumHeads = 4;
        public const int KVHeads = 2; // For GQA testing
        public const int HeadDim = ModelDim / NumHeads; // 16
        public const int MaxSeqLen = 64;
        public const int Seed = 42;

        // Special token IDs
        public const int BOS_ID = 0;
        public const int EOS_ID = 1;
        public const int UNK_ID = 2;

        // Deterministic test vocabulary (ASCII printable subset)
        private const string TestVocab =
            " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\n\r\t";

        private TransformerModel? _cachedModel;
        private ITokenizer? _cachedTokenizer;

        /// <summary>
        /// Creates a new tiny transformer model with deterministic weights.
        /// Model is cached after first creation for performance.
        /// </summary>
        public TransformerModel CreateModel()
        {
            if (_cachedModel != null)
            {
                return _cachedModel;
            }

            _cachedModel = new TransformerModelBuilder()
                .WithVocabSize(VocabSize)
                .WithBlockSize(MaxSeqLen)
                .WithEmbedDim(ModelDim)
                .WithNumLayers(NumLayers)
                .WithNumHeads(NumHeads)
                .WithDropout(0.0) // Disable dropout for determinism
                .WithSeed(Seed)
                .Build();

            return _cachedModel;
        }

        /// <summary>
        /// Creates a new tiny transformer model (no caching).
        /// Use when you need multiple independent model instances.
        /// </summary>
        public TransformerModel CreateFreshModel()
        {
            return new TransformerModelBuilder()
                .WithVocabSize(VocabSize)
                .WithBlockSize(MaxSeqLen)
                .WithEmbedDim(ModelDim)
                .WithNumLayers(NumLayers)
                .WithNumHeads(NumHeads)
                .WithDropout(0.0)
                .WithSeed(Seed)
                .Build();
        }

        /// <summary>
        /// Creates a character-level tokenizer for the test vocabulary.
        /// Cached after first creation.
        /// </summary>
        public ITokenizer CreateTokenizer()
        {
            if (_cachedTokenizer != null)
            {
                return _cachedTokenizer;
            }

            _cachedTokenizer = new CharTokenizer(TestVocab);
            return _cachedTokenizer;
        }

        /// <summary>
        /// Gets the known test prompts with their expected characteristics.
        /// These can be used for determinism and correctness tests.
        /// </summary>
        public IReadOnlyDictionary<string, PromptInfo> GetKnownPrompts()
        {
            return new Dictionary<string, PromptInfo>
            {
                ["hello"] = new PromptInfo
                {
                    Prompt = "hello",
                    ExpectedTokenCount = 5,
                    Description = "Simple ASCII word"
                },
                ["test"] = new PromptInfo
                {
                    Prompt = "test",
                    ExpectedTokenCount = 4,
                    Description = "Another simple ASCII word"
                },
                ["The quick brown fox"] = new PromptInfo
                {
                    Prompt = "The quick brown fox",
                    ExpectedTokenCount = 19, // Including spaces
                    Description = "Multi-word phrase with spaces"
                },
                [""] = new PromptInfo
                {
                    Prompt = "",
                    ExpectedTokenCount = 0,
                    Description = "Empty prompt (tests BOS handling)"
                },
                [" "] = new PromptInfo
                {
                    Prompt = " ",
                    ExpectedTokenCount = 1,
                    Description = "Single space"
                },
                ["123"] = new PromptInfo
                {
                    Prompt = "123",
                    ExpectedTokenCount = 3,
                    Description = "Numeric characters"
                }
            };
        }

        /// <summary>
        /// Clears cached model and tokenizer to free memory.
        /// Call this if you need to reset state between test runs.
        /// </summary>
        public void ClearCache()
        {
            _cachedModel = null;
            _cachedTokenizer = null;
        }
    }

    /// <summary>
    /// Information about a known test prompt.
    /// </summary>
    public sealed class PromptInfo
    {
        public string Prompt { get; init; } = string.Empty;
        public int ExpectedTokenCount { get; init; }
        public string Description { get; init; } = string.Empty;
    }
}

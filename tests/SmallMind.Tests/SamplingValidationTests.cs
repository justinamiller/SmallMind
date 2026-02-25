using SmallMind.Core.Core;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Tests;

/// <summary>
/// Unit tests for sampling-path correctness in InferenceSession.
/// Validates temperature, top-k, top-p, and repetition penalty behaviors.
/// </summary>
public class SamplingValidationTests
{
    private const string TestVocab = "abcdefghijklmnopqrstuvwxyz ";

    private (TransformerModel model, ITokenizer tokenizer) CreateTestModel(int seed = 42)
    {
        int vocabSize = TestVocab.Length;
        var model = new TransformerModel(vocabSize, blockSize: 32, nEmbd: 16,
            nLayer: 2, nHead: 2, dropout: 0.0, seed: seed);
        var tokenizer = new CharTokenizer(TestVocab);
        return (model, tokenizer);
    }

    [Fact]
    public async Task TopK1_ProducesDeterministicOutput_RegardlessOfSeed()
    {
        // With top-k=1, only the single highest-probability token can be sampled.
        // The output should be the same regardless of random seed.
        var (model, tokenizer) = CreateTestModel(seed: 1);

        var options1 = new ProductionInferenceOptions
        {
            MaxNewTokens = 8,
            Temperature = 1.0,
            TopK = 1,
            Seed = 1
        };
        var options2 = new ProductionInferenceOptions
        {
            MaxNewTokens = 8,
            Temperature = 1.0,
            TopK = 1,
            Seed = 99999
        };

        using var session1 = new InferenceSession(model, tokenizer, options1, blockSize: 32);
        using var session2 = new InferenceSession(model, tokenizer, options2, blockSize: 32);

        string result1 = await session1.GenerateAsync("a");
        string result2 = await session2.GenerateAsync("a");

        // Top-k=1 means argmax: same model + same prompt => same generated tokens
        Assert.Equal(result1, result2);
    }

    [Fact]
    public async Task TopK1_IsDeterministic_AcrossMultipleRuns()
    {
        // Top-k=1 is effectively argmax and must be fully deterministic
        var (model, tokenizer) = CreateTestModel(seed: 7);

        var options = new ProductionInferenceOptions
        {
            MaxNewTokens = 5,
            Temperature = 1.0,
            TopK = 1,
            Seed = 42
        };

        string? firstResult = null;
        for (int i = 0; i < 3; i++)
        {
            using var session = new InferenceSession(model, tokenizer, options, blockSize: 32);
            string result = await session.GenerateAsync("test");
            if (firstResult == null)
                firstResult = result;
            else
                Assert.Equal(firstResult, result);
        }
    }

    [Fact]
    public async Task TopK_Clipping_LimitsTokenCandidates()
    {
        // With a deterministic seed, top-k=1 and top-k=26 should differ in output
        // because higher k allows more candidates. This verifies k parameter is applied.
        var (model, tokenizer) = CreateTestModel(seed: 10);

        // Collect outputs from multiple different seeds with topK=1 (always same result)
        // vs multiple seeds with topK=full vocab (varied results expected sometimes)
        var seenTopK1 = new HashSet<string>();
        var seenTopKFull = new HashSet<string>();

        for (int seed = 1; seed <= 10; seed++)
        {
            var optK1 = new ProductionInferenceOptions
            {
                MaxNewTokens = 5,
                Temperature = 1.0,
                TopK = 1,
                Seed = seed
            };
            var optKFull = new ProductionInferenceOptions
            {
                MaxNewTokens = 5,
                Temperature = 1.0,
                TopK = 0, // disabled = full vocab
                Seed = seed
            };

            using var s1 = new InferenceSession(model, tokenizer, optK1, blockSize: 32);
            using var s2 = new InferenceSession(model, tokenizer, optKFull, blockSize: 32);

            seenTopK1.Add(await s1.GenerateAsync("a"));
            seenTopKFull.Add(await s2.GenerateAsync("a"));
        }

        // top-k=1: should always produce the same token sequence (argmax is deterministic)
        Assert.Equal(1, seenTopK1.Count);

        // full vocab: different seeds should produce different results (stochastic)
        Assert.True(seenTopKFull.Count > 1, "Full-vocab sampling across 10 different seeds should produce varied outputs");
    }

    [Fact]
    public async Task TopP_NearZero_ProducesConcentratedDistribution()
    {
        // With very low top-p (0.01), only highest-probability token is included.
        // Combined with a fixed seed, output should be the same as top-k=1 behavior.
        var (model, tokenizer) = CreateTestModel(seed: 5);

        // First get the argmax output
        var optK1 = new ProductionInferenceOptions
        {
            MaxNewTokens = 6,
            Temperature = 1.0,
            TopK = 1,
            Seed = 42
        };
        using var sessionK1 = new InferenceSession(model, tokenizer, optK1, blockSize: 32);
        string argmaxResult = await sessionK1.GenerateAsync("ab");

        // Now with very small top-p (essentially argmax)
        var optP = new ProductionInferenceOptions
        {
            MaxNewTokens = 6,
            Temperature = 1.0,
            TopK = 0,
            TopP = 0.01, // extremely concentrated
            Seed = 42
        };
        using var sessionP = new InferenceSession(model, tokenizer, optP, blockSize: 32);
        string topPResult = await sessionP.GenerateAsync("ab");

        // Both should produce the same output (only top token included in both cases)
        Assert.Equal(argmaxResult, topPResult);
    }

    [Fact]
    public async Task SameSeedProducesSameOutput()
    {
        // Baseline determinism test: same seed produces identical output
        var (model, tokenizer) = CreateTestModel(seed: 3);

        var options = new ProductionInferenceOptions
        {
            MaxNewTokens = 10,
            Temperature = 1.0,
            TopK = 5,
            Seed = 777
        };

        using var session1 = new InferenceSession(model, tokenizer, options, blockSize: 32);
        using var session2 = new InferenceSession(model, tokenizer, options, blockSize: 32);

        string result1 = await session1.GenerateAsync("hello");
        string result2 = await session2.GenerateAsync("hello");

        Assert.Equal(result1, result2);
    }

    [Fact]
    public async Task DifferentSeeds_ProduceDifferentOutputs()
    {
        // Verify stochasticity: different seeds should generally produce different output
        var (model, tokenizer) = CreateTestModel(seed: 3);

        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 5; seed++)
        {
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 1.0,
                Seed = seed
            };
            using var session = new InferenceSession(model, tokenizer, options, blockSize: 32);
            seen.Add(await session.GenerateAsync("hello"));
        }

        // With 5 different seeds, we should see more than 1 unique output
        Assert.True(seen.Count > 1, "Different seeds should produce varied outputs");
    }

    [Fact]
    public async Task RepetitionPenalty_ReducesRepetition()
    {
        // With high repetition penalty, the same token should appear fewer times
        // than with no penalty.
        var (model, tokenizer) = CreateTestModel(seed: 8);

        const int maxTokens = 20;

        var optNoPenalty = new ProductionInferenceOptions
        {
            MaxNewTokens = maxTokens,
            Temperature = 1.0,
            TopK = 1,          // argmax to make the test deterministic
            RepetitionPenalty = 1.0f,  // no penalty
            Seed = 42
        };

        var optWithPenalty = new ProductionInferenceOptions
        {
            MaxNewTokens = maxTokens,
            Temperature = 1.0,
            RepetitionPenalty = 1.3f,  // significant penalty
            RepetitionWindow = 8,
            Seed = 42
        };

        using var sessionNoPenalty = new InferenceSession(model, tokenizer, optNoPenalty, blockSize: 32);
        using var sessionPenalty = new InferenceSession(model, tokenizer, optWithPenalty, blockSize: 32);

        string resultNoPenalty = await sessionNoPenalty.GenerateAsync("a");
        string resultPenalty = await sessionPenalty.GenerateAsync("a");

        // Count max run of any single character in each output
        static int MaxConsecutiveRun(string s)
        {
            if (s.Length == 0) return 0;
            int max = 1, cur = 1;
            for (int i = 1; i < s.Length; i++)
            {
                if (s[i] == s[i - 1]) cur++;
                else cur = 1;
                if (cur > max) max = cur;
            }
            return max;
        }

        int runNoPenalty = MaxConsecutiveRun(resultNoPenalty);
        int runPenalty = MaxConsecutiveRun(resultPenalty);

        // The penalty run should be at most as long as no-penalty run
        // (or equal if the model naturally doesn't repeat at k=1)
        Assert.True(runPenalty <= runNoPenalty,
            $"Repetition penalty should not increase repetition: noPenalty run={runNoPenalty}, penalty run={runPenalty}");
    }

    [Fact]
    public async Task EnableLogitsDiagnostics_DoesNotThrow()
    {
        // Verify that enabling logit diagnostics does not break generation
        var (model, tokenizer) = CreateTestModel(seed: 1);

        var options = new ProductionInferenceOptions
        {
            MaxNewTokens = 3,
            Temperature = 1.0,
            TopK = 5,
            Seed = 42,
            EnableLogitsDiagnostics = true
        };

        using var session = new InferenceSession(model, tokenizer, options, blockSize: 32);
        string result = await session.GenerateAsync("test");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Temperature_ZeroInvalid_ThrowsValidationException()
    {
        // Temperature=0 must be rejected (would cause division by zero in sampling)
        var (model, tokenizer) = CreateTestModel();

        var ex = Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() =>
        {
            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 5,
                Temperature = 0.0
            };
            return new InferenceSession(model, tokenizer, options, blockSize: 32);
        });

        Assert.NotNull(ex);
    }
}

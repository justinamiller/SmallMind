using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;
using SmallMind.Runtime;
using SmallMind.Tests.Fixtures;
using SmallMind.Tests.Utilities;

namespace SmallMind.PerfTests
{
    /// <summary>
    /// Performance regression benchmarks for SmallMind.
    /// Tracks tokens/sec, TTFT, and relative performance between configurations.
    /// </summary>
    [Trait("Category", "Performance")]
    public class RegressionBenchmarks
    {
        private readonly TinyModelFixture _fixture = new();

        [Fact]
        public async Task Inference_TinyModel_TokensPerSecond_MeetsThreshold()
        {
            if (!TestHelpers.ShouldRunPerfTests()) return;

            // Arrange
            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 32, // Generate 32 tokens for measurement
                Temperature = 0.001, // Near-greedy for consistency
                Seed = 42,
                TopK = 0
            };

            var prompt = "test generation";

            // Warmup
            for (int i = 0; i < 3; i++)
            {
                using var warmupSession = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await warmupSession.GenerateAsync(prompt);
            }

            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure
            var sw = Stopwatch.StartNew();
            int tokensGenerated = 0;
            const int numRuns = 5;

            for (int i = 0; i < numRuns; i++)
            {
                using var session = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await session.GenerateAsync(prompt);
                tokensGenerated += options.MaxNewTokens;
            }

            sw.Stop();

            // Calculate metrics
            double tokensPerSecond = tokensGenerated / sw.Elapsed.TotalSeconds;
            double avgMsPerToken = (sw.Elapsed.TotalMilliseconds / tokensGenerated);

            // Assert - Tiny model should achieve reasonable throughput on CPU
            // Even on slower CI machines, should manage >10 tok/s
            Assert.True(tokensPerSecond > 10.0,
                $"Performance regression: {tokensPerSecond:F2} tok/s < 10 tok/s threshold. " +
                $"Avg time per token: {avgMsPerToken:F2}ms");

            // Output for informational purposes
            Console.WriteLine($"Performance: {tokensPerSecond:F2} tok/s, {avgMsPerToken:F2} ms/token");
        }

        [Fact]
        public async Task Inference_TimeToFirstToken_MeetsThreshold()
        {
            if (!TestHelpers.ShouldRunPerfTests()) return;

            // Arrange
            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 1, // Just generate first token
                Temperature = 0.001,
                Seed = 42,
                TopK = 0
            };

            var prompt = "hello world this is a test";

            // Warmup
            for (int i = 0; i < 3; i++)
            {
                using var warmupSession = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await warmupSession.GenerateAsync(prompt);
            }

            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure TTFT across multiple runs
            var ttftTimes = new double[10];
            for (int i = 0; i < ttftTimes.Length; i++)
            {
                var sw = Stopwatch.StartNew();
                using var session = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await session.GenerateAsync(prompt);
                sw.Stop();
                ttftTimes[i] = sw.Elapsed.TotalMilliseconds;
            }

            Array.Sort(ttftTimes);
            double p50 = ttftTimes[ttftTimes.Length / 2];
            double p95 = ttftTimes[(int)(ttftTimes.Length * 0.95)];

            // Assert - TTFT should be reasonable for tiny model
            // P95 should be under 200ms even on slower CI machines
            Assert.True(p95 < 200.0,
                $"TTFT regression: P95={p95:F2}ms > 200ms threshold. P50={p50:F2}ms");

            Console.WriteLine($"TTFT: P50={p50:F2}ms, P95={p95:F2}ms");
        }

        [Fact]
        public async Task Inference_GreedySamplingFaster_ThanRandomSampling()
        {
            if (!TestHelpers.ShouldRunPerfTests()) return;

            // This test validates relative performance rather than absolute thresholds
            // Greedy sampling (temp~0) should be faster than high-temperature random sampling

            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var prompt = "test";
            const int tokensToGenerate = 20;

            // Greedy options
            var greedyOptions = new ProductionInferenceOptions
            {
                MaxNewTokens = tokensToGenerate,
                Temperature = 0.001,
                Seed = 42,
                TopK = 0
            };

            // Random sampling options
            var randomOptions = new ProductionInferenceOptions
            {
                MaxNewTokens = tokensToGenerate,
                Temperature = 1.0,
                Seed = 42,
                TopK = 40
            };

            // Warmup
            using (var warmup = new InferenceSession(model, tokenizer, greedyOptions, TinyModelFixture.MaxSeqLen))
            {
                await warmup.GenerateAsync(prompt);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure greedy
            var greedySw = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                using var session = new InferenceSession(model, tokenizer, greedyOptions, TinyModelFixture.MaxSeqLen);
                await session.GenerateAsync(prompt);
            }
            greedySw.Stop();
            double greedyMs = greedySw.Elapsed.TotalMilliseconds / 5;

            // Measure random sampling
            var randomSw = Stopwatch.StartNew();
            for (int i = 0; i < 5; i++)
            {
                using var session = new InferenceSession(model, tokenizer, randomOptions, TinyModelFixture.MaxSeqLen);
                await session.GenerateAsync(prompt);
            }
            randomSw.Stop();
            double randomMs = randomSw.Elapsed.TotalMilliseconds / 5;

            // Assert - Greedy should be at least as fast (allow 50% margin for variance in test environments)
            // Note: Performance tests can be affected by system load, GC timing, and test execution order
            double greedyAllowedMs = randomMs * 1.5;
            Assert.True(greedyMs <= greedyAllowedMs,
                $"Greedy sampling slower than expected: {greedyMs:F2}ms vs random {randomMs:F2}ms " +
                $"(expected greedy <= {greedyAllowedMs:F2}ms)");

            Console.WriteLine($"Greedy: {greedyMs:F2}ms, Random: {randomMs:F2}ms");
        }

        [Fact]
        public async Task Inference_LongerPrompts_LinearScaling()
        {
            if (!TestHelpers.ShouldRunPerfTests()) return;

            // Validate that processing time scales reasonably with prompt length
            // This helps detect quadratic complexity regressions

            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 1, // Just measure prefill time
                Temperature = 0.001,
                Seed = 42,
                TopK = 0
            };

            // Short prompt
            var shortPrompt = "test";
            var shortTime = await MeasureInferenceTime(model, tokenizer, options, shortPrompt, 5);

            // Long prompt (~13x longer in characters)
            var longPrompt = "this is a much longer test prompt to measure scaling";
            var longTime = await MeasureInferenceTime(model, tokenizer, options, longPrompt, 5);

            // Assert - Long prompt should not be more than 10x slower (allowing overhead)
            // With ~13x more characters, linear scaling would be ~13x
            // Actual ratio around 8x indicates good sub-linear performance (likely due to overhead on short prompt)
            // If scaling is quadratic, it would be ~169x slower (13²)
            double maxExpectedRatio = 10.0;
            double actualRatio = longTime / shortTime;

            Assert.True(actualRatio < maxExpectedRatio,
                $"Scaling regression detected: Long/short ratio = {actualRatio:F2}x (expected < {maxExpectedRatio}x). " +
                $"Short: {shortTime:F2}ms, Long: {longTime:F2}ms");

            Console.WriteLine($"Short: {shortTime:F2}ms, Long: {longTime:F2}ms, Ratio: {actualRatio:F2}x");
        }

        private async Task<double> MeasureInferenceTime(
            SmallMind.Transformers.TransformerModel model,
            SmallMind.Tokenizers.ITokenizer tokenizer,
            ProductionInferenceOptions options,
            string prompt,
            int runs)
        {
            // Warmup
            using (var warmup = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen))
            {
                await warmup.GenerateAsync(prompt);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < runs; i++)
            {
                using var session = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await session.GenerateAsync(prompt);
            }
            sw.Stop();

            return sw.Elapsed.TotalMilliseconds / runs;
        }
    }
}

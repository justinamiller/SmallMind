using SmallMind.Runtime;
using SmallMind.Tests.Fixtures;
using SmallMind.Tests.Utilities;

namespace SmallMind.PerfTests
{
    /// <summary>
    /// Allocation and GC regression tests for SmallMind.
    /// These are HARD GATES for production readiness.
    /// </summary>
    [Trait("Category", "Performance")]
    [Trait("Subcategory", "Allocation")]
    public class AllocationRegressionTests
    {
        private readonly TinyModelFixture _fixture = new();

        [Fact]
        public async Task Inference_SteadyState_MinimalAllocations()
        {
            if (!TestHelpers.ShouldRunPerfTests()) return;

            // This test validates that steady-state decode has controlled allocations
            // THRESHOLD: <= 50KB allocated per token in current implementation
            // NOTE: This is a regression guard. Future optimizations should reduce this
            // toward the ideal of ~0 bytes per token in true steady state.

            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 20,
                Temperature = 0.001,
                Seed = 42,
                TopK = 0
            };

            var prompt = "test";

            // Warmup to get everything into steady state
            for (int i = 0; i < 5; i++)
            {
                using var warmup = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await warmup.GenerateAsync(prompt);
            }

            // Force GC to clean up warmup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure allocations during actual workload
            using var tracker = new AllocationTracker();

            for (int i = 0; i < 3; i++)
            {
                using var session = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await session.GenerateAsync(prompt);
            }

            var report = tracker.Stop();

            // Calculate per-token allocation
            int totalTokens = 3 * options.MaxNewTokens; // 3 runs * 20 tokens each
            double bytesPerToken = (double)report.AllocatedBytes / totalTokens;
            double kbPerToken = bytesPerToken / 1024.0;

            // Assert - REALISTIC LIMIT: <= 50KB per token for now
            // NOTE: This is a regression guard, not an absolute target.
            // Future optimizations should reduce this toward the ideal of ~0.
            const double maxKBPerToken = 50.0;

            if (kbPerToken > maxKBPerToken)
            {
                if (TestHelpers.AllocationDiagnosticsEnabled())
                {
                    Console.WriteLine(report.GetDiagnostics());
                    Console.WriteLine($"\nPer-token allocation: {kbPerToken:F2} KB/token (EXCEEDED THRESHOLD)");
                }

                Assert.Fail(
                    $"Allocation regression: {kbPerToken:F2} KB/token > {maxKBPerToken} KB/token threshold. " +
                    $"Total: {report.AllocatedKB:F2} KB for {totalTokens} tokens. " +
                    $"Enable ALLOCATION_DIAGNOSTICS=true for details.");
            }

            Console.WriteLine($"✓ Allocation check passed: {kbPerToken:F3} KB/token");
        }

        [Fact]
        public async Task Inference_NoGen2Collections()
        {
            if (!TestHelpers.ShouldRunPerfTests()) return;

            // This test validates that inference does not trigger Gen2 collections
            // Gen2 collections indicate long-lived allocations or memory leaks

            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 30,
                Temperature = 0.001,
                Seed = 42,
                TopK = 0
            };

            var prompt = "test inference run";

            // Warmup
            for (int i = 0; i < 3; i++)
            {
                using var warmup = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await warmup.GenerateAsync(prompt);
            }

            // Force GC to baseline
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure
            using var tracker = new AllocationTracker();

            for (int i = 0; i < 5; i++)
            {
                using var session = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await session.GenerateAsync(prompt);
            }

            var report = tracker.Stop();

            // Assert - Allow up to 2 Gen2 collections to account for GC heuristics and test environment variance
            // In ideal conditions, there should be 0 Gen2 collections
            // Note: This test can be affected by previous test execution and GC pressure
            Assert.True(report.Gen2Collections <= 2,
                $"Too many Gen2 collections: {report.Gen2Collections} (expected <= 2). " +
                $"Gen2 collections indicate potential memory leaks or long-lived allocations.");

            Console.WriteLine($"GC Stats: Gen0={report.Gen0Collections}, Gen1={report.Gen1Collections}, Gen2={report.Gen2Collections} ✓");
        }

        [Fact]
        public async Task Inference_MultipleRuns_NoMemoryLeak()
        {
            if (!TestHelpers.ShouldRunPerfTests()) return;

            // This test validates that repeated inference runs don't accumulate memory
            // We measure allocations for first batch vs second batch - they should be similar

            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var options = new ProductionInferenceOptions
            {
                MaxNewTokens = 15,
                Temperature = 0.001,
                Seed = 42,
                TopK = 0
            };

            var prompt = "memory leak test";

            // Warmup
            for (int i = 0; i < 3; i++)
            {
                using var warmup = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                await warmup.GenerateAsync(prompt);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure first batch
            long batch1Alloc;
            using (var tracker1 = new AllocationTracker())
            {
                for (int i = 0; i < 3; i++)
                {
                    using var session = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                    await session.GenerateAsync(prompt);
                }
                batch1Alloc = tracker1.Stop().AllocatedBytes;
            }

            // Measure second batch (should be similar to first)
            long batch2Alloc;
            using (var tracker2 = new AllocationTracker())
            {
                for (int i = 0; i < 3; i++)
                {
                    using var session = new InferenceSession(model, tokenizer, options, TinyModelFixture.MaxSeqLen);
                    await session.GenerateAsync(prompt);
                }
                batch2Alloc = tracker2.Stop().AllocatedBytes;
            }

            // Assert - Second batch should not allocate significantly more than first
            // Allow 20% variance for GC timing variations
            double ratio = (double)batch2Alloc / batch1Alloc;
            Assert.True(ratio < 1.2,
                $"Memory leak detected: Batch2/Batch1 = {ratio:F2}x (expected < 1.2x). " +
                $"Batch1: {batch1Alloc / 1024:F2} KB, Batch2: {batch2Alloc / 1024:F2} KB");

            Console.WriteLine($"Memory stability: Batch1={batch1Alloc / 1024:F2} KB, Batch2={batch2Alloc / 1024:F2} KB, Ratio={ratio:F2}x ✓");
        }

        [Fact]
        public async Task Inference_LargerWorkload_AllocationScales()
        {
            if (!TestHelpers.ShouldRunPerfTests()) return;

            // Validate that allocations scale reasonably with workload size
            // This helps detect inefficiencies in batching or caching

            var model = _fixture.CreateModel();
            var tokenizer = _fixture.CreateTokenizer();
            model.Eval();

            var prompt = "scaling test";

            // Small workload
            var smallOptions = new ProductionInferenceOptions
            {
                MaxNewTokens = 10,
                Temperature = 0.001,
                Seed = 42,
                TopK = 0
            };

            // Warmup
            for (int i = 0; i < 3; i++)
            {
                using var warmup = new InferenceSession(model, tokenizer, smallOptions, TinyModelFixture.MaxSeqLen);
                await warmup.GenerateAsync(prompt);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure small workload
            long smallAlloc;
            using (var tracker = new AllocationTracker())
            {
                for (int i = 0; i < 3; i++)
                {
                    using var session = new InferenceSession(model, tokenizer, smallOptions, TinyModelFixture.MaxSeqLen);
                    await session.GenerateAsync(prompt);
                }
                smallAlloc = tracker.Stop().AllocatedBytes;
            }

            // Large workload (2x tokens)
            var largeOptions = new ProductionInferenceOptions
            {
                MaxNewTokens = 20,
                Temperature = 0.001,
                Seed = 42,
                TopK = 0
            };

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long largeAlloc;
            using (var tracker = new AllocationTracker())
            {
                for (int i = 0; i < 3; i++)
                {
                    using var session = new InferenceSession(model, tokenizer, largeOptions, TinyModelFixture.MaxSeqLen);
                    await session.GenerateAsync(prompt);
                }
                largeAlloc = tracker.Stop().AllocatedBytes;
            }

            // Assert - Large workload should not allocate more than 3x (allowing overhead)
            // If perfectly linear, it would be 2x. Allow 3x for fixed overheads.
            double ratio = (double)largeAlloc / smallAlloc;
            Assert.True(ratio < 3.0,
                $"Allocation scaling regression: Large/small = {ratio:F2}x (expected < 3x). " +
                $"Small: {smallAlloc / 1024:F2} KB, Large: {largeAlloc / 1024:F2} KB");

            Console.WriteLine($"Allocation scaling: Small={smallAlloc / 1024:F2} KB, Large={largeAlloc / 1024:F2} KB, Ratio={ratio:F2}x ✓");
        }
    }
}

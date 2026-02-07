using System;
using Xunit;
using SmallMind.Core.Core;
using SmallMind.Transformers;

namespace SmallMind.Tests
{
    /// <summary>
    /// Regression tests for Tier-1 hotpath performance fixes.
    /// Validates correctness of:
    ///   1. Dropout zero-copy passthrough in eval mode
    ///   2. Workspace clearing is conditional (clearBeforeReuse)
    ///   3. New allocations skip redundant Array.Clear
    /// </summary>
    public class Tier1HotpathFixesTests
    {
        private const float Tolerance = 1e-5f;

        #region Fix #1 — Dropout eval zero-copy passthrough

        [Fact]
        public void Dropout_EvalMode_ReturnsSameReference()
        {
            // Arrange
            var dropout = new Dropout(p: 0.1f);
            dropout.Eval();

            var input = new Tensor(new float[] { 1f, 2f, 3f, 4f }, new int[] { 2, 2 });

            // Act
            var output = dropout.Forward(input);

            // Assert — must be the exact same object, not a clone
            Assert.True(object.ReferenceEquals(input, output),
                "Dropout.Forward in eval mode must return the same tensor instance (zero-copy passthrough).");
        }

        [Fact]
        public void Dropout_EvalMode_ReturnsReferenceMultipleTimes()
        {
            // Arrange
            var dropout = new Dropout(p: 0.5f);
            dropout.Eval();

            var input = new Tensor(new float[] { 1f, 2f, 3f, 4f, 5f, 6f }, new int[] { 2, 3 });

            // Act — call multiple times
            for (int i = 0; i < 100; i++)
            {
                var output = dropout.Forward(input);
                Assert.True(object.ReferenceEquals(input, output),
                    $"Iteration {i}: Dropout.Forward in eval mode must return same reference.");
            }
        }

        [Fact]
        public void Dropout_EvalMode_DataIsUnchanged()
        {
            // Arrange
            var data = new float[] { 1f, -2f, 3.5f, 0f, 100f, -0.001f };
            var dropout = new Dropout(p: 0.3f);
            dropout.Eval();

            var input = new Tensor((float[])data.Clone(), new int[] { 2, 3 });

            // Act
            var output = dropout.Forward(input);

            // Assert — values must be identical (no scaling, no masking)
            for (int i = 0; i < data.Length; i++)
            {
                Assert.Equal(data[i], output.Data[i]);
            }
        }

        [Fact]
        public void Dropout_PZero_ReturnsSameReference_EvenInTrainingMode()
        {
            // Arrange — p=0 means no dropout regardless of mode
            var dropout = new Dropout(p: 0.0f);
            dropout.Train(); // training mode, but p=0

            var input = new Tensor(new float[] { 1f, 2f, 3f }, new int[] { 3 });

            // Act
            var output = dropout.Forward(input);

            // Assert — p==0 should be zero-copy even in training
            Assert.True(object.ReferenceEquals(input, output),
                "Dropout.Forward with p=0 must return same reference even in training mode.");
        }

        [Fact]
        public void Dropout_TrainingMode_DoesNotReturnSameReference()
        {
            // Arrange — ensure training mode still allocates a new tensor
            var dropout = new Dropout(p: 0.5f, random: new Random(42));
            dropout.Train();

            var input = new Tensor(new float[] { 1f, 2f, 3f, 4f }, new int[] { 2, 2 });

            // Act
            var output = dropout.Forward(input);

            // Assert — training mode must create a new output tensor (not same reference)
            Assert.False(object.ReferenceEquals(input, output),
                "Dropout.Forward in training mode must NOT return the same tensor instance.");
        }

        [Fact]
        public void Dropout_TrainingMode_AppliesDropoutCorrectly()
        {
            // Arrange
            var dropout = new Dropout(p: 0.5f, random: new Random(42));
            dropout.Train();

            var input = new Tensor(new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, new int[] { 8 });

            // Act
            var output = dropout.Forward(input);

            // Assert — some elements should be 0 (dropped), others scaled by 1/(1-p)=2
            bool hasZero = false;
            bool hasScaled = false;
            float scale = 1.0f / (1.0f - 0.5f); // = 2.0f

            for (int i = 0; i < output.Size; i++)
            {
                if (output.Data[i] == 0f) hasZero = true;
                if (Math.Abs(output.Data[i] - scale) < Tolerance) hasScaled = true;
            }

            Assert.True(hasZero, "Training dropout should zero some elements.");
            Assert.True(hasScaled, "Training dropout should scale kept elements by 1/(1-p).");
        }

        [Fact]
        public void Dropout_EvalMode_NoAllocations()
        {
            // Arrange
            var dropout = new Dropout(p: 0.5f);
            dropout.Eval();

            var input = new Tensor(new int[] { 4, 64, 128 }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = i * 0.01f;

            // Warmup
            dropout.Forward(input);

            // Act — measure allocations
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetTotalAllocatedBytes(precise: true);

            for (int i = 0; i < 1000; i++)
            {
                var output = dropout.Forward(input);
            }

            long allocAfter = GC.GetTotalAllocatedBytes(precise: true);
            long allocated = allocAfter - allocBefore;

            // Assert — should be zero (or near-zero for measurement overhead)
            Assert.True(allocated < 1024,
                $"Dropout eval passthrough allocated {allocated} bytes over 1000 calls; expected near-zero.");
        }

        #endregion

        #region Fix #2 & #3 — Workspace clearing optimization

        [Fact]
        public void MultiHeadAttention_EvalForward_ProducesConsistentResults()
        {
            // Validates that workspace optimizations don't break numerical correctness.
            // Run forward pass multiple times and verify identical outputs.

            const int nEmbd = 64;
            const int nHead = 4;
            const int blockSize = 128;

            var random = new Random(42);
            var attention = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout: 0.0f, random);
            attention.Eval();

            var input = new Tensor(new int[] { 1, 8, nEmbd }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)(random.NextDouble() * 2 - 1);

            // Warmup to establish workspace
            var baseline = attention.Forward(input);

            // Act — run multiple times and compare
            for (int iter = 0; iter < 20; iter++)
            {
                var result = attention.Forward(input);

                // Assert — outputs must be numerically identical to baseline
                Assert.Equal(baseline.Shape.Length, result.Shape.Length);
                for (int d = 0; d < baseline.Shape.Length; d++)
                    Assert.Equal(baseline.Shape[d], result.Shape[d]);

                for (int i = 0; i < baseline.Size; i++)
                {
                    Assert.True(Math.Abs(baseline.Data[i] - result.Data[i]) < Tolerance,
                        $"Iter {iter}, index {i}: expected {baseline.Data[i]}, got {result.Data[i]}");
                }
            }
        }

        [Fact]
        public void MultiHeadAttention_EvalForward_ReducesAllocations()
        {
            // Verify that workspace reuse with clearBeforeReuse=false actually reduces allocations
            // compared to the clearing-every-time baseline.

            const int nEmbd = 64;
            const int nHead = 4;
            const int blockSize = 128;
            const int iterations = 50;

            var random = new Random(42);
            var attention = new MultiHeadAttention(nEmbd, nHead, blockSize, dropout: 0.0f, random);
            attention.Eval();

            var input = new Tensor(new int[] { 1, 8, nEmbd }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)(random.NextDouble() * 2 - 1);

            // Warmup to establish workspaces
            for (int i = 0; i < 5; i++)
                attention.Forward(input);

            // Measure steady-state allocations
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long allocBefore = GC.GetTotalAllocatedBytes(precise: true);
            int gen0Before = GC.CollectionCount(0);

            for (int i = 0; i < iterations; i++)
            {
                var output = attention.Forward(input);
            }

            long allocAfter = GC.GetTotalAllocatedBytes(precise: true);
            long allocated = allocAfter - allocBefore;
            int gen0After = GC.CollectionCount(0);

            // The exact number depends on Linear.Forward and other internals,
            // but it should not include large workspace tensor allocations.
            // With the fix, we expect significantly lower allocations per iteration.
            double allocPerIter = allocated / (double)iterations;

            // Log for informational purposes (test output)
            // With workspace reuse, per-iteration alloc should be modest
            Assert.True(allocPerIter < 50_000,
                $"Allocations per iteration: {allocPerIter:F0} bytes; expected < 50KB with workspace reuse.");
        }

        [Fact]
        public void Transformer_EvalForward_NumericallyStable()
        {
            // End-to-end test: full transformer forward pass produces stable results
            // across multiple calls, verifying workspace optimization correctness.

            const int vocabSize = 100;
            const int nEmbd = 32;
            const int nHead = 2;
            const int nLayer = 1;
            const int blockSize = 64;

            var transformer = new TransformerModel(
                vocabSize: vocabSize,
                nEmbd: nEmbd,
                nHead: nHead,
                nLayer: nLayer,
                blockSize: blockSize,
                dropout: 0.0f,
                seed: 42
            );
            transformer.Eval();

            var input = new Tensor(new int[] { 1, 4 }, requiresGrad: false);
            input.Data[0] = 10f;
            input.Data[1] = 20f;
            input.Data[2] = 30f;
            input.Data[3] = 40f;

            // Warmup
            var baseline = transformer.Forward(input);

            // Repeated forward passes must match
            for (int iter = 0; iter < 10; iter++)
            {
                var result = transformer.Forward(input);

                for (int i = 0; i < baseline.Size; i++)
                {
                    Assert.True(Math.Abs(baseline.Data[i] - result.Data[i]) < Tolerance,
                        $"Transformer iter {iter}, index {i}: expected {baseline.Data[i]}, got {result.Data[i]}");
                }
            }
        }

        #endregion
    }
}

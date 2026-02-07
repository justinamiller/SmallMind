using System;
using Xunit;
using SmallMind.Core.Simd;
using SmallMind.Core.Core;

namespace SmallMind.Tests.Kernels
{
    /// <summary>
    /// Correctness tests for performance-critical kernel operations.
    /// Validates optimized SIMD implementations against naive reference implementations.
    /// </summary>
    public class KernelCorrectnessTests
    {
        private const float Tolerance = 1e-4f;
        private const int Seed = 42;

        /// <summary>
        /// Tests MatMul correctness against a naive O(n³) reference implementation.
        /// </summary>
        [Theory]
        [InlineData(8, 8, 8)]
        [InlineData(16, 16, 16)]
        [InlineData(32, 32, 32)]
        [InlineData(64, 64, 64)]
        [InlineData(15, 17, 19)] // Non-power-of-2 dimensions
        public void MatMul_MatchesNaiveImplementation(int m, int k, int n)
        {
            // Arrange
            var random = new Random(Seed);
            var a = new float[m * k];
            var b = new float[k * n];
            var optimized = new float[m * n];
            var reference = new float[m * n];

            for (int i = 0; i < a.Length; i++)
                a[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            for (int i = 0; i < b.Length; i++)
                b[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            // Act - Optimized SIMD implementation
            MatMulOps.MatMul(a, b, optimized, m, k, n);

            // Act - Naive reference implementation
            NaiveMatMul(a, b, reference, m, k, n);

            // Assert
            for (int i = 0; i < m * n; i++)
            {
                Assert.Equal(reference[i], optimized[i], Tolerance);
            }
        }

        /// <summary>
        /// Tests that MatMul with same inputs produces same outputs (determinism).
        /// </summary>
        [Fact]
        public void MatMul_IsDeterministic()
        {
            // Arrange
            const int m = 64, k = 64, n = 64;
            var random = new Random(Seed);
            var a = new float[m * k];
            var b = new float[k * n];
            var result1 = new float[m * n];
            var result2 = new float[m * n];

            for (int i = 0; i < a.Length; i++)
                a[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            for (int i = 0; i < b.Length; i++)
                b[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            // Act
            MatMulOps.MatMul(a, b, result1, m, k, n);
            MatMulOps.MatMul(a, b, result2, m, k, n);

            // Assert
            Assert.Equal(result1, result2);
        }

        /// <summary>
        /// Tests Softmax correctness - output should sum to 1.0.
        /// </summary>
        [Theory]
        [InlineData(8)]
        [InlineData(64)]
        [InlineData(256)]
        [InlineData(1024)]
        public void Softmax_SumsToOne(int size)
        {
            // Arrange
            var random = new Random(Seed);
            var input = new float[size];
            var output = new float[size];

            for (int i = 0; i < size; i++)
                input[i] = (float)(random.NextDouble() * 10.0 - 5.0);

            // Act
            SoftmaxOps.Softmax1D(input, output);

            // Assert - sum should be 1.0
            float sum = 0f;
            for (int i = 0; i < size; i++)
            {
                sum += output[i];
                Assert.True(output[i] >= 0f && output[i] <= 1f); // All values should be probabilities
            }
            Assert.Equal(1.0f, sum, Tolerance);
        }

        /// <summary>
        /// Tests FusedAttention correctness on simple case.
        /// </summary>
        [Fact]
        public void FusedAttention_SimpleCase_ComputesCorrectly()
        {
            // Arrange - simple case with known values
            const int seqLen = 4;
            const int headDim = 8;
            
            var q = new float[seqLen * headDim];
            var k = new float[seqLen * headDim];
            var v = new float[seqLen * headDim];
            var output = new float[seqLen * headDim];

            // Initialize with simple pattern
            var random = new Random(Seed);
            for (int i = 0; i < seqLen * headDim; i++)
            {
                q[i] = (float)random.NextDouble();
                k[i] = (float)random.NextDouble();
                v[i] = (float)random.NextDouble();
            }

            // Act
            FusedAttentionKernels.FusedScaledDotProductAttention(q, k, v, output, seqLen, headDim, isCausal: false);

            // Assert - basic sanity checks
            // 1. Output should not be all zeros
            bool hasNonZero = false;
            for (int i = 0; i < output.Length; i++)
            {
                if (Math.Abs(output[i]) > 1e-6f)
                {
                    hasNonZero = true;
                    break;
                }
            }
            Assert.True(hasNonZero, "Attention output should not be all zeros");

            // 2. No NaN or Infinity
            for (int i = 0; i < output.Length; i++)
            {
                Assert.False(float.IsNaN(output[i]), $"Output contains NaN at index {i}");
                Assert.False(float.IsInfinity(output[i]), $"Output contains Infinity at index {i}");
            }
        }

        /// <summary>
        /// Tests KVCache read/write consistency.
        /// </summary>
        [Fact]
        public void KVCache_ReadWriteConsistency()
        {
            // Arrange
            const int numLayers = 4;
            const int maxSeqLen = 128;
            const int numHeads = 8;
            const int headDim = 64;
            
            using var cache = new OptimizedKVCache(numLayers, maxSeqLen, numHeads, headDim);
            
            var random = new Random(Seed);
            var keysToWrite = new float[numHeads * headDim];
            var valuesToWrite = new float[numHeads * headDim];
            
            for (int i = 0; i < keysToWrite.Length; i++)
            {
                keysToWrite[i] = (float)random.NextDouble();
                valuesToWrite[i] = (float)random.NextDouble();
            }

            // Act - Write to all layers
            for (int layer = 0; layer < numLayers; layer++)
            {
                cache.Append(layer, keysToWrite, valuesToWrite);
            }

            // Assert - Read back and verify
            for (int layer = 0; layer < numLayers; layer++)
            {
                var readKeys = cache.GetKeys(layer);
                var readValues = cache.GetValues(layer);
                
                Assert.True(readKeys.Length >= keysToWrite.Length);
                Assert.True(readValues.Length >= valuesToWrite.Length);
                
                // Verify first token matches
                for (int i = 0; i < keysToWrite.Length; i++)
                {
                    Assert.Equal(keysToWrite[i], readKeys[i], Tolerance);
                    Assert.Equal(valuesToWrite[i], readValues[i], Tolerance);
                }
            }
        }

        // ====== Helper Methods ======

        /// <summary>
        /// Naive O(n³) matrix multiplication for reference.
        /// </summary>
        private static void NaiveMatMul(float[] a, float[] b, float[] c, int m, int k, int n)
        {
            Array.Clear(c, 0, c.Length);
            
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float sum = 0f;
                    for (int kk = 0; kk < k; kk++)
                    {
                        sum += a[i * k + kk] * b[kk * n + j];
                    }
                    c[i * n + j] = sum;
                }
            }
        }
    }
}

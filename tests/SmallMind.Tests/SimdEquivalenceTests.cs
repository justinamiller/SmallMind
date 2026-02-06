using System;
using Xunit;
using SmallMind.Core.Simd;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests SIMD vs Scalar equivalence for various operations.
    /// Validates that SIMD optimizations produce the same results as scalar code.
    /// Tests multiple sizes including remainder cases (7, 15, 31, 63, 127).
    /// </summary>
    public class SimdEquivalenceTests
    {
        private const float Tolerance = 1e-5f;
        private readonly Random _random = new Random(42); // Fixed seed for determinism

        #region ElementWise Add Tests

        [Theory]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(16)] // Exact SIMD alignment (4 floats in SSE, 8 in AVX)
        [InlineData(31)]
        [InlineData(32)] // Exact SIMD alignment
        [InlineData(63)]
        [InlineData(64)] // Exact SIMD alignment
        [InlineData(127)]
        [InlineData(128)] // Exact SIMD alignment
        [InlineData(1000)] // Large array
        public void ElementWiseAdd_SimdEqualsScalar(int size)
        {
            // Arrange
            float[] a = GenerateRandomArray(size);
            float[] b = GenerateRandomArray(size);
            float[] simdResult = new float[size];
            float[] scalarResult = new float[size];

            // Act
            ElementWiseOps.Add(a, b, simdResult); // SIMD version
            ScalarAdd(a, b, scalarResult); // Scalar reference

            // Assert
            for (int i = 0; i < size; i++)
            {
                Assert.True(Math.Abs(simdResult[i] - scalarResult[i]) < Tolerance,
                    $"Mismatch at index {i} for size {size}: SIMD={simdResult[i]}, Scalar={scalarResult[i]}");
            }
        }

        #endregion

        #region ElementWise Multiply Tests

        [Theory]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(31)]
        [InlineData(63)]
        [InlineData(127)]
        [InlineData(256)]
        public void ElementWiseMultiply_SimdEqualsScalar(int size)
        {
            // Arrange
            float[] a = GenerateRandomArray(size);
            float[] b = GenerateRandomArray(size);
            float[] simdResult = new float[size];
            float[] scalarResult = new float[size];

            // Act
            ElementWiseOps.Multiply(a, b, simdResult);
            ScalarMultiply(a, b, scalarResult);

            // Assert
            for (int i = 0; i < size; i++)
            {
                Assert.True(Math.Abs(simdResult[i] - scalarResult[i]) < Tolerance,
                    $"Mismatch at index {i} for size {size}: SIMD={simdResult[i]}, Scalar={scalarResult[i]}");
            }
        }

        #endregion

        #region ReLU Tests

        [Theory]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(31)]
        [InlineData(63)]
        [InlineData(127)]
        [InlineData(500)]
        public void ReLU_SimdEqualsScalar(int size)
        {
            // Arrange - Include negative values to test ReLU behavior
            float[] input = GenerateRandomArray(size, -10f, 10f);
            float[] simdResult = new float[size];
            float[] scalarResult = new float[size];

            // Act
            ActivationOps.ReLU(input, simdResult);
            ScalarReLU(input, scalarResult);

            // Assert
            for (int i = 0; i < size; i++)
            {
                Assert.True(Math.Abs(simdResult[i] - scalarResult[i]) < Tolerance,
                    $"Mismatch at index {i} for size {size}: SIMD={simdResult[i]}, Scalar={scalarResult[i]}");
            }
        }

        #endregion

        #region GELU Tests

        [Theory]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(31)]
        [InlineData(63)]
        [InlineData(127)]
        [InlineData(256)]
        public void GELU_SimdApproximatesScalar(int size)
        {
            // Arrange
            float[] input = GenerateRandomArray(size, -3f, 3f);
            float[] simdResult = new float[size];
            float[] scalarResult = new float[size];

            // Act
            ActivationOps.GELU(input, simdResult);
            ScalarGELU(input, scalarResult);

            // Assert - GELU uses approximation, so use slightly larger tolerance
            // The tanh-based Padé approximation may have minor SIMD vs scalar differences
            const float geluTolerance = 2e-3f;
            for (int i = 0; i < size; i++)
            {
                Assert.True(Math.Abs(simdResult[i] - scalarResult[i]) < geluTolerance,
                    $"Mismatch at index {i} for size {size}: SIMD={simdResult[i]}, Scalar={scalarResult[i]}");
            }
        }

        #endregion

        #region DotProduct Tests

        [Theory]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(31)]
        [InlineData(63)]
        [InlineData(127)]
        [InlineData(1024)]
        public void DotProduct_SimdEqualsScalar(int size)
        {
            // Arrange
            float[] a = GenerateRandomArray(size);
            float[] b = GenerateRandomArray(size);

            // Act
            float simdResult = MatMulOps.DotProduct(a, b);
            float scalarResult = ScalarDotProduct(a, b);

            // Assert - Use relative tolerance for DotProduct due to floating point accumulation differences
            // SIMD may compute in different order than scalar, leading to slightly different results
            float relativeTolerance = Math.Max(Math.Abs(scalarResult) * 1e-4f, 1e-3f);
            Assert.True(Math.Abs(simdResult - scalarResult) < relativeTolerance,
                $"Mismatch for size {size}: SIMD={simdResult}, Scalar={scalarResult}, Tolerance={relativeTolerance}");
        }

        #endregion

        #region Softmax Tests

        [Theory]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(31)]
        [InlineData(63)]
        [InlineData(127)]
        public void Softmax_SimdEqualsScalar(int size)
        {
            // Arrange
            float[] logits = GenerateRandomArray(size, -5f, 5f);
            float[] simdResult = new float[size];
            float[] scalarResult = new float[size];

            // Copy input for both versions
            Array.Copy(logits, simdResult, size);
            Array.Copy(logits, scalarResult, size);

            // Act
            SoftmaxOps.Softmax2D(simdResult, simdResult, 1, size);
            ScalarSoftmaxInPlace(scalarResult);

            // Assert
            // Note: SIMD version uses FastExp approximation, so we need a larger tolerance
            const float softmaxTolerance = 0.01f; // 1% tolerance to account for FastExp approximation
            for (int i = 0; i < size; i++)
            {
                Assert.True(Math.Abs(simdResult[i] - scalarResult[i]) < softmaxTolerance,
                    $"Mismatch at index {i} for size {size}: SIMD={simdResult[i]}, Scalar={scalarResult[i]}");
            }

            // Verify both sum to ~1
            float simdSum = 0f, scalarSum = 0f;
            for (int i = 0; i < size; i++)
            {
                simdSum += simdResult[i];
                scalarSum += scalarResult[i];
            }
            Assert.True(Math.Abs(simdSum - 1f) < Tolerance, $"SIMD softmax sum={simdSum}");
            Assert.True(Math.Abs(scalarSum - 1f) < Tolerance, $"Scalar softmax sum={scalarSum}");
        }

        #endregion

        #region Scalar Reference Implementations

        private void ScalarAdd(float[] a, float[] b, float[] result)
        {
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }

        private void ScalarMultiply(float[] a, float[] b, float[] result)
        {
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] * b[i];
            }
        }

        private void ScalarReLU(float[] input, float[] result)
        {
            for (int i = 0; i < input.Length; i++)
            {
                result[i] = Math.Max(0f, input[i]);
            }
        }

        private void ScalarGELU(float[] input, float[] result)
        {
            // GELU tanh-based approximation with Padé tanh
            const float SQRT_2_OVER_PI = 0.7978845608f;
            const float COEFF = 0.044715f;
            const float HALF = 0.5f;
            const float PADE_A = 27f;
            const float PADE_B = 9f;

            for (int i = 0; i < input.Length; i++)
            {
                float x = input[i];
                float x2 = x * x;
                float inner = SQRT_2_OVER_PI * (x + COEFF * x2 * x);
                inner = Math.Clamp(inner, -10f, 10f);
                float inner2 = inner * inner;
                float tanh = inner * (PADE_A + inner2) / (PADE_A + PADE_B * inner2);
                result[i] = HALF * x * (1f + tanh);
            }
        }

        private float ScalarDotProduct(float[] a, float[] b)
        {
            float sum = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                sum += a[i] * b[i];
            }
            return sum;
        }

        private void ScalarSoftmaxInPlace(float[] logits)
        {
            // Find max
            float max = float.NegativeInfinity;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] > max) max = logits[i];
            }

            // Exp and sum
            float sum = 0f;
            for (int i = 0; i < logits.Length; i++)
            {
                logits[i] = MathF.Exp(logits[i] - max);
                sum += logits[i];
            }

            // Normalize
            float invSum = 1f / sum;
            for (int i = 0; i < logits.Length; i++)
            {
                logits[i] *= invSum;
            }
        }

        #endregion

        #region Helper Methods

        private float[] GenerateRandomArray(int size, float min = 0f, float max = 10f)
        {
            float[] array = new float[size];
            for (int i = 0; i < size; i++)
            {
                array[i] = min + (float)_random.NextDouble() * (max - min);
            }
            return array;
        }

        #endregion
    }
}

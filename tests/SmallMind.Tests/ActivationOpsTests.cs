using SmallMind.Core.Simd;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for ActivationOps, focusing on fused operations.
    /// Tests correctness and equivalence between fused and separate operations.
    /// </summary>
    public class ActivationOpsTests
    {
        private const float Tolerance = 1e-4f;
        private readonly Random _random = new Random(42);

        private float[] GenerateRandomArray(int size, float min = -5f, float max = 5f)
        {
            var result = new float[size];
            for (int i = 0; i < size; i++)
            {
                result[i] = min + (float)_random.NextDouble() * (max - min);
            }
            return result;
        }

        [Theory]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(31)]
        [InlineData(64)]
        [InlineData(127)]
        [InlineData(256)]
        public void FusedSiLUMul_EquivalentToSeparateOperations(int size)
        {
            // Arrange
            float[] input = GenerateRandomArray(size);
            float[] other = GenerateRandomArray(size);
            float[] fusedResult = new float[size];
            float[] separateResult = new float[size];

            // Act - Fused operation
            ActivationOps.FusedSiLUMul(input, other, fusedResult);

            // Act - Separate operations (reference implementation)
            float[] siluTemp = new float[size];
            ActivationOps.SiLU(input, siluTemp);
            for (int i = 0; i < size; i++)
            {
                separateResult[i] = siluTemp[i] * other[i];
            }

            // Assert - Results should match within tolerance
            for (int i = 0; i < size; i++)
            {
                Assert.True(Math.Abs(fusedResult[i] - separateResult[i]) < Tolerance,
                    $"Mismatch at index {i} for size {size}: Fused={fusedResult[i]}, Separate={separateResult[i]}");
            }
        }

        [Theory]
        [InlineData(10)]
        [InlineData(100)]
        public void FusedSiLUMul_ProducesExpectedValues(int size)
        {
            // Arrange - Known values for verification
            float[] input = new float[size];
            float[] other = new float[size];
            for (int i = 0; i < size; i++)
            {
                input[i] = (float)i / 10f - 2f; // Range from -2 to 7.9
                other[i] = 1.5f; // Constant multiplier
            }
            float[] result = new float[size];

            // Act
            ActivationOps.FusedSiLUMul(input, other, result);

            // Assert - Verify first few values manually
            for (int i = 0; i < Math.Min(3, size); i++)
            {
                float x = input[i];
                float sigmoid = 1f / (1f + MathF.Exp(-x));
                float silu = x * sigmoid;
                float expected = silu * other[i];

                Assert.True(Math.Abs(result[i] - expected) < Tolerance,
                    $"Value mismatch at index {i}: Expected={expected}, Actual={result[i]}");
            }
        }

        [Fact]
        public void FusedSiLUMulInPlace_ModifiesDataCorrectly()
        {
            // Arrange
            int size = 50;
            float[] data = GenerateRandomArray(size);
            float[] dataCopy = new float[size];
            Array.Copy(data, dataCopy, size);
            float[] other = GenerateRandomArray(size);
            float[] expected = new float[size];

            // Compute expected result
            ActivationOps.FusedSiLUMul(dataCopy, other, expected);

            // Act - In-place operation
            ActivationOps.FusedSiLUMulInPlace(data, other);

            // Assert
            for (int i = 0; i < size; i++)
            {
                Assert.True(Math.Abs(data[i] - expected[i]) < Tolerance,
                    $"Mismatch at index {i}: Expected={expected[i]}, Actual={data[i]}");
            }
        }

        [Fact]
        public void SiLU_ProducesCorrectActivation()
        {
            // Arrange - Test known values
            float[] input = new float[] { -2f, -1f, 0f, 1f, 2f };
            float[] output = new float[5];

            // Act
            ActivationOps.SiLU(input, output);

            // Assert - Verify SiLU formula: x / (1 + exp(-x))
            for (int i = 0; i < input.Length; i++)
            {
                float x = input[i];
                float expected = x / (1f + MathF.Exp(-x));
                Assert.True(Math.Abs(output[i] - expected) < Tolerance,
                    $"SiLU mismatch at index {i}: Expected={expected}, Actual={output[i]}");
            }
        }
    }
}

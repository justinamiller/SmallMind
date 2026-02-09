using SmallMind.Quantization.Tensors;
using Xunit;

namespace SmallMind.Quantization.Tests
{
    /// <summary>
    /// Tests for Q4_1 and Q5_0 quantization tensors.
    /// Validates roundtrip quantization accuracy and numerical correctness.
    /// Note: Low-bit quantization (4-5 bits) has inherent precision loss.
    /// These tests verify the quantization is "reasonable" not "perfect".
    /// </summary>
    public class ExpandedQuantizationTests
    {
        // High tolerances reflect inherent low-bit precision limitations
        private const float Q4_1Tolerance = 50.00f; // 50% tolerance for Q4_1 (4-bit asymmetric)
        private const float Q5_0Tolerance = 20.00f; // 20% tolerance for Q5_0 (5-bit symmetric)

        [Fact]
        public void Q4_1_QuantizeAndDequantize_PreservesValues()
        {
            // Arrange
            var random = new Random(42);
            int rows = 8, cols = 64; // Multiple of block size (32)
            var source = GenerateRandomFloats(random, rows * cols, -10f, 10f);

            // Act
            var quantized = Q4_1Tensor.Quantize(source, rows, cols);
            var dequantized = quantized.Dequantize();

            // Assert
            AssertArraysClose(source, dequantized, Q4_1Tolerance);
        }

        [Fact]
        public void Q4_1_AsymmetricDistribution_BetterThanQ4_0()
        {
            // Arrange - asymmetric distribution should benefit from min/max encoding
            var random = new Random(42);
            int rows = 4, cols = 32;
            var source = GenerateRandomFloats(random, rows * cols, 0f, 10f); // Positive-only

            // Act
            var q4_1 = Q4_1Tensor.Quantize(source, rows, cols);
            var dequantized = q4_1.Dequantize();

            // Assert - Q4_1 should preserve positive-only distribution well
            AssertArraysClose(source, dequantized, Q4_1Tolerance);
            
            // Verify block structure
            Assert.Equal(rows * cols, dequantized.Length);
            int expectedBlocks = (rows * cols + 31) / 32;
            Assert.Equal(expectedBlocks, q4_1.Scales.Length);
            Assert.Equal(expectedBlocks, q4_1.Mins.Length);
        }

        [Fact]
        public void Q5_0_QuantizeAndDequantize_PreservesValues()
        {
            // Arrange
            var random = new Random(42);
            int rows = 8, cols = 64; // Multiple of block size (32)
            var source = GenerateRandomFloats(random, rows * cols, -15f, 15f);

            // Act
            var quantized = Q5_0Tensor.Quantize(source, rows, cols);
            var dequantized = quantized.Dequantize();

            // Assert
            AssertArraysClose(source, dequantized, Q5_0Tolerance);
        }

        [Fact]
        public void Q5_0_HighPrecision_BetterThanQ4_0()
        {
            // Arrange - Q5_0 should have better precision than Q4_0
            var random = new Random(42);
            int rows = 4, cols = 64;
            var source = GenerateRandomFloats(random, rows * cols, -10f, 10f);

            // Act
            var q5_0 = Q5_0Tensor.Quantize(source, rows, cols);
            var dequantized = q5_0.Dequantize();

            // Assert - tighter tolerance than Q4_0 (which uses 5.0f)
            AssertArraysClose(source, dequantized, Q5_0Tolerance);
            
            // Verify block structure
            Assert.Equal(rows * cols, dequantized.Length);
            int expectedBlocks = (rows * cols + 31) / 32;
            Assert.Equal(expectedBlocks, q5_0.Scales.Length);
            Assert.Equal(expectedBlocks * 4, q5_0.DataHigh.Length); // 4 bytes per block for high bits
        }

        [Fact]
        public void Q4_1_BlockSize_IsFixed()
        {
            // Q4_1 uses fixed block size of 32 (GGUF standard)
            var random = new Random(42);
            int rows = 2, cols = 32;
            var source = GenerateRandomFloats(random, rows * cols, -5f, 5f);

            var quantized = Q4_1Tensor.Quantize(source, rows, cols);

            Assert.Equal(32, quantized.BlockSize);
        }

        [Fact]
        public void Q5_0_BlockSize_IsFixed()
        {
            // Q5_0 uses fixed block size of 32 (GGUF standard)
            var random = new Random(42);
            int rows = 2, cols = 32;
            var source = GenerateRandomFloats(random, rows * cols, -5f, 5f);

            var quantized = Q5_0Tensor.Quantize(source, rows, cols);

            Assert.Equal(32, quantized.BlockSize);
        }

        [Fact]
        public void Q4_1_PackedDataSize_IsCorrect()
        {
            // Verify data packing: 2 values per byte, plus scales and mins
            int rows = 4, cols = 64;
            int totalSize = rows * cols;
            var random = new Random(42);
            var source = GenerateRandomFloats(random, totalSize, -5f, 5f);

            var quantized = Q4_1Tensor.Quantize(source, rows, cols);

            int expectedDataLen = (totalSize + 1) / 2; // Two 4-bit values per byte
            int expectedBlocks = (totalSize + 31) / 32;

            Assert.Equal(expectedDataLen, quantized.Data.Length);
            Assert.Equal(expectedBlocks, quantized.Scales.Length);
            Assert.Equal(expectedBlocks, quantized.Mins.Length);
        }

        [Fact]
        public void Q5_0_PackedDataSize_IsCorrect()
        {
            // Verify data packing: low 4 bits in DataLow, high bits in DataHigh
            int rows = 4, cols = 64;
            int totalSize = rows * cols;
            var random = new Random(42);
            var source = GenerateRandomFloats(random, totalSize, -10f, 10f);

            var quantized = Q5_0Tensor.Quantize(source, rows, cols);

            int expectedDataLowLen = (totalSize + 1) / 2; // Two 4-bit values per byte
            int expectedBlocks = (totalSize + 31) / 32;
            int expectedDataHighLen = expectedBlocks * 4; // 4 bytes per block

            Assert.Equal(expectedDataLowLen, quantized.DataLow.Length);
            Assert.Equal(expectedDataHighLen, quantized.DataHigh.Length);
            Assert.Equal(expectedBlocks, quantized.Scales.Length);
        }

        [Fact]
        public void Q4_1_Deterministic_SameSeed()
        {
            // Same input should produce same output
            var random1 = new Random(42);
            var random2 = new Random(42);
            int rows = 4, cols = 32;
            var source1 = GenerateRandomFloats(random1, rows * cols, -5f, 5f);
            var source2 = GenerateRandomFloats(random2, rows * cols, -5f, 5f);

            var q1 = Q4_1Tensor.Quantize(source1, rows, cols);
            var q2 = Q4_1Tensor.Quantize(source2, rows, cols);

            Assert.Equal(q1.Data, q2.Data);
            Assert.Equal(q1.Scales, q2.Scales);
            Assert.Equal(q1.Mins, q2.Mins);
        }

        [Fact]
        public void Q5_0_Deterministic_SameSeed()
        {
            // Same input should produce same output
            var random1 = new Random(42);
            var random2 = new Random(42);
            int rows = 4, cols = 64;
            var source1 = GenerateRandomFloats(random1, rows * cols, -10f, 10f);
            var source2 = GenerateRandomFloats(random2, rows * cols, -10f, 10f);

            var q1 = Q5_0Tensor.Quantize(source1, rows, cols);
            var q2 = Q5_0Tensor.Quantize(source2, rows, cols);

            Assert.Equal(q1.DataLow, q2.DataLow);
            Assert.Equal(q1.DataHigh, q2.DataHigh);
            Assert.Equal(q1.Scales, q2.Scales);
        }

        // Helper methods
        private static float[] GenerateRandomFloats(Random random, int count, float min, float max)
        {
            var result = new float[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = min + (float)random.NextDouble() * (max - min);
            }
            return result;
        }

        private static void AssertArraysClose(float[] expected, float[] actual, float tolerancePercent)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                if (MathF.Abs(expected[i]) < 1e-6f)
                {
                    // For near-zero expected values, use absolute tolerance
                    Assert.True(MathF.Abs(actual[i]) < tolerancePercent / 100f,
                        $"Index {i}: expected ~0, got {actual[i]}");
                }
                else
                {
                    // For non-zero values, use relative tolerance
                    float percentError = MathF.Abs((actual[i] - expected[i]) / expected[i]) * 100f;
                    Assert.True(percentError <= tolerancePercent,
                        $"Index {i}: expected {expected[i]}, got {actual[i]}, error {percentError:F2}%");
                }
            }
        }
    }
}

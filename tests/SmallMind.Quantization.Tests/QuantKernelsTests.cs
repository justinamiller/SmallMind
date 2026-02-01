using SmallMind.Quantization.Tensors;
using SmallMind.Quantization.Kernels;
using Xunit;

namespace SmallMind.Quantization.Tests
{
    /// <summary>
    /// Tests for Q8_0 and Q4_0 quantization kernels.
    /// Validates numerical correctness against reference float operations.
    /// </summary>
    public class QuantKernelsTests
    {
        private const float Q8Tolerance = 0.08f; // 8% relative error for Q8
        private const float Q4Tolerance = 0.30f; // 30% relative error for Q4

        [Fact]
        public void Q8_QuantizeAndDequantize_PreservesValues()
        {
            // Arrange
            var random = new Random(42);
            int rows = 4, cols = 8;
            var source = GenerateRandomFloats(random, rows * cols, -10f, 10f);

            // Act
            var quantized = Q8Tensor.Quantize(source, rows, cols, blockSize: 4);
            var dequantized = quantized.Dequantize();

            // Assert
            AssertArraysClose(source, dequantized, Q8Tolerance);
        }

        [Fact]
        public void Q4_QuantizeAndDequantize_PreservesValues()
        {
            // Arrange
            var random = new Random(42);
            int rows = 4, cols = 8;
            var source = GenerateRandomFloats(random, rows * cols, -10f, 10f);

            // Act
            var quantized = Q4Tensor.Quantize(source, rows, cols, blockSize: 4);
            var dequantized = quantized.Dequantize();

            // Assert
            AssertArraysClose(source, dequantized, Q4Tolerance);
        }

        [Fact]
        public void Q8_MatMul_MatchesReferenceImplementation()
        {
            // Arrange
            var random = new Random(42);
            int m = 2, k = 16, n = 8;
            var a = GenerateRandomFloats(random, m * k, -1f, 1f);
            var bFloat = GenerateRandomFloats(random, k * n, -1f, 1f);
            var bQuant = Q8Tensor.Quantize(bFloat, k, n, blockSize: 8);

            // Reference: float matmul
            var expected = new float[m * n];
            MatMulReference(a, bFloat, expected, m, k, n);

            // Act: quantized matmul
            var actual = new float[m * n];
            MatMulF32Q8.Multiply(a, bQuant, actual, m, k, n);

            // Assert
            AssertArraysClose(expected, actual, Q8Tolerance);
        }

        [Fact]
        public void Q4_MatMul_MatchesReferenceImplementation()
        {
            // Arrange
            var random = new Random(42);
            int m = 2, k = 16, n = 8;
            var a = GenerateRandomFloats(random, m * k, -1f, 1f);
            var bFloat = GenerateRandomFloats(random, k * n, -1f, 1f);
            var bQuant = Q4Tensor.Quantize(bFloat, k, n, blockSize: 8);

            // Reference: float matmul
            var expected = new float[m * n];
            MatMulReference(a, bFloat, expected, m, k, n);

            // Act: quantized matmul
            var actual = new float[m * n];
            MatMulF32Q4.Multiply(a, bQuant, actual, m, k, n);

            // Assert
            AssertArraysClose(expected, actual, Q4Tolerance);
        }

        [Fact]
        public void Q8_MatMul_SingleRow_MatchesReference()
        {
            // Test fast path for single-row inference
            var random = new Random(42);
            int k = 32, n = 16;
            var a = GenerateRandomFloats(random, k, -1f, 1f);
            var bFloat = GenerateRandomFloats(random, k * n, -1f, 1f);
            var bQuant = Q8Tensor.Quantize(bFloat, k, n, blockSize: 16);

            var expected = new float[n];
            MatMulReference(a, bFloat, expected, 1, k, n);

            var actual = new float[n];
            MatMulF32Q8.Multiply(a, bQuant, actual, 1, k, n);

            AssertArraysClose(expected, actual, Q8Tolerance);
        }

        [Fact]
        public void Q4_MatMul_SingleRow_MatchesReference()
        {
            // Test fast path for single-row inference
            var random = new Random(42);
            int k = 32, n = 16;
            var a = GenerateRandomFloats(random, k, -1f, 1f);
            var bFloat = GenerateRandomFloats(random, k * n, -1f, 1f);
            var bQuant = Q4Tensor.Quantize(bFloat, k, n, blockSize: 16);

            var expected = new float[n];
            MatMulReference(a, bFloat, expected, 1, k, n);

            var actual = new float[n];
            MatMulF32Q4.Multiply(a, bQuant, actual, 1, k, n);

            AssertArraysClose(expected, actual, Q4Tolerance);
        }

        [Fact]
        public void Q8_MatMul_LargerMatrix_MatchesReference()
        {
            // Test with larger dimensions
            var random = new Random(42);
            int m = 4, k = 64, n = 32;
            var a = GenerateRandomFloats(random, m * k, -1f, 1f);
            var bFloat = GenerateRandomFloats(random, k * n, -1f, 1f);
            var bQuant = Q8Tensor.Quantize(bFloat, k, n, blockSize: 64);

            var expected = new float[m * n];
            MatMulReference(a, bFloat, expected, m, k, n);

            var actual = new float[m * n];
            MatMulF32Q8.Multiply(a, bQuant, actual, m, k, n);

            AssertArraysClose(expected, actual, Q8Tolerance);
        }

        [Fact]
        public void Q4_DecodeNibble_CorrectSignedValues()
        {
            // Test 4-bit two's complement decoding
            Assert.Equal(0, Q4Tensor.DecodeNibble(0));
            Assert.Equal(1, Q4Tensor.DecodeNibble(1));
            Assert.Equal(7, Q4Tensor.DecodeNibble(7));
            Assert.Equal(-8, Q4Tensor.DecodeNibble(8));
            Assert.Equal(-7, Q4Tensor.DecodeNibble(9));
            Assert.Equal(-1, Q4Tensor.DecodeNibble(15));
        }

        [Fact]
        public void Q8_BlockSizeAlignment_HandlesOddSizes()
        {
            // Test that non-aligned sizes work correctly
            var random = new Random(42);
            int rows = 3, cols = 7; // Odd sizes
            var source = GenerateRandomFloats(random, rows * cols, -5f, 5f);

            var quantized = Q8Tensor.Quantize(source, rows, cols, blockSize: 4);
            var dequantized = quantized.Dequantize();

            AssertArraysClose(source, dequantized, Q8Tolerance);
        }

        [Fact]
        public void Q4_BlockSizeAlignment_HandlesOddSizes()
        {
            // Test that non-aligned sizes work correctly
            var random = new Random(42);
            int rows = 3, cols = 7; // Odd sizes
            var source = GenerateRandomFloats(random, rows * cols, -5f, 5f);

            var quantized = Q4Tensor.Quantize(source, rows, cols, blockSize: 4);
            var dequantized = quantized.Dequantize();

            AssertArraysClose(source, dequantized, Q4Tolerance);
        }

        // Helper methods

        private static float[] GenerateRandomFloats(Random random, int count, float min, float max)
        {
            var result = new float[count];
            float range = max - min;
            for (int i = 0; i < count; i++)
            {
                result[i] = (float)random.NextDouble() * range + min;
            }
            return result;
        }

        private static void MatMulReference(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c, int m, int k, int n)
        {
            // Reference float32 matrix multiplication: C = A * B
            c.Clear();
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float sum = 0f;
                    for (int p = 0; p < k; p++)
                    {
                        sum += a[i * k + p] * b[p * n + j];
                    }
                    c[i * n + j] = sum;
                }
            }
        }

        private static void AssertArraysClose(ReadOnlySpan<float> expected, ReadOnlySpan<float> actual, float tolerance)
        {
            Assert.Equal(expected.Length, actual.Length);

            for (int i = 0; i < expected.Length; i++)
            {
                float exp = expected[i];
                float act = actual[i];

                // Handle zero case
                if (Math.Abs(exp) < 1e-6f)
                {
                    Assert.True(Math.Abs(act) < tolerance,
                        $"Index {i}: expected ~0, got {act}");
                }
                else
                {
                    float relativeError = Math.Abs((act - exp) / exp);
                    Assert.True(relativeError < tolerance,
                        $"Index {i}: expected {exp}, got {act}, relative error {relativeError:F4} > {tolerance}");
                }
            }
        }
    }
}

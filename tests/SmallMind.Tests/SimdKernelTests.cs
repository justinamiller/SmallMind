using System;
using Xunit;
using SmallMind.Core.Simd;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for SIMD-accelerated kernels.
    /// Validates correctness by comparing SIMD results against scalar reference implementations.
    /// </summary>
    public class SimdKernelTests
    {
        private const float Tolerance = 1e-5f;

        [Fact]
        public void ElementWiseAdd_ProducesCorrectResults()
        {
            // Arrange
            float[] a = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f, 10.0f };
            float[] b = new float[] { 0.5f, 1.5f, 2.5f, 3.5f, 4.5f, 5.5f, 6.5f, 7.5f, 8.5f, 9.5f };
            float[] result = new float[10];
            float[] expected = new float[] { 1.5f, 3.5f, 5.5f, 7.5f, 9.5f, 11.5f, 13.5f, 15.5f, 17.5f, 19.5f };

            // Act
            ElementWiseOps.Add(a, b, result);

            // Assert
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(Math.Abs(result[i] - expected[i]) < Tolerance, 
                    $"Mismatch at index {i}: expected {expected[i]}, got {result[i]}");
            }
        }

        [Fact]
        public void ElementWiseAdd_HandlesOddSizes()
        {
            // Test with non-vector-aligned size
            float[] a = new float[] { 1.0f, 2.0f, 3.0f };
            float[] b = new float[] { 4.0f, 5.0f, 6.0f };
            float[] result = new float[3];
            float[] expected = new float[] { 5.0f, 7.0f, 9.0f };

            ElementWiseOps.Add(a, b, result);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], result[i], 5);
            }
        }

        [Fact]
        public void ElementWiseMultiply_ProducesCorrectResults()
        {
            float[] a = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f };
            float[] b = new float[] { 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f };
            float[] result = new float[8];
            float[] expected = new float[] { 2.0f, 6.0f, 12.0f, 20.0f, 30.0f, 42.0f, 56.0f, 72.0f };

            ElementWiseOps.Multiply(a, b, result);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], result[i], 5);
            }
        }

        [Fact]
        public void MultiplyAdd_FMA_ProducesCorrectResults()
        {
            float[] a = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f };
            float[] b = new float[] { 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f, 9.0f };
            float[] c = new float[] { 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f };
            float[] result = new float[8];
            float[] expected = new float[] { 2.5f, 6.5f, 12.5f, 20.5f, 30.5f, 42.5f, 56.5f, 72.5f };

            ElementWiseOps.MultiplyAdd(a, b, c, result);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(Math.Abs(result[i] - expected[i]) < Tolerance,
                    $"Mismatch at index {i}: expected {expected[i]}, got {result[i]}");
            }
        }

        [Fact]
        public void Scale_ProducesCorrectResults()
        {
            float[] a = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f, 8.0f };
            float[] result = new float[8];
            float scalar = 2.5f;
            float[] expected = new float[] { 2.5f, 5.0f, 7.5f, 10.0f, 12.5f, 15.0f, 17.5f, 20.0f };

            ElementWiseOps.Scale(a, scalar, result);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], result[i], 5);
            }
        }

        [Fact]
        public void ReLU_ProducesCorrectResults()
        {
            float[] input = new float[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f, 3.0f, -0.5f, 4.0f };
            float[] output = new float[8];
            float[] expected = new float[] { 0.0f, 0.0f, 0.0f, 1.0f, 2.0f, 3.0f, 0.0f, 4.0f };

            ActivationOps.ReLU(input, output);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], output[i], 5);
            }
        }

        [Fact]
        public void ReLUBackward_ProducesCorrectResults()
        {
            float[] input = new float[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f, 3.0f, -0.5f, 4.0f };
            float[] outputGrad = new float[] { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f };
            float[] inputGrad = new float[8];
            float[] expected = new float[] { 0.0f, 0.0f, 0.0f, 1.0f, 1.0f, 1.0f, 0.0f, 1.0f };

            ActivationOps.ReLUBackward(input, outputGrad, inputGrad);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], inputGrad[i], 5);
            }
        }

        [Fact]
        public void GELU_ProducesReasonableResults()
        {
            // GELU is an approximation, so we just verify it's in reasonable range
            float[] input = new float[] { -2.0f, -1.0f, 0.0f, 1.0f, 2.0f };
            float[] output = new float[5];

            ActivationOps.GELU(input, output);

            // GELU(-2) ≈ -0.045, GELU(0) = 0, GELU(2) ≈ 1.95
            Assert.True(output[0] < 0 && output[0] > -0.5f, "GELU(-2) should be slightly negative");
            Assert.True(Math.Abs(output[2]) < 0.1f, "GELU(0) should be close to 0");
            Assert.True(output[4] > 1.5f && output[4] < 2.5f, "GELU(2) should be close to 2");
        }

        [Fact]
        public void Softmax2D_SingleRow_ProducesCorrectResults()
        {
            float[] input = new float[] { 1.0f, 2.0f, 3.0f, 4.0f };
            float[] output = new float[4];

            SoftmaxOps.Softmax2D(input, output, rows: 1, cols: 4);

            // Verify sum = 1
            float sum = 0f;
            for (int i = 0; i < output.Length; i++)
            {
                sum += output[i];
            }
            Assert.True(Math.Abs(sum - 1.0f) < Tolerance, $"Sum should be 1.0, got {sum}");

            // Verify largest input has largest output
            Assert.True(output[3] > output[2] && output[2] > output[1] && output[1] > output[0],
                "Softmax output should be monotonic with input");
        }

        [Fact]
        public void Softmax2D_MultipleRows_ProducesCorrectResults()
        {
            float[] input = new float[] 
            { 
                1.0f, 2.0f, 3.0f, 
                4.0f, 5.0f, 6.0f 
            };
            float[] output = new float[6];

            SoftmaxOps.Softmax2D(input, output, rows: 2, cols: 3);

            // Verify each row sums to 1
            float sum1 = output[0] + output[1] + output[2];
            float sum2 = output[3] + output[4] + output[5];

            Assert.True(Math.Abs(sum1 - 1.0f) < Tolerance, $"Row 1 sum should be 1.0, got {sum1}");
            Assert.True(Math.Abs(sum2 - 1.0f) < Tolerance, $"Row 2 sum should be 1.0, got {sum2}");
        }

        [Fact]
        public void MatMul_SmallMatrix_ProducesCorrectResults()
        {
            // 2x3 × 3x2 = 2x2
            float[] A = new float[] { 1, 2, 3, 4, 5, 6 }; // 2x3
            float[] B = new float[] { 7, 8, 9, 10, 11, 12 }; // 3x2
            float[] C = new float[4]; // 2x2

            // Expected: [[58, 64], [139, 154]]
            float[] expected = new float[] { 58, 64, 139, 154 };

            MatMulOps.MatMul(A, B, C, M: 2, K: 3, N: 2);

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.True(Math.Abs(C[i] - expected[i]) < Tolerance,
                    $"Mismatch at index {i}: expected {expected[i]}, got {C[i]}");
            }
        }

        [Fact]
        public void MatMul_LargerMatrix_ProducesCorrectResults()
        {
            // 4x4 × 4x4 = 4x4 (identity test)
            float[] A = new float[16];
            float[] B = new float[16];
            float[] C = new float[16];

            // A = identity matrix
            for (int i = 0; i < 4; i++)
                A[i * 4 + i] = 1.0f;

            // B = all ones
            for (int i = 0; i < 16; i++)
                B[i] = 1.0f;

            MatMulOps.MatMul(A, B, C, M: 4, K: 4, N: 4);

            // C should equal B (since A is identity)
            for (int i = 0; i < 16; i++)
            {
                Assert.True(Math.Abs(C[i] - B[i]) < Tolerance,
                    $"Identity test failed at index {i}: expected {B[i]}, got {C[i]}");
            }
        }

        [Fact]
        public void DotProduct_ProducesCorrectResults()
        {
            float[] a = new float[] { 1.0f, 2.0f, 3.0f, 4.0f, 5.0f };
            float[] b = new float[] { 2.0f, 3.0f, 4.0f, 5.0f, 6.0f };
            float expected = 1*2 + 2*3 + 3*4 + 4*5 + 5*6; // = 70

            float result = MatMulOps.DotProduct(a, b);

            Assert.True(Math.Abs(result - expected) < Tolerance,
                $"Expected {expected}, got {result}");
        }

        [Fact]
        public void DotProduct_HandlesOddSizes()
        {
            float[] a = new float[] { 1.0f, 2.0f, 3.0f };
            float[] b = new float[] { 4.0f, 5.0f, 6.0f };
            float expected = 1*4 + 2*5 + 3*6; // = 32

            float result = MatMulOps.DotProduct(a, b);

            Assert.Equal(expected, result, 5);
        }

        [Fact]
        public void PackedMatMul_ARM64_ProducesCorrectResults()
        {
            // Arrange: Test small matrix multiplication (ARM64 NR=8, MR=4)
            const int M = 4, K = 8, N = 8;
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C = new float[M * N];
            float[] expected = new float[M * N];
            
            // Initialize with simple values for easy verification
            for (int i = 0; i < M * K; i++) A[i] = (i % 3) + 1;
            for (int i = 0; i < K * N; i++) B[i] = (i % 2) + 1;
            
            // Compute reference result with naive matrix multiply
            for (int m = 0; m < M; m++)
            {
                for (int n = 0; n < N; n++)
                {
                    float sum = 0f;
                    for (int k = 0; k < K; k++)
                    {
                        sum += A[m * K + k] * B[k * N + n];
                    }
                    expected[m * N + n] = sum;
                }
            }
            
            // Act: Use PackedMatMul
            var packed = PackedMatMul.Pack(B.AsSpan(), K, N);
            PackedMatMul.Multiply(A, packed, C, M, K, N);
            
            // Assert
            for (int i = 0; i < M * N; i++)
            {
                Assert.True(Math.Abs(C[i] - expected[i]) < Tolerance,
                    $"Mismatch at index {i}: expected {expected[i]}, got {C[i]}");
            }
        }

        [Fact]
        public void PackedMatMul_Accumulate_ProducesCorrectResults()
        {
            // Test accumulate semantics
            const int M = 2, K = 4, N = 4;
            float[] A = { 1, 2, 3, 4, 5, 6, 7, 8 };
            float[] B = { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 }; // Identity matrix
            float[] C = { 1, 1, 1, 1, 1, 1, 1, 1 }; // Pre-filled with 1s
            
            var packed = PackedMatMul.Pack(B.AsSpan(), K, N);
            
            // accumulate=true: C += A×B
            PackedMatMul.Multiply(A, packed, C, M, K, N, accumulate: true);
            
            // Expected: C = original_C + A×I = original_C + A = [2,3,4,5, 6,7,8,9]
            float[] expected = { 2, 3, 4, 5, 6, 7, 8, 9 };
            
            for (int i = 0; i < M * N; i++)
            {
                Assert.True(Math.Abs(C[i] - expected[i]) < Tolerance,
                    $"Accumulate test failed at index {i}: expected {expected[i]}, got {C[i]}");
            }
        }
    }
}

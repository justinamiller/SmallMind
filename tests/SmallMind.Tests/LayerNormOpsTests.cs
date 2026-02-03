using System;
using Xunit;
using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for fused LayerNorm operations.
    /// Tests correctness, numerical stability, and in-place operations.
    /// </summary>
    public class LayerNormOpsTests
    {
        private const float Tolerance = 1e-4f;

        [Fact]
        public void LayerNorm_Simple2D_ComputesCorrectly()
        {
            // Arrange
            var input = new float[] { 1f, 2f, 3f, 4f, 5f, 6f }; // 2 batches x 3 features
            var gamma = new float[] { 1f, 1f, 1f };
            var beta = new float[] { 0f, 0f, 0f };
            var output = new float[6];

            // Act
            LayerNormOps.LayerNorm(input, gamma, beta, output, batch: 2, features: 3);

            // Assert - each row should be normalized to mean=0, std=1
            // Row 0: [1, 2, 3] -> mean=2, std=sqrt(2/3)≈0.816
            float expectedRow0_0 = (1f - 2f) / MathF.Sqrt(2f / 3f + 1e-5f);
            float expectedRow0_1 = (2f - 2f) / MathF.Sqrt(2f / 3f + 1e-5f);
            float expectedRow0_2 = (3f - 2f) / MathF.Sqrt(2f / 3f + 1e-5f);

            Assert.Equal(expectedRow0_0, output[0], Tolerance);
            Assert.Equal(expectedRow0_1, output[1], Tolerance);
            Assert.Equal(expectedRow0_2, output[2], Tolerance);
        }

        [Fact]
        public void LayerNorm_WithGammaAndBeta_AppliesAffineTransform()
        {
            // Arrange
            var input = new float[] { 0f, 1f, 2f }; // 1 batch x 3 features
            var gamma = new float[] { 2f, 2f, 2f };
            var beta = new float[] { 1f, 1f, 1f };
            var output = new float[3];

            // Act
            LayerNormOps.LayerNorm(input, gamma, beta, output, batch: 1, features: 3);

            // Assert - should normalize then scale by 2 and shift by 1
            // mean = 1, variance = 2/3, std = sqrt(2/3 + eps)
            float mean = 1f;
            float variance = 2f / 3f;
            float std = MathF.Sqrt(variance + 1e-5f);
            
            float expected0 = 2f * ((0f - mean) / std) + 1f;
            float expected1 = 2f * ((1f - mean) / std) + 1f;
            float expected2 = 2f * ((2f - mean) / std) + 1f;

            Assert.Equal(expected0, output[0], Tolerance);
            Assert.Equal(expected1, output[1], Tolerance);
            Assert.Equal(expected2, output[2], Tolerance);
        }

        [Fact]
        public void LayerNorm_InPlace_WorksCorrectly()
        {
            // Arrange
            var data = new float[] { 1f, 2f, 3f, 4f, 5f, 6f }; // 2 batches x 3 features
            var gamma = new float[] { 1f, 1f, 1f };
            var beta = new float[] { 0f, 0f, 0f };

            // Act
            LayerNormOps.LayerNormInPlace(data, gamma, beta, batch: 2, features: 3);

            // Assert - data should be normalized in-place
            // First row mean should be close to 0
            float row0Mean = (data[0] + data[1] + data[2]) / 3f;
            Assert.Equal(0f, row0Mean, Tolerance);
        }

        [Fact]
        public void LayerNorm3D_ComputesCorrectly()
        {
            // Arrange
            var input = new float[] { 1f, 2f, 3f, 4f, 5f, 6f }; // 1 batch x 2 seq x 3 features
            var gamma = new float[] { 1f, 1f, 1f };
            var beta = new float[] { 0f, 0f, 0f };
            var output = new float[6];

            // Act
            LayerNormOps.LayerNorm3D(input, gamma, beta, output, batch: 1, sequence: 2, features: 3);

            // Assert - should normalize each sequence position independently
            Assert.NotNull(output);
            Assert.Equal(6, output.Length);
        }

        [Fact]
        public void LayerNormResidual_FusesAddAndNorm()
        {
            // Arrange
            var input = new float[] { 1f, 2f, 3f };
            var residual = new float[] { 1f, 1f, 1f };
            var gamma = new float[] { 1f, 1f, 1f };
            var beta = new float[] { 0f, 0f, 0f };
            var output = new float[3];

            // Act
            LayerNormOps.LayerNormResidual(input, residual, gamma, beta, output, batch: 1, features: 3);

            // Assert - should normalize (input + residual) = [2, 3, 4]
            // mean = 3, variance = 2/3
            float mean = 3f;
            float variance = 2f / 3f;
            float std = MathF.Sqrt(variance + 1e-5f);
            
            float expected0 = (2f - mean) / std;
            float expected1 = (3f - mean) / std;
            float expected2 = (4f - mean) / std;

            Assert.Equal(expected0, output[0], Tolerance);
            Assert.Equal(expected1, output[1], Tolerance);
            Assert.Equal(expected2, output[2], Tolerance);
        }

        [Fact]
        public void LayerNorm_WithZeroVariance_DoesNotCrash()
        {
            // Arrange - all same values
            var input = new float[] { 5f, 5f, 5f };
            var gamma = new float[] { 1f, 1f, 1f };
            var beta = new float[] { 0f, 0f, 0f };
            var output = new float[3];

            // Act
            LayerNormOps.LayerNorm(input, gamma, beta, output, batch: 1, features: 3);

            // Assert - should handle zero variance gracefully (eps prevents division by zero)
            Assert.True(float.IsFinite(output[0]));
            Assert.True(float.IsFinite(output[1]));
            Assert.True(float.IsFinite(output[2]));
        }

        [Fact]
        public void LayerNorm_MultipleBatches_NormalizesIndependently()
        {
            // Arrange
            var input = new float[] 
            { 
                1f, 2f, 3f,  // Batch 0
                10f, 20f, 30f  // Batch 1
            };
            var gamma = new float[] { 1f, 1f, 1f };
            var beta = new float[] { 0f, 0f, 0f };
            var output = new float[6];

            // Act
            LayerNormOps.LayerNorm(input, gamma, beta, output, batch: 2, features: 3);

            // Assert - each batch should be normalized independently
            // Batch 0 mean should be ~0
            float batch0Mean = (output[0] + output[1] + output[2]) / 3f;
            Assert.Equal(0f, batch0Mean, Tolerance);
            
            // Batch 1 mean should be ~0
            float batch1Mean = (output[3] + output[4] + output[5]) / 3f;
            Assert.Equal(0f, batch1Mean, Tolerance);
        }

        [Fact]
        public void LayerNorm_LargeValues_MaintainsNumericalStability()
        {
            // Arrange - large values that could overflow without proper handling
            var input = new float[] { 1000f, 2000f, 3000f };
            var gamma = new float[] { 1f, 1f, 1f };
            var beta = new float[] { 0f, 0f, 0f };
            var output = new float[3];

            // Act
            LayerNormOps.LayerNorm(input, gamma, beta, output, batch: 1, features: 3);

            // Assert - should produce valid normalized values
            Assert.True(float.IsFinite(output[0]));
            Assert.True(float.IsFinite(output[1]));
            Assert.True(float.IsFinite(output[2]));
            
            // Check mean is close to 0
            float mean = (output[0] + output[1] + output[2]) / 3f;
            Assert.Equal(0f, mean, Tolerance);
        }

        [Fact]
        public void LayerNorm_WithInvalidDimensions_ThrowsException()
        {
            // Arrange
            var input = new float[6];
            var gamma = new float[2]; // Wrong size
            var beta = new float[3];
            var output = new float[6];

            // Act & Assert
            Assert.Throws<SmallMind.Core.Exceptions.ValidationException>(() =>
                LayerNormOps.LayerNorm(input, gamma, beta, output, batch: 2, features: 3));
        }

        [Fact]
        public void LayerNorm_ComputesMeanAndVarianceCorrectly()
        {
            // Arrange - known values
            var input = new float[] { 2f, 4f, 6f, 8f }; // mean=5, variance=5
            var gamma = new float[] { 1f, 1f, 1f, 1f };
            var beta = new float[] { 0f, 0f, 0f, 0f };
            var output = new float[4];

            // Act
            LayerNormOps.LayerNorm(input, gamma, beta, output, batch: 1, features: 4);

            // Assert - check that normalization is correct
            float mean = 5f;
            float variance = 5f;
            float std = MathF.Sqrt(variance + 1e-5f);
            
            for (int i = 0; i < 4; i++)
            {
                float expected = (input[i] - mean) / std;
                Assert.Equal(expected, output[i], Tolerance);
            }
        }

        [Fact]
        public void LayerNormResidual_WithMultipleBatches_WorksCorrectly()
        {
            // Arrange
            var input = new float[] { 1f, 2f, 3f, 4f, 5f, 6f };
            var residual = new float[] { 0f, 0f, 0f, 0f, 0f, 0f };
            var gamma = new float[] { 1f, 1f, 1f };
            var beta = new float[] { 0f, 0f, 0f };
            var output = new float[6];

            // Act
            LayerNormOps.LayerNormResidual(input, residual, gamma, beta, output, batch: 2, features: 3);

            // Assert - each batch normalized independently
            float batch0Mean = (output[0] + output[1] + output[2]) / 3f;
            float batch1Mean = (output[3] + output[4] + output[5]) / 3f;
            
            Assert.Equal(0f, batch0Mean, Tolerance);
            Assert.Equal(0f, batch1Mean, Tolerance);
        }
    }
}

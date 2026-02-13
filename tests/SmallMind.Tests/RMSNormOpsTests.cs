using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for fused RMSNorm operations.
    /// Tests correctness, numerical stability, and in-place operations.
    /// </summary>
    public class RMSNormOpsTests
    {
        private const float Tolerance = 1e-4f;

        [Fact]
        public void RMSNorm_Simple2D_ComputesCorrectly()
        {
            // Arrange
            var input = new float[] { 1f, 2f, 3f, 4f, 5f, 6f }; // 2 batches x 3 features
            var gamma = new float[] { 1f, 1f, 1f };
            var output = new float[6];

            // Act
            RMSNormOps.RMSNorm(input, gamma, output, batch: 2, features: 3);

            // Assert - each row should be normalized by RMS
            // Row 0: [1, 2, 3] -> rms = sqrt((1^2 + 2^2 + 3^2)/3) = sqrt(14/3) ≈ 2.16
            float sumSqRow0 = 1f * 1f + 2f * 2f + 3f * 3f; // 14
            float rmsRow0 = MathF.Sqrt(sumSqRow0 / 3f + 1e-5f);
            float invRmsRow0 = 1f / rmsRow0;

            float expectedRow0_0 = 1f * invRmsRow0;
            float expectedRow0_1 = 2f * invRmsRow0;
            float expectedRow0_2 = 3f * invRmsRow0;

            Assert.Equal(expectedRow0_0, output[0], Tolerance);
            Assert.Equal(expectedRow0_1, output[1], Tolerance);
            Assert.Equal(expectedRow0_2, output[2], Tolerance);
        }

        [Fact]
        public void RMSNorm_WithGamma_AppliesScaling()
        {
            // Arrange
            var input = new float[] { 1f, 2f, 3f }; // 1 batch x 3 features
            var gamma = new float[] { 2f, 3f, 4f };
            var output = new float[3];

            // Act
            RMSNormOps.RMSNorm(input, gamma, output, batch: 1, features: 3);

            // Assert - should normalize then scale by gamma
            float sumSq = 1f * 1f + 2f * 2f + 3f * 3f; // 14
            float invRms = 1f / MathF.Sqrt(sumSq / 3f + 1e-5f);

            float expected0 = 2f * (1f * invRms);
            float expected1 = 3f * (2f * invRms);
            float expected2 = 4f * (3f * invRms);

            Assert.Equal(expected0, output[0], Tolerance);
            Assert.Equal(expected1, output[1], Tolerance);
            Assert.Equal(expected2, output[2], Tolerance);
        }

        [Fact]
        public void RMSNorm_InPlace_WorksCorrectly()
        {
            // Arrange
            var data = new float[] { 1f, 2f, 3f, 4f, 5f, 6f }; // 2 batches x 3 features
            var gamma = new float[] { 1f, 1f, 1f };
            var originalData = (float[])data.Clone();

            // Act
            RMSNormOps.RMSNormInPlace(data, gamma, batch: 2, features: 3);

            // Assert - data should be normalized in-place
            // Verify RMS of first row is ~1 after normalization with gamma=1
            float sumSqNormalized = data[0] * data[0] + data[1] * data[1] + data[2] * data[2];
            float rmsNormalized = MathF.Sqrt(sumSqNormalized / 3f);
            Assert.Equal(1f, rmsNormalized, Tolerance);
        }

        [Fact]
        public void RMSNorm_3D_ComputesCorrectly()
        {
            // Arrange - 1 batch x 2 seq x 3 features = 6 elements
            var input = new float[] { 1f, 2f, 3f, 4f, 5f, 6f };
            var gamma = new float[] { 1f, 1f, 1f };
            var output = new float[6];

            // Act
            RMSNormOps.RMSNorm3D(input, gamma, output, batch: 1, sequence: 2, features: 3);

            // Assert - should normalize each of the 2 sequences independently
            // Seq 0: [1, 2, 3]
            float sumSqSeq0 = 1f * 1f + 2f * 2f + 3f * 3f;
            float invRmsSeq0 = 1f / MathF.Sqrt(sumSqSeq0 / 3f + 1e-5f);

            Assert.Equal(1f * invRmsSeq0, output[0], Tolerance);
            Assert.Equal(2f * invRmsSeq0, output[1], Tolerance);
            Assert.Equal(3f * invRmsSeq0, output[2], Tolerance);
        }

        [Fact]
        public void RMSNorm_WithResidual_ComputesCorrectly()
        {
            // Arrange
            var input = new float[] { 1f, 2f, 3f };
            var residual = new float[] { 1f, 1f, 1f };
            var gamma = new float[] { 1f, 1f, 1f };
            var output = new float[3];

            // Act
            RMSNormOps.RMSNormResidual(input, residual, gamma, output, batch: 1, features: 3);

            // Assert - should normalize (input + residual)
            // Combined: [2, 3, 4]
            float sumSq = 2f * 2f + 3f * 3f + 4f * 4f; // 29
            float invRms = 1f / MathF.Sqrt(sumSq / 3f + 1e-5f);

            float expected0 = 2f * invRms;
            float expected1 = 3f * invRms;
            float expected2 = 4f * invRms;

            Assert.Equal(expected0, output[0], Tolerance);
            Assert.Equal(expected1, output[1], Tolerance);
            Assert.Equal(expected2, output[2], Tolerance);
        }

        [Fact]
        public void RMSNorm_HandlesZeroInput()
        {
            // Arrange
            var input = new float[] { 0f, 0f, 0f };
            var gamma = new float[] { 1f, 1f, 1f };
            var output = new float[3];

            // Act
            RMSNormOps.RMSNorm(input, gamma, output, batch: 1, features: 3);

            // Assert - should not crash, output should be zero (0 / sqrt(eps))
            Assert.Equal(0f, output[0], Tolerance);
            Assert.Equal(0f, output[1], Tolerance);
            Assert.Equal(0f, output[2], Tolerance);
        }

        [Fact]
        public void RMSNorm_ComparesWithReference()
        {
            // Arrange - test against a known reference computation
            var input = new float[] { 0.5f, 1.5f, 2.5f, 3.5f };
            var gamma = new float[] { 1.2f, 0.8f, 1.5f, 0.5f };
            var output = new float[4];

            // Act
            RMSNormOps.RMSNorm(input, gamma, output, batch: 1, features: 4);

            // Assert - verify against manual calculation
            float sumSq = 0.5f * 0.5f + 1.5f * 1.5f + 2.5f * 2.5f + 3.5f * 3.5f;
            float rms = MathF.Sqrt(sumSq / 4f + 1e-5f);
            float invRms = 1f / rms;

            Assert.Equal(1.2f * 0.5f * invRms, output[0], Tolerance);
            Assert.Equal(0.8f * 1.5f * invRms, output[1], Tolerance);
            Assert.Equal(1.5f * 2.5f * invRms, output[2], Tolerance);
            Assert.Equal(0.5f * 3.5f * invRms, output[3], Tolerance);
        }
    }
}

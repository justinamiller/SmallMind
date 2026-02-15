using SmallMind.Core.Core;
using SmallMind.Runtime.Metrics;

namespace SmallMind.Tests.Metrics
{
    /// <summary>
    /// Tests for metrics computation utilities.
    /// </summary>
    public class MetricsComputerTests
    {
        [Fact]
        public void ComputeTokenAccuracy_CalculatesCorrectly()
        {
            // Arrange - Create simple logits and targets
            // Logits: (B=1, T=3, V=4)
            var logits = new Tensor(new float[]
            {
                // Position 0: class 1 has highest logit
                0.1f, 2.0f, 0.5f, 0.3f,
                // Position 1: class 0 has highest logit
                1.5f, 0.2f, 0.3f, 0.1f,
                // Position 2: class 2 has highest logit
                0.2f, 0.3f, 1.8f, 0.1f
            }, new int[] { 1, 3, 4 });

            // Targets: (B=1, T=3)
            var targets = new Tensor(new float[]
            {
                1, // Matches position 0 prediction (class 1)
                0, // Matches position 1 prediction (class 0)
                3  // Does NOT match position 2 prediction (class 2)
            }, new int[] { 1, 3 });

            // Act
            float accuracy = MetricsComputer.ComputeTokenAccuracy(logits, targets);

            // Assert - 2 out of 3 predictions correct = 66.67%
            Assert.InRange(accuracy, 0.66f, 0.67f);
        }

        [Fact]
        public void ComputeTokenAccuracy_AllCorrect_Returns100Percent()
        {
            // Arrange
            var logits = new Tensor(new float[]
            {
                2.0f, 0.1f,  // class 0 highest
                0.1f, 2.0f   // class 1 highest
            }, new int[] { 1, 2, 2 });

            var targets = new Tensor(new float[] { 0, 1 }, new int[] { 1, 2 });

            // Act
            float accuracy = MetricsComputer.ComputeTokenAccuracy(logits, targets);

            // Assert
            Assert.Equal(1.0f, accuracy);
        }

        [Fact]
        public void ComputeTokenAccuracy_AllWrong_Returns0Percent()
        {
            // Arrange
            var logits = new Tensor(new float[]
            {
                2.0f, 0.1f,  // class 0 highest
                0.1f, 2.0f   // class 1 highest
            }, new int[] { 1, 2, 2 });

            var targets = new Tensor(new float[] { 1, 0 }, new int[] { 1, 2 });

            // Act
            float accuracy = MetricsComputer.ComputeTokenAccuracy(logits, targets);

            // Assert
            Assert.Equal(0.0f, accuracy);
        }

        [Fact]
        public void ComputeTokenAccuracy_ThrowsOnInvalidLogitsSize()
        {
            // Arrange - Create logits with shape (1, 2, 3) but data only for (1, 2, 2)
            var logits = new Tensor(new float[]
            {
                1.0f, 2.0f,  // Only 2 values per position
                1.0f, 2.0f
            }, new int[] { 1, 2, 3 }); // But shape says 3 values per position

            var targets = new Tensor(new float[] { 0, 1 }, new int[] { 1, 2 });

            // Act & Assert
            Assert.Throws<ArgumentException>(() => MetricsComputer.ComputeTokenAccuracy(logits, targets));
        }

        [Fact]
        public void ComputeTokenAccuracy_ThrowsOnInvalidTargetsSize()
        {
            // Arrange
            var logits = new Tensor(new float[]
            {
                1.0f, 2.0f,
                1.0f, 2.0f
            }, new int[] { 1, 2, 2 });

            // Targets with shape (1, 2) but only 1 element in data
            var targets = new Tensor(new float[] { 0 }, new int[] { 1, 2 });

            // Act & Assert
            Assert.Throws<ArgumentException>(() => MetricsComputer.ComputeTokenAccuracy(logits, targets));
        }

        [Fact]
        public void ComputeGradientStats_DetectsHealthyGradients()
        {
            // Arrange
            var param1 = new Tensor(new float[] { 1, 2, 3 }, new int[] { 3 }, requiresGrad: true);
            param1.Grad = new float[] { 0.1f, 0.2f, 0.1f };

            var param2 = new Tensor(new float[] { 4, 5 }, new int[] { 2 }, requiresGrad: true);
            param2.Grad = new float[] { 0.05f, 0.15f };

            var parameters = new List<Tensor> { param1, param2 };

            // Act
            var (meanNorm, maxNorm, minNorm, nanCount, infCount) =
                MetricsComputer.ComputeGradientStats(parameters);

            // Assert
            Assert.True(meanNorm > 0);
            Assert.True(maxNorm >= minNorm);
            Assert.Equal(0, nanCount);
            Assert.Equal(0, infCount);
        }

        [Fact]
        public void ComputeGradientStats_DetectsNaN()
        {
            // Arrange
            var param = new Tensor(new float[] { 1, 2 }, new int[] { 2 }, requiresGrad: true);
            param.Grad = new float[] { 0.1f, float.NaN };

            var parameters = new List<Tensor> { param };

            // Act
            var (_, _, _, nanCount, _) = MetricsComputer.ComputeGradientStats(parameters);

            // Assert
            Assert.Equal(1, nanCount);
        }

        [Fact]
        public void ComputeGradientStats_DetectsInfinity()
        {
            // Arrange
            var param = new Tensor(new float[] { 1, 2 }, new int[] { 2 }, requiresGrad: true);
            param.Grad = new float[] { float.PositiveInfinity, 0.1f };

            var parameters = new List<Tensor> { param };

            // Act
            var (_, _, _, _, infCount) = MetricsComputer.ComputeGradientStats(parameters);

            // Assert
            Assert.Equal(1, infCount);
        }

        [Fact]
        public void AreGradientsHealthy_ReturnsTrueForHealthyGradients()
        {
            // Arrange
            float meanNorm = 0.1f;
            float maxNorm = 0.5f;
            int nanCount = 0;
            int infCount = 0;

            // Act
            bool healthy = MetricsComputer.AreGradientsHealthy(meanNorm, maxNorm, nanCount, infCount);

            // Assert
            Assert.True(healthy);
        }

        [Fact]
        public void AreGradientsHealthy_DetectsExplodingGradients()
        {
            // Arrange
            float meanNorm = 50f;
            float maxNorm = 150f; // Above default threshold of 100
            int nanCount = 0;
            int infCount = 0;

            // Act
            bool healthy = MetricsComputer.AreGradientsHealthy(meanNorm, maxNorm, nanCount, infCount);

            // Assert
            Assert.False(healthy);
        }

        [Fact]
        public void AreGradientsHealthy_DetectsVanishingGradients()
        {
            // Arrange
            float meanNorm = 1e-10f; // Too small
            float maxNorm = 1e-9f;
            int nanCount = 0;
            int infCount = 0;

            // Act
            bool healthy = MetricsComputer.AreGradientsHealthy(meanNorm, maxNorm, nanCount, infCount);

            // Assert
            Assert.False(healthy);
        }

        [Fact]
        public void AreGradientsHealthy_DetectsNaN()
        {
            // Arrange
            float meanNorm = 0.1f;
            float maxNorm = 0.5f;
            int nanCount = 1; // Has NaN
            int infCount = 0;

            // Act
            bool healthy = MetricsComputer.AreGradientsHealthy(meanNorm, maxNorm, nanCount, infCount);

            // Assert
            Assert.False(healthy);
        }

        [Fact]
        public void ComputePerplexity_CalculatesCorrectly()
        {
            // Arrange
            float loss = 1.0f;

            // Act
            float perplexity = MetricsComputer.ComputePerplexity(loss);

            // Assert
            // exp(1.0) ≈ 2.718
            Assert.InRange(perplexity, 2.7f, 2.8f);
        }

        [Fact]
        public void ComputePerplexity_ClampsHighValues()
        {
            // Arrange
            float loss = 100.0f; // Very high

            // Act
            float perplexity = MetricsComputer.ComputePerplexity(loss);

            // Assert
            Assert.True(perplexity < float.MaxValue);
            // Should be clamped to exp(11.5) ≈ 99,000
            Assert.InRange(perplexity, 90000f, 100000f);
        }
    }
}

using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    public class MixedPrecisionTests
    {
        [Fact]
        public void FloatToHalf_ConvertsCorrectly()
        {
            // Arrange
            float[] source = { 1.0f, 2.5f, -3.14f, 0.0f, 100.5f };
            Half[] dest = new Half[source.Length];

            // Act
            MixedPrecision.FloatToHalf(source, dest);

            // Assert
            Assert.Equal(5, dest.Length);
            Assert.Equal((Half)1.0f, dest[0]);
            Assert.Equal((Half)2.5f, dest[1]);
            Assert.Equal((Half)(-3.14f), dest[2]);
            Assert.Equal((Half)0.0f, dest[3]);
        }

        [Fact]
        public void HalfToFloat_ConvertsCorrectly()
        {
            // Arrange
            Half[] source = { (Half)1.5f, (Half)2.25f, (Half)(-1.0f), (Half)0.0f };
            float[] dest = new float[source.Length];

            // Act
            MixedPrecision.HalfToFloat(source, dest);

            // Assert
            Assert.Equal(4, dest.Length);
            Assert.Equal(1.5f, dest[0], precision: 2);
            Assert.Equal(2.25f, dest[1], precision: 2);
            Assert.Equal(-1.0f, dest[2], precision: 2);
            Assert.Equal(0.0f, dest[3], precision: 2);
        }

        [Fact]
        public void RoundTripConversion_MaintainsPrecision()
        {
            // Arrange
            float[] original = { 1.5f, 2.25f, -3.5f, 0.0f, 10.75f };
            Half[] half = new Half[original.Length];
            float[] roundTrip = new float[original.Length];

            // Act
            MixedPrecision.FloatToHalf(original, half);
            MixedPrecision.HalfToFloat(half, roundTrip);

            // Assert - FP16 has limited precision, so use tolerance
            for (int i = 0; i < original.Length; i++)
            {
                Assert.Equal(original[i], roundTrip[i], precision: 1);
            }
        }

        [Fact]
        public void HasGradientOverflow_DetectsInfinity()
        {
            // Arrange
            float[] gradients = { 1.0f, 2.0f, float.PositiveInfinity, 4.0f };

            // Act
            bool hasOverflow = MixedPrecision.HasGradientOverflow(gradients);

            // Assert
            Assert.True(hasOverflow);
        }

        [Fact]
        public void HasGradientOverflow_DetectsNaN()
        {
            // Arrange
            float[] gradients = { 1.0f, float.NaN, 3.0f };

            // Act
            bool hasOverflow = MixedPrecision.HasGradientOverflow(gradients);

            // Assert
            Assert.True(hasOverflow);
        }

        [Fact]
        public void HasGradientOverflow_ReturnsFalseForValidGradients()
        {
            // Arrange
            float[] gradients = { -1.5f, 0.0f, 2.5f, -0.01f, 100.0f };

            // Act
            bool hasOverflow = MixedPrecision.HasGradientOverflow(gradients);

            // Assert
            Assert.False(hasOverflow);
        }

        [Fact]
        public void MixedPrecisionTrainer_InitializesCorrectly()
        {
            // Arrange
            var parameters = new System.Collections.Generic.List<Tensor>
            {
                new Tensor(new int[] { 10 }, requiresGrad: true),
                new Tensor(new int[] { 5, 3 }, requiresGrad: true)
            };
            var optimizer = new AdamW(parameters);

            // Act
            var trainer = new MixedPrecisionTrainer(optimizer, parameters, initialLossScale: 1024f);

            // Assert
            Assert.Equal(1024f, trainer.LossScale);
            Assert.Equal(0, trainer.OverflowCount);
        }

        [Fact]
        public void MixedPrecisionTrainer_SyncToFP16_CopiesWeights()
        {
            // Arrange
            var parameters = new System.Collections.Generic.List<Tensor>
            {
                new Tensor(new float[] { 1.0f, 2.0f, 3.0f }, new int[] { 3 }, requiresGrad: true)
            };
            parameters[0].Data[0] = 1.5f;
            parameters[0].Data[1] = 2.5f;
            parameters[0].Data[2] = 3.5f;

            var optimizer = new AdamW(parameters);
            var trainer = new MixedPrecisionTrainer(optimizer, parameters);

            // Act
            trainer.SyncToFP16(parameters);

            // Assert - values should be approximately preserved after FP16 roundtrip
            Assert.Equal(1.5f, parameters[0].Data[0], precision: 1);
            Assert.Equal(2.5f, parameters[0].Data[1], precision: 1);
            Assert.Equal(3.5f, parameters[0].Data[2], precision: 1);
        }

        [Fact]
        public void MixedPrecisionTrainer_DetectsOverflow()
        {
            // Arrange
            var parameters = new System.Collections.Generic.List<Tensor>
            {
                new Tensor(new float[] { 1.0f, 2.0f }, new int[] { 2 }, requiresGrad: true)
            };
            parameters[0].Grad = new float[] { 1.0f, float.PositiveInfinity };

            var optimizer = new AdamW(parameters);
            var trainer = new MixedPrecisionTrainer(optimizer, parameters, initialLossScale: 100f);

            // Act
            bool isValid = trainer.CheckAndUnscaleGradients(parameters);

            // Assert
            Assert.False(isValid);
            Assert.Equal(1, trainer.OverflowCount);
            Assert.True(trainer.LossScale < 100f); // Should have reduced loss scale
        }
    }
}

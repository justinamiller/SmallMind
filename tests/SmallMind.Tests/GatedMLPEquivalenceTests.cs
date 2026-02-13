using SmallMind.Core.Core;
using SmallMind.Transformers;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests that GatedMLP produces equivalent outputs in training and inference modes.
    /// Verifies that the fused SiLU×Up optimization in inference mode produces the same results.
    /// </summary>
    public class GatedMLPEquivalenceTests
    {
        private const float Tolerance = 1e-4f;

        [Fact]
        public void GatedMLP_TrainingVsInference_ProduceEquivalentOutputs()
        {
            // Arrange
            const int batchSize = 2;
            const int seqLen = 4;
            const int nEmbd = 8;
            const int hiddenDim = 16;
            const float dropout = 0.0f; // Disable dropout for deterministic testing
            var random = new Random(42);

            // Create GatedMLP
            var mlp = new GatedMLP(nEmbd, hiddenDim, dropout, random);

            // Create test input
            var inputShape = new int[] { batchSize, seqLen, nEmbd };
            var input = new Tensor(inputShape, requiresGrad: false);

            // Fill with random values
            for (int i = 0; i < input.Size; i++)
            {
                input.Data[i] = (float)random.NextDouble() * 2f - 1f; // Range [-1, 1]
            }

            // Clone input for second forward pass
            var inputClone = new Tensor(inputShape, requiresGrad: false);
            Array.Copy(input.Data, inputClone.Data, input.Size);

            // Act - Training mode
            mlp.Train();
            var trainingOutput = mlp.Forward(input);

            // Act - Inference mode (uses fused operation)
            mlp.Eval();
            var inferenceOutput = mlp.Forward(inputClone);

            // Assert - Outputs should be identical
            Assert.Equal(trainingOutput.Size, inferenceOutput.Size);
            for (int i = 0; i < trainingOutput.Size; i++)
            {
                Assert.True(Math.Abs(trainingOutput.Data[i] - inferenceOutput.Data[i]) < Tolerance,
                    $"Output mismatch at index {i}: Training={trainingOutput.Data[i]}, Inference={inferenceOutput.Data[i]}");
            }
        }

        [Theory]
        [InlineData(1, 2, 4, 8)]
        [InlineData(2, 3, 6, 12)]
        [InlineData(1, 1, 16, 32)]
        public void GatedMLP_VariousSizes_ProduceEquivalentOutputs(int batchSize, int seqLen, int nEmbd, int hiddenDim)
        {
            // Arrange
            const float dropout = 0.0f;
            var random = new Random(123);

            var mlp = new GatedMLP(nEmbd, hiddenDim, dropout, random);

            var inputShape = new int[] { batchSize, seqLen, nEmbd };
            var input1 = new Tensor(inputShape, requiresGrad: false);
            var input2 = new Tensor(inputShape, requiresGrad: false);

            for (int i = 0; i < input1.Size; i++)
            {
                float value = (float)random.NextDouble() * 4f - 2f; // Range [-2, 2]
                input1.Data[i] = value;
                input2.Data[i] = value;
            }

            // Act
            mlp.Train();
            var trainingOutput = mlp.Forward(input1);

            mlp.Eval();
            var inferenceOutput = mlp.Forward(input2);

            // Assert
            for (int i = 0; i < trainingOutput.Size; i++)
            {
                Assert.True(Math.Abs(trainingOutput.Data[i] - inferenceOutput.Data[i]) < Tolerance,
                    $"Size ({batchSize}×{seqLen}×{nEmbd}→{hiddenDim}): Mismatch at {i}");
            }
        }
    }
}

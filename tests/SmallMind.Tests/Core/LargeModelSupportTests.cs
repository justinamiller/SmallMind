using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;

namespace SmallMind.Tests.Core
{
    /// <summary>
    /// Tests for large model support utilities and overflow protection.
    /// </summary>
    public class LargeModelSupportTests
    {
        [Fact]
        public void CalculateParameterCount_SmallModel_ReturnsCorrectCount()
        {
            // Arrange: GPT-2 Small configuration
            int vocabSize = 50257;
            int blockSize = 1024;
            int embeddingDim = 768;
            int numLayers = 12;
            int numHeads = 12;

            // Act
            long paramCount = LargeModelSupport.CalculateParameterCount(
                vocabSize, blockSize, embeddingDim, numLayers, numHeads);

            // Assert: Should be approximately 163M parameters (includes embeddings, layers, LM head)
            Assert.InRange(paramCount, 160_000_000L, 170_000_000L);
        }

        [Fact]
        public void CalculateParameterCount_BillionParamModel_ReturnsCorrectCount()
        {
            // Arrange: 1B parameter configuration
            int vocabSize = 32000;
            int blockSize = 2048;
            int embeddingDim = 2048;
            int numLayers = 22;
            int numHeads = 16;

            // Act
            long paramCount = LargeModelSupport.CalculateParameterCount(
                vocabSize, blockSize, embeddingDim, numLayers, numHeads);

            // Assert: Should be approximately 1.24B parameters
            Assert.InRange(paramCount, 1_200_000_000L, 1_300_000_000L);
        }

        [Fact]
        public void EstimateMemoryBytes_FP32_ReturnsCorrectSize()
        {
            // Arrange
            long paramCount = 1_000_000_000L; // 1B parameters

            // Act
            long memoryBytes = LargeModelSupport.EstimateMemoryBytes(
                paramCount, bytesPerParam: 4.0);

            // Assert: 1B params × 4 bytes = 4GB
            Assert.Equal(4_000_000_000L, memoryBytes);
        }

        [Fact]
        public void ValidateConfiguration_OversizedTensor_ThrowsException()
        {
            // Arrange: Configuration that exceeds int.MaxValue for embedding tensor
            int vocabSize = 100000;
            int blockSize = 2048;
            int embeddingDim = 30000;  // 100K × 30K = 3B > int.MaxValue
            int numLayers = 12;
            int numHeads = 12;

            // Act & Assert
            var exception = Assert.Throws<ValidationException>(() =>
                LargeModelSupport.ValidateConfiguration(
                    vocabSize, blockSize, embeddingDim, numLayers, numHeads,
                    availableMemoryBytes: 0,
                    quantizationBits: 32));

            Assert.Contains("exceeds int32 limit", exception.Message);
        }

        [Fact]
        public void TensorShapeToSize_OversizedTensor_ThrowsException()
        {
            // Arrange: Oversized tensor that exceeds int.MaxValue
            int[] shape = new int[] { 100000, 30000 }; // 3 billion elements

            // Act & Assert
            var exception = Assert.Throws<ValidationException>(() =>
                Tensor.ShapeToSize(shape));

            Assert.Contains("overflow", exception.Message.ToLower());
            Assert.Contains("int.MaxValue", exception.Message);
        }
    }
}

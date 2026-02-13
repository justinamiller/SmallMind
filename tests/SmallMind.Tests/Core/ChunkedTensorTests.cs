using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;

namespace SmallMind.Tests.Core
{
    /// <summary>
    /// Unit tests for chunked tensor functionality.
    /// Tests tensor creation, operations, and large tensor support.
    /// </summary>
    public class ChunkedTensorTests
    {
        private const float Tolerance = 1e-6f;

        [Fact]
        public void CreateChunked_WithValidShape_CreatesTensor()
        {
            // Arrange
            int[] shape = new int[] { 1000, 1000 }; // 1M elements

            // Act
            var tensor = Tensor.CreateChunked(shape);

            // Assert
            Assert.NotNull(tensor);
            Assert.Equal(shape, tensor.Shape);
            Assert.True(tensor.IsChunked);
            Assert.Equal(1_000_000L, tensor.TotalElements);
        }

        [Fact]
        public void CreateChunked_WithLargeShape_CreatesTensor()
        {
            // Arrange - Shape that exceeds int.MaxValue
            int[] shape = new int[] { 50000, 50000 }; // 2.5B elements

            // Act
            var tensor = Tensor.CreateChunked(shape);

            // Assert
            Assert.NotNull(tensor);
            Assert.True(tensor.IsChunked);
            Assert.Equal(2_500_000_000L, tensor.TotalElements);
        }

        [Fact]
        public void CreateChunked_WithGradientsEnabled_InitializesGradients()
        {
            // Arrange
            int[] shape = new int[] { 1000, 1000 };

            // Act
            var tensor = Tensor.CreateChunked(shape, requiresGrad: true);

            // Assert
            Assert.True(tensor.RequiresGrad);
            Assert.True(tensor.IsChunked);
        }

        [Fact]
        public void ShapeToSizeLong_CalculatesCorrectly()
        {
            // Arrange
            int[] shape1 = new int[] { 1000, 1000 };
            int[] shape2 = new int[] { 50000, 50000 };
            int[] shape3 = new int[] { 100, 100, 100 };

            // Act
            long size1 = Tensor.ShapeToSizeLong(shape1);
            long size2 = Tensor.ShapeToSizeLong(shape2);
            long size3 = Tensor.ShapeToSizeLong(shape3);

            // Assert
            Assert.Equal(1_000_000L, size1);
            Assert.Equal(2_500_000_000L, size2);
            Assert.Equal(1_000_000L, size3);
        }

        [Fact]
        public void InitializeRandom_OnChunkedTensor_Initializes()
        {
            // Arrange
            int[] shape = new int[] { 10000, 1000 }; // 10M elements
            var tensor = Tensor.CreateChunked(shape);
            var random = new Random(42);

            // Act
            tensor.InitializeRandom(random, 0.02f);

            // Assert
            var buffer = tensor.GetChunkedBuffer();
            bool hasNonZero = false;
            for (int chunkIdx = 0; chunkIdx < buffer.ChunkCount && !hasNonZero; chunkIdx++)
            {
                var chunk = buffer.GetChunkReadOnlySpan(chunkIdx);
                for (int i = 0; i < Math.Min(100, chunk.Length); i++)
                {
                    if (chunk[i] != 0.0f)
                    {
                        hasNonZero = true;
                        break;
                    }
                }
            }
            Assert.True(hasNonZero, "Tensor should have non-zero values after initialization");
        }

        [Fact]
        public void InitializeXavier_OnChunkedTensor_Initializes()
        {
            // Arrange
            int[] shape = new int[] { 10000, 1000 };
            var tensor = Tensor.CreateChunked(shape);
            var random = new Random(42);

            // Act
            tensor.InitializeXavier(random, fanIn: 1000, fanOut: 10000);

            // Assert
            var buffer = tensor.GetChunkedBuffer();
            bool hasNonZero = false;
            for (int chunkIdx = 0; chunkIdx < buffer.ChunkCount && !hasNonZero; chunkIdx++)
            {
                var chunk = buffer.GetChunkReadOnlySpan(chunkIdx);
                for (int i = 0; i < Math.Min(100, chunk.Length); i++)
                {
                    if (chunk[i] != 0.0f)
                    {
                        hasNonZero = true;
                        break;
                    }
                }
            }
            Assert.True(hasNonZero, "Tensor should have non-zero values after Xavier initialization");
        }

        [Fact]
        public void CopyTo_FromChunkedTensor_WorksCorrectly()
        {
            // Arrange
            int[] shape = new int[] { 1000, 1000 };
            var tensor = Tensor.CreateChunked(shape);

            // Set some values
            var buffer = tensor.GetChunkedBuffer();
            buffer.Set(0, 1.0f);
            buffer.Set(500_000, 2.0f);
            buffer.Set(999_999, 3.0f);

            // Act
            var dest = new float[100];
            tensor.CopyTo(0, dest, 100);

            // Assert
            Assert.Equal(1.0f, dest[0], Tolerance);
        }

        [Fact]
        public void CopyFrom_ToChunkedTensor_WorksCorrectly()
        {
            // Arrange
            int[] shape = new int[] { 1000, 1000 };
            var tensor = Tensor.CreateChunked(shape);
            var source = new float[100];
            for (int i = 0; i < 100; i++)
                source[i] = (float)i;

            // Act
            tensor.CopyFrom(source, 0);

            // Assert
            var buffer = tensor.GetChunkedBuffer();
            for (int i = 0; i < 100; i++)
                Assert.Equal((float)i, buffer.Get(i), Tolerance);
        }

        [Fact]
        public void ToString_ForChunkedTensor_ShowsChunkedInfo()
        {
            // Arrange
            int[] shape = new int[] { 50000, 50000 };
            var tensor = Tensor.CreateChunked(shape);

            // Act
            string str = tensor.ToString();

            // Assert
            Assert.Contains("chunked", str);
            Assert.Contains("2,500,000,000", str);
        }

        [Fact]
        public void ShapeToSize_WithExceedingShape_ThrowsWithHelpfulMessage()
        {
            // Arrange
            int[] shape = new int[] { 50000, 50000 }; // Exceeds int.MaxValue

            // Act & Assert
            var ex = Assert.Throws<ValidationException>(() => Tensor.ShapeToSize(shape));
            Assert.Contains("CreateChunked", ex.Message);
        }

        [Fact]
        public void GetChunkedBuffer_OnNonChunkedTensor_ThrowsException()
        {
            // Arrange
            var tensor = new Tensor(new int[] { 100, 100 });

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => tensor.GetChunkedBuffer());
        }

        [Fact]
        public void GetChunkedGradBuffer_OnNonChunkedTensor_ThrowsException()
        {
            // Arrange
            var tensor = new Tensor(new int[] { 100, 100 }, requiresGrad: true);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => tensor.GetChunkedGradBuffer());
        }

        [Fact]
        public void IsChunked_OnDenseTensor_ReturnsFalse()
        {
            // Arrange
            var tensor = new Tensor(new int[] { 100, 100 });

            // Act & Assert
            Assert.False(tensor.IsChunked);
        }

        [Fact]
        public void IsChunked_OnChunkedTensor_ReturnsTrue()
        {
            // Arrange
            var tensor = Tensor.CreateChunked(new int[] { 100, 100 });

            // Act & Assert
            Assert.True(tensor.IsChunked);
        }

        [Fact]
        public void TotalElements_OnDenseTensor_ReturnsCorrectSize()
        {
            // Arrange
            var tensor = new Tensor(new int[] { 100, 100 });

            // Act
            long totalElements = tensor.TotalElements;

            // Assert
            Assert.Equal(10_000L, totalElements);
        }

        [Fact]
        public void TotalElements_OnChunkedTensor_ReturnsCorrectSize()
        {
            // Arrange
            var tensor = Tensor.CreateChunked(new int[] { 50000, 50000 });

            // Act
            long totalElements = tensor.TotalElements;

            // Assert
            Assert.Equal(2_500_000_000L, totalElements);
        }
    }
}

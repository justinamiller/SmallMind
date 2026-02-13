using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;

namespace SmallMind.Tests.Core
{
    /// <summary>
    /// Unit tests for memory-mapped tensor functionality.
    /// Tests disk-based storage for very large models.
    /// </summary>
    public class MemoryMappedTensorTests : IDisposable
    {
        private const float Tolerance = 1e-6f;
        private readonly string _testDirectory;

        public MemoryMappedTensorTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "SmallMindTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }

        [Fact]
        public void CreateMemoryMappedFile_CreatesFileAndTensor()
        {
            // Arrange
            string filePath = Path.Combine(_testDirectory, "test_tensor.bin");
            int[] shape = new int[] { 1000, 1000 }; // 1M elements

            // Act
            using var tensor = Tensor.CreateMemoryMappedFile(filePath, shape);

            // Assert
            Assert.True(File.Exists(filePath));
            Assert.Equal(shape, tensor.Shape);
            Assert.True(tensor.IsMemoryMapped);
            Assert.False(tensor.IsChunked);
            Assert.Equal(1_000_000L, tensor.TotalElements);

            // Check file size
            var fileInfo = new FileInfo(filePath);
            Assert.Equal(1_000_000L * sizeof(float), fileInfo.Length);
        }

        [Fact]
        public void CreateMemoryMapped_LoadsExistingFile()
        {
            // Arrange
            string filePath = Path.Combine(_testDirectory, "existing_tensor.bin");
            int[] shape = new int[] { 100, 100 };

            // Create and populate a file
            using (var createTensor = Tensor.CreateMemoryMappedFile(filePath, shape))
            {
                var testData = new float[100];
                for (int i = 0; i < 100; i++)
                    testData[i] = i * 0.5f;
                createTensor.CopyFrom(testData, 0);
            }

            // Act - Load the existing file
            using var tensor = Tensor.CreateMemoryMapped(filePath, shape, writable: false);

            // Assert
            Assert.True(tensor.IsMemoryMapped);
            var loadedData = new float[100];
            tensor.CopyTo(0, loadedData, 100);

            for (int i = 0; i < 100; i++)
                Assert.Equal(i * 0.5f, loadedData[i], Tolerance);
        }

        [Fact]
        public void MemoryMappedTensor_CopyTo_ReadsCorrectData()
        {
            // Arrange
            string filePath = Path.Combine(_testDirectory, "read_test.bin");
            int[] shape = new int[] { 1000, 100 };

            using var tensor = Tensor.CreateMemoryMappedFile(filePath, shape);

            // Write some test data
            var writeData = new float[100];
            for (int i = 0; i < 100; i++)
                writeData[i] = (float)i;
            tensor.CopyFrom(writeData, 5000); // Write at offset 5000

            // Act
            var readData = new float[100];
            tensor.CopyTo(5000, readData, 100);

            // Assert
            for (int i = 0; i < 100; i++)
                Assert.Equal((float)i, readData[i], Tolerance);
        }

        [Fact]
        public void MemoryMappedTensor_CopyFrom_WritesCorrectData()
        {
            // Arrange
            string filePath = Path.Combine(_testDirectory, "write_test.bin");
            int[] shape = new int[] { 1000, 100 };

            using var tensor = Tensor.CreateMemoryMappedFile(filePath, shape);

            var testData = new float[50];
            for (int i = 0; i < 50; i++)
                testData[i] = i * 2.5f;

            // Act
            tensor.CopyFrom(testData, 1000);

            // Assert - Read it back
            var readData = new float[50];
            tensor.CopyTo(1000, readData, 50);

            for (int i = 0; i < 50; i++)
                Assert.Equal(i * 2.5f, readData[i], Tolerance);
        }

        [Fact]
        public void MemoryMappedTensor_LargeSize_WorksCorrectly()
        {
            // Arrange - Test with size > int.MaxValue would be too slow, so test large but reasonable size
            string filePath = Path.Combine(_testDirectory, "large_tensor.bin");
            int[] shape = new int[] { 10000, 10000 }; // 100M elements = 400MB

            // Act
            using var tensor = Tensor.CreateMemoryMappedFile(filePath, shape);

            // Assert
            Assert.Equal(100_000_000L, tensor.TotalElements);
            Assert.True(File.Exists(filePath));

            // Test reading/writing at various offsets
            var testData = new float[100];
            for (int i = 0; i < 100; i++)
                testData[i] = i * 0.1f;

            tensor.CopyFrom(testData, 50_000_000); // Middle of tensor

            var readData = new float[100];
            tensor.CopyTo(50_000_000, readData, 100);

            for (int i = 0; i < 100; i++)
                Assert.Equal(i * 0.1f, readData[i], Tolerance);
        }

        [Fact]
        public void CreateMemoryMapped_NonExistentFile_ThrowsException()
        {
            // Arrange
            string filePath = Path.Combine(_testDirectory, "does_not_exist.bin");
            int[] shape = new int[] { 100, 100 };

            // Act & Assert
            Assert.Throws<ValidationException>(() =>
                Tensor.CreateMemoryMapped(filePath, shape));
        }

        [Fact]
        public void MemoryMappedTensor_ToString_ShowsFileInfo()
        {
            // Arrange
            string filePath = Path.Combine(_testDirectory, "toString_test.bin");
            int[] shape = new int[] { 100, 100 };

            using var tensor = Tensor.CreateMemoryMappedFile(filePath, shape);

            // Act
            string str = tensor.ToString();

            // Assert
            Assert.Contains("memory-mapped", str);
            Assert.Contains("toString_test.bin", str);
            Assert.Contains("10,000", str);
        }

        [Fact]
        public void MemoryMappedTensor_IsMemoryMapped_ReturnsTrue()
        {
            // Arrange
            string filePath = Path.Combine(_testDirectory, "check_flag.bin");
            using var tensor = Tensor.CreateMemoryMappedFile(filePath, new int[] { 100, 100 });

            // Act & Assert
            Assert.True(tensor.IsMemoryMapped);
            Assert.False(tensor.IsChunked);
        }

        [Fact]
        public void DenseTensor_IsMemoryMapped_ReturnsFalse()
        {
            // Arrange
            var tensor = new Tensor(new int[] { 100, 100 });

            // Act & Assert
            Assert.False(tensor.IsMemoryMapped);
        }

        [Fact]
        public void ChunkedTensor_IsMemoryMapped_ReturnsFalse()
        {
            // Arrange
            var tensor = Tensor.CreateChunked(new int[] { 100, 100 });

            // Act & Assert
            Assert.False(tensor.IsMemoryMapped);
            Assert.True(tensor.IsChunked);
        }
    }
}

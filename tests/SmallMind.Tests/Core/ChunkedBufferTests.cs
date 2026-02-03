using System;
using Xunit;
using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;

namespace SmallMind.Tests.Core
{
    /// <summary>
    /// Unit tests for ChunkedBuffer class.
    /// Tests basic operations, chunk boundaries, and large buffer handling.
    /// </summary>
    public class ChunkedBufferTests
    {
        private const float Tolerance = 1e-6f;

        [Fact]
        public void Constructor_WithValidLength_CreatesBuffer()
        {
            // Arrange
            long totalLength = 1000;
            int chunkSize = 100;

            // Act
            var buffer = new ChunkedBuffer(totalLength, chunkSize);

            // Assert
            Assert.Equal(totalLength, buffer.Length);
            Assert.Equal(chunkSize, buffer.ChunkSize);
            Assert.Equal(10, buffer.ChunkCount); // 1000 / 100 = 10 chunks
        }

        [Fact]
        public void Constructor_WithNonDivisibleLength_CreatesCorrectChunkCount()
        {
            // Arrange
            long totalLength = 250;
            int chunkSize = 100;

            // Act
            var buffer = new ChunkedBuffer(totalLength, chunkSize);

            // Assert
            Assert.Equal(totalLength, buffer.Length);
            Assert.Equal(3, buffer.ChunkCount); // 3 chunks: 100, 100, 50
        }

        [Fact]
        public void Constructor_WithLargeLength_CreatesCorrectly()
        {
            // Arrange
            long totalLength = 3_000_000_000L; // 3 billion elements
            int chunkSize = 64 * 1024 * 1024; // 64M

            // Act
            var buffer = new ChunkedBuffer(totalLength, chunkSize);

            // Assert
            Assert.Equal(totalLength, buffer.Length);
            Assert.True(buffer.ChunkCount > 0);
            Assert.Equal((totalLength + chunkSize - 1) / chunkSize, buffer.ChunkCount);
        }

        [Fact]
        public void GetSet_WithValidIndex_WorksCorrectly()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);
            float expectedValue = 42.5f;

            // Act
            buffer.Set(500, expectedValue);
            float actualValue = buffer.Get(500);

            // Assert
            Assert.Equal(expectedValue, actualValue, Tolerance);
        }

        [Fact]
        public void GetSet_AcrossChunkBoundary_WorksCorrectly()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);

            // Act & Assert - Set values at chunk boundaries
            buffer.Set(99, 1.0f);   // Last element of chunk 0
            buffer.Set(100, 2.0f);  // First element of chunk 1
            buffer.Set(199, 3.0f);  // Last element of chunk 1
            buffer.Set(200, 4.0f);  // First element of chunk 2

            Assert.Equal(1.0f, buffer.Get(99), Tolerance);
            Assert.Equal(2.0f, buffer.Get(100), Tolerance);
            Assert.Equal(3.0f, buffer.Get(199), Tolerance);
            Assert.Equal(4.0f, buffer.Get(200), Tolerance);
        }

        [Fact]
        public void GetChunkOffset_ReturnsCorrectValues()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);

            // Act & Assert
            var (chunk0, offset0) = buffer.GetChunkOffset(0);
            Assert.Equal(0, chunk0);
            Assert.Equal(0, offset0);

            var (chunk1, offset1) = buffer.GetChunkOffset(99);
            Assert.Equal(0, chunk1);
            Assert.Equal(99, offset1);

            var (chunk2, offset2) = buffer.GetChunkOffset(100);
            Assert.Equal(1, chunk2);
            Assert.Equal(0, offset2);

            var (chunk3, offset3) = buffer.GetChunkOffset(250);
            Assert.Equal(2, chunk3);
            Assert.Equal(50, offset3);
        }

        [Fact]
        public void CopyTo_WithinSingleChunk_WorksCorrectly()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);
            for (long i = 0; i < 50; i++)
                buffer.Set(i, (float)i);

            var destination = new float[50];

            // Act
            buffer.CopyTo(0, destination, 50);

            // Assert
            for (int i = 0; i < 50; i++)
                Assert.Equal((float)i, destination[i], Tolerance);
        }

        [Fact]
        public void CopyTo_AcrossChunkBoundary_WorksCorrectly()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);
            for (long i = 50; i < 150; i++)
                buffer.Set(i, (float)i);

            var destination = new float[100];

            // Act
            buffer.CopyTo(50, destination, 100);

            // Assert
            for (int i = 0; i < 100; i++)
                Assert.Equal((float)(50 + i), destination[i], Tolerance);
        }

        [Fact]
        public void CopyFrom_WithinSingleChunk_WorksCorrectly()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);
            var source = new float[50];
            for (int i = 0; i < 50; i++)
                source[i] = (float)i;

            // Act
            buffer.CopyFrom(source, 0);

            // Assert
            for (int i = 0; i < 50; i++)
                Assert.Equal((float)i, buffer.Get(i), Tolerance);
        }

        [Fact]
        public void CopyFrom_AcrossChunkBoundary_WorksCorrectly()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);
            var source = new float[100];
            for (int i = 0; i < 100; i++)
                source[i] = (float)(50 + i);

            // Act
            buffer.CopyFrom(source, 50);

            // Assert
            for (int i = 0; i < 100; i++)
                Assert.Equal((float)(50 + i), buffer.Get(50 + i), Tolerance);
        }

        [Fact]
        public void Fill_FillsAllChunks()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);
            float fillValue = 3.14f;

            // Act
            buffer.Fill(fillValue);

            // Assert
            for (long i = 0; i < buffer.Length; i++)
                Assert.Equal(fillValue, buffer.Get(i), Tolerance);
        }

        [Fact]
        public void Clear_ClearsAllChunks()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);
            buffer.Fill(5.0f);

            // Act
            buffer.Clear();

            // Assert
            for (long i = 0; i < buffer.Length; i++)
                Assert.Equal(0.0f, buffer.Get(i), Tolerance);
        }

        [Fact]
        public void GetChunkSpan_ReturnsCorrectSpan()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);
            
            // Act
            var span = buffer.GetChunkSpan(0);
            
            // Assert
            Assert.Equal(100, span.Length);
            
            // Modify through span
            span[0] = 42.0f;
            Assert.Equal(42.0f, buffer.Get(0), Tolerance);
        }

        [Fact]
        public void GetMemoryUsageBytes_ReturnsReasonableValue()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);

            // Act
            long memoryUsage = buffer.GetMemoryUsageBytes();

            // Assert
            // Should be at least 1000 * 4 bytes for data
            Assert.True(memoryUsage >= 4000);
            // Should include overhead
            Assert.True(memoryUsage > 4000);
        }

        [Fact]
        public void Constructor_WithExceedingIntMaxValue_Works()
        {
            // Arrange
            long totalLength = (long)int.MaxValue + 1000L;
            int chunkSize = 100_000_000; // 100M elements per chunk

            // Act
            var buffer = new ChunkedBuffer(totalLength, chunkSize);

            // Assert
            Assert.Equal(totalLength, buffer.Length);
            Assert.True(buffer.ChunkCount > 0);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_WithInvalidLength_ThrowsException(long invalidLength)
        {
            // Act & Assert
            Assert.Throws<ValidationException>(() => new ChunkedBuffer(invalidLength));
        }

        [Fact]
        public void Get_WithInvalidIndex_ThrowsException()
        {
            // Arrange
            var buffer = new ChunkedBuffer(1000, 100);

            // Act & Assert
            Assert.Throws<ValidationException>(() => buffer.Get(1000));
            Assert.Throws<ValidationException>(() => buffer.Get(-1));
        }
    }
}

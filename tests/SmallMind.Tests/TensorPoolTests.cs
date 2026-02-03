using System;
using Xunit;
using SmallMind.Core.Core;
using SmallMind.Core.Exceptions;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for TensorPool memory pooling functionality.
    /// Tests bucket sizing, reuse, capacity limits, and tracking.
    /// </summary>
    public class TensorPoolTests : IDisposable
    {
        private readonly TensorPool _pool;

        public TensorPoolTests()
        {
            _pool = new TensorPool();
        }

        public void Dispose()
        {
            _pool.Clear();
            _pool.Dispose();
        }

        [Fact]
        public void Rent_WithValidSize_ReturnsArrayOfCorrectSize()
        {
            // Arrange
            int requestedSize = 100;

            // Act
            var array = _pool.Rent(requestedSize);

            // Assert
            Assert.NotNull(array);
            Assert.True(array.Length >= requestedSize);
        }

        [Fact]
        public void Rent_WithCapacityOut_ReturnsCorrectCapacity()
        {
            // Arrange
            int requestedSize = 100;

            // Act
            var array = _pool.Rent(requestedSize, out int capacity);

            // Assert
            Assert.NotNull(array);
            Assert.Equal(array.Length, capacity);
            Assert.True(capacity >= requestedSize);
        }

        [Fact]
        public void Rent_WithSmallSize_UsesSmallestBucket()
        {
            // Arrange
            int requestedSize = 50;

            // Act
            var array = _pool.Rent(requestedSize);

            // Assert
            Assert.Equal(64, array.Length); // Should use 64-bucket
        }

        [Fact]
        public void Rent_WithExactBucketSize_UsesCorrectBucket()
        {
            // Arrange
            int requestedSize = 256;

            // Act
            var array = _pool.Rent(requestedSize);

            // Assert
            Assert.Equal(256, array.Length);
        }

        [Fact]
        public void Return_AndRent_ReusesArray()
        {
            // Arrange
            var array1 = _pool.Rent(256);
            array1[0] = 42f; // Mark the array

            // Act
            _pool.Return(array1);
            var array2 = _pool.Rent(256);

            // Assert
            Assert.Same(array1, array2);
            Assert.Equal(0f, array2[0]); // Should be cleared
        }

        [Fact]
        public void Return_WithClearArrayFalse_DoesNotClearArray()
        {
            // Arrange
            var array1 = _pool.Rent(256);
            array1[0] = 42f;

            // Act
            _pool.Return(array1, clearArray: false);
            var array2 = _pool.Rent(256);

            // Assert
            Assert.Same(array1, array2);
            Assert.Equal(42f, array2[0]); // Should NOT be cleared
        }

        [Fact]
        public void Return_WithNullArray_DoesNotThrow()
        {
            // Act & Assert
            var ex = Record.Exception(() => _pool.Return(null!));
            Assert.Null(ex);
        }

        [Fact]
        public void Return_WithWrongSizeArray_DoesNotPool()
        {
            // Arrange
            var wrongSize = new float[100]; // Not a bucket size

            // Act
            _pool.Return(wrongSize);
            var newArray = _pool.Rent(100);

            // Assert
            Assert.NotSame(wrongSize, newArray); // Should allocate new
        }

        [Fact]
        public void CapacityLimits_PreventUnboundedGrowth()
        {
            // Arrange
            const int bucketSize = 64;
            const int capacity = 32; // Max 32 arrays per small bucket
            var arrays = new float[capacity + 10][];

            // Act - Rent many arrays
            for (int i = 0; i < arrays.Length; i++)
            {
                arrays[i] = _pool.Rent(bucketSize);
            }

            // Return all arrays
            for (int i = 0; i < arrays.Length; i++)
            {
                _pool.Return(arrays[i]);
            }

            // Rent again - should get mix of pooled and new
            int reusedCount = 0;
            for (int i = 0; i < arrays.Length; i++)
            {
                var newArray = _pool.Rent(bucketSize);
                for (int j = 0; j < arrays.Length; j++)
                {
                    if (ReferenceEquals(newArray, arrays[j]))
                    {
                        reusedCount++;
                        break;
                    }
                }
            }

            // Assert - should have reused at most capacity arrays
            Assert.True(reusedCount <= capacity, $"Reused {reusedCount} arrays, expected <= {capacity}");
        }

        [Fact]
        public void Clear_RemovesAllPooledArrays()
        {
            // Arrange
            var array1 = _pool.Rent(256);
            var array2 = _pool.Rent(512);
            _pool.Return(array1);
            _pool.Return(array2);

            // Act
            _pool.Clear();
            var newArray1 = _pool.Rent(256);
            var newArray2 = _pool.Rent(512);

            // Assert
            Assert.NotSame(array1, newArray1);
            Assert.NotSame(array2, newArray2);
        }

        [Fact]
        public void GetStats_TracksRentsAndReturns()
        {
            // Arrange
            var array1 = _pool.Rent(256);
            var array2 = _pool.Rent(512);
            _pool.Return(array1);

            // Act
            var stats = _pool.GetStats();

            // Assert
            Assert.Equal(2, stats.totalRents);
            Assert.Equal(1, stats.totalReturns);
            Assert.True(stats.totalAllocations >= 1); // At least one allocation
        }

        [Fact]
        public void Rent_AfterDispose_ThrowsObjectDisposedException()
        {
            // Arrange
            var pool = new TensorPool();
            pool.Dispose();

            // Act & Assert
            Assert.Throws<SmallMindObjectDisposedException>(() => pool.Rent(256));
        }

        [Fact]
        public void Return_AfterDispose_DoesNotThrow()
        {
            // Arrange
            var pool = new TensorPool();
            var array = pool.Rent(256);
            pool.Dispose();

            // Act & Assert
            var ex = Record.Exception(() => pool.Return(array));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(2048)]
        [InlineData(4096)]
        public void Rent_ForCommonSizes_UsesBuckets(int size)
        {
            // Act
            var array = _pool.Rent(size);

            // Assert
            Assert.Equal(size, array.Length);
        }

        [Fact]
        public void Rent_ForVeryLargeSize_AllocatesExactSize()
        {
            // Arrange
            // Use a size larger than the largest bucket (524288) to ensure exact allocation
            int veryLargeSize = 1000000;

            // Act
            var array = _pool.Rent(veryLargeSize);

            // Assert
            Assert.Equal(veryLargeSize, array.Length);
        }
    }
}

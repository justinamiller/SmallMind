using SmallMind.Core.Core;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for in-place tensor operations and pooled tensor lifecycle.
    /// Tests correctness, memory pooling, and automatic disposal.
    /// </summary>
    public class InPlaceOperationsTests
    {
        private const float Tolerance = 1e-5f;

        #region In-Place Operations Tests

        [Fact]
        public void AddInPlace_WithMatchingShapes_AddsCorrectly()
        {
            // Arrange
            var a = new Tensor(new[] { 1f, 2f, 3f, 4f }, new[] { 2, 2 });
            var b = new Tensor(new[] { 5f, 6f, 7f, 8f }, new[] { 2, 2 });

            // Act
            a.AddInPlace(b);

            // Assert
            Assert.Equal(6f, a.Data[0]);
            Assert.Equal(8f, a.Data[1]);
            Assert.Equal(10f, a.Data[2]);
            Assert.Equal(12f, a.Data[3]);
        }

        [Fact]
        public void AddInPlace_WithMismatchedShapes_ThrowsException()
        {
            // Arrange
            var a = new Tensor(new[] { 1f, 2f, 3f, 4f }, new[] { 2, 2 });
            var b = new Tensor(new[] { 5f, 6f, 7f }, new[] { 3 });

            // Act & Assert
            Assert.Throws<ArgumentException>(() => a.AddInPlace(b));
        }

        [Fact]
        public void MulInPlace_WithMatchingShapes_MultipliesCorrectly()
        {
            // Arrange
            var a = new Tensor(new[] { 2f, 3f, 4f, 5f }, new[] { 2, 2 });
            var b = new Tensor(new[] { 2f, 2f, 2f, 2f }, new[] { 2, 2 });

            // Act
            a.MulInPlace(b);

            // Assert
            Assert.Equal(4f, a.Data[0]);
            Assert.Equal(6f, a.Data[1]);
            Assert.Equal(8f, a.Data[2]);
            Assert.Equal(10f, a.Data[3]);
        }

        [Fact]
        public void ScaleInPlace_WithScalar_ScalesCorrectly()
        {
            // Arrange
            var a = new Tensor(new[] { 1f, 2f, 3f, 4f }, new[] { 2, 2 });

            // Act
            a.ScaleInPlace(2.5f);

            // Assert
            Assert.Equal(2.5f, a.Data[0]);
            Assert.Equal(5f, a.Data[1]);
            Assert.Equal(7.5f, a.Data[2]);
            Assert.Equal(10f, a.Data[3]);
        }

        [Fact]
        public void AddScaledInPlace_ComputesCorrectly()
        {
            // Arrange
            var a = new Tensor(new[] { 1f, 2f, 3f, 4f }, new[] { 2, 2 });
            var b = new Tensor(new[] { 10f, 20f, 30f, 40f }, new[] { 2, 2 });

            // Act - a += 0.1 * b
            a.AddScaledInPlace(b, 0.1f);

            // Assert
            Assert.Equal(2f, a.Data[0], 5);
            Assert.Equal(4f, a.Data[1], 5);
            Assert.Equal(6f, a.Data[2], 5);
            Assert.Equal(8f, a.Data[3], 5);
        }

        [Fact]
        public void CopyFrom_CopiesDataCorrectly()
        {
            // Arrange
            var source = new Tensor(new[] { 1f, 2f, 3f, 4f }, new[] { 2, 2 });
            var dest = new Tensor(new int[] { 2, 2 });

            // Act
            dest.CopyFrom(source);

            // Assert
            Assert.Equal(1f, dest.Data[0]);
            Assert.Equal(2f, dest.Data[1]);
            Assert.Equal(3f, dest.Data[2]);
            Assert.Equal(4f, dest.Data[3]);
        }

        [Fact]
        public void Add_WithDestination_WritesToDest()
        {
            // Arrange
            var a = new Tensor(new[] { 1f, 2f, 3f, 4f }, new[] { 2, 2 });
            var b = new Tensor(new[] { 5f, 6f, 7f, 8f }, new[] { 2, 2 });
            var dest = new Tensor(new int[] { 2, 2 });

            // Act
            var result = Tensor.Add(a, b, dest);

            // Assert
            Assert.Same(dest, result);
            Assert.Equal(6f, dest.Data[0]);
            Assert.Equal(8f, dest.Data[1]);
            Assert.Equal(10f, dest.Data[2]);
            Assert.Equal(12f, dest.Data[3]);
        }

        [Fact]
        public void Add_WithDestination_DoesNotAllocateNewTensor()
        {
            // Arrange
            var a = new Tensor(new[] { 1f, 2f }, new[] { 2 });
            var b = new Tensor(new[] { 3f, 4f }, new[] { 2 });
            var dest = new Tensor(new int[] { 2 });

            // Get reference to dest
            var originalDest = dest;

            // Act
            var result = Tensor.Add(a, b, dest);

            // Assert
            Assert.Same(originalDest, result);
        }

        #endregion

        #region Pooled Tensor Tests

        [Fact]
        public void CreatePooled_CreatesPooledTensor()
        {
            // Arrange & Act
            var tensor = Tensor.CreatePooled(new[] { 2, 2 });

            // Assert
            Assert.IsType<PooledTensor>(tensor);
            Assert.NotNull(tensor.Data);
            Assert.Equal(4, tensor.Size); // Logical size
            Assert.True(tensor.Capacity >= 4); // Capacity should be >= logical size

            // Cleanup
            ((PooledTensor)tensor).Dispose();
        }

        [Fact]
        public void PooledTensor_HasCorrectCapacity()
        {
            // Arrange
            var tensor = Tensor.CreatePooled(new[] { 100 }); // Should use 128-bucket

            // Assert
            Assert.Equal(128, tensor.Capacity); // Capacity from pool
            Assert.Equal(100, tensor.Size);     // Logical size

            // Cleanup
            tensor.Dispose();
        }

        [Fact]
        public void PooledTensor_Dispose_ReturnsToPool()
        {
            // Arrange
            var tensor1 = Tensor.CreatePooled(new[] { 64 });
            var data1 = tensor1.Data;

            // Act
            tensor1.Dispose();
            var tensor2 = Tensor.CreatePooled(new[] { 64 });

            // Assert
            Assert.Same(data1, tensor2.Data); // Should reuse the same array

            // Cleanup
            tensor2.Dispose();
        }

        [Fact]
        public void PooledTensor_DoubleDispose_IsSafe()
        {
            // Arrange
            var tensor = Tensor.CreatePooled(new[] { 64 });

            // Act & Assert
            tensor.Dispose();
            var ex = Record.Exception(() => tensor.Dispose());
            Assert.Null(ex);
        }

        [Fact]
        public void TensorScope_DisposesAllTensors()
        {
            // Arrange
            PooledTensor tensor1, tensor2;
            float[] data1, data2;

            using (var scope = new TensorScope())
            {
                tensor1 = scope.Rent(new[] { 64 });
                tensor2 = scope.Rent(new[] { 128 });
                data1 = tensor1.Data;
                data2 = tensor2.Data;

                // Tensors are valid here
                Assert.NotNull(tensor1.Data);
                Assert.NotNull(tensor2.Data);
            }

            // After scope disposal, tensors should be returned to pool
            var newTensor1 = Tensor.CreatePooled(new[] { 64 });
            var newTensor2 = Tensor.CreatePooled(new[] { 128 });

            // Assert - should reuse the same arrays
            Assert.Same(data1, newTensor1.Data);
            Assert.Same(data2, newTensor2.Data);

            // Cleanup
            newTensor1.Dispose();
            newTensor2.Dispose();
        }

        [Fact]
        public void TensorScope_CanRentMultipleTensors()
        {
            // Arrange & Act
            using var scope = new TensorScope();
            var tensor1 = scope.Rent(new[] { 2, 2 });
            var tensor2 = scope.Rent(new[] { 3, 3 });
            var tensor3 = scope.Rent(new[] { 4, 4 });

            // Assert
            Assert.NotNull(tensor1);
            Assert.NotNull(tensor2);
            Assert.NotNull(tensor3);
            Assert.NotSame(tensor1.Data, tensor2.Data);
            Assert.NotSame(tensor2.Data, tensor3.Data);
        }

        [Fact]
        public void TensorScope_WithGradients_WorksCorrectly()
        {
            // Arrange & Act
            using var scope = new TensorScope();
            var tensor = scope.Rent(new[] { 2, 2 }, requiresGrad: true);

            // Assert
            Assert.True(tensor.RequiresGrad);
            Assert.NotNull(tensor.Grad);
        }

        [Fact]
        public void PooledTensor_CanBeUsedInOperations()
        {
            // Arrange
            using var tensor1 = Tensor.CreatePooled(new[] { 2, 2 });
            using var tensor2 = Tensor.CreatePooled(new[] { 2, 2 });

            for (int i = 0; i < 4; i++)
            {
                tensor1.Data[i] = i + 1f;
                tensor2.Data[i] = (i + 1f) * 2f;
            }

            // Act
            tensor1.AddInPlace(tensor2);

            // Assert
            Assert.Equal(3f, tensor1.Data[0]);   // 1 + 2
            Assert.Equal(6f, tensor1.Data[1]);   // 2 + 4
            Assert.Equal(9f, tensor1.Data[2]);   // 3 + 6
            Assert.Equal(12f, tensor1.Data[3]);  // 4 + 8
        }

        #endregion

        #region Memory Efficiency Tests

        [Fact]
        public void InPlaceOps_DoNotAllocateNewArrays()
        {
            // Arrange
            var tensor = new Tensor(new[] { 1f, 2f, 3f, 4f }, new[] { 2, 2 });
            var originalData = tensor.Data;

            // Act
            tensor.ScaleInPlace(2f);
            tensor.AddScaledInPlace(tensor, 0.5f);

            // Assert
            Assert.Same(originalData, tensor.Data); // Same array reference
        }

        [Fact]
        public void PooledTensor_ReuseReducesAllocations()
        {
            // Arrange
            const int iterations = 10;
            int newAllocations = 0;
            float[]? lastArray = null;

            // Act
            for (int i = 0; i < iterations; i++)
            {
                using var tensor = Tensor.CreatePooled(new[] { 256 });
                if (lastArray == null || !ReferenceEquals(tensor.Data, lastArray))
                {
                    newAllocations++;
                    lastArray = tensor.Data;
                }
            }

            // Assert - should have allocated only once or twice (due to pooling)
            Assert.True(newAllocations <= 2, $"Expected <= 2 allocations, got {newAllocations}");
        }

        #endregion
    }
}

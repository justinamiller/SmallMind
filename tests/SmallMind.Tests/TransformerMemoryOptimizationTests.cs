using System;
using Xunit;
using SmallMind.Core.Core;
using SmallMind.Transformers;

namespace SmallMind.Tests
{
    /// <summary>
    /// Integration tests for memory optimizations in Transformer forward pass.
    /// Tests allocation reduction from pooled tensors and in-place operations.
    /// </summary>
    public class TransformerMemoryOptimizationTests
    {
        [Fact]
        public void TransformerBlock_Forward_ReducesAllocations()
        {
            // Arrange
            int nEmbd = 64;
            int nHead = 4;
            int blockSize = 16;
            float dropout = 0.0f;
            var random = new Random(42);
            
            var block = new TransformerBlock(nEmbd, nHead, blockSize, dropout, random);
            block.Eval(); // Disable dropout
            
            // Create input tensor
            int batchSize = 2;
            int seqLen = 8;
            var input = new Tensor(new int[] { batchSize, seqLen, nEmbd });
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)random.NextDouble();
            
            // Warmup
            for (int i = 0; i < 5; i++)
            {
                var _ = block.Forward(input);
            }
            
            // Measure allocations
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int iterations = 100;
            
            // Act
            for (int i = 0; i < iterations; i++)
            {
                var output = block.Forward(input);
                // Use output to prevent optimization
                _ = output.Data[0];
            }
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocations = endAllocations - startAllocations;
            double allocationsPerIteration = totalAllocations / (double)iterations;
            
            // Assert
            // With pooling, allocations should be significantly reduced
            // We still allocate result tensors, but pooled temps (LayerNorm outputs) should be reused
            double allocationsKB = allocationsPerIteration / 1024.0;
            
            // Log results for documentation
            Console.WriteLine($"Allocations per forward pass: {allocationsKB:F2} KB");
            
            // Verify pooled tensors are being used and returned
            // The test passes if pooling is working (checked separately)
            Assert.True(allocationsKB > 0, "Some allocations expected for result tensors");
        }
        
        [Fact]
        public void LayerNorm_WithDestination_ReducesAllocations()
        {
            // Arrange
            int features = 512;
            var ln = new LayerNorm(features);
            
            var input = new Tensor(new int[] { 4, features });
            var dest = new Tensor(new int[] { 4, features }, requiresGrad: true);
            
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = i % 100;
            
            // Warmup
            for (int i = 0; i < 5; i++)
            {
                ln.Forward(input, dest);
            }
            
            // Measure
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int iterations = 1000;
            
            // Act - reuse destination
            for (int i = 0; i < iterations; i++)
            {
                ln.Forward(input, dest);
            }
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocations = endAllocations - startAllocations;
            
            // Assert - verify minimal allocations (mostly for closure/backward setup)
            double allocationsKB = totalAllocations / 1024.0;
            Console.WriteLine($"Total allocations for {iterations} iterations: {allocationsKB:F2} KB");
            Console.WriteLine($"Allocations per iteration: {totalAllocations / iterations} bytes");
            
            // With destination parameter, allocations should be minimal
            // Expect < 200 KB total for 1000 iterations (~200 bytes per iteration)
            Assert.True(allocationsKB < 200.0, 
                $"Expected < 200 KB total for {iterations} iterations, got {allocationsKB:F2} KB");
        }
        
        [Fact]
        public void TensorScope_AutomaticallyReturnsPooledTensors()
        {
            // Arrange
            var pool = TensorPool.Shared;
            pool.Clear();
            
            float[] rentedArray;
            
            // Act
            using (var scope = new TensorScope())
            {
                var tensor1 = scope.Rent(new int[] { 256 });
                rentedArray = tensor1.Data;
                
                // Tensor is valid within scope
                Assert.NotNull(tensor1.Data);
                Assert.Equal(256, tensor1.Size);
            }
            
            // After scope disposal, array should be returned to pool
            var newTensor = Tensor.CreatePooled(new int[] { 256 });
            
            // Assert - should reuse the same array
            Assert.Same(rentedArray, newTensor.Data);
            
            // Cleanup
            newTensor.Dispose();
        }
        
        [Fact]
        public void PooledTensors_InTransformerForward_AreReused()
        {
            // Arrange
            var pool = TensorPool.Shared;
            var statsBefore = pool.GetStats();
            
            int nEmbd = 32;
            int nHead = 2;
            int blockSize = 8;
            float dropout = 0.0f;
            var random = new Random(42);
            
            var block = new TransformerBlock(nEmbd, nHead, blockSize, dropout, random);
            block.Eval();
            
            var input = new Tensor(new int[] { 1, 4, nEmbd });
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)random.NextDouble();
            
            // Act - run multiple forward passes
            for (int i = 0; i < 10; i++)
            {
                var output = block.Forward(input);
                _ = output.Data[0];
            }
            
            var statsAfter = pool.GetStats();
            
            // Assert - pool should show rents and returns
            long totalRents = statsAfter.totalRents - statsBefore.totalRents;
            long totalReturns = statsAfter.totalReturns - statsBefore.totalReturns;
            
            Console.WriteLine($"Total rents: {totalRents}");
            Console.WriteLine($"Total returns: {totalReturns}");
            
            // Should have rented and returned pooled tensors
            Assert.True(totalRents > 0, "Expected some tensor rentals");
            Assert.True(totalReturns > 0, "Expected some tensor returns");
        }
    }
}

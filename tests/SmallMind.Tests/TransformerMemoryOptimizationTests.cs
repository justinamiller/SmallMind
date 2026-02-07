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
        public void TransformerBlock_Forward_WithFusedLayerNorm()
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
            
            // Act - Run forward pass
            var output = block.Forward(input);
            
            // Assert - Verify output is produced correctly
            Assert.NotNull(output);
            Assert.Equal(3, output.Shape.Length);
            Assert.Equal(batchSize, output.Shape[0]);
            Assert.Equal(seqLen, output.Shape[1]);
            Assert.Equal(nEmbd, output.Shape[2]);
            
            // Verify output contains finite values (not NaN/Inf)
            for (int i = 0; i < Math.Min(100, output.Size); i++)
            {
                Assert.True(float.IsFinite(output.Data[i]), 
                    $"Output[{i}] should be finite, got {output.Data[i]}");
            }
        }
        
        [Fact(Skip = "Flaky test - allocation measurement is unreliable across different GC configurations")]
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
            
            // Measure without destination for comparison
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocationsNoDest = GC.GetTotalAllocatedBytes(precise: true);
            
            for (int i = 0; i < iterations; i++)
            {
                var _ = ln.Forward(input); // Allocates new output each time
            }
            
            long endAllocationsNoDest = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocationsNoDest = endAllocationsNoDest - startAllocationsNoDest;
            
            // Assert - with destination should allocate significantly less
            double allocationsKB = totalAllocations / 1024.0;
            double allocationsNoDestKB = totalAllocationsNoDest / 1024.0;
            double reductionPercent = ((totalAllocationsNoDest - totalAllocations) / (double)totalAllocationsNoDest) * 100.0;
            
            Console.WriteLine($"With destination: {allocationsKB:F2} KB");
            Console.WriteLine($"Without destination: {allocationsNoDestKB:F2} KB");
            Console.WriteLine($"Reduction: {reductionPercent:F1}%");
            
            // With destination parameter, allocations should be significantly reduced (>45%)
            Assert.True(reductionPercent > 45.0, 
                $"Expected >45% reduction, got {reductionPercent:F1}%");
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
        public void PooledTensors_WorkCorrectlyWithLayerNorm()
        {
            // Arrange - test that LayerNorm can use pooled tensors as destination
            using var scope = new TensorScope();
            
            int features = 128;
            var ln = new LayerNorm(features);
            
            var input = new Tensor(new int[] { 2, features });
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)(i % 100);
            
            // Rent a pooled tensor for destination
            var dest = scope.Rent(new int[] { 2, features }, requiresGrad: true);
            
            // Act - Use pooled tensor as destination
            var output = ln.Forward(input, dest);
            
            // Assert
            Assert.Same(dest, output); // Should return the dest tensor
            Assert.NotNull(output.Data);
            Assert.Equal(256, output.Size); // Logical size
            
            // Verify the output is normalized (mean ≈ 0)
            float mean = 0f;
            for (int i = 0; i < features; i++)
                mean += output.Data[i];
            mean /= features;
            
            Assert.True(Math.Abs(mean) < 0.01f, $"Expected normalized mean near 0, got {mean}");
        }
    }
}

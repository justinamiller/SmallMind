using System;
using System.Diagnostics;
using SmallMind.Core.Core;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Allocation profiler to measure GC pressure improvements from ArrayPool optimizations.
    /// Specifically targets the MatMul backward pass temp buffer allocations.
    /// </summary>
    class AllocationProfiler
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Allocation Profiler ===");
            Console.WriteLine($"Runtime: .NET {Environment.Version}");
            Console.WriteLine($"OS: {Environment.OSVersion}");
            Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
            Console.WriteLine();
            
            ProfileMatMulBackwardPass();
            ProfileTrainingWorkload();
            
            Console.WriteLine("\n=== Profiling Complete ===");
        }
        
        static void ProfileMatMulBackwardPass()
        {
            Console.WriteLine("--- MatMul Backward Pass Allocation Profile ---");
            Console.WriteLine("This benchmark measures allocation reduction from using ArrayPool");
            Console.WriteLine("for temporary gradient buffers in MatMul backward pass.\n");
            
            const int iterations = 100;
            const int M = 128;
            const int K = 256;
            const int N = 128;
            
            // Create tensors for matmul
            var a = new Tensor(new int[] { M, K }, requiresGrad: true);
            var b = new Tensor(new int[] { K, N }, requiresGrad: true);
            
            // Initialize with random data
            var random = new Random(42);
            for (int i = 0; i < a.Size; i++)
                a.Data[i] = (float)random.NextDouble();
            for (int i = 0; i < b.Size; i++)
                b.Data[i] = (float)random.NextDouble();
            
            // Warmup
            for (int i = 0; i < 10; i++)
            {
                var result = Tensor.MatMul(a, b, requiresGrad: true);
                result.Backward();
                a.ZeroGrad();
                b.ZeroGrad();
            }
            
            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);
            
            var sw = Stopwatch.StartNew();
            
            // Run benchmark
            for (int i = 0; i < iterations; i++)
            {
                var result = Tensor.MatMul(a, b, requiresGrad: true);
                result.Backward(); // This triggers the backward pass with temp buffers
                a.ZeroGrad();
                b.ZeroGrad();
            }
            
            sw.Stop();
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocations = endAllocations - startAllocations;
            int gen0Collections = GC.CollectionCount(0) - startGen0;
            int gen1Collections = GC.CollectionCount(1) - startGen1;
            int gen2Collections = GC.CollectionCount(2) - startGen2;
            
            Console.WriteLine($"Matrix dimensions: {M}×{K} @ {K}×{N} = {M}×{N}");
            Console.WriteLine($"Iterations: {iterations}");
            Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Avg time per iteration: {sw.Elapsed.TotalMilliseconds / iterations:F3}ms");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total allocations: {totalAllocations / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"  Allocations per iteration: {totalAllocations / iterations / 1024.0:F2} KB");
            Console.WriteLine($"  Gen0 Collections: {gen0Collections}");
            Console.WriteLine($"  Gen1 Collections: {gen1Collections}");
            Console.WriteLine($"  Gen2 Collections: {gen2Collections}");
            Console.WriteLine();
            
            // Calculate expected allocation without pooling
            long tempGradASize = M * K * sizeof(float);
            long tempGradBSize = K * N * sizeof(float);
            long expectedPerIteration = tempGradASize + tempGradBSize;
            long expectedTotal = expectedPerIteration * iterations;
            
            Console.WriteLine("Analysis:");
            Console.WriteLine($"  Expected allocations WITHOUT pooling: {expectedTotal / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"  Expected per iteration: {expectedPerIteration / 1024.0:F2} KB");
            
            // With ArrayPool, we expect near-zero allocations after warmup
            double reductionPercent = ((expectedTotal - totalAllocations) / (double)expectedTotal) * 100;
            Console.WriteLine($"  Estimated reduction: {reductionPercent:F1}%");
            
            if (reductionPercent > 80)
            {
                Console.WriteLine("  ✓ Excellent reduction - ArrayPool is working effectively!");
            }
            else if (reductionPercent > 50)
            {
                Console.WriteLine("  ✓ Good reduction - ArrayPool is helping");
            }
            else
            {
                Console.WriteLine("  ⚠️  Lower than expected reduction");
            }
            
            Console.WriteLine();
        }
        
        static void ProfileTrainingWorkload()
        {
            Console.WriteLine("--- Training Workload Allocation Profile ---");
            Console.WriteLine("Simulates a mini training loop to measure overall allocation impact.\n");
            
            const int steps = 50;
            const int batchSize = 32;
            const int hiddenSize = 256;
            
            // Create simple 2-layer network
            var w1 = new Tensor(new int[] { batchSize, hiddenSize }, requiresGrad: true);
            var w2 = new Tensor(new int[] { hiddenSize, hiddenSize }, requiresGrad: true);
            var w3 = new Tensor(new int[] { hiddenSize, batchSize }, requiresGrad: true);
            
            var random = new Random(42);
            for (int i = 0; i < w1.Size; i++) w1.Data[i] = (float)random.NextDouble();
            for (int i = 0; i < w2.Size; i++) w2.Data[i] = (float)random.NextDouble();
            for (int i = 0; i < w3.Size; i++) w3.Data[i] = (float)random.NextDouble();
            
            // Warmup
            for (int i = 0; i < 5; i++)
            {
                var h1 = Tensor.MatMul(w1, w2, requiresGrad: true);
                var output = Tensor.MatMul(h1, w3, requiresGrad: true);
                output.Backward();
                w1.ZeroGrad();
                w2.ZeroGrad();
                w3.ZeroGrad();
            }
            
            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            
            var sw = Stopwatch.StartNew();
            
            // Training loop
            for (int step = 0; step < steps; step++)
            {
                // Forward pass (2 matmuls)
                var h1 = Tensor.MatMul(w1, w2, requiresGrad: true);
                var output = Tensor.MatMul(h1, w3, requiresGrad: true);
                
                // Backward pass (triggers temp buffer allocations)
                output.Backward();
                
                // Zero gradients for next iteration
                w1.ZeroGrad();
                w2.ZeroGrad();
                w3.ZeroGrad();
            }
            
            sw.Stop();
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocations = endAllocations - startAllocations;
            int gen0Collections = GC.CollectionCount(0) - startGen0;
            
            Console.WriteLine($"Steps: {steps}");
            Console.WriteLine($"Batch size: {batchSize}, Hidden size: {hiddenSize}");
            Console.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
            Console.WriteLine($"Avg time per step: {sw.Elapsed.TotalMilliseconds / steps:F3}ms");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total allocations: {totalAllocations / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"  Allocations per step: {totalAllocations / steps / 1024.0:F2} KB");
            Console.WriteLine($"  Gen0 Collections: {gen0Collections}");
            Console.WriteLine($"  Throughput: {steps * batchSize / sw.Elapsed.TotalSeconds:F0} samples/sec");
            Console.WriteLine();
            
            // Each step has 2 matmuls, each with 2 temp buffers in backward
            // 4 temp buffers per step total
            long expectedPerStep = 4 * (batchSize * hiddenSize + hiddenSize * hiddenSize + hiddenSize * batchSize) * sizeof(float);
            long expectedTotal = expectedPerStep * steps;
            
            Console.WriteLine("Analysis:");
            Console.WriteLine($"  Expected WITHOUT pooling: {expectedTotal / 1024.0 / 1024.0:F2} MB");
            double reductionPercent = ((expectedTotal - totalAllocations) / (double)expectedTotal) * 100;
            Console.WriteLine($"  Estimated reduction: {reductionPercent:F1}%");
            
            if (gen0Collections == 0)
            {
                Console.WriteLine("  ✓ Zero Gen0 collections - excellent memory pressure reduction!");
            }
            
            Console.WriteLine();
        }
    }
}

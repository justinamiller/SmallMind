using System;
using System.Diagnostics;
using SmallMind.Core.Core;

namespace SmallMind.Benchmark
{
    /// <summary>
    /// Memory optimization benchmark - measures allocation reduction from:
    /// 1. TensorPool usage
    /// 2. In-place operations
    /// 3. Fused LayerNorm
    /// </summary>
    class MemoryBenchmark
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Memory Optimization Benchmark ===\n");
            Console.WriteLine($"Runtime: .NET {Environment.Version}");
            Console.WriteLine($"OS: {Environment.OSVersion}");
            Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
            Console.WriteLine();
            
            // Run benchmarks
            BenchmarkTensorPooling();
            BenchmarkInPlaceOperations();
            BenchmarkFusedLayerNorm();
            
            Console.WriteLine("\n=== Benchmark Complete ===");
        }
        
        static void BenchmarkTensorPooling()
        {
            Console.WriteLine("--- TensorPool Benchmark ---");
            
            const int iterations = 1000;
            const int tensorSize = 512;
            
            // Baseline: without pooling
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var tensor = new Tensor(new int[] { tensorSize });
                // Simulate work
                for (int j = 0; j < tensorSize; j++)
                    tensor.Data[j] = j;
            }
            sw.Stop();
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long baselineAllocations = endAllocations - startAllocations;
            int baselineGen0 = GC.CollectionCount(0) - startGen0;
            int baselineGen1 = GC.CollectionCount(1) - startGen1;
            int baselineGen2 = GC.CollectionCount(2) - startGen2;
            long baselineTimeMs = sw.ElapsedMilliseconds;
            
            Console.WriteLine($"  Baseline (No Pooling):");
            Console.WriteLine($"    Allocations: {baselineAllocations / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"    Gen0 Collections: {baselineGen0}");
            Console.WriteLine($"    Gen1 Collections: {baselineGen1}");
            Console.WriteLine($"    Gen2 Collections: {baselineGen2}");
            Console.WriteLine($"    Time: {baselineTimeMs}ms");
            Console.WriteLine();
            
            // With pooling
            TensorPool.Shared.Clear();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            startGen0 = GC.CollectionCount(0);
            startGen1 = GC.CollectionCount(1);
            startGen2 = GC.CollectionCount(2);
            
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                using var tensor = Tensor.CreatePooled(new int[] { tensorSize });
                // Simulate work
                for (int j = 0; j < tensorSize; j++)
                    tensor.Data[j] = j;
            }
            sw.Stop();
            
            endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long pooledAllocations = endAllocations - startAllocations;
            int pooledGen0 = GC.CollectionCount(0) - startGen0;
            int pooledGen1 = GC.CollectionCount(1) - startGen1;
            int pooledGen2 = GC.CollectionCount(2) - startGen2;
            long pooledTimeMs = sw.ElapsedMilliseconds;
            
            Console.WriteLine($"  With Pooling:");
            Console.WriteLine($"    Allocations: {pooledAllocations / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"    Gen0 Collections: {pooledGen0}");
            Console.WriteLine($"    Gen1 Collections: {pooledGen1}");
            Console.WriteLine($"    Gen2 Collections: {pooledGen2}");
            Console.WriteLine($"    Time: {pooledTimeMs}ms");
            Console.WriteLine();
            
            double allocationReduction = ((baselineAllocations - pooledAllocations) / (double)baselineAllocations) * 100;
            double gen0Reduction = ((baselineGen0 - pooledGen0) / (double)Math.Max(1, baselineGen0)) * 100;
            
            Console.WriteLine($"  Improvement:");
            Console.WriteLine($"    Allocation Reduction: {allocationReduction:F1}%");
            Console.WriteLine($"    Gen0 Collection Reduction: {gen0Reduction:F1}%");
            Console.WriteLine();
        }
        
        static void BenchmarkInPlaceOperations()
        {
            Console.WriteLine("--- In-Place Operations Benchmark ---");
            
            const int iterations = 1000;
            const int tensorSize = 512;
            
            // Baseline: allocating operations
            var a = new Tensor(new int[] { tensorSize });
            var b = new Tensor(new int[] { tensorSize });
            for (int i = 0; i < tensorSize; i++)
            {
                a.Data[i] = i;
                b.Data[i] = i * 2;
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                var result = Tensor.Add(a, b, requiresGrad: false);
                // Use result to prevent optimization
                _ = result.Data[0];
            }
            sw.Stop();
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long baselineAllocations = endAllocations - startAllocations;
            int baselineGen0 = GC.CollectionCount(0) - startGen0;
            long baselineTimeMs = sw.ElapsedMilliseconds;
            
            Console.WriteLine($"  Baseline (Allocating):");
            Console.WriteLine($"    Allocations: {baselineAllocations / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"    Gen0 Collections: {baselineGen0}");
            Console.WriteLine($"    Time: {baselineTimeMs}ms");
            Console.WriteLine();
            
            // In-place operations
            var dest = new Tensor(new int[] { tensorSize });
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            startGen0 = GC.CollectionCount(0);
            
            sw.Restart();
            for (int i = 0; i < iterations; i++)
            {
                Tensor.Add(a, b, dest);
                // Use result to prevent optimization
                _ = dest.Data[0];
            }
            sw.Stop();
            
            endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long inPlaceAllocations = endAllocations - startAllocations;
            int inPlaceGen0 = GC.CollectionCount(0) - startGen0;
            long inPlaceTimeMs = sw.ElapsedMilliseconds;
            
            Console.WriteLine($"  In-Place (Reusing Destination):");
            Console.WriteLine($"    Allocations: {inPlaceAllocations / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"    Gen0 Collections: {inPlaceGen0}");
            Console.WriteLine($"    Time: {inPlaceTimeMs}ms");
            Console.WriteLine();
            
            double allocationReduction = ((baselineAllocations - inPlaceAllocations) / (double)baselineAllocations) * 100;
            
            Console.WriteLine($"  Improvement:");
            Console.WriteLine($"    Allocation Reduction: {allocationReduction:F1}%");
            Console.WriteLine();
        }
        
        static void BenchmarkFusedLayerNorm()
        {
            Console.WriteLine("--- Fused LayerNorm Benchmark ---");
            
            const int iterations = 1000;
            const int batch = 32;
            const int features = 512;
            
            var input = new float[batch * features];
            var gamma = new float[features];
            var beta = new float[features];
            var output = new float[batch * features];
            
            var random = new Random(42);
            for (int i = 0; i < input.Length; i++)
                input[i] = (float)random.NextDouble();
            for (int i = 0; i < features; i++)
            {
                gamma[i] = 1.0f;
                beta[i] = 0.0f;
            }
            
            // Warmup
            for (int i = 0; i < 10; i++)
            {
                LayerNormOps.LayerNorm(input, gamma, beta, output, batch, features);
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAllocations = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                LayerNormOps.LayerNorm(input, gamma, beta, output, batch, features);
            }
            sw.Stop();
            
            long endAllocations = GC.GetTotalAllocatedBytes(precise: true);
            long allocations = endAllocations - startAllocations;
            int gen0 = GC.CollectionCount(0) - startGen0;
            
            Console.WriteLine($"  Fused LayerNorm ({iterations} iterations):");
            Console.WriteLine($"    Batch Size: {batch}");
            Console.WriteLine($"    Features: {features}");
            Console.WriteLine($"    Allocations: {allocations / 1024.0:F2} KB");
            Console.WriteLine($"    Gen0 Collections: {gen0}");
            Console.WriteLine($"    Average Time: {sw.Elapsed.TotalMilliseconds / iterations:F3}ms");
            Console.WriteLine($"    Throughput: {(batch * features * iterations) / sw.Elapsed.TotalSeconds:F0} elements/sec");
            
            if (allocations == 0)
            {
                Console.WriteLine($"    ✓ Zero allocations - fully fused!");
            }
            
            Console.WriteLine();
        }
    }
}

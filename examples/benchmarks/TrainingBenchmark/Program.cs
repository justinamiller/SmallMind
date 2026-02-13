using System;
using System.Diagnostics;
using SmallMind.Core;

namespace SmallMind.Benchmark
{
    /// <summary>
    /// Quick benchmark to demonstrate training optimizations
    /// </summary>
    class TrainingBenchmark
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind Training Performance Benchmark ===\n");
            
            // Benchmark AdamW Optimizer
            BenchmarkAdamW();
            
            // Benchmark MatMul
            BenchmarkMatMul();
            
            // Benchmark LayerNorm
            BenchmarkLayerNorm();
            
            Console.WriteLine("\n=== Benchmark Complete ===");
        }
        
        static void BenchmarkAdamW()
        {
            Console.WriteLine("--- AdamW Optimizer Benchmark ---");
            
            // Create parameters with various sizes
            var parameters = new System.Collections.Generic.List<Tensor>();
            parameters.Add(new Tensor(new int[] { 512, 512 }, requiresGrad: true)); // 262K params
            parameters.Add(new Tensor(new int[] { 512, 2048 }, requiresGrad: true)); // 1M params
            parameters.Add(new Tensor(new int[] { 2048, 512 }, requiresGrad: true)); // 1M params
            
            // Initialize with random data
            var random = new Random(42);
            foreach (var param in parameters)
            {
                param.InitializeRandom(random, 0.02f);
                // Initialize gradients
                for (int i = 0; i < param.Grad!.Length; i++)
                {
                    param.Grad[i] = (float)(random.NextDouble() * 2 - 1) * 0.01f;
                }
            }
            
            var optimizer = new AdamW(parameters, lr: 0.001f);
            
            // Warmup
            for (int i = 0; i < 3; i++)
            {
                optimizer.Step();
            }
            
            // Measure
            var sw = Stopwatch.StartNew();
            int iterations = 100;
            for (int i = 0; i < iterations; i++)
            {
                optimizer.Step();
            }
            sw.Stop();
            
            double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
            long totalParams = 0;
            foreach (var p in parameters) totalParams += p.Size;
            
            Console.WriteLine($"  Parameters: {totalParams:N0}");
            Console.WriteLine($"  Average step time: {avgMs:F3}ms");
            Console.WriteLine($"  Throughput: {totalParams / (avgMs / 1000.0):F0} params/sec");
            Console.WriteLine();
        }
        
        static void BenchmarkMatMul()
        {
            Console.WriteLine("--- Matrix Multiplication Benchmark ---");
            
            int[] sizes = { 128, 256, 512 };
            
            foreach (int size in sizes)
            {
                var a = new Tensor(new int[] { size, size });
                var b = new Tensor(new int[] { size, size });
                
                var random = new Random(42);
                a.InitializeRandom(random);
                b.InitializeRandom(random);
                
                // Warmup
                for (int i = 0; i < 3; i++)
                {
                    var _ = Tensor.MatMul(a, b);
                }
                
                // Measure
                var sw = Stopwatch.StartNew();
                int iterations = 10;
                for (int i = 0; i < iterations; i++)
                {
                    var result = Tensor.MatMul(a, b);
                }
                sw.Stop();
                
                double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
                double flops = 2.0 * size * size * size; // 2*N^3 for matmul
                double gflops = (flops / (avgMs / 1000.0)) / 1e9;
                
                Console.WriteLine($"  {size}x{size}: {avgMs:F3}ms, {gflops:F2} GFLOPS");
            }
            Console.WriteLine();
        }
        
        static void BenchmarkLayerNorm()
        {
            Console.WriteLine("--- LayerNorm Benchmark ---");
            
            int[] batchSizes = { 8, 16, 32 };
            int seqLen = 64;
            int features = 512;
            
            foreach (int batchSize in batchSizes)
            {
                var layerNorm = new LayerNorm(features);
                var input = new Tensor(new int[] { batchSize, seqLen, features });
                
                var random = new Random(42);
                input.InitializeRandom(random);
                
                // Warmup
                for (int i = 0; i < 3; i++)
                {
                    var _ = layerNorm.Forward(input);
                }
                
                // Measure
                var sw = Stopwatch.StartNew();
                int iterations = 100;
                for (int i = 0; i < iterations; i++)
                {
                    var output = layerNorm.Forward(input);
                }
                sw.Stop();
                
                double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
                long totalElements = (long)batchSize * seqLen * features;
                double throughputGB = (totalElements * sizeof(float) / (avgMs / 1000.0)) / 1e9;
                
                Console.WriteLine($"  Batch {batchSize}, Seq {seqLen}, Features {features}:");
                Console.WriteLine($"    Time: {avgMs:F3}ms, Throughput: {throughputGB:F2} GB/s");
            }
            Console.WriteLine();
        }
    }
}

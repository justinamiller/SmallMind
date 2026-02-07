using System;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using SmallMind.Core.Simd;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Comprehensive MatMul benchmark with allocation tracking and JIT mode testing.
    /// Measures GFLOPS, allocated bytes/op, and GC collection counts.
    /// </summary>
    class MatMulBenchmark
    {
        static void Main(string[] args)
        {
            // Parse arguments
            int size = 512;
            int warmup = 20;
            int iterations = 100;
            
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--size" && i + 1 < args.Length)
                    size = int.Parse(args[i + 1]);
                else if (args[i] == "--warmup" && i + 1 < args.Length)
                    warmup = int.Parse(args[i + 1]);
                else if (args[i] == "--iters" && i + 1 < args.Length)
                    iterations = int.Parse(args[i + 1]);
            }
            
            Console.WriteLine("=== MatMul Performance Benchmark ===\n");
            
            // Display environment info
            PrintEnvironment();
            
            // Run benchmark
            RunBenchmark(size, warmup, iterations);
            
            Console.WriteLine("\n=== Benchmark Complete ===");
        }
        
        static void PrintEnvironment()
        {
            Console.WriteLine("Environment:");
            Console.WriteLine($"  OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            Console.WriteLine($"  Architecture: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine($"  .NET: {Environment.Version}");
            Console.WriteLine($"  Processor Count: {Environment.ProcessorCount}");
            Console.WriteLine();
            
            Console.WriteLine("SIMD Support:");
            Console.WriteLine($"  AVX: {(Avx.IsSupported ? "✓" : "✗")}");
            Console.WriteLine($"  AVX2: {(Avx2.IsSupported ? "✓" : "✗")}");
            Console.WriteLine($"  FMA: {(Fma.IsSupported ? "✓" : "✗")}");
            Console.WriteLine($"  AVX-512: {(Avx512F.IsSupported ? "✓" : "✗")}");
            Console.WriteLine();
            
            Console.WriteLine("JIT Configuration:");
            Console.WriteLine($"  DOTNET_TieredCompilation: {Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "1 (default)"}");
            Console.WriteLine($"  DOTNET_TieredPGO: {Environment.GetEnvironmentVariable("DOTNET_TieredPGO") ?? "1 (default)"}");
            Console.WriteLine($"  DOTNET_ReadyToRun: {Environment.GetEnvironmentVariable("DOTNET_ReadyToRun") ?? "1 (default)"}");
            Console.WriteLine();
        }
        
        static void RunBenchmark(int size, int warmup, int iterations)
        {
            int M = size, K = size, N = size;
            
            Console.WriteLine($"Matrix Size: {M} × {K} × {K} × {N}");
            Console.WriteLine($"Warmup Iterations: {warmup}");
            Console.WriteLine($"Measured Iterations: {iterations}");
            Console.WriteLine();
            
            // Allocate matrices
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C = new float[M * N];
            
            // Initialize with deterministic random data
            Random rand = new Random(42);
            for (int i = 0; i < A.Length; i++) A[i] = (float)rand.NextDouble();
            for (int i = 0; i < B.Length; i++) B[i] = (float)rand.NextDouble();
            
            // Warmup phase
            Console.WriteLine("Warming up...");
            for (int i = 0; i < warmup; i++)
            {
                Array.Clear(C);
                MatMulOps.MatMul(A, B, C, M, K, N);
            }
            
            // Report which kernel is being used
            Console.WriteLine($"Kernel Selected: {MatMulOps.LastKernelUsed}");
            Console.WriteLine();
            
            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Measurement phase
            Console.WriteLine("Measuring...");
            
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                Array.Clear(C);
                MatMulOps.MatMul(A, B, C, M, K, N);
            }
            sw.Stop();
            
            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            int gen0After = GC.CollectionCount(0);
            int gen1After = GC.CollectionCount(1);
            int gen2After = GC.CollectionCount(2);
            
            // Calculate metrics
            double totalMs = sw.Elapsed.TotalMilliseconds;
            double msPerOp = totalMs / iterations;
            long flops = 2L * M * K * N; // 2 ops per multiply-add
            double gflops = (flops / (msPerOp / 1000.0)) / 1e9;
            long allocatedBytes = allocatedAfter - allocatedBefore;
            long bytesPerOp = allocatedBytes / iterations;
            
            // Report results
            Console.WriteLine("\n--- Results ---");
            Console.WriteLine($"Total Time: {totalMs:F2} ms");
            Console.WriteLine($"Time per Operation: {msPerOp:F3} ms");
            Console.WriteLine($"Performance: {gflops:F2} GFLOPS");
            Console.WriteLine();
            
            Console.WriteLine("Memory & GC:");
            Console.WriteLine($"  Allocated: {bytesPerOp:N0} bytes/op");
            Console.WriteLine($"  Gen0 Collections: {gen0After - gen0Before}");
            Console.WriteLine($"  Gen1 Collections: {gen1After - gen1Before}");
            Console.WriteLine($"  Gen2 Collections: {gen2After - gen2Before}");
            Console.WriteLine();
            
            // Verify correctness (spot check)
            Console.WriteLine("Correctness Check:");
            Console.WriteLine($"  C[0] = {C[0]:F6}");
            Console.WriteLine($"  C[M*N/2] = {C[M * N / 2]:F6}");
            Console.WriteLine($"  C[M*N-1] = {C[M * N - 1]:F6}");
        }
    }
}

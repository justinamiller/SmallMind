using System;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using SmallMind.Core.Simd;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Quick comparison between MatMulOps and GemmMicrokernels
    /// to identify the fastest path for 60+ GFLOPS.
    /// </summary>
    class MatMulKernelComparison
    {
        static void Main(string[] args)
        {
            Console.WriteLine("MatMul Kernel Comparison Benchmark");
            Console.WriteLine("===================================\n");
            
            int[] sizes = new int[] { 128, 256, 512, 1024 };
            int warmup = 20;
            int iterations = 100;
            
            foreach (int size in sizes)
            {
                Console.WriteLine($"\n--- Matrix Size: {size}×{size}×{size} ---");
                RunComparison(size, size, size, warmup, iterations);
            }
        }
        
        static void RunComparison(int M, int K, int N, int warmup, int iterations)
        {
            // Allocate matrices
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C1 = new float[M * N];
            float[] C2 = new float[M * N];
            
            // Initialize with random data
            var rand = new Random(42);
            for (int i = 0; i < A.Length; i++) A[i] = (float)rand.NextDouble();
            for (int i = 0; i < B.Length; i++) B[i] = (float)rand.NextDouble();
            
            // Test 1: MatMulOps (current implementation)
            Console.WriteLine("\n1. MatMulOps (current):");
            for (int i = 0; i < warmup; i++)
            {
                Array.Clear(C1);
                MatMulOps.MatMul(A, B, C1, M, K, N);
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long alloc1Before = GC.GetAllocatedBytesForCurrentThread();
            var sw1 = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                Array.Clear(C1);
                MatMulOps.MatMul(A, B, C1, M, K, N);
            }
            sw1.Stop();
            long alloc1After = GC.GetAllocatedBytesForCurrentThread();
            
            double ms1 = sw1.Elapsed.TotalMilliseconds / iterations;
            long flops = 2L * M * K * N;
            double gflops1 = (flops / (ms1 / 1000.0)) / 1e9;
            long bytes1 = (alloc1After - alloc1Before) / iterations;
            
            Console.WriteLine($"  Time/Op: {ms1:F3} ms");
            Console.WriteLine($"  GFLOPS:  {gflops1:F2}");
            Console.WriteLine($"  Alloc/Op: {bytes1:N0} bytes");
            Console.WriteLine($"  Kernel: {MatMulOps.LastKernelUsed}");
            
            // Test 2: GemmMicrokernels (optimized blocked GEMM)
            Console.WriteLine("\n2. GemmMicrokernels (blocked):");
            for (int i = 0; i < warmup; i++)
            {
                Array.Clear(C2);
                GemmMicrokernels.MatMul(A.AsSpan(), B.AsSpan(), C2.AsSpan(), M, K, N);
            }
            
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long alloc2Before = GC.GetAllocatedBytesForCurrentThread();
            var sw2 = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                Array.Clear(C2);
                GemmMicrokernels.MatMul(A.AsSpan(), B.AsSpan(), C2.AsSpan(), M, K, N);
            }
            sw2.Stop();
            long alloc2After = GC.GetAllocatedBytesForCurrentThread();
            
            double ms2 = sw2.Elapsed.TotalMilliseconds / iterations;
            double gflops2 = (flops / (ms2 / 1000.0)) / 1e9;
            long bytes2 = (alloc2After - alloc2Before) / iterations;
            
            Console.WriteLine($"  Time/Op: {ms2:F3} ms");
            Console.WriteLine($"  GFLOPS:  {gflops2:F2}");
            Console.WriteLine($"  Alloc/Op: {bytes2:N0} bytes");
            
            // Comparison
            Console.WriteLine("\n3. Comparison:");
            Console.WriteLine($"  Speedup: {(gflops2 / gflops1):F2}x");
            Console.WriteLine($"  Winner: {(gflops2 > gflops1 ? "GemmMicrokernels" : "MatMulOps")}");
            
            // Verify correctness (sample check)
            double maxDiff = 0;
            for (int i = 0; i < Math.Min(100, C1.Length); i++)
            {
                double diff = Math.Abs(C1[i] - C2[i]);
                if (diff > maxDiff) maxDiff = diff;
            }
            Console.WriteLine($"  Max Difference: {maxDiff:E6} (should be ~0)");
        }
    }
}

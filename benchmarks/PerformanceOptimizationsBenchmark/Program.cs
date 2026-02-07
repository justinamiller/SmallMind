using System;
using System.Diagnostics;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;

namespace SmallMind.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SmallMind High-Performance Optimization Benchmark Suite");
            Console.WriteLine($"Runtime: .NET {Environment.Version}");
            Console.WriteLine($"Processors: {Environment.ProcessorCount}");
            Console.WriteLine();
            
            BenchmarkGemmMicrokernel();
        }
        
        static void BenchmarkGemmMicrokernel()
        {
            Console.WriteLine("GEMM Microkernel Throughput Benchmark");
            Console.WriteLine("Purpose: Compare new GEMM kernels vs baseline");
            Console.WriteLine();
            
            int[] sizes = { 128, 256, 512, 1024 };
            
            foreach (int N in sizes)
            {
                int M = N, K = N;
                
                var A = new float[M * K];
                var B = new float[K * N];
                var C1 = new float[M * N];
                var C2 = new float[M * N];
                
                var rand = new Random(42);
                for (int i = 0; i < A.Length; i++) A[i] = (float)rand.NextDouble();
                for (int i = 0; i < B.Length; i++) B[i] = (float)rand.NextDouble();
                
                const int warmup = 10;
                const int measure = 50;
                
                for (int i = 0; i < warmup; i++)
                    MatMulOps.MatMul(A, B, C1, M, K, N);
                
                GC.Collect();
                var sw1 = Stopwatch.StartNew();
                for (int i = 0; i < measure; i++)
                {
                    Array.Clear(C1);
                    MatMulOps.MatMul(A, B, C1, M, K, N);
                }
                sw1.Stop();
                
                for (int i = 0; i < warmup; i++)
                    GemmMicrokernels.MatMul(A, B, C2, M, K, N);
                
                GC.Collect();
                var sw2 = Stopwatch.StartNew();
                for (int i = 0; i < measure; i++)
                {
                    Array.Clear(C2);
                    GemmMicrokernels.MatMul(A, B, C2, M, K, N);
                }
                sw2.Stop();
                
                double baselineMs = sw1.Elapsed.TotalMilliseconds / measure;
                double optimizedMs = sw2.Elapsed.TotalMilliseconds / measure;
                
                long ops = 2L * M * N * K;
                double baselineGFlops = ops / (baselineMs / 1000.0) / 1e9;
                double optimizedGFlops = ops / (optimizedMs / 1000.0) / 1e9;
                double speedup = baselineMs / optimizedMs;
                
                Console.WriteLine($"Matrix: {M}x{K}x{N}");
                Console.WriteLine($"  Baseline:  {baselineMs:F3} ms, {baselineGFlops:F2} GFLOPS");
                Console.WriteLine($"  Optimized: {optimizedMs:F3} ms, {optimizedGFlops:F2} GFLOPS");
                Console.WriteLine($"  Speedup:   {speedup:F2}x {(speedup >= 1.3 ? "GOOD" : speedup >= 1.0 ? "OK" : "REGRESSION")}");
                Console.WriteLine();
            }
        }
    }
}

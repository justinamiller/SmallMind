using System;
using System.Diagnostics;
using System.Runtime.Intrinsics.X86;
using SmallMind.Core.Simd;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Comprehensive MatMul benchmark suite for 60+ GFLOPS optimization project.
    /// Implements all requirements from Phase 0-1:
    /// - Environment locking (CPU, SIMD, threads, etc.)
    /// - Unpacked baseline benchmarks
    /// - Packed-B steady-state benchmarks (LLM realistic)
    /// - Multiple matrix sizes (small, medium, LLM-ish shapes)
    /// - Before/after comparison reporting
    /// </summary>
    class MatMulComprehensiveBenchmark
    {
        private struct BenchmarkResult
        {
            public string Name;
            public int M, K, N;
            public double GFlops;
            public double MsPerOp;
            public long BytesPerOp;
            public int Gen0, Gen1, Gen2;
            public string KernelUsed;
            public bool IsPacked;
        }

        static void Main(string[] args)
        {
            // Parse arguments
            bool runFast = Array.IndexOf(args, "--fast") >= 0;
            bool runPackedOnly = Array.IndexOf(args, "--packed-only") >= 0;
            bool runUnpackedOnly = Array.IndexOf(args, "--unpacked-only") >= 0;
            
            int warmup = runFast ? 10 : 50;
            int iterations = runFast ? 50 : 200;
            
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  MatMul Comprehensive Benchmark - 60+ GFLOPS Optimization    ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            
            // Phase 0.2: Lock benchmark environment and print specs
            PrintEnvironmentSpecs();
            
            Console.WriteLine($"\nBenchmark Configuration:");
            Console.WriteLine($"  Warmup iterations: {warmup}");
            Console.WriteLine($"  Measured iterations: {iterations}");
            Console.WriteLine($"  Mode: {(runFast ? "FAST" : "THOROUGH")}");
            Console.WriteLine();
            
            // Run all benchmark suites
            var results = new System.Collections.Generic.List<BenchmarkResult>();
            
            // Phase 1A: Unpacked baseline benchmarks
            if (!runPackedOnly)
            {
                Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  Phase 1A: Unpacked Baseline (per-call packing overhead)     ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
                
                results.Add(RunUnpackedBenchmark("Unpacked-Small", 128, 128, 128, warmup, iterations));
                results.Add(RunUnpackedBenchmark("Unpacked-Medium", 512, 512, 512, warmup, iterations));
                results.Add(RunUnpackedBenchmark("Unpacked-Decode-4K", 1, 4096, 4096, warmup, iterations));
                results.Add(RunUnpackedBenchmark("Unpacked-Decode-16K", 1, 4096, 16384, warmup, iterations));
                results.Add(RunUnpackedBenchmark("Unpacked-Prefill-256", 256, 4096, 4096, warmup, iterations));
                results.Add(RunUnpackedBenchmark("Unpacked-Prefill-512", 512, 4096, 4096, warmup, iterations));
            }
            
            // Phase 1B: Packed-B steady-state benchmarks (LLM realistic)
            if (!runUnpackedOnly)
            {
                Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  Phase 1B: Packed-B Steady-State (LLM realistic, zero-alloc) ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
                Console.WriteLine();
                
                results.Add(RunPackedBenchmark("Packed-Small", 128, 128, 128, warmup, iterations));
                results.Add(RunPackedBenchmark("Packed-Medium", 512, 512, 512, warmup, iterations));
                results.Add(RunPackedBenchmark("Packed-Decode-4K", 1, 4096, 4096, warmup, iterations));
                results.Add(RunPackedBenchmark("Packed-Decode-16K", 1, 4096, 16384, warmup, iterations));
                results.Add(RunPackedBenchmark("Packed-Prefill-256", 256, 4096, 4096, warmup, iterations));
                results.Add(RunPackedBenchmark("Packed-Prefill-512", 512, 4096, 4096, warmup, iterations));
            }
            
            // Summary table
            PrintSummaryTable(results);
            
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Benchmark Complete                                           ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
        }
        
        /// <summary>
        /// Phase 0.2: Print environment specs for reproducibility.
        /// </summary>
        static void PrintEnvironmentSpecs()
        {
            Console.WriteLine("Environment Specifications:");
            Console.WriteLine("─────────────────────────────────────────────────────────────");
            
            // Runtime & OS
            Console.WriteLine($"Runtime:");
            Console.WriteLine($"  .NET Version:        {Environment.Version}");
            Console.WriteLine($"  OS:                  {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
            Console.WriteLine($"  Architecture:        {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
            Console.WriteLine();
            
            // CPU info
            Console.WriteLine($"CPU:");
            Console.WriteLine($"  Processor Count:     {Environment.ProcessorCount}");
            // Note: CPU model string not easily available in .NET without P/Invoke
            Console.WriteLine();
            
            // SIMD flags
            Console.WriteLine($"SIMD Support:");
            Console.WriteLine($"  AVX:                 {(Avx.IsSupported ? "✓" : "✗")}");
            Console.WriteLine($"  AVX2:                {(Avx2.IsSupported ? "✓" : "✗")}");
            Console.WriteLine($"  FMA:                 {(Fma.IsSupported ? "✓" : "✗")}");
            Console.WriteLine($"  AVX-512F:            {(Avx512F.IsSupported ? "✓" : "✗")}");
            Console.WriteLine($"  Vector<T> Width:     {System.Numerics.Vector<float>.Count}");
            Console.WriteLine();
            
            // JIT configuration
            Console.WriteLine($"JIT Configuration:");
            Console.WriteLine($"  TieredCompilation:   {Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "1 (default)"}");
            Console.WriteLine($"  TieredPGO:           {Environment.GetEnvironmentVariable("DOTNET_TieredPGO") ?? "1 (default)"}");
            Console.WriteLine($"  ReadyToRun:          {Environment.GetEnvironmentVariable("DOTNET_ReadyToRun") ?? "1 (default)"}");
            Console.WriteLine();
            
            // Thread configuration for GEMM
            Console.WriteLine($"Threading:");
            Console.WriteLine($"  Max Threads (GEMM):  {Environment.ProcessorCount}");
            Console.WriteLine($"  ThreadPool Threads:  {System.Threading.ThreadPool.ThreadCount}");
            Console.WriteLine();
        }
        
        /// <summary>
        /// Run unpacked MatMul benchmark (includes internal packing per call).
        /// </summary>
        static BenchmarkResult RunUnpackedBenchmark(string name, int M, int K, int N, int warmup, int iterations)
        {
            Console.WriteLine($"Running: {name} ({M}×{K}×{N})...");
            
            // Allocate matrices
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C = new float[M * N];
            
            // Initialize with random data
            var rand = new Random(42);
            for (int i = 0; i < A.Length; i++) A[i] = (float)rand.NextDouble();
            for (int i = 0; i < B.Length; i++) B[i] = (float)rand.NextDouble();
            
            // Warmup
            for (int i = 0; i < warmup; i++)
            {
                Array.Clear(C);
                MatMulOps.MatMul(A, B, C, M, K, N);
            }
            
            // Get kernel selection
            string kernelUsed = MatMulOps.LastKernelUsed.ToString();
            
            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Measure
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
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
            
            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
            int gen0After = GC.CollectionCount(0);
            int gen1After = GC.CollectionCount(1);
            int gen2After = GC.CollectionCount(2);
            
            // Calculate metrics
            double msPerOp = sw.Elapsed.TotalMilliseconds / iterations;
            long flops = 2L * M * K * N; // 2 ops per multiply-add
            double gflops = (flops / (msPerOp / 1000.0)) / 1e9;
            long bytesPerOp = (allocAfter - allocBefore) / iterations;
            
            var result = new BenchmarkResult
            {
                Name = name,
                M = M, K = K, N = N,
                GFlops = gflops,
                MsPerOp = msPerOp,
                BytesPerOp = bytesPerOp,
                Gen0 = gen0After - gen0Before,
                Gen1 = gen1After - gen1Before,
                Gen2 = gen2After - gen2Before,
                KernelUsed = kernelUsed,
                IsPacked = false
            };
            
            PrintResult(result);
            return result;
        }
        
        /// <summary>
        /// Run packed-B MatMul benchmark (pre-packed, zero-alloc steady-state).
        /// </summary>
        static BenchmarkResult RunPackedBenchmark(string name, int M, int K, int N, int warmup, int iterations)
        {
            Console.WriteLine($"Running: {name} ({M}×{K}×{N})...");
            
            // Allocate matrices
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C = new float[M * N];
            
            // Initialize with random data
            var rand = new Random(42);
            for (int i = 0; i < A.Length; i++) A[i] = (float)rand.NextDouble();
            for (int i = 0; i < B.Length; i++) B[i] = (float)rand.NextDouble();
            
            // PRE-PACK B matrix once (NOT in timed loop)
            var packedB = PackedMatMul.CreatePackedMatrix(B, K, N);
            
            // Warmup with packed B
            for (int i = 0; i < warmup; i++)
            {
                Array.Clear(C);
                PackedMatMul.Multiply(A, packedB, C, M, K, N);
            }
            
            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Measure (no packing in this loop!)
            long allocBefore = GC.GetAllocatedBytesForCurrentThread();
            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                Array.Clear(C);
                PackedMatMul.Multiply(A, packedB, C, M, K, N);
            }
            sw.Stop();
            
            long allocAfter = GC.GetAllocatedBytesForCurrentThread();
            int gen0After = GC.CollectionCount(0);
            int gen1After = GC.CollectionCount(1);
            int gen2After = GC.CollectionCount(2);
            
            // Cleanup
            packedB.Dispose();
            
            // Calculate metrics
            double msPerOp = sw.Elapsed.TotalMilliseconds / iterations;
            long flops = 2L * M * K * N;
            double gflops = (flops / (msPerOp / 1000.0)) / 1e9;
            long bytesPerOp = (allocAfter - allocBefore) / iterations;
            
            var result = new BenchmarkResult
            {
                Name = name,
                M = M, K = K, N = N,
                GFlops = gflops,
                MsPerOp = msPerOp,
                BytesPerOp = bytesPerOp,
                Gen0 = gen0After - gen0Before,
                Gen1 = gen1After - gen1Before,
                Gen2 = gen2After - gen2Before,
                KernelUsed = "Packed (AVX2/AVX-512)",
                IsPacked = true
            };
            
            PrintResult(result);
            return result;
        }
        
        static void PrintResult(BenchmarkResult result)
        {
            Console.WriteLine($"  GFLOPS:              {result.GFlops:F2}");
            Console.WriteLine($"  Time/Op:             {result.MsPerOp:F3} ms");
            Console.WriteLine($"  Alloc/Op:            {result.BytesPerOp:N0} bytes");
            Console.WriteLine($"  GC (Gen0/1/2):       {result.Gen0}/{result.Gen1}/{result.Gen2}");
            Console.WriteLine($"  Kernel:              {result.KernelUsed}");
            Console.WriteLine($"  Is Packed:           {(result.IsPacked ? "Yes" : "No")}");
            Console.WriteLine();
        }
        
        static void PrintSummaryTable(System.Collections.Generic.List<BenchmarkResult> results)
        {
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Summary: Before/After Comparison Table                                               ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("┌────────────────────────┬──────────────┬──────────┬──────────┬──────────────┬──────────┐");
            Console.WriteLine("│ Benchmark              │ Dims (M×K×N) │ GFLOPS   │ ms/op    │ Alloc/op     │ GC (012) │");
            Console.WriteLine("├────────────────────────┼──────────────┼──────────┼──────────┼──────────────┼──────────┤");
            
            foreach (var r in results)
            {
                string dims = $"{r.M}×{r.K}×{r.N}";
                string allocStr = r.BytesPerOp == 0 ? "0" : $"{r.BytesPerOp:N0}";
                Console.WriteLine(
                    $"│ {r.Name,-22} │ {dims,-12} │ {r.GFlops,8:F2} │ {r.MsPerOp,8:F3} │ {allocStr,12} │ {r.Gen0}/{r.Gen1}/{r.Gen2,-6} │");
            }
            
            Console.WriteLine("└────────────────────────┴──────────────┴──────────┴──────────┴──────────────┴──────────┘");
            
            // Find best results
            var unpackedResults = results.FindAll(r => !r.IsPacked);
            var packedResults = results.FindAll(r => r.IsPacked);
            
            if (unpackedResults.Count > 0 && packedResults.Count > 0)
            {
                double maxUnpackedGFlops = 0;
                double maxPackedGFlops = 0;
                
                foreach (var r in unpackedResults)
                    if (r.GFlops > maxUnpackedGFlops) maxUnpackedGFlops = r.GFlops;
                
                foreach (var r in packedResults)
                    if (r.GFlops > maxPackedGFlops) maxPackedGFlops = r.GFlops;
                
                Console.WriteLine();
                Console.WriteLine($"Best Unpacked GFLOPS:  {maxUnpackedGFlops:F2}");
                Console.WriteLine($"Best Packed GFLOPS:    {maxPackedGFlops:F2}");
                Console.WriteLine($"Speedup (Packed/Unpacked): {(maxPackedGFlops / maxUnpackedGFlops):F2}x");
            }
        }
    }
}

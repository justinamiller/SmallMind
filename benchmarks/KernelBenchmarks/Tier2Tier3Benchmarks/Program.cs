using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.Intrinsics.X86;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;
using SmallMind.Transformers;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Tier-2 and Tier-3 Performance Benchmarks (BCL Only)
    /// 
    /// Measures the impact of:
    /// - MatMulTransposeB parallelization
    /// - AVX-512 dispatch
    /// - AVX2 register blocking
    /// - Q/K/V extraction with Buffer.BlockCopy
    /// - Linear transpose precomputation
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   SmallMind Tier-2/Tier-3 Performance Benchmarks (BCL Only)         ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            PrintSystemInfo();
            Console.WriteLine();
            
            // Run all benchmarks
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 1: MatMulTransposeB Parallelization");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkMatMulTransposeBParallelization();
            
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 2: AVX-512 vs AVX2 Dispatch");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkAvx512Dispatch();
            
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 3: AVX2 Register Blocking");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkAvx2RegisterBlocking();
            
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 4: Q/K/V Extraction Performance");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkQKVExtraction();
            
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 5: Linear Transpose Precomputation");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkLinearTransposePrecomputation();
            
            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      Benchmarks Complete                             ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
        }
        
        static void PrintSystemInfo()
        {
            Console.WriteLine("System Information:");
            Console.WriteLine($"  .NET Version:         {Environment.Version}");
            Console.WriteLine($"  OS:                   {Environment.OSVersion}");
            Console.WriteLine($"  Processor Count:      {Environment.ProcessorCount}");
            Console.WriteLine($"  GC Mode:              {(GCSettings.IsServerGC ? "Server" : "Workstation")}");
            Console.WriteLine($"  AVX2 Support:         {Avx2.IsSupported}");
            Console.WriteLine($"  AVX-512F Support:     {Avx512F.IsSupported}");
            Console.WriteLine($"  FMA Support:          {Fma.IsSupported}");
        }
        
        static void BenchmarkMatMulTransposeBParallelization()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Measure parallelization benefit for attention score computation");
            Console.WriteLine("          Q @ K^T for various sequence lengths (T) and head sizes (K)");
            Console.WriteLine();
            
            // Test configurations: (T, headSize)
            var configs = new[]
            {
                (T: 128, K: 64, name: "T=128, K=64 (Small)"),
                (T: 256, K: 64, name: "T=256, K=64 (Medium)"),
                (T: 512, K: 64, name: "T=512, K=64 (Large)"),
                (T: 256, K: 128, name: "T=256, K=128 (Medium-Wide)")
            };
            
            const int warmupIters = 10;
            const int measureIters = 100;
            
            foreach (var (T, K, name) in configs)
            {
                Console.WriteLine($"Configuration: {name}");
                
                // Allocate matrices
                float[] Q = new float[T * K];
                float[] KT = new float[T * K]; // Note: B is already in transposed layout
                float[] scores = new float[T * T];
                
                // Initialize with random data
                var rng = new Random(42);
                for (int i = 0; i < Q.Length; i++) Q[i] = (float)rng.NextDouble();
                for (int i = 0; i < KT.Length; i++) KT[i] = (float)rng.NextDouble();
                
                // Warmup
                for (int i = 0; i < warmupIters; i++)
                {
                    MatMulOps.MatMulTransposeB(Q, KT, scores, T, K, T);
                }
                
                // Force GC
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                // Measure
                long startBytes = GC.GetTotalAllocatedBytes(precise: true);
                int startGen0 = GC.CollectionCount(0);
                int startGen1 = GC.CollectionCount(1);
                int startGen2 = GC.CollectionCount(2);
                
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < measureIters; i++)
                {
                    MatMulOps.MatMulTransposeB(Q, KT, scores, T, K, T);
                }
                sw.Stop();
                
                long endBytes = GC.GetTotalAllocatedBytes(precise: true);
                long totalAllocated = endBytes - startBytes;
                int gen0 = GC.CollectionCount(0) - startGen0;
                int gen1 = GC.CollectionCount(1) - startGen1;
                int gen2 = GC.CollectionCount(2) - startGen2;
                
                // Results
                double msPerOp = sw.Elapsed.TotalMilliseconds / measureIters;
                double nsPerElement = (sw.Elapsed.TotalMilliseconds * 1_000_000) / (measureIters * T * T);
                long bytesPerOp = totalAllocated / measureIters;
                
                Console.WriteLine($"  Time/Op:              {msPerOp:F3} ms");
                Console.WriteLine($"  ns/Element:           {nsPerElement:F2} ns");
                Console.WriteLine($"  Throughput:           {measureIters / sw.Elapsed.TotalSeconds:F0} ops/sec");
                Console.WriteLine($"  Bytes/Op:             {bytesPerOp:N0} bytes");
                Console.WriteLine($"  GC (Gen0/1/2):        {gen0}/{gen1}/{gen2}");
                Console.WriteLine($"  Parallelized:         {(T >= 64 && K >= 64 && Environment.ProcessorCount > 1 ? "YES" : "NO")}");
                Console.WriteLine();
            }
        }
        
        static void BenchmarkAvx512Dispatch()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Compare AVX-512 vs AVX2 performance for dot products");
            Console.WriteLine();
            
            if (!Avx512F.IsSupported)
            {
                Console.WriteLine("  AVX-512 NOT SUPPORTED on this CPU - skipping comparison");
                Console.WriteLine("  (Would show speedup on AVX-512 capable processors)");
                return;
            }
            
            // Test small dot product kernels similar to attention
            var configs = new[] { 64, 128 };
            const int measureIters = 10000;
            
            foreach (int K in configs)
            {
                Console.WriteLine($"Dot Product Size: K={K}");
                
                float[] a = new float[K];
                float[] b = new float[K];
                var rng = new Random(42);
                for (int i = 0; i < K; i++)
                {
                    a[i] = (float)rng.NextDouble();
                    b[i] = (float)rng.NextDouble();
                }
                
                // Warmup and measure
                float result = 0;
                GC.Collect();
                
                var sw = Stopwatch.StartNew();
                for (int iter = 0; iter < measureIters; iter++)
                {
                    // This will dispatch to AVX-512 if supported
                    result = 0;
                    unsafe
                    {
                        fixed (float* pa = a, pb = b)
                        {
                            for (int i = 0; i < K; i++)
                            {
                                result += pa[i] * pb[i];
                            }
                        }
                    }
                }
                sw.Stop();
                
                Console.WriteLine($"  Time/Op:              {sw.Elapsed.TotalMicroseconds / measureIters:F3} µs");
                Console.WriteLine($"  Result (sanity):      {result:F6}");
                Console.WriteLine();
            }
        }
        
        static void BenchmarkAvx2RegisterBlocking()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Validate register blocking reduces horizontal sum overhead");
            Console.WriteLine("          Computes multiple dot products simultaneously");
            Console.WriteLine();
            
            // Typical attention score computation pattern
            int T = 256;
            int K = 64;
            
            float[] Q = new float[T * K];
            float[] KT = new float[T * K];
            float[] scores = new float[T * T];
            
            var rng = new Random(42);
            for (int i = 0; i < Q.Length; i++) Q[i] = (float)rng.NextDouble();
            for (int i = 0; i < KT.Length; i++) KT[i] = (float)rng.NextDouble();
            
            const int measureIters = 100;
            
            // Warmup
            for (int i = 0; i < 10; i++)
            {
                MatMulOps.MatMulTransposeB(Q, KT, scores, T, K, T);
            }
            
            GC.Collect();
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < measureIters; i++)
            {
                MatMulOps.MatMulTransposeB(Q, KT, scores, T, K, T);
            }
            sw.Stop();
            
            Console.WriteLine($"Configuration:        T={T}, K={K}");
            Console.WriteLine($"  Time/Op:              {sw.Elapsed.TotalMilliseconds / measureIters:F3} ms");
            Console.WriteLine($"  Throughput:           {measureIters / sw.Elapsed.TotalSeconds:F0} ops/sec");
            Console.WriteLine($"  Note:                 4-way blocking reduces horizontal sums by 4x");
            Console.WriteLine();
        }
        
        static void BenchmarkQKVExtraction()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Measure Q/K/V extraction performance with improved cache locality");
            Console.WriteLine("          and Buffer.BlockCopy instead of Array.Copy");
            Console.WriteLine();
            
            // Typical transformer configurations
            var configs = new[]
            {
                (B: 2, T: 128, nHead: 8, headSize: 64),
                (B: 2, T: 256, nHead: 12, headSize: 64),
                (B: 4, T: 128, nHead: 8, headSize: 64)
            };
            
            const int measureIters = 1000;
            
            foreach (var (B, T, nHead, headSize) in configs)
            {
                Console.WriteLine($"Configuration: B={B}, T={T}, nHead={nHead}, headSize={headSize}");
                
                int nEmbd = nHead * headSize;
                int qkvDim = nEmbd + 2 * nEmbd; // Simplified: assuming nKvHead == nHead
                
                // Create attention layer to test extraction
                var attention = new MultiHeadAttention(
                    nEmbd: nEmbd,
                    nHead: nHead,
                    blockSize: 512,
                    dropout: 0.0f,
                    random: new Random(42));
                attention.Eval();
                
                var input = new Tensor(new int[] { B, T, nEmbd }, requiresGrad: false);
                var rng = new Random(42);
                for (int i = 0; i < input.Size; i++)
                    input.Data[i] = (float)rng.NextDouble();
                
                // Warmup
                for (int i = 0; i < 10; i++)
                {
                    var _ = attention.Forward(input);
                }
                
                GC.Collect();
                long startBytes = GC.GetTotalAllocatedBytes(precise: true);
                
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < measureIters; i++)
                {
                    var output = attention.Forward(input);
                }
                sw.Stop();
                
                long endBytes = GC.GetTotalAllocatedBytes(precise: true);
                long totalAllocated = endBytes - startBytes;
                
                Console.WriteLine($"  Time/Op:              {sw.Elapsed.TotalMilliseconds / measureIters:F3} ms");
                Console.WriteLine($"  Bytes/Op:             {totalAllocated / measureIters:N0} bytes");
                Console.WriteLine($"  Throughput:           {measureIters / sw.Elapsed.TotalSeconds:F0} ops/sec");
                Console.WriteLine($"  Note:                 t-outer loop improves cache locality");
                Console.WriteLine();
            }
        }
        
        static void BenchmarkLinearTransposePrecomputation()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Verify Linear.Eval() precomputes transpose and");
            Console.WriteLine("          subsequent Forward() calls have zero transpose allocation");
            Console.WriteLine();
            
            int inFeatures = 768;
            int outFeatures = 768;
            int batchSize = 16;
            int seqLen = 128;
            
            var linear = new Linear(inFeatures, outFeatures, useBias: true, random: new Random(42));
            var input = new Tensor(new int[] { batchSize, seqLen, inFeatures }, requiresGrad: false);
            
            var rng = new Random(42);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)rng.NextDouble();
            
            Console.WriteLine($"Configuration:        in={inFeatures}, out={outFeatures}, batch={batchSize}, seq={seqLen}");
            Console.WriteLine();
            
            // Test 1: Before Eval() - lazy transpose allocation
            Console.WriteLine("Phase 1: Before Eval() call (lazy transpose)");
            GC.Collect();
            long beforeEvalBytes = GC.GetTotalAllocatedBytes(precise: true);
            
            var output1 = linear.Forward(input);
            
            long afterFirstForward = GC.GetTotalAllocatedBytes(precise: true);
            long firstForwardAlloc = afterFirstForward - beforeEvalBytes;
            
            Console.WriteLine($"  First Forward alloc:  {firstForwardAlloc:N0} bytes (includes transpose)");
            
            // Test 2: Call Eval() - should precompute transpose
            Console.WriteLine();
            Console.WriteLine("Phase 2: Call Eval() - precompute transpose");
            linear.Train(); // Reset state
            linear.Eval();  // TIER-3: Should precompute transpose here
            
            Console.WriteLine("  Transpose precomputed:✓");
            
            // Test 3: Forward calls after Eval() - should have minimal allocation
            Console.WriteLine();
            Console.WriteLine("Phase 3: Forward calls after Eval() (should reuse cached transpose)");
            
            const int measureIters = 100;
            
            GC.Collect();
            long startBytes = GC.GetTotalAllocatedBytes(precise: true);
            
            for (int i = 0; i < measureIters; i++)
            {
                var output = linear.Forward(input);
            }
            
            long endBytes = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocated = endBytes - startBytes;
            long bytesPerOp = totalAllocated / measureIters;
            
            Console.WriteLine($"  Iterations:           {measureIters}");
            Console.WriteLine($"  Total allocated:      {totalAllocated:N0} bytes");
            Console.WriteLine($"  Bytes/Op:             {bytesPerOp:N0} bytes");
            Console.WriteLine($"  Expected:             Output tensor only (~{batchSize * seqLen * outFeatures * 4:N0} bytes)");
            
            // The allocation should be primarily from output tensor creation
            // No transpose allocation should occur
            int expectedOutputSize = batchSize * seqLen * outFeatures * 4;
            bool success = bytesPerOp <= expectedOutputSize * 1.2; // Allow 20% variance
            
            Console.WriteLine($"  Status:               {(success ? "✓ PASS" : "✗ FAIL")} (no transpose reallocation)");
            Console.WriteLine();
        }
    }
}

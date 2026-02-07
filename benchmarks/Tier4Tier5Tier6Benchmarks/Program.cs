using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.Intrinsics.X86;
using SmallMind.Core;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;
using SmallMind.Core.Utilities;
using SmallMind.Text;

namespace SmallMind.Benchmarks.Tier456
{
    /// <summary>
    /// Tier 4-6 Performance Benchmarks (BCL Only)
    /// 
    /// Measures the impact of:
    /// - TIER 4: Tensor buffer reuse in generation loop
    /// - TIER 4: FastExp in Softmax
    /// - TIER 5: SkipLocalsInit on hot methods
    /// - TIER 5: K-loop unrolling in AVX2 MatMul
    /// - TIER 6: Fused residual + layernorm
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   SmallMind Tier 4-6 Performance Benchmarks (BCL Only)              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            PrintSystemInfo();
            Console.WriteLine();
            
            // Run all benchmarks
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 1: Softmax FastExp vs MathF.Exp");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkSoftmaxFastExp();
            
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 2: LayerNorm with Fused Residual");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkFusedResidualLayerNorm();
            
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 3: MatMul K-Loop Unrolling (Tier 5)");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkKLoopUnrolling();
            
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
        
        /// <summary>
        /// TIER 4: Benchmark FastExp vs MathF.Exp in Softmax over various vocab sizes
        /// </summary>
        static void BenchmarkSoftmaxFastExp()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Measure FastExp speedup over MathF.Exp in Softmax");
            Console.WriteLine("          Test over realistic vocab sizes: 32K, 64K, 128K");
            Console.WriteLine();
            
            var vocabSizes = new[] { 32_000, 64_000, 128_000 };
            const int warmupIters = 100;
            const int measureIters = 1000;
            
            foreach (var vocabSize in vocabSizes)
            {
                Console.WriteLine($"\nVocab Size: {vocabSize:N0}");
                Console.WriteLine("─────────────────────────────────────────────");
                
                // Allocate logits and output buffers
                float[] logits = new float[vocabSize];
                float[] probsFast = new float[vocabSize];
                float[] probsExact = new float[vocabSize];
                
                // Initialize with random logits in typical softmax range
                var rng = new Random(42);
                for (int i = 0; i < vocabSize; i++)
                {
                    logits[i] = (float)(rng.NextDouble() * 20.0 - 10.0); // Range [-10, 10]
                }
                
                // Warmup
                for (int i = 0; i < warmupIters; i++)
                {
                    SoftmaxFast(logits, probsFast);
                    SoftmaxExact(logits, probsExact);
                }
                
                // Measure allocations
                long allocStartFast = GC.GetAllocatedBytesForCurrentThread();
                long allocStartExact = GC.GetAllocatedBytesForCurrentThread();
                
                // Benchmark FastExp version
                var swFast = Stopwatch.StartNew();
                for (int i = 0; i < measureIters; i++)
                {
                    SoftmaxFast(logits, probsFast);
                }
                swFast.Stop();
                long allocEndFast = GC.GetAllocatedBytesForCurrentThread();
                long allocFast = allocEndFast - allocStartFast;
                
                // Benchmark MathF.Exp version
                var swExact = Stopwatch.StartNew();
                for (int i = 0; i < measureIters; i++)
                {
                    SoftmaxExact(logits, probsExact);
                }
                swExact.Stop();
                long allocEndExact = GC.GetAllocatedBytesForCurrentThread();
                long allocExact = allocEndExact - allocStartExact;
                
                // Calculate accuracy metrics
                double maxAbsDiff = 0;
                double sumAbsDiff = 0;
                for (int i = 0; i < vocabSize; i++)
                {
                    double diff = Math.Abs(probsFast[i] - probsExact[i]);
                    maxAbsDiff = Math.Max(maxAbsDiff, diff);
                    sumAbsDiff += diff;
                }
                double meanAbsDiff = sumAbsDiff / vocabSize;
                
                // Calculate KL divergence (for distribution quality check)
                double klDiv = 0;
                for (int i = 0; i < vocabSize; i++)
                {
                    if (probsExact[i] > 1e-10)
                    {
                        klDiv += probsExact[i] * Math.Log(probsExact[i] / Math.Max(probsFast[i], 1e-10));
                    }
                }
                
                double fastMs = swFast.Elapsed.TotalMilliseconds;
                double exactMs = swExact.Elapsed.TotalMilliseconds;
                double speedup = exactMs / fastMs;
                
                Console.WriteLine($"  FastExp:       {fastMs:F3} ms  ({fastMs / measureIters:F4} ms/op)");
                Console.WriteLine($"  MathF.Exp:     {exactMs:F3} ms  ({exactMs / measureIters:F4} ms/op)");
                Console.WriteLine($"  Speedup:       {speedup:F2}x");
                Console.WriteLine($"  Allocations:   {allocFast} bytes (Fast), {allocExact} bytes (Exact)");
                Console.WriteLine($"  Accuracy:");
                Console.WriteLine($"    Max Abs Diff:  {maxAbsDiff:E4}");
                Console.WriteLine($"    Mean Abs Diff: {meanAbsDiff:E4}");
                Console.WriteLine($"    KL Divergence: {klDiv:E4}");
            }
        }
        
        static void SoftmaxFast(float[] logits, float[] probs)
        {
            // Find max for numerical stability
            float max = float.NegativeInfinity;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] > max) max = logits[i];
            }
            
            // Compute exp and sum using FastExp
            float sum = 0;
            for (int i = 0; i < logits.Length; i++)
            {
                probs[i] = MathUtils.FastExp(logits[i] - max);
                sum += probs[i];
            }
            
            // Normalize
            if (sum > 0)
            {
                for (int i = 0; i < logits.Length; i++)
                {
                    probs[i] /= sum;
                }
            }
        }
        
        static void SoftmaxExact(float[] logits, float[] probs)
        {
            // Find max for numerical stability
            float max = float.NegativeInfinity;
            for (int i = 0; i < logits.Length; i++)
            {
                if (logits[i] > max) max = logits[i];
            }
            
            // Compute exp and sum using MathF.Exp
            float sum = 0;
            for (int i = 0; i < logits.Length; i++)
            {
                probs[i] = MathF.Exp(logits[i] - max);
                sum += probs[i];
            }
            
            // Normalize
            if (sum > 0)
            {
                for (int i = 0; i < logits.Length; i++)
                {
                    probs[i] /= sum;
                }
            }
        }
        
        /// <summary>
        /// TIER 6: Benchmark fused residual + layernorm vs separate operations
        /// </summary>
        static void BenchmarkFusedResidualLayerNorm()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Measure fused residual+layernorm vs separate Add+LayerNorm");
            Console.WriteLine("          Test typical transformer hidden sizes");
            Console.WriteLine();
            
            var configs = new[]
            {
                (BatchSize: 1 * 128, Features: 768, Name: "B*T=128, nEmbd=768 (Small)"),
                (BatchSize: 1 * 512, Features: 768, Name: "B*T=512, nEmbd=768 (Medium)"),
                (BatchSize: 1 * 512, Features: 1024, Name: "B*T=512, nEmbd=1024 (Large)")
            };
            
            const int warmupIters = 100;
            const int measureIters = 1000;
            
            foreach (var (batchSize, features, name) in configs)
            {
                Console.WriteLine($"\nConfiguration: {name}");
                Console.WriteLine("─────────────────────────────────────────────");
                
                // Allocate buffers
                float[] input = new float[batchSize * features];
                float[] residual = new float[batchSize * features];
                float[] gamma = new float[features];
                float[] beta = new float[features];
                float[] outputFused = new float[batchSize * features];
                float[] outputSeparate = new float[batchSize * features];
                float[] temp = new float[batchSize * features];
                
                // Initialize with random data
                var rng = new Random(42);
                for (int i = 0; i < input.Length; i++)
                {
                    input[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
                    residual[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
                }
                for (int i = 0; i < features; i++)
                {
                    gamma[i] = 1.0f;
                    beta[i] = 0.0f;
                }
                
                // Warmup
                for (int i = 0; i < warmupIters; i++)
                {
                    LayerNormOps.LayerNormResidual(input, residual, gamma, beta, outputFused, batchSize, features);
                    
                    // Separate: Add then LayerNorm
                    AddArrays(input, residual, temp);
                    LayerNormOps.LayerNorm(temp, gamma, beta, outputSeparate, batchSize, features);
                }
                
                // Benchmark fused version
                long allocStartFused = GC.GetAllocatedBytesForCurrentThread();
                int gc0StartFused = GC.CollectionCount(0);
                int gc1StartFused = GC.CollectionCount(1);
                int gc2StartFused = GC.CollectionCount(2);
                
                var swFused = Stopwatch.StartNew();
                for (int i = 0; i < measureIters; i++)
                {
                    LayerNormOps.LayerNormResidual(input, residual, gamma, beta, outputFused, batchSize, features);
                }
                swFused.Stop();
                
                long allocEndFused = GC.GetAllocatedBytesForCurrentThread();
                int gc0EndFused = GC.CollectionCount(0);
                int gc1EndFused = GC.CollectionCount(1);
                int gc2EndFused = GC.CollectionCount(2);
                
                // Benchmark separate version
                long allocStartSeparate = GC.GetAllocatedBytesForCurrentThread();
                int gc0StartSeparate = GC.CollectionCount(0);
                int gc1StartSeparate = GC.CollectionCount(1);
                int gc2StartSeparate = GC.CollectionCount(2);
                
                var swSeparate = Stopwatch.StartNew();
                for (int i = 0; i < measureIters; i++)
                {
                    AddArrays(input, residual, temp);
                    LayerNormOps.LayerNorm(temp, gamma, beta, outputSeparate, batchSize, features);
                }
                swSeparate.Stop();
                
                long allocEndSeparate = GC.GetAllocatedBytesForCurrentThread();
                int gc0EndSeparate = GC.CollectionCount(0);
                int gc1EndSeparate = GC.CollectionCount(1);
                int gc2EndSeparate = GC.CollectionCount(2);
                
                // Check correctness
                double maxDiff = 0;
                double sumDiff = 0;
                for (int i = 0; i < outputFused.Length; i++)
                {
                    double diff = Math.Abs(outputFused[i] - outputSeparate[i]);
                    maxDiff = Math.Max(maxDiff, diff);
                    sumDiff += diff;
                }
                
                double fusedMs = swFused.Elapsed.TotalMilliseconds;
                double separateMs = swSeparate.Elapsed.TotalMilliseconds;
                double speedup = separateMs / fusedMs;
                
                Console.WriteLine($"  Fused:         {fusedMs:F3} ms  ({fusedMs / measureIters:F4} ms/op)");
                Console.WriteLine($"  Separate:      {separateMs:F3} ms  ({separateMs / measureIters:F4} ms/op)");
                Console.WriteLine($"  Speedup:       {speedup:F2}x");
                Console.WriteLine($"  Allocations:   {allocEndFused - allocStartFused} bytes (Fused), {allocEndSeparate - allocStartSeparate} bytes (Separate)");
                Console.WriteLine($"  GC Gen0/1/2:   {gc0EndFused - gc0StartFused}/{gc1EndFused - gc1StartFused}/{gc2EndFused - gc2StartFused} (Fused), " +
                                $"{gc0EndSeparate - gc0StartSeparate}/{gc1EndSeparate - gc1StartSeparate}/{gc2EndSeparate - gc2StartSeparate} (Separate)");
                Console.WriteLine($"  Accuracy:");
                Console.WriteLine($"    Max Diff:      {maxDiff:E4}");
                Console.WriteLine($"    Mean Diff:     {sumDiff / outputFused.Length:E4}");
            }
        }
        
        static void AddArrays(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> result)
        {
            for (int i = 0; i < a.Length; i++)
            {
                result[i] = a[i] + b[i];
            }
        }
        
        /// <summary>
        /// TIER 5: Benchmark K-loop unrolling in MatMulTransposeBRowAvx2
        /// </summary>
        static void BenchmarkKLoopUnrolling()
        {
            if (!Avx2.IsSupported || !Fma.IsSupported)
            {
                Console.WriteLine("\n  Skipping: AVX2+FMA not supported on this CPU");
                return;
            }
            
            Console.WriteLine();
            Console.WriteLine("Purpose:  Measure K-loop unrolling benefit in attention score computation");
            Console.WriteLine("          Test typical head dimensions: K=64, K=128");
            Console.WriteLine();
            
            var configs = new[]
            {
                (M: 128, K: 64, N: 128, Name: "T=128, headSize=64"),
                (M: 256, K: 64, N: 256, Name: "T=256, headSize=64"),
                (M: 128, K: 128, N: 128, Name: "T=128, headSize=128"),
                (M: 256, K: 128, N: 256, Name: "T=256, headSize=128")
            };
            
            const int warmupIters = 100;
            const int measureIters = 1000;
            
            foreach (var (M, K, N, name) in configs)
            {
                Console.WriteLine($"\nConfiguration: {name}");
                Console.WriteLine("─────────────────────────────────────────────");
                
                // Allocate matrices
                float[] A = new float[M * K];
                float[] B = new float[N * K]; // B is in transposed layout
                float[] C = new float[M * N];
                
                // Initialize with random data
                var rng = new Random(42);
                for (int i = 0; i < A.Length; i++)
                {
                    A[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
                }
                for (int i = 0; i < B.Length; i++)
                {
                    B[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
                }
                
                // Warmup
                for (int i = 0; i < warmupIters; i++)
                {
                    MatMulOps.MatMulTransposeB(A, B, C, M, K, N);
                }
                
                // Benchmark
                long allocStart = GC.GetAllocatedBytesForCurrentThread();
                int gc0Start = GC.CollectionCount(0);
                
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < measureIters; i++)
                {
                    MatMulOps.MatMulTransposeB(A, B, C, M, K, N);
                }
                sw.Stop();
                
                long allocEnd = GC.GetAllocatedBytesForCurrentThread();
                int gc0End = GC.CollectionCount(0);
                
                double ms = sw.Elapsed.TotalMilliseconds;
                double gflops = (2.0 * M * K * N * measureIters) / (ms * 1e6); // 2*M*K*N FLOPs per matmul
                
                Console.WriteLine($"  Time:          {ms:F3} ms  ({ms / measureIters:F4} ms/op)");
                Console.WriteLine($"  Performance:   {gflops:F2} GFLOPS");
                Console.WriteLine($"  Allocations:   {allocEnd - allocStart} bytes");
                Console.WriteLine($"  GC Gen0:       {gc0End - gc0Start}");
            }
        }
    }
}

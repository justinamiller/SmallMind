using System;
using System.Diagnostics;
using SmallMind.Quantization.Tensors;
using SmallMind.Quantization.Kernels;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Q4 quantization profiler to measure performance and GC pressure of 4-bit operations.
    /// Validates zero-GC goal and identifies optimization opportunities.
    /// </summary>
    class Q4ProfilerBenchmark
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║       SmallMind Q4 Quantization Performance Profiler          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine($"Runtime: .NET {Environment.Version}");
            Console.WriteLine($"OS: {Environment.OSVersion}");
            Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
            Console.WriteLine($"SIMD Enabled: {System.Numerics.Vector.IsHardwareAccelerated}");
            Console.WriteLine($"SIMD Width: {System.Numerics.Vector<float>.Count}");
            Console.WriteLine();
            
            // Run comprehensive Q4 benchmarks
            ProfileQ4QuantizationRoundTrip();
            ProfileQ4MatMulPerformance();
            ProfileQ4MatMulSIMD();
            ProfileQ4MemoryEfficiency();
            ProfileQ4InferenceScenario();
            
            Console.WriteLine("\n╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    PROFILING COMPLETE                          ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
        }
        
        static void ProfileQ4QuantizationRoundTrip()
        {
            Console.WriteLine("┌─ Q4 Quantization Round-Trip Profile ──────────────────────────┐");
            Console.WriteLine("│ Measuring quantize/dequantize performance and accuracy        │");
            Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
            
            var sizes = new[] { (128, 128), (256, 256), (512, 512), (1024, 1024) };
            
            foreach (var (rows, cols) in sizes)
            {
                int totalElements = rows * cols;
                var source = new float[totalElements];
                var random = new Random(42);
                for (int i = 0; i < totalElements; i++)
                    source[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                
                // Warmup
                for (int i = 0; i < 3; i++)
                {
                    var q = Q4Tensor.Quantize(source, rows, cols, blockSize: 64);
                    var d = q.Dequantize();
                }
                
                // Force GC before measurement
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                long startAlloc = GC.GetTotalAllocatedBytes(precise: true);
                int startGen0 = GC.CollectionCount(0);
                var sw = Stopwatch.StartNew();
                
                const int iterations = 100;
                for (int i = 0; i < iterations; i++)
                {
                    var quantized = Q4Tensor.Quantize(source, rows, cols, blockSize: 64);
                    var dequantized = quantized.Dequantize();
                }
                
                sw.Stop();
                long endAlloc = GC.GetTotalAllocatedBytes(precise: true);
                int endGen0 = GC.CollectionCount(0);
                
                double avgTimeMs = sw.Elapsed.TotalMilliseconds / iterations;
                double allocPerIterMB = (endAlloc - startAlloc) / (double)iterations / (1024 * 1024);
                int gen0Collections = endGen0 - startGen0;
                
                // Calculate compression ratio
                long originalSize = totalElements * sizeof(float);
                long quantizedSize = (totalElements + 1) / 2 + ((totalElements + 63) / 64) * sizeof(float);
                double compressionRatio = (double)originalSize / quantizedSize;
                
                Console.WriteLine($"  {rows}×{cols}:");
                Console.WriteLine($"    Avg time: {avgTimeMs:F3} ms/iteration");
                Console.WriteLine($"    Allocations: {allocPerIterMB:F2} MB/iteration");
                Console.WriteLine($"    GC Collections: {gen0Collections}");
                Console.WriteLine($"    Compression: {compressionRatio:F2}x ({originalSize / 1024}KB → {quantizedSize / 1024}KB)");
            }
            Console.WriteLine();
        }
        
        static void ProfileQ4MatMulPerformance()
        {
            Console.WriteLine("┌─ Q4 MatMul Performance Profile ───────────────────────────────┐");
            Console.WriteLine("│ Comparing Q4 vs FP32 matrix multiplication performance        │");
            Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
            
            var configs = new[] 
            { 
                (m: 1, k: 512, n: 512, name: "Inference (1×512 @ 512×512)"),
                (m: 4, k: 512, n: 512, name: "Small Batch (4×512 @ 512×512)"),
                (m: 1, k: 1024, n: 1024, name: "Large Inference (1×1024 @ 1024×1024)"),
                (m: 32, k: 256, n: 256, name: "Training Batch (32×256 @ 256×256)")
            };
            
            foreach (var (m, k, n, name) in configs)
            {
                // Prepare data
                var a = new float[m * k];
                var bFloat = new float[k * n];
                var random = new Random(42);
                
                for (int i = 0; i < a.Length; i++)
                    a[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                for (int i = 0; i < bFloat.Length; i++)
                    bFloat[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                
                var bQuant = Q4Tensor.Quantize(bFloat, k, n, blockSize: 64);
                var cFloat = new float[m * n];
                var cQuant = new float[m * n];
                
                // Warmup
                for (int i = 0; i < 5; i++)
                {
                    MatMulReference(a, bFloat, cFloat, m, k, n);
                    MatMulF32Q4.Multiply(a, bQuant, cQuant, m, k, n);
                }
                
                // Benchmark FP32
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                const int iterations = 100;
                var swFloat = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    MatMulReference(a, bFloat, cFloat, m, k, n);
                }
                swFloat.Stop();
                
                // Benchmark Q4
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                long startAlloc = GC.GetTotalAllocatedBytes(precise: true);
                int startGen0 = GC.CollectionCount(0);
                
                var swQuant = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    MatMulF32Q4.Multiply(a, bQuant, cQuant, m, k, n);
                }
                swQuant.Stop();
                
                long endAlloc = GC.GetTotalAllocatedBytes(precise: true);
                int endGen0 = GC.CollectionCount(0);
                
                double floatTimeMs = swFloat.Elapsed.TotalMilliseconds / iterations;
                double quantTimeMs = swQuant.Elapsed.TotalMilliseconds / iterations;
                double speedup = floatTimeMs / quantTimeMs;
                long allocBytes = endAlloc - startAlloc;
                int gen0Collections = endGen0 - startGen0;
                
                // Calculate GFLOPS
                long flops = 2L * m * k * n; // 2 ops per multiply-add
                double gflopsFloat = flops / (floatTimeMs * 1e6);
                double gflopsQuant = flops / (quantTimeMs * 1e6);
                
                Console.WriteLine($"  {name}:");
                Console.WriteLine($"    FP32:  {floatTimeMs:F3} ms/iter, {gflopsFloat:F2} GFLOPS");
                Console.WriteLine($"    Q4:    {quantTimeMs:F3} ms/iter, {gflopsQuant:F2} GFLOPS");
                Console.WriteLine($"    Speedup: {speedup:F2}x");
                Console.WriteLine($"    Q4 Allocations: {allocBytes / 1024}KB total, Gen0: {gen0Collections}");
            }
            Console.WriteLine();
        }
        
        static void ProfileQ4MatMulSIMD()
        {
            Console.WriteLine("┌─ Q4 SIMD MatMul Performance ──────────────────────────────────┐");
            Console.WriteLine("│ Testing SIMD-optimized Q4 matrix multiplication               │");
            Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
            
            var configs = new[] 
            { 
                (m: 1, k: 512, n: 512, name: "Inference"),
                (m: 1, k: 1024, n: 1024, name: "Large Inference")
            };
            
            foreach (var (m, k, n, name) in configs)
            {
                var a = new float[m * k];
                var bFloat = new float[k * n];
                var random = new Random(42);
                
                for (int i = 0; i < a.Length; i++)
                    a[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                for (int i = 0; i < bFloat.Length; i++)
                    bFloat[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                
                var bQuant = Q4Tensor.Quantize(bFloat, k, n, blockSize: 64);
                var cScalar = new float[m * n];
                var cSIMD = new float[m * n];
                
                // Warmup
                for (int i = 0; i < 5; i++)
                {
                    MatMulF32Q4.Multiply(a, bQuant, cScalar, m, k, n);
                    MatMulF32Q4.MultiplyVectorMatrixSIMD(a, bQuant, cSIMD, k, n);
                }
                
                const int iterations = 100;
                
                // Benchmark scalar path
                var swScalar = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    MatMulF32Q4.Multiply(a, bQuant, cScalar, m, k, n);
                }
                swScalar.Stop();
                
                // Benchmark SIMD path
                var swSIMD = Stopwatch.StartNew();
                for (int i = 0; i < iterations; i++)
                {
                    MatMulF32Q4.MultiplyVectorMatrixSIMD(a, bQuant, cSIMD, k, n);
                }
                swSIMD.Stop();
                
                double scalarTimeMs = swScalar.Elapsed.TotalMilliseconds / iterations;
                double simdTimeMs = swSIMD.Elapsed.TotalMilliseconds / iterations;
                double speedup = scalarTimeMs / simdTimeMs;
                
                Console.WriteLine($"  {name} ({m}×{k} @ {k}×{n}):");
                Console.WriteLine($"    Scalar: {scalarTimeMs:F3} ms/iter");
                Console.WriteLine($"    SIMD:   {simdTimeMs:F3} ms/iter");
                Console.WriteLine($"    SIMD Speedup: {speedup:F2}x");
            }
            Console.WriteLine();
        }
        
        static void ProfileQ4MemoryEfficiency()
        {
            Console.WriteLine("┌─ Q4 Memory Efficiency Profile ────────────────────────────────┐");
            Console.WriteLine("│ Measuring memory usage and allocation patterns                │");
            Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
            
            var sizes = new[] { 256, 512, 1024, 2048 };
            
            foreach (int size in sizes)
            {
                int totalElements = size * size;
                var source = new float[totalElements];
                var random = new Random(42);
                for (int i = 0; i < totalElements; i++)
                    source[i] = (float)(random.NextDouble() * 2.0 - 1.0);
                
                // Test different block sizes
                var blockSizes = new[] { 32, 64, 128, 256 };
                
                Console.WriteLine($"  Matrix {size}×{size}:");
                foreach (int blockSize in blockSizes)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    
                    long before = GC.GetTotalMemory(true);
                    var quantized = Q4Tensor.Quantize(source, size, size, blockSize: blockSize);
                    long after = GC.GetTotalMemory(true);
                    
                    long memoryUsed = after - before;
                    double compressionRatio = (totalElements * sizeof(float)) / (double)memoryUsed;
                    
                    Console.WriteLine($"    BlockSize={blockSize}: {memoryUsed / 1024}KB, Compression={compressionRatio:F2}x");
                }
            }
            Console.WriteLine();
        }
        
        static void ProfileQ4InferenceScenario()
        {
            Console.WriteLine("┌─ Q4 Inference Scenario Profile ───────────────────────────────┐");
            Console.WriteLine("│ Realistic LLM inference workload (sequential token generation)│");
            Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
            
            // Simulate transformer layer dimensions (similar to small GPT model)
            int hiddenSize = 512;
            int numTokens = 50;
            
            // Simulate weight matrices for a single transformer layer
            var wq = CreateRandomFloatArray(hiddenSize * hiddenSize);
            var wk = CreateRandomFloatArray(hiddenSize * hiddenSize);
            var wv = CreateRandomFloatArray(hiddenSize * hiddenSize);
            var wo = CreateRandomFloatArray(hiddenSize * hiddenSize);
            
            // Quantize weights
            var wqQuant = Q4Tensor.Quantize(wq, hiddenSize, hiddenSize, blockSize: 64);
            var wkQuant = Q4Tensor.Quantize(wk, hiddenSize, hiddenSize, blockSize: 64);
            var wvQuant = Q4Tensor.Quantize(wv, hiddenSize, hiddenSize, blockSize: 64);
            var woQuant = Q4Tensor.Quantize(wo, hiddenSize, hiddenSize, blockSize: 64);
            
            // Prepare activation buffer (single token input)
            var activation = new float[hiddenSize];
            for (int i = 0; i < hiddenSize; i++)
                activation[i] = (float)(new Random(42).NextDouble() * 2.0 - 1.0);
            
            var q = new float[hiddenSize];
            var k = new float[hiddenSize];
            var v = new float[hiddenSize];
            var output = new float[hiddenSize];
            
            // Warmup
            for (int i = 0; i < 5; i++)
            {
                MatMulF32Q4.Multiply(activation, wqQuant, q, 1, hiddenSize, hiddenSize);
                MatMulF32Q4.Multiply(activation, wkQuant, k, 1, hiddenSize, hiddenSize);
                MatMulF32Q4.Multiply(activation, wvQuant, v, 1, hiddenSize, hiddenSize);
                MatMulF32Q4.Multiply(activation, woQuant, output, 1, hiddenSize, hiddenSize);
            }
            
            // Profile token generation
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            long startAlloc = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);
            
            var sw = Stopwatch.StartNew();
            
            for (int token = 0; token < numTokens; token++)
            {
                // Simulate attention mechanism with Q4 weights
                MatMulF32Q4.Multiply(activation, wqQuant, q, 1, hiddenSize, hiddenSize);
                MatMulF32Q4.Multiply(activation, wkQuant, k, 1, hiddenSize, hiddenSize);
                MatMulF32Q4.Multiply(activation, wvQuant, v, 1, hiddenSize, hiddenSize);
                MatMulF32Q4.Multiply(activation, woQuant, output, 1, hiddenSize, hiddenSize);
                
                // Copy output to activation for next iteration
                Array.Copy(output, activation, hiddenSize);
            }
            
            sw.Stop();
            
            long endAlloc = GC.GetTotalAllocatedBytes(precise: true);
            int endGen0 = GC.CollectionCount(0);
            int endGen1 = GC.CollectionCount(1);
            int endGen2 = GC.CollectionCount(2);
            
            double totalTimeMs = sw.Elapsed.TotalMilliseconds;
            double timePerToken = totalTimeMs / numTokens;
            double tokensPerSec = numTokens / sw.Elapsed.TotalSeconds;
            long totalAlloc = endAlloc - startAlloc;
            long allocPerToken = totalAlloc / numTokens;
            
            Console.WriteLine($"  Configuration:");
            Console.WriteLine($"    Hidden size: {hiddenSize}");
            Console.WriteLine($"    Tokens generated: {numTokens}");
            Console.WriteLine($"    Weight format: Q4_0 (block_size=64)");
            Console.WriteLine();
            Console.WriteLine($"  Performance:");
            Console.WriteLine($"    Total time: {totalTimeMs:F2} ms");
            Console.WriteLine($"    Time per token: {timePerToken:F3} ms");
            Console.WriteLine($"    Throughput: {tokensPerSec:F2} tokens/sec");
            Console.WriteLine();
            Console.WriteLine($"  Memory:");
            Console.WriteLine($"    Total allocations: {totalAlloc / 1024.0:F2} KB");
            Console.WriteLine($"    Allocation per token: {allocPerToken / 1024.0:F3} KB");
            Console.WriteLine($"    Gen0 collections: {endGen0 - startGen0}");
            Console.WriteLine($"    Gen1 collections: {endGen1 - startGen1}");
            Console.WriteLine($"    Gen2 collections: {endGen2 - startGen2}");
            
            if (endGen0 == startGen0 && endGen1 == startGen1 && endGen2 == startGen2)
            {
                Console.WriteLine($"    ✓ Zero GC collections - excellent!");
            }
            else
            {
                Console.WriteLine($"    ⚠️  GC collections detected");
            }
            Console.WriteLine();
        }
        
        // Helper methods
        
        static float[] CreateRandomFloatArray(int size)
        {
            var array = new float[size];
            var random = new Random(42);
            for (int i = 0; i < size; i++)
                array[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return array;
        }
        
        static void MatMulReference(ReadOnlySpan<float> a, ReadOnlySpan<float> b, Span<float> c, int m, int k, int n)
        {
            c.Clear();
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    float sum = 0f;
                    for (int p = 0; p < k; p++)
                    {
                        sum += a[i * k + p] * b[p * n + j];
                    }
                    c[i * n + j] = sum;
                }
            }
        }
    }
}

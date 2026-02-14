using System;
using System.Diagnostics;
using SmallMind.Core.Core;
using SmallMind.Transformers;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Tier-1 Hotpath Performance Benchmark
    /// 
    /// Measures the impact of three critical optimizations:
    /// 1. Dropout zero-copy passthrough in eval mode (eliminates Clone())
    /// 2. Conditional workspace clearing (skip clearing for store-once kernels)
    /// 3. Skip clearing newly allocated arrays (already zeroed by runtime)
    /// 
    /// Captures: execution time, allocations/op, GC counts, working set deltas.
    /// All measurements use built-in .NET APIs (no third-party dependencies).
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     SmallMind Tier-1 Hotpath Performance Benchmark (BCL Only)       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"Runtime:          .NET {Environment.Version}");
            Console.WriteLine($"OS:               {Environment.OSVersion}");
            Console.WriteLine($"Processor Count:  {Environment.ProcessorCount}");
            Console.WriteLine($"GC Mode:          {(System.Runtime.GCSettings.IsServerGC ? "Server" : "Workstation")}");
            Console.WriteLine($"Date:             {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            
            // Run all benchmarks
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 1: Dropout Eval Passthrough (Zero-Copy Optimization)");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkDropoutEvalPassthrough();
            
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 2: Workspace Reuse (Conditional Clearing Optimization)");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkWorkspaceReuse();
            
            Console.WriteLine("\n═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("  BENCHMARK 3: End-to-End Transformer Forward Pass");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            BenchmarkEndToEndForward();
            
            Console.WriteLine("\n╔══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                      Benchmark Complete                              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════════════╝");
        }
        
        static void BenchmarkDropoutEvalPassthrough()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Verify Dropout.Forward() in eval mode returns input reference");
            Console.WriteLine("          without cloning, eliminating a major allocation hotspot.");
            Console.WriteLine();
            
            const int warmupIterations = 100;
            const int measureIterations = 10_000;
            int B = 4, T = 128, D = 768;
            
            var dropout = new Dropout(0.1f, new Random(42));
            dropout.Eval();
            
            var input = new Tensor(new int[] { B, T, D }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)Math.Sin(i * 0.001);
            
            // Warmup
            for (int i = 0; i < warmupIterations; i++)
            {
                var _ = dropout.Forward(input);
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
            long startWorkingSet = Process.GetCurrentProcess().WorkingSet64;
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < measureIterations; i++)
            {
                var output = dropout.Forward(input);
            }
            sw.Stop();
            
            long endBytes = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocated = endBytes - startBytes;
            int gen0 = GC.CollectionCount(0) - startGen0;
            int gen1 = GC.CollectionCount(1) - startGen1;
            int gen2 = GC.CollectionCount(2) - startGen2;
            long endWorkingSet = Process.GetCurrentProcess().WorkingSet64;
            long workingSetDelta = endWorkingSet - startWorkingSet;
            
            // Verify zero-copy
            var testOutput = dropout.Forward(input);
            bool isZeroCopy = object.ReferenceEquals(input, testOutput);
            
            // Results
            Console.WriteLine($"Configuration:    Batch={B}, SeqLen={T}, EmbedDim={D}");
            Console.WriteLine($"Iterations:       {measureIterations:N0}");
            Console.WriteLine($"Zero-Copy Check:  {(isZeroCopy ? "✓ PASS" : "✗ FAIL")} (returns same reference)");
            Console.WriteLine();
            
            Console.WriteLine("Performance Metrics:");
            Console.WriteLine($"  Total Time:            {sw.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"  Avg Time/Op:           {sw.Elapsed.TotalMicroseconds / measureIterations:F3} µs");
            Console.WriteLine($"  Throughput:            {measureIterations / sw.Elapsed.TotalSeconds:N0} ops/sec");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total Allocated:       {totalAllocated:N0} bytes ({totalAllocated / 1024.0:F1} KB)");
            Console.WriteLine($"  Bytes/Op:              {(double)totalAllocated / measureIterations:F2} bytes");
            Console.WriteLine($"  Gen0 Collections:      {gen0}");
            Console.WriteLine($"  Gen1 Collections:      {gen1}");
            Console.WriteLine($"  Gen2 Collections:      {gen2}");
            Console.WriteLine($"  Working Set Delta:     {workingSetDelta / 1024.0:F1} KB");
            Console.WriteLine();
            
            // Expected result: ~0 bytes/op after optimization (zero-copy)
            // Before optimization: ~393,216 bytes/op (B*T*D*4 per call due to Clone)
            int tensorSize = B * T * D * sizeof(float);
            Console.WriteLine($"Expected Improvement:");
            Console.WriteLine($"  Before (Clone):        ~{tensorSize:N0} bytes/op");
            Console.WriteLine($"  After (Zero-Copy):     ~0 bytes/op");
            Console.WriteLine($"  Actual Measured:       {(double)totalAllocated / measureIterations:F2} bytes/op");
            
            if (totalAllocated / measureIterations < 100)
            {
                Console.WriteLine("  Status:                ✓ OPTIMIZATION EFFECTIVE");
            }
            else
            {
                Console.WriteLine("  Status:                ⚠ UNEXPECTED ALLOCATIONS");
            }
        }
        
        static void BenchmarkWorkspaceReuse()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Measure allocation reduction from conditional workspace clearing");
            Console.WriteLine("          and skipping clears on newly allocated arrays.");
            Console.WriteLine();
            
            const int warmupIterations = 10;
            const int measureIterations = 100;
            int B = 2, T = 64, nEmbd = 256, nHead = 4;
            
            var random = new Random(42);
            var attention = new MultiHeadAttention(
                nEmbd: nEmbd,
                nHead: nHead,
                blockSize: 512,
                dropout: 0.0f,
                random: random);
            attention.Eval();
            
            var input = new Tensor(new int[] { B, T, nEmbd }, requiresGrad: false);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = (float)Math.Sin(i * 0.01);
            
            // Warmup - allocates workspaces
            Console.WriteLine("Warming up (allocating workspaces)...");
            for (int i = 0; i < warmupIterations; i++)
            {
                var _ = attention.Forward(input);
            }
            
            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Measure reuse performance
            long startBytes = GC.GetTotalAllocatedBytes(precise: true);
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);
            long startWorkingSet = Process.GetCurrentProcess().WorkingSet64;
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < measureIterations; i++)
            {
                var output = attention.Forward(input);
            }
            sw.Stop();
            
            long endBytes = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocated = endBytes - startBytes;
            int gen0 = GC.CollectionCount(0) - startGen0;
            int gen1 = GC.CollectionCount(1) - startGen1;
            int gen2 = GC.CollectionCount(2) - startGen2;
            long endWorkingSet = Process.GetCurrentProcess().WorkingSet64;
            long workingSetDelta = endWorkingSet - startWorkingSet;
            
            // Results
            Console.WriteLine($"Configuration:    Batch={B}, SeqLen={T}, EmbedDim={nEmbd}, Heads={nHead}");
            Console.WriteLine($"Iterations:       {measureIterations:N0} (after warmup)");
            Console.WriteLine();
            
            Console.WriteLine("Performance Metrics:");
            Console.WriteLine($"  Total Time:            {sw.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"  Avg Time/Op:           {sw.Elapsed.TotalMilliseconds / measureIterations:F3} ms");
            Console.WriteLine($"  Throughput:            {measureIterations / sw.Elapsed.TotalSeconds:N0} ops/sec");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total Allocated:       {totalAllocated:N0} bytes ({totalAllocated / 1024.0:F1} KB)");
            Console.WriteLine($"  Bytes/Op:              {(double)totalAllocated / measureIterations:F2} bytes");
            Console.WriteLine($"  Gen0 Collections:      {gen0}");
            Console.WriteLine($"  Gen1 Collections:      {gen1}");
            Console.WriteLine($"  Gen2 Collections:      {gen2}");
            Console.WriteLine($"  Working Set Delta:     {workingSetDelta / 1024.0:F1} KB");
            Console.WriteLine();
            
            // Analysis
            double bytesPerOp = (double)totalAllocated / measureIterations;
            Console.WriteLine("Optimization Impact:");
            Console.WriteLine($"  Measured Allocation:   {bytesPerOp:F1} bytes/op");
            
            // The main allocation should be the output tensor
            int expectedOutputSize = B * T * nEmbd * sizeof(float);
            Console.WriteLine($"  Expected (output):     ~{expectedOutputSize:N0} bytes/op");
            
            if (bytesPerOp < expectedOutputSize * 1.5)
            {
                Console.WriteLine("  Status:                ✓ WORKSPACE REUSE EFFECTIVE");
                Console.WriteLine("                         (minimal overhead beyond output tensor)");
            }
            else
            {
                Console.WriteLine("  Status:                ⚠ UNEXPECTED ALLOCATIONS");
            }
        }
        
        static void BenchmarkEndToEndForward()
        {
            Console.WriteLine();
            Console.WriteLine("Purpose:  Measure end-to-end transformer inference with all optimizations.");
            Console.WriteLine();
            
            const int warmupIterations = 5;
            const int measureIterations = 50;
            
            // Create small model
            var model = new TransformerModel(
                vocabSize: 256,
                blockSize: 64,
                nEmbd: 128,
                nLayer: 2,
                nHead: 4,
                dropout: 0.1,
                seed: 42);
            model.Eval();
            
            int B = 2, T = 32;
            var input = new Tensor(new int[] { B, T }, requiresGrad: false);
            var rng = new Random(123);
            for (int i = 0; i < input.Size; i++)
                input.Data[i] = rng.Next(256);
            
            // Warmup
            Console.WriteLine("Warming up model...");
            for (int i = 0; i < warmupIterations; i++)
            {
                var _ = model.Forward(input);
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
            long startWorkingSet = Process.GetCurrentProcess().WorkingSet64;
            
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < measureIterations; i++)
            {
                var output = model.Forward(input);
            }
            sw.Stop();
            
            long endBytes = GC.GetTotalAllocatedBytes(precise: true);
            long totalAllocated = endBytes - startBytes;
            int gen0 = GC.CollectionCount(0) - startGen0;
            int gen1 = GC.CollectionCount(1) - startGen1;
            int gen2 = GC.CollectionCount(2) - startGen2;
            long endWorkingSet = Process.GetCurrentProcess().WorkingSet64;
            long workingSetDelta = endWorkingSet - startWorkingSet;
            
            // Results
            Console.WriteLine($"Model Config:     vocab=256, blockSize=64, embed=128, layers=2, heads=4");
            Console.WriteLine($"Input:            Batch={B}, SeqLen={T}");
            Console.WriteLine($"Iterations:       {measureIterations:N0} (after warmup)");
            Console.WriteLine();
            
            Console.WriteLine("Performance Metrics:");
            Console.WriteLine($"  Total Time:            {sw.ElapsedMilliseconds:N0} ms");
            Console.WriteLine($"  Avg Time/Forward:      {sw.Elapsed.TotalMilliseconds / measureIterations:F3} ms");
            Console.WriteLine($"  Tokens/Second:         {(B * T * measureIterations) / sw.Elapsed.TotalSeconds:N0}");
            Console.WriteLine($"  Throughput:            {measureIterations / sw.Elapsed.TotalSeconds:N1} forwards/sec");
            Console.WriteLine();
            
            Console.WriteLine("Memory Metrics:");
            Console.WriteLine($"  Total Allocated:       {totalAllocated:N0} bytes ({totalAllocated / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"  Bytes/Forward:         {(double)totalAllocated / measureIterations / 1024.0:F1} KB");
            Console.WriteLine($"  Gen0 Collections:      {gen0}");
            Console.WriteLine($"  Gen1 Collections:      {gen1}");
            Console.WriteLine($"  Gen2 Collections:      {gen2}");
            Console.WriteLine($"  Working Set Delta:     {workingSetDelta / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine();
            
            // Analysis
            double kbPerForward = (double)totalAllocated / measureIterations / 1024.0;
            Console.WriteLine("Summary:");
            Console.WriteLine($"  Allocation Rate:       {kbPerForward:F1} KB/forward");
            Console.WriteLine($"  GC Pressure:           {(gen0 > 0 || gen1 > 0 || gen2 > 0 ? "⚠ Collections occurred" : "✓ No collections")}");
            
            if (gen0 == 0 && gen1 == 0 && gen2 == 0)
            {
                Console.WriteLine("  Status:                ✓ EXCELLENT (no GC during benchmark)");
            }
            else if (gen0 < 3 && gen1 == 0 && gen2 == 0)
            {
                Console.WriteLine("  Status:                ✓ GOOD (minimal Gen0 collections)");
            }
            else
            {
                Console.WriteLine("  Status:                ⚠ HIGH GC ACTIVITY");
            }
        }
    }
}

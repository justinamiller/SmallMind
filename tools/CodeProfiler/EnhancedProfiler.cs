using CodeProfiler;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;
using SmallMind.Transformers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CodeProfiler
{
    /// <summary>
    /// Enhanced profiler for detailed hot path analysis.
    /// Provides granular instrumentation of transformer components.
    /// </summary>
    public static class EnhancedProfiler
    {
        public static void RunComprehensiveProfiling()
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║       SmallMind Comprehensive Performance Profiling           ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            using var profiler = new PerformanceProfiler();

            // Phase 1: Low-level SIMD operations
            ProfileLowLevelOperations(profiler);

            // Phase 2: Mid-level tensor operations
            ProfileTensorOperations(profiler);

            // Phase 3: High-level transformer inference
            ProfileTransformerInference(profiler);

            // Generate comprehensive report
            GenerateDetailedReport(profiler);
        }

        private static void ProfileLowLevelOperations(PerformanceProfiler profiler)
        {
            Console.WriteLine("┌─ Phase 1: Low-Level SIMD Operations ──────────────────────────┐");
            Console.WriteLine("│ Testing fundamental kernels...                                │");
            Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
            Console.WriteLine();

            // Matrix multiplication at various sizes
            var matMulSizes = new[] { 64, 128, 256, 512 };
            foreach (var size in matMulSizes)
            {
                Console.Write($"  MatMul {size}×{size}... ");
                ProfileMatMul(profiler, size);
                Console.WriteLine("✓");
            }

            // Activation functions
            var activationSizes = new[] { 1_000, 10_000, 100_000, 1_000_000 };
            foreach (var size in activationSizes)
            {
                Console.Write($"  GELU {size} elements... ");
                ProfileGELU(profiler, size);
                Console.WriteLine("✓");
            }

            // Softmax
            var softmaxSizes = new[] { 256, 512, 1024, 2048 };
            foreach (var size in softmaxSizes)
            {
                Console.Write($"  Softmax {size}... ");
                ProfileSoftmax(profiler, size);
                Console.WriteLine("✓");
            }

            Console.WriteLine();
        }

        private static void ProfileTensorOperations(PerformanceProfiler profiler)
        {
            Console.WriteLine("┌─ Phase 2: Tensor Operations ──────────────────────────────────┐");
            Console.WriteLine("│ Testing mid-level tensor operations...                        │");
            Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
            Console.WriteLine();

            // Element-wise operations
            Console.Write("  Tensor Addition... ");
            ProfileTensorAddition(profiler, 10_000);
            Console.WriteLine("✓");

            Console.Write("  Tensor Multiplication... ");
            ProfileTensorMultiplication(profiler, 10_000);
            Console.WriteLine("✓");

            // Broadcasting operations
            Console.Write("  Broadcasting Add... ");
            ProfileBroadcastAdd(profiler, 100, 100);
            Console.WriteLine("✓");

            Console.WriteLine();
        }

        private static void ProfileTransformerInference(PerformanceProfiler profiler)
        {
            Console.WriteLine("┌─ Phase 3: Transformer Inference ──────────────────────────────┐");
            Console.WriteLine("│ Testing full transformer pipeline...                          │");
            Console.WriteLine("└────────────────────────────────────────────────────────────────┘");
            Console.WriteLine();

            // Small model (fast iteration)
            Console.WriteLine("  Small Model (128 dim, 2 layers):");
            ProfileModel(profiler, "Small", vocabSize: 256, blockSize: 64, 
                        nEmbd: 128, nLayer: 2, nHead: 4, tokens: 25);

            // Medium model (representative)
            Console.WriteLine("  Medium Model (256 dim, 4 layers):");
            ProfileModel(profiler, "Medium", vocabSize: 512, blockSize: 128, 
                        nEmbd: 256, nLayer: 4, nHead: 8, tokens: 25);

            Console.WriteLine();
        }

        private static void ProfileMatMul(PerformanceProfiler profiler, int size)
        {
            var a = CreateRandomArray(size * size);
            var b = CreateRandomArray(size * size);
            var c = new float[size * size];

            using (profiler.BeginScope($"MatMul_{size}x{size}"))
            {
                // Warmup
                MatMulOps.MatMul(a, b, c, size, size, size);

                // Actual profiling (3 iterations for stability)
                for (int i = 0; i < 3; i++)
                {
                    using (profiler.BeginScope($"MatMul_Iteration"))
                    {
                        MatMulOps.MatMul(a, b, c, size, size, size);
                    }
                }
            }
        }

        private static void ProfileGELU(PerformanceProfiler profiler, int size)
        {
            var input = CreateRandomArray(size);
            var output = new float[size];

            using (profiler.BeginScope($"GELU_{size}"))
            {
                // Warmup
                ActivationOps.GELU(input, output);

                // Actual profiling (5 iterations)
                for (int i = 0; i < 5; i++)
                {
                    using (profiler.BeginScope($"GELU_Iteration"))
                    {
                        ActivationOps.GELU(input, output);
                    }
                }
            }
        }

        private static void ProfileSoftmax(PerformanceProfiler profiler, int size)
        {
            var input = CreateRandomArray(size);
            var output = new float[size];

            using (profiler.BeginScope($"Softmax_{size}"))
            {
                // Warmup
                SoftmaxOps.Softmax1D(input, output);

                // Actual profiling (5 iterations)
                for (int i = 0; i < 5; i++)
                {
                    using (profiler.BeginScope($"Softmax_Iteration"))
                    {
                        SoftmaxOps.Softmax1D(input, output);
                    }
                }
            }
        }

        private static void ProfileTensorAddition(PerformanceProfiler profiler, int size)
        {
            var a = new Tensor(new[] { size });
            var b = new Tensor(new[] { size });
            var random = new Random(42);
            for (int i = 0; i < size; i++)
            {
                a.Data[i] = (float)random.NextDouble();
                b.Data[i] = (float)random.NextDouble();
            }

            using (profiler.BeginScope($"TensorAdd_{size}"))
            {
                for (int i = 0; i < 10; i++)
                {
                    using (profiler.BeginScope("TensorAdd_Iteration"))
                    {
                        var result = Tensor.Add(a, b, null);
                    }
                }
            }
        }

        private static void ProfileTensorMultiplication(PerformanceProfiler profiler, int size)
        {
            var a = new Tensor(new[] { size });
            var b = new Tensor(new[] { size });
            var random = new Random(42);
            for (int i = 0; i < size; i++)
            {
                a.Data[i] = (float)random.NextDouble();
                b.Data[i] = (float)random.NextDouble();
            }

            using (profiler.BeginScope($"TensorMul_{size}"))
            {
                for (int i = 0; i < 10; i++)
                {
                    using (profiler.BeginScope("TensorMul_Iteration"))
                    {
                        // Manual element-wise multiplication for profiling
                        var result = new Tensor(new[] { size });
                        for (int j = 0; j < size; j++)
                        {
                            result.Data[j] = a.Data[j] * b.Data[j];
                        }
                    }
                }
            }
        }

        private static void ProfileBroadcastAdd(PerformanceProfiler profiler, int rows, int cols)
        {
            var a = new Tensor(new[] { rows, cols });
            var b = new Tensor(new[] { cols });
            var random = new Random(42);
            
            for (int i = 0; i < a.Data.Length; i++)
                a.Data[i] = (float)random.NextDouble();
            for (int i = 0; i < b.Data.Length; i++)
                b.Data[i] = (float)random.NextDouble();

            using (profiler.BeginScope($"BroadcastAdd_{rows}x{cols}"))
            {
                for (int i = 0; i < 10; i++)
                {
                    using (profiler.BeginScope("BroadcastAdd_Iteration"))
                    {
                        var result = Tensor.Add(a, b, null);
                    }
                }
            }
        }

        private static void ProfileModel(PerformanceProfiler profiler, string modelName,
            int vocabSize, int blockSize, int nEmbd, int nLayer, int nHead, int tokens)
        {
            TransformerModel model;
            
            using (profiler.BeginScope($"Model_{modelName}_Creation"))
            {
                Console.Write($"    Creating {modelName} model... ");
                model = TransformerModelBuilder.Create()
                    .WithVocabSize(vocabSize)
                    .WithBlockSize(blockSize)
                    .WithEmbedDim(nEmbd)
                    .WithNumLayers(nLayer)
                    .WithNumHeads(nHead)
                    .WithDropout(0.0f)
                    .WithSeed(42)
                    .Build();
                Console.WriteLine($"✓ ({model.Parameters.Count} tensors)");
            }

            // Run inference
            Console.Write($"    Generating {tokens} tokens... ");
            var random = new Random(42);
            var inputTokens = new List<int>();
            
            // Initialize with random tokens
            for (int i = 0; i < 10; i++)
                inputTokens.Add(random.Next(0, vocabSize));

            using (profiler.BeginScope($"Model_{modelName}_Inference"))
            {
                for (int i = 0; i < tokens; i++)
                {
                    using (profiler.BeginScope($"Model_{modelName}_GenerateToken"))
                    {
                        var contextWindow = Math.Min(inputTokens.Count, blockSize);
                        var context = inputTokens.Skip(Math.Max(0, inputTokens.Count - contextWindow)).ToArray();
                        
                        var inputTensor = new Tensor(new[] { 1, context.Length });
                        for (int j = 0; j < context.Length; j++)
                            inputTensor.Data[j] = context[j];

                        using (profiler.BeginScope($"Model_{modelName}_Forward"))
                        {
                            var logits = model.Forward(inputTensor);
                        }

                        // Sample next token (simplified)
                        inputTokens.Add(random.Next(0, vocabSize));
                    }
                }
            }
            Console.WriteLine($"✓ ({inputTokens.Count} total tokens)");
        }

        private static void GenerateDetailedReport(PerformanceProfiler profiler)
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    PROFILING RESULTS                           ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Top 30 hot paths by time
            Console.WriteLine("═══ Top 30 Hot Paths (by Time) ════════════════════════════════");
            Console.WriteLine();
            
            var hotPaths = profiler.GetHotPaths(30);
            Console.WriteLine($"{"Rank",-5} {"Method",-40} {"Time (ms)",-15} {"Calls",-10} {"Avg (ms)",-12} {"Alloc (MB)",-12}");
            Console.WriteLine(new string('─', 100));
            
            for (int i = 0; i < hotPaths.Count; i++)
            {
                var profile = hotPaths[i];
                var totalMs = (profile.TotalTicks * 1000.0) / Stopwatch.Frequency;
                var avgMs = profile.CallCount > 0 ? totalMs / profile.CallCount : 0;
                var allocMB = profile.TotalAllocatedBytes / (1024.0 * 1024.0);
                
                var methodName = profile.Name.Length > 40 ? 
                    profile.Name.Substring(0, 37) + "..." : profile.Name;
                
                Console.WriteLine($"{i + 1,-5} {methodName,-40} {totalMs,-15:F2} {profile.CallCount,-10:N0} {avgMs,-12:F3} {allocMB,-12:F2}");
            }

            Console.WriteLine();
            Console.WriteLine("═══ Memory Allocation Analysis ════════════════════════════════");
            Console.WriteLine();
            
            var topAllocators = profiler.GetTopAllocators(15);
            Console.WriteLine($"{"Rank",-5} {"Method",-45} {"Total (MB)",-15} {"Calls",-10} {"Avg (KB)",-12}");
            Console.WriteLine(new string('─', 95));
            
            for (int i = 0; i < topAllocators.Count; i++)
            {
                var profile = topAllocators[i];
                var allocMB = profile.TotalAllocatedBytes / (1024.0 * 1024.0);
                var avgKB = profile.CallCount > 0 ? 
                    (profile.TotalAllocatedBytes / (1024.0 * profile.CallCount)) : 0;
                
                var methodName = profile.Name.Length > 45 ? 
                    profile.Name.Substring(0, 42) + "..." : profile.Name;
                
                Console.WriteLine($"{i + 1,-5} {methodName,-45} {allocMB,-15:F2} {profile.CallCount,-10:N0} {avgKB,-12:F2}");
            }

            Console.WriteLine();
            Console.WriteLine("═══ Summary Statistics ═════════════════════════════════════════");
            Console.WriteLine();
            
            var totalTime = hotPaths.Sum(p => (p.TotalTicks * 1000.0) / Stopwatch.Frequency);
            var totalAlloc = hotPaths.Sum(p => p.TotalAllocatedBytes / (1024.0 * 1024.0));
            var totalCalls = hotPaths.Sum(p => p.CallCount);
            
            Console.WriteLine($"  Total Profiled Time:        {totalTime:F2} ms");
            Console.WriteLine($"  Total Memory Allocated:     {totalAlloc:F2} MB");
            Console.WriteLine($"  Total Method Calls:         {totalCalls:N0}");
            Console.WriteLine($"  Unique Methods Profiled:    {hotPaths.Count}");
            
            Console.WriteLine();
            
            // Save detailed report to file
            var reportPath = "enhanced-profile-report.md";
            SaveMarkdownReport(profiler, reportPath, hotPaths, topAllocators);
            Console.WriteLine($"✓ Detailed report saved to: {reportPath}");
            Console.WriteLine();
        }

        private static void SaveMarkdownReport(PerformanceProfiler profiler, string path,
            List<MethodProfile> hotPaths, List<MethodProfile> topAllocators)
        {
            using var writer = new System.IO.StreamWriter(path);
            
            writer.WriteLine("# Enhanced Performance Profile Report");
            writer.WriteLine();
            writer.WriteLine($"**Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            writer.WriteLine();
            
            var totalTime = hotPaths.Sum(p => (p.TotalTicks * 1000.0) / Stopwatch.Frequency);
            var totalAlloc = hotPaths.Sum(p => p.TotalAllocatedBytes / (1024.0 * 1024.0));
            
            writer.WriteLine("## Summary");
            writer.WriteLine();
            writer.WriteLine($"- **Total Runtime:** {totalTime:F2} ms");
            writer.WriteLine($"- **Total Allocations:** {totalAlloc:F2} MB");
            writer.WriteLine($"- **Methods Profiled:** {hotPaths.Count}");
            writer.WriteLine();
            
            writer.WriteLine("## 🔥 Hot Paths (Top 30)");
            writer.WriteLine();
            writer.WriteLine("| Rank | Method | Time (ms) | Calls | Avg (ms) | Alloc (MB) |");
            writer.WriteLine("|------|--------|-----------|-------|----------|------------|");
            
            for (int i = 0; i < Math.Min(30, hotPaths.Count); i++)
            {
                var profile = hotPaths[i];
                var totalMs = (profile.TotalTicks * 1000.0) / Stopwatch.Frequency;
                var avgMs = profile.CallCount > 0 ? totalMs / profile.CallCount : 0;
                var allocMB = profile.TotalAllocatedBytes / (1024.0 * 1024.0);
                
                writer.WriteLine($"| {i + 1} | `{profile.Name}` | {totalMs:F2} | {profile.CallCount:N0} | {avgMs:F3} | {allocMB:F2} |");
            }
            
            writer.WriteLine();
            writer.WriteLine("## 💾 Top Allocators");
            writer.WriteLine();
            writer.WriteLine("| Rank | Method | Total (MB) | Calls | Avg (KB) |");
            writer.WriteLine("|------|--------|------------|-------|----------|");
            
            for (int i = 0; i < Math.Min(15, topAllocators.Count); i++)
            {
                var profile = topAllocators[i];
                var allocMB = profile.TotalAllocatedBytes / (1024.0 * 1024.0);
                var avgKB = profile.CallCount > 0 ? 
                    (profile.TotalAllocatedBytes / (1024.0 * profile.CallCount)) : 0;
                
                writer.WriteLine($"| {i + 1} | `{profile.Name}` | {allocMB:F2} | {profile.CallCount:N0} | {avgKB:F2} |");
            }
            
            writer.WriteLine();
            writer.WriteLine("## Analysis");
            writer.WriteLine();
            writer.WriteLine("See COMPREHENSIVE_HOT_PATHS_ANALYSIS.md for detailed recommendations.");
        }

        private static float[] CreateRandomArray(int size)
        {
            var array = new float[size];
            var random = new Random(42);
            for (int i = 0; i < size; i++)
                array[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return array;
        }
    }
}

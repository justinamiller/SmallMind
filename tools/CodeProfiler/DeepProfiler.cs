using CodeProfiler;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;
using SmallMind.Transformers;
using System.Diagnostics;

public static class DeepProfiler
{
    public static void ProfileSimdOperations(PerformanceProfiler profiler)
    {
        Console.WriteLine("1. Profiling SIMD Operations");
        Console.WriteLine("   ---------------------------");
        
        // Test various matrix sizes
        var sizes = new[] { 128, 256, 512 };
        
        foreach (var size in sizes)
        {
            Console.WriteLine($"   Testing {size}x{size} matrices...");
            
            var a = new float[size * size];
            var b = new float[size * size];
            var c = new float[size * size];
            
            // Initialize with random data
            var random = new Random(42);
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = (float)(random.NextDouble() - 0.5);
                b[i] = (float)(random.NextDouble() - 0.5);
            }
            
            // Profile matrix multiplication
            using (profiler.BeginScope($"MatMul_{size}x{size}"))
            {
                for (int iter = 0; iter < 3; iter++)
                {
                    using (profiler.BeginScope($"MatMul_Iteration"))
                    {
                        MatMulOps.MatMul(a, b, c, size, size, size);
                    }
                }
            }
        }
        
        Console.WriteLine("   ✓ SIMD operations profiled");
        Console.WriteLine();
    }

    public static void ProfileDeepTransformer(PerformanceProfiler profiler, int numInferences, int maxTokens)
    {
        Console.WriteLine("2. Profiling Transformer Operations");
        Console.WriteLine("   ---------------------------------");
        
        // Create model
        TransformerModel model;
        using (profiler.BeginScope("Transformer_ModelCreation"))
        {
            Console.Write("   Creating model... ");
            model = TransformerModelBuilder.Create()
                .WithVocabSize(512)
                .WithBlockSize(128)
                .WithEmbedDim(256)
                .WithNumLayers(4)
                .WithNumHeads(8)
                .WithDropout(0.0f)
                .WithSeed(42)
                .Build();
            Console.WriteLine($"✓ ({model.Parameters.Count} tensors)");
        }

        // Profile inferences
        for (int i = 0; i < numInferences; i++)
        {
            Console.Write($"   Inference {i + 1}/{numInferences}... ");
            RunDeepInference(profiler, model, 32, maxTokens);
            Console.WriteLine("✓");
        }
        
        Console.WriteLine("   ✓ Transformer profiling complete");
        Console.WriteLine();
    }

    private static void RunDeepInference(PerformanceProfiler profiler, TransformerModel model, int inputLength, int maxTokens)
    {
        using (profiler.BeginScope("Inference"))
        {
            var random = new Random(42);
            var tokens = new List<int>();
            
            for (int i = 0; i < inputLength; i++)
            {
                tokens.Add(random.Next(0, model.VocabSize));
            }

            for (int i = 0; i < maxTokens; i++)
            {
                using (profiler.BeginScope("GenerateToken"))
                {
                    var contextWindow = Math.Min(tokens.Count, model.BlockSize);
                    var inputTokens = tokens.Skip(Math.Max(0, tokens.Count - contextWindow)).ToArray();
                    
                    var inputTensor = new Tensor(new[] { 1, inputTokens.Length });
                    for (int j = 0; j < inputTokens.Length; j++)
                    {
                        inputTensor.Data[j] = inputTokens[j];
                    }

                    using (profiler.BeginScope("Transformer_Forward"))
                    {
                        var logits = model.Forward(inputTensor);
                    }

                    tokens.Add(random.Next(0, model.VocabSize));
                }
            }
        }
    }

    public static void PrintDetailedSummary(PerformanceProfiler profiler)
    {
        Console.WriteLine("=== PERFORMANCE SUMMARY ===");
        Console.WriteLine();
        
        Console.WriteLine("Top 20 Hot Paths:");
        Console.WriteLine("---------------------------");
        var hotPaths = profiler.GetHotPaths(20);
        for (int i = 0; i < hotPaths.Count; i++)
        {
            var profile = hotPaths[i];
            var totalMs = (profile.TotalTicks * 1000.0) / Stopwatch.Frequency;
            var avgMs = profile.CallCount > 0 ? totalMs / profile.CallCount : 0;
            var allocMB = profile.TotalAllocatedBytes / (1024.0 * 1024.0);
            
            Console.WriteLine($"{i + 1,2}. {profile.Name}");
            Console.WriteLine($"    ⏱  {totalMs:F2} ms total | {avgMs:F3} ms avg | {profile.CallCount:N0} calls");
            if (allocMB > 0.1)
            {
                Console.WriteLine($"    💾 {allocMB:F2} MB allocated");
            }
            Console.WriteLine();
        }
    }
}

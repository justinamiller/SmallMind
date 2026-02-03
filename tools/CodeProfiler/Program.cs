using CodeProfiler;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;
using SmallMind.Transformers;
using System.Diagnostics;

Console.WriteLine("SmallMind Code Profiler");
Console.WriteLine("========================");
Console.WriteLine();

// Parse arguments
string mode = "standard";
if (args.Length > 0 && (args[0] == "--enhanced" || args[0] == "-e"))
{
    mode = "enhanced";
}
else if (args.Length > 0 && (args[0] == "--deep" || args[0] == "-d"))
{
    mode = "deep";
}
else if (args.Length > 0 && (args[0] == "--compare" || args[0] == "-c"))
{
    mode = "compare";
}
else if (args.Length > 0 && (args[0] == "--model-compare" || args[0] == "-m"))
{
    mode = "model-compare";
}

string outputPath = mode == "enhanced" ? "enhanced-profile-report.md" : 
                   (args.Length > 0 && !args[0].StartsWith("--") ? args[0] : "profile-report.md");
int numInferences = args.Length > 1 ? int.Parse(args[1]) : 3;
int maxTokens = args.Length > 2 ? int.Parse(args[2]) : 50;
bool deepProfile = mode == "deep";

Console.WriteLine($"Mode: {mode}");
if (mode != "enhanced")
{
    Console.WriteLine($"Output: {outputPath}");
    Console.WriteLine($"Inferences: {numInferences}");
    Console.WriteLine($"Max Tokens: {maxTokens}");
}
Console.WriteLine();

try
{
    if (mode == "compare")
    {
        // Compare two profile reports
        string previousReport = args.Length > 1 ? args[1] : "previous-profile-report.md";
        string currentReport = args.Length > 2 ? args[2] : "enhanced-profile-report.md";
        string outputReport = args.Length > 3 ? args[3] : "profile-comparison-report.md";

        Console.WriteLine($"Comparing:");
        Console.WriteLine($"  Previous: {previousReport}");
        Console.WriteLine($"  Current:  {currentReport}");
        Console.WriteLine($"  Output:   {outputReport}");
        Console.WriteLine();

        var previousData = ProfileComparator.ParseProfileReport(previousReport);
        var currentData = ProfileComparator.ParseProfileReport(currentReport);

        if (!previousData.Any())
        {
            Console.WriteLine($"Error: Could not parse previous report: {previousReport}");
            return 1;
        }

        if (!currentData.Any())
        {
            Console.WriteLine($"Error: Could not parse current report: {currentReport}");
            return 1;
        }

        var comparisonReport = ProfileComparator.GenerateComparisonReport(
            previousData, 
            currentData,
            "2026-02-03 02:36:36",
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));

        await File.WriteAllTextAsync(outputReport, comparisonReport);
        Console.WriteLine($"\n✓ Comparison report generated: {outputReport}");
    }
    else if (mode == "model-compare")
    {
        // Compare model performance only
        string currentReport = args.Length > 1 ? args[1] : "enhanced-profile-report.md";
        string outputReport = args.Length > 2 ? args[2] : "model-comparison-report.md";

        Console.WriteLine($"Analyzing model performance from: {currentReport}");
        Console.WriteLine($"Output: {outputReport}");
        Console.WriteLine();

        var currentData = ProfileComparator.ParseProfileReport(currentReport);
        
        if (!currentData.Any())
        {
            Console.WriteLine($"Error: Could not parse report: {currentReport}");
            return 1;
        }

        ProfileComparator.GenerateModelOnlyComparison(currentData, outputReport);
    }
    else if (mode == "enhanced")
    {
        // Enhanced comprehensive profiling
        EnhancedProfiler.RunComprehensiveProfiling();
    }
    else
    {
        // Create profiler for standard/deep modes
        using var profiler = new PerformanceProfiler();
        
        if (deepProfile)
        {
            // Deep profiling with SIMD operations
            DeepProfiler.ProfileSimdOperations(profiler);
            DeepProfiler.ProfileDeepTransformer(profiler, numInferences, maxTokens);
        }
        else
        {
            // Standard profiling
            ProfileInference(profiler, numInferences, maxTokens);
        }

        // Generate and save report
        Console.WriteLine("\nGenerating performance report...");
        string report = profiler.GenerateReport();
        
        await File.WriteAllTextAsync(outputPath, report);
        Console.WriteLine($"\n✓ Report saved to: {Path.GetFullPath(outputPath)}");
        Console.WriteLine();
        
        // Print summary to console
        if (deepProfile)
        {
            DeepProfiler.PrintDetailedSummary(profiler);
        }
        else
        {
            PrintSummary(profiler);
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"\n✗ Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}

return 0;

static void ProfileInference(PerformanceProfiler profiler, int numInferences, int maxTokens)
{
    Console.WriteLine("Profiling transformer operations...");
    
    // Create a small transformer model
    TransformerModel model;
    using (profiler.BeginScope("ModelCreation"))
    {
        Console.Write("  Creating model... ");
        model = CreateSmallTransformer(profiler);
        Console.WriteLine("✓");
    }

    try
    {
        // Run inference multiple times
        for (int i = 0; i < numInferences; i++)
        {
            Console.WriteLine($"\n  Inference {i + 1}/{numInferences}");
            RunInference(profiler, model, maxTokens);
        }
    }
    finally
    {
        // Cleanup if needed
    }
}

static TransformerModel CreateSmallTransformer(PerformanceProfiler profiler)
{
    using (profiler.BeginScope("BuilderSetup"))
    {
        // Create a tiny model for profiling
        var model = TransformerModelBuilder.Create()
            .WithVocabSize(256)
            .WithBlockSize(64)
            .WithEmbedDim(128)
            .WithNumLayers(2)
            .WithNumHeads(4)
            .WithDropout(0.0f)
            .WithSeed(42)
            .Build();

        return model;
    }
}

static void RunInference(PerformanceProfiler profiler, TransformerModel model, int maxTokens)
{
    using (profiler.BeginScope("InferenceComplete"))
    {
        // Create input tokens (simulate a prompt)
        var random = new Random(42);
        var inputLength = 10;
        var tokens = new List<int>();
        
        using (profiler.BeginScope("PrepareInput"))
        {
            for (int i = 0; i < inputLength; i++)
            {
                tokens.Add(random.Next(0, 256));
            }
        }

        Console.Write($"    Generating {maxTokens} tokens... ");
        
        // Generate tokens using the model
        for (int i = 0; i < maxTokens; i++)
        {
            using (profiler.BeginScope("GenerateToken"))
            {
                // Forward pass
                Tensor logits;
                using (profiler.BeginScope("ForwardPass"))
                {
                    // Ensure we don't exceed block size
                    var contextWindow = Math.Min(tokens.Count, model.BlockSize);
                    var inputTokens = tokens.Skip(Math.Max(0, tokens.Count - contextWindow)).ToArray();
                    
                    var inputTensor = new Tensor(new[] { 1, inputTokens.Length });
                    for (int j = 0; j < inputTokens.Length; j++)
                    {
                        inputTensor.Data[j] = inputTokens[j];
                    }

                    // Perform forward pass
                    logits = model.Forward(inputTensor);
                }

                // Sample next token
                int nextToken;
                using (profiler.BeginScope("SampleToken"))
                {
                    // Get logits for last position
                    var vocabSize = model.VocabSize;
                    var lastPos = logits.Shape[1] - 1;
                    var logitsForLastPos = new float[vocabSize];
                    
                    for (int v = 0; v < vocabSize; v++)
                    {
                        logitsForLastPos[v] = logits.Data[lastPos * vocabSize + v];
                    }

                    nextToken = SampleToken(profiler, logitsForLastPos, temperature: 0.8f, random);
                }

                tokens.Add(nextToken);
            }
        }

        Console.WriteLine("✓");
        Console.WriteLine($"    Generated {tokens.Count} total tokens");
    }
}

static int SampleToken(PerformanceProfiler profiler, float[] logits, float temperature, Random random)
{
    using (profiler.BeginScope("ApplyTemperature"))
    {
        for (int i = 0; i < logits.Length; i++)
        {
            logits[i] /= temperature;
        }
    }

    using (profiler.BeginScope("Softmax"))
    {
        // Softmax
        float max = logits.Max();
        float sum = 0;
        
        for (int i = 0; i < logits.Length; i++)
        {
            logits[i] = MathF.Exp(logits[i] - max);
            sum += logits[i];
        }

        for (int i = 0; i < logits.Length; i++)
        {
            logits[i] /= sum;
        }
    }

    using (profiler.BeginScope("MultinomialSample"))
    {
        float r = (float)random.NextDouble();
        float cumulative = 0;
        
        for (int i = 0; i < logits.Length; i++)
        {
            cumulative += logits[i];
            if (r < cumulative)
            {
                return i;
            }
        }

        return logits.Length - 1;
    }
}

static void PrintSummary(PerformanceProfiler profiler)
{
    Console.WriteLine("=== Top 10 Hot Paths ===");
    Console.WriteLine();

    var hotPaths = profiler.GetHotPaths(10);
    for (int i = 0; i < hotPaths.Count; i++)
    {
        var profile = hotPaths[i];
        var totalMs = (profile.TotalTicks * 1000.0) / Stopwatch.Frequency;
        var avgMs = profile.CallCount > 0 ? totalMs / profile.CallCount : 0;
        
        Console.WriteLine($"{i + 1,2}. {profile.Name}");
        Console.WriteLine($"    Time: {totalMs:F2} ms total, {avgMs:F3} ms avg ({profile.CallCount:N0} calls)");
        
        var allocMB = profile.TotalAllocatedBytes / (1024.0 * 1024.0);
        if (allocMB > 0.01)
        {
            Console.WriteLine($"    Alloc: {allocMB:F2} MB");
        }
        Console.WriteLine();
    }
}

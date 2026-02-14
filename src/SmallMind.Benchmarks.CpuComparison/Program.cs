using System.Diagnostics;
using System.Numerics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.Json;
using SmallMind.Core.Core;
using SmallMind.Tests.TestHelpers;
using SmallMind.Tokenizers;

namespace SmallMind.Benchmarks.CpuComparison;

/// <summary>
/// CPU benchmark suite for SmallMind inference performance.
/// Measures prompt processing and generation throughput with SIMD detection.
/// No third-party dependencies - uses built-in Stopwatch and GC APIs.
/// </summary>
internal class Program
{
    private const int WARMUP_RUNS = 2;
    private const int BENCHMARK_RUNS = 5;
    private const int PROMPT_TOKENS = 32; // Reduced to fit synthetic model context (64)
    private const int GENERATION_TOKENS = 20; // Reduced for synthetic model

    static void Main(string[] args)
    {
        Console.WriteLine("=== SmallMind CPU Benchmark Suite ===");
        Console.WriteLine();

        // Detect system capabilities
        var sysInfo = CollectSystemInfo();
        PrintSystemInfo(sysInfo);

        // Create benchmark directory
        var resultsDir = Path.Combine(Directory.GetCurrentDirectory(), "benchmarks", "results");
        Directory.CreateDirectory(resultsDir);

        // Run benchmarks
        Console.WriteLine("\n=== Running Benchmarks ===\n");

        var results = new BenchmarkResults
        {
            Timestamp = DateTime.UtcNow,
            SystemInfo = sysInfo,
            PromptProcessing = BenchmarkPromptProcessing(),
            Generation = BenchmarkGeneration()
        };

        // Output results
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var jsonPath = Path.Combine(resultsDir, $"{timestamp}_cpu_comparison.json");
        var mdPath = Path.Combine(resultsDir, $"{timestamp}_cpu_comparison.md");

        OutputJson(results, jsonPath);
        OutputMarkdown(results, mdPath);

        Console.WriteLine($"\n✓ Results saved to:");
        Console.WriteLine($"  JSON: {jsonPath}");
        Console.WriteLine($"  MD:   {mdPath}");
    }

    static SystemInfo CollectSystemInfo()
    {
        return new SystemInfo
        {
            CpuArchitecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
            LogicalCores = Environment.ProcessorCount,
            DotNetVersion = Environment.Version.ToString(),
            SimdVectorSize = Vector<float>.Count,
            SimdCapabilities = new()
            {
                ["Vector_IsHardwareAccelerated"] = Vector.IsHardwareAccelerated,
                ["SSE"] = Sse.IsSupported,
                ["SSE2"] = Sse2.IsSupported,
                ["AVX"] = Avx.IsSupported,
                ["AVX2"] = Avx2.IsSupported,
                ["AVX512F"] = Avx512F.IsSupported,
                ["FMA"] = Fma.IsSupported,
                ["AdvSimd"] = AdvSimd.IsSupported
            },
            BestInstructionSet = GetBestInstructionSet()
        };
    }

    static string GetBestInstructionSet()
    {
        if (Avx512F.IsSupported) return "AVX-512F";
        if (Avx2.IsSupported && Fma.IsSupported) return "AVX2+FMA";
        if (Avx2.IsSupported) return "AVX2";
        if (Avx.IsSupported) return "AVX";
        if (AdvSimd.IsSupported) return "AdvSimd (ARM NEON)";
        if (Vector.IsHardwareAccelerated) return $"Vector<T> ({Vector<float>.Count}x)";
        return "Scalar";
    }

    static void PrintSystemInfo(SystemInfo info)
    {
        Console.WriteLine("=== System Information ===");
        Console.WriteLine($"CPU Architecture:    {info.CpuArchitecture}");
        Console.WriteLine($"Logical Cores:       {info.LogicalCores}");
        Console.WriteLine($".NET Version:        {info.DotNetVersion}");
        Console.WriteLine($"SIMD Vector Size:    {info.SimdVectorSize} floats");
        Console.WriteLine($"Best Instruction Set: {info.BestInstructionSet}");
        Console.WriteLine($"Working Set:         {Process.GetCurrentProcess().WorkingSet64 / 1024 / 1024} MB");
        Console.WriteLine($"GC Memory:           {GC.GetTotalMemory(false) / 1024 / 1024} MB");

        Console.WriteLine("\nSIMD Capabilities:");
        foreach (var cap in info.SimdCapabilities.Where(kv => kv.Value))
        {
            Console.WriteLine($"  ✓ {cap.Key}");
        }
    }

    static BenchmarkResult BenchmarkPromptProcessing()
    {
        Console.WriteLine($"Benchmarking Prompt Processing ({PROMPT_TOKENS} tokens)...");

        // Create synthetic model for consistent benchmarking (use dynamic to access internal type)
        dynamic model = SyntheticModelFactory.CreateTinyModel(seed: 42);
        var tokenizer = TokenizerFactory.CreateCharLevel(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?\n"
        );

        // Generate synthetic prompt
        var promptText = "The quick brown fox jumps over the lazy dog. ";
        var promptTokens = tokenizer.Encode(promptText);

        // Ensure we have exactly PROMPT_TOKENS tokens
        while (promptTokens.Count < PROMPT_TOKENS)
        {
            promptTokens.AddRange(tokenizer.Encode(" more"));
        }
        promptTokens = promptTokens.Take(PROMPT_TOKENS).ToList();

        var results = new double[BENCHMARK_RUNS];

        // Warmup
        for (int i = 0; i < WARMUP_RUNS; i++)
        {
            RunPromptProcessing(model, promptTokens);
        }

        // Benchmark runs
        for (int i = 0; i < BENCHMARK_RUNS; i++)
        {
            var sw = Stopwatch.StartNew();
            RunPromptProcessing(model, promptTokens);
            sw.Stop();
            results[i] = sw.Elapsed.TotalMilliseconds;
            Console.WriteLine($"  Run {i + 1}: {results[i]:F2} ms ({PROMPT_TOKENS / (results[i] / 1000.0):F0} tok/s)");
        }

        return CalculateStats(results, PROMPT_TOKENS);
    }

    static void RunPromptProcessing(dynamic model, System.Collections.Generic.List<int> tokens)
    {
        // Create input tensor
        var inputData = new float[tokens.Count];
        for (int i = 0; i < tokens.Count; i++)
        {
            inputData[i] = tokens[i];
        }

        var inputTensor = new Tensor(inputData, new int[] { 1, tokens.Count });

        // Run forward pass
        _ = model.Forward(inputTensor);
    }

    static BenchmarkResult BenchmarkGeneration()
    {
        Console.WriteLine($"\nBenchmarking Generation ({GENERATION_TOKENS} tokens)...");

        // For generation, we'd ideally use InferenceSession, but for this benchmark
        // we'll simulate by running forward passes (use dynamic to access internal type)
        dynamic model = SyntheticModelFactory.CreateTinyModel(seed: 42);
        var tokenizer = TokenizerFactory.CreateCharLevel(
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?\n"
        );

        var results = new double[BENCHMARK_RUNS];
        var ttftResults = new double[BENCHMARK_RUNS];

        // Warmup
        for (int i = 0; i < WARMUP_RUNS; i++)
        {
            double ttftTemp;
            RunGeneration(model, tokenizer, GENERATION_TOKENS, out ttftTemp);
        }

        // Benchmark runs
        for (int i = 0; i < BENCHMARK_RUNS; i++)
        {
            double ttft;
            var sw = Stopwatch.StartNew();
            RunGeneration(model, tokenizer, GENERATION_TOKENS, out ttft);
            sw.Stop();
            results[i] = sw.Elapsed.TotalMilliseconds;
            ttftResults[i] = ttft;
            Console.WriteLine($"  Run {i + 1}: {results[i]:F2} ms total, {ttft:F2} ms TTFT, {GENERATION_TOKENS / (results[i] / 1000.0):F0} tok/s");
        }

        var stats = CalculateStats(results, GENERATION_TOKENS);
        stats.MeanTTFT = ttftResults.Average();
        return stats;
    }

    static void RunGeneration(dynamic model, ITokenizer tokenizer, int numTokens, out double ttft)
    {
        var context = new System.Collections.Generic.List<int> { 1 }; // Start with BOS
        var ttftSw = Stopwatch.StartNew();
        bool firstToken = true;
        ttft = 0;

        for (int i = 0; i < numTokens; i++)
        {
            // Create input tensor
            var inputData = new float[context.Count];
            for (int j = 0; j < context.Count; j++)
            {
                inputData[j] = context[j];
            }

            var inputTensor = new Tensor(inputData, new int[] { 1, context.Count });
            dynamic output = model.Forward(inputTensor);

            // Simple greedy sampling - take argmax
            float[] logits = output.Data;
            int[] shape = output.Shape;
            int vocabSize = shape[shape.Length - 1];
            int offset = logits.Length - vocabSize; // Get last token's logits

            int maxIdx = 0;
            float maxVal = logits[offset];
            for (int j = 1; j < vocabSize; j++)
            {
                if (logits[offset + j] > maxVal)
                {
                    maxVal = logits[offset + j];
                    maxIdx = j;
                }
            }

            context.Add(maxIdx);

            if (firstToken)
            {
                ttft = ttftSw.Elapsed.TotalMilliseconds;
                firstToken = false;
            }
        }
    }

    static BenchmarkResult CalculateStats(double[] values, int tokenCount)
    {
        Array.Sort(values);
        var mean = values.Average();
        var median = values[values.Length / 2];
        var p95 = values[(int)(values.Length * 0.95)];
        var p99 = values[values.Length - 1]; // For 5 runs, p99 = max

        return new BenchmarkResult
        {
            MeanMs = mean,
            MedianMs = median,
            P95Ms = p95,
            P99Ms = p99,
            MeanTokensPerSecond = tokenCount / (mean / 1000.0),
            TokenCount = tokenCount
        };
    }

    static void OutputJson(BenchmarkResults results, string path)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(results, options);
        File.WriteAllText(path, json);
    }

    static void OutputMarkdown(BenchmarkResults results, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# SmallMind CPU Benchmark Results");
        sb.AppendLine();
        sb.AppendLine($"**Timestamp**: {results.Timestamp:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        sb.AppendLine("## System Information");
        sb.AppendLine();
        sb.AppendLine($"- **CPU Architecture**: {results.SystemInfo.CpuArchitecture}");
        sb.AppendLine($"- **Logical Cores**: {results.SystemInfo.LogicalCores}");
        sb.AppendLine($"- **.NET Version**: {results.SystemInfo.DotNetVersion}");
        sb.AppendLine($"- **SIMD Vector Size**: {results.SystemInfo.SimdVectorSize} floats");
        sb.AppendLine($"- **Best Instruction Set**: {results.SystemInfo.BestInstructionSet}");
        sb.AppendLine();

        sb.AppendLine("### SIMD Capabilities");
        sb.AppendLine();
        foreach (var cap in results.SystemInfo.SimdCapabilities.Where(kv => kv.Value))
        {
            sb.AppendLine($"- ✓ {cap.Key}");
        }
        sb.AppendLine();

        sb.AppendLine("## Benchmark Results");
        sb.AppendLine();

        sb.AppendLine("### Prompt Processing (32 tokens - synthetic model)");
        sb.AppendLine();
        OutputBenchmarkTable(sb, results.PromptProcessing);
        sb.AppendLine();

        sb.AppendLine("### Generation (20 tokens - synthetic model)");
        sb.AppendLine();
        OutputBenchmarkTable(sb, results.Generation);
        if (results.Generation.MeanTTFT > 0)
        {
            sb.AppendLine($"- **Mean TTFT**: {results.Generation.MeanTTFT:F2} ms");
        }
        sb.AppendLine();

        sb.AppendLine("## Reference: llama.cpp");
        sb.AppendLine();
        sb.AppendLine("To compare with llama.cpp, run:");
        sb.AppendLine("```bash");
        sb.AppendLine("# Prompt processing");
        sb.AppendLine("./llama-bench -m model.gguf -p 512 -n 0");
        sb.AppendLine();
        sb.AppendLine("# Generation");
        sb.AppendLine("./llama-bench -m model.gguf -p 1 -n 128");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("_Note: Results will vary based on model, CPU, and other factors._");

        File.WriteAllText(path, sb.ToString());
    }

    static void OutputBenchmarkTable(StringBuilder sb, BenchmarkResult result)
    {
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|-------|");
        sb.AppendLine($"| Mean | {result.MeanMs:F2} ms ({result.MeanTokensPerSecond:F0} tok/s) |");
        sb.AppendLine($"| Median | {result.MedianMs:F2} ms |");
        sb.AppendLine($"| P95 | {result.P95Ms:F2} ms |");
        sb.AppendLine($"| P99 | {result.P99Ms:F2} ms |");
    }
}

// Data classes
class SystemInfo
{
    public string CpuArchitecture { get; set; } = "";
    public int LogicalCores { get; set; }
    public string DotNetVersion { get; set; } = "";
    public int SimdVectorSize { get; set; }
    public Dictionary<string, bool> SimdCapabilities { get; set; } = new();
    public string BestInstructionSet { get; set; } = "";
}

class BenchmarkResult
{
    public double MeanMs { get; set; }
    public double MedianMs { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double MeanTokensPerSecond { get; set; }
    public int TokenCount { get; set; }
    public double MeanTTFT { get; set; } // Time to first token
}

class BenchmarkResults
{
    public DateTime Timestamp { get; set; }
    public SystemInfo SystemInfo { get; set; } = new();
    public BenchmarkResult PromptProcessing { get; set; } = new();
    public BenchmarkResult Generation { get; set; } = new();
}

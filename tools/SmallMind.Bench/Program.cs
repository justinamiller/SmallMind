using System.Diagnostics;
using System.Text.Json;
using System.Runtime.InteropServices;
using SmallMind.Core.Simd;

namespace SmallMind.Bench;

/// <summary>
/// Phase 0 Baseline Benchmarking Tool
/// No external dependencies - uses only .NET built-in APIs
/// Measures tokens/sec, latency, GFLOPS, GC metrics, and memory
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            var config = ParseArguments(args);
            
            if (config.ShowHelp)
            {
                ShowHelp();
                return 0;
            }

            // Ensure artifacts directory exists
            EnsureArtifactsDirectory();

            if (config.RunMatmulBenchmark)
            {
                return RunMatmulBenchmark(config);
            }
            else if (!string.IsNullOrEmpty(config.ModelPath))
            {
                return await RunModelBenchmark(config);
            }
            else
            {
                Console.WriteLine("Error: Either --model or --matmul must be specified");
                ShowHelp();
                return 1;
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static BenchConfig ParseArguments(string[] args)
    {
        var config = new BenchConfig();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    config.ShowHelp = true;
                    break;
                case "--model":
                    if (i + 1 < args.Length)
                        config.ModelPath = args[++i];
                    break;
                case "--prompt":
                    if (i + 1 < args.Length)
                        config.Prompt = args[++i];
                    break;
                case "--max-tokens":
                    if (i + 1 < args.Length)
                        config.MaxTokens = int.Parse(args[++i]);
                    break;
                case "--threads":
                    if (i + 1 < args.Length)
                        config.Threads = int.Parse(args[++i]);
                    break;
                case "--repeat":
                    if (i + 1 < args.Length)
                        config.Repeat = int.Parse(args[++i]);
                    break;
                case "--matmul":
                    config.RunMatmulBenchmark = true;
                    break;
                case "--m":
                    if (i + 1 < args.Length)
                        config.M = int.Parse(args[++i]);
                    break;
                case "--n":
                    if (i + 1 < args.Length)
                        config.N = int.Parse(args[++i]);
                    break;
                case "--k":
                    if (i + 1 < args.Length)
                        config.K = int.Parse(args[++i]);
                    break;
            }
        }
        
        return config;
    }

    static void ShowHelp()
    {
        Console.WriteLine("SmallMind.Bench - Phase 0 Baseline Benchmarking Tool");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  bench --model <path.gguf> --prompt \"<text>\" [options]");
        Console.WriteLine("  bench --matmul --m <M> --n <N> --k <K> [options]");
        Console.WriteLine();
        Console.WriteLine("Model Benchmark Options:");
        Console.WriteLine("  --model <path>       Path to GGUF model file");
        Console.WriteLine("  --prompt <text>      Prompt text for generation");
        Console.WriteLine("  --max-tokens <N>     Maximum tokens to generate (default: 100)");
        Console.WriteLine("  --threads <M>        Number of threads (default: auto)");
        Console.WriteLine("  --repeat <R>         Number of repetitions (default: 1)");
        Console.WriteLine();
        Console.WriteLine("MatMul Benchmark Options:");
        Console.WriteLine("  --matmul             Run matrix multiplication benchmark");
        Console.WriteLine("  --m <M>              Matrix M dimension (default: 128)");
        Console.WriteLine("  --n <N>              Matrix N dimension (default: 128)");
        Console.WriteLine("  --k <K>              Matrix K dimension (default: 128)");
        Console.WriteLine("  --repeat <R>         Number of repetitions (default: 100)");
        Console.WriteLine();
        Console.WriteLine("Metrics Collected:");
        Console.WriteLine("  - Tokens/sec (prefill and decode separately)");
        Console.WriteLine("  - Average ms/token and p50/p95 latency for decode");
        Console.WriteLine("  - GFLOPS for matmul kernels");
        Console.WriteLine("  - GC allocation deltas");
        Console.WriteLine("  - GC collection counts");
        Console.WriteLine("  - Process working set");
        Console.WriteLine();
        Console.WriteLine("Results are saved to: /artifacts/benchmarks/");
    }

    static void EnsureArtifactsDirectory()
    {
        var artifactsPath = GetArtifactsPath();
        Directory.CreateDirectory(artifactsPath);
    }

    static string GetArtifactsPath()
    {
        // Find repo root
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "SmallMind.sln")))
        {
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        
        if (dir == null)
            dir = Directory.GetCurrentDirectory();
        
        return Path.Combine(dir, "artifacts", "benchmarks");
    }

    static int RunMatmulBenchmark(BenchConfig config)
    {
        Console.WriteLine("=== MatMul Benchmark ===");
        Console.WriteLine();
        
        PrintSystemInfo();
        
        Console.WriteLine($"Matrix dimensions: M={config.M}, N={config.N}, K={config.K}");
        Console.WriteLine($"Repetitions: {config.Repeat}");
        Console.WriteLine();
        
        var metrics = new MatmulMetrics
        {
            M = config.M,
            N = config.N,
            K = config.K,
            Iterations = config.Repeat
        };
        
        // Allocate matrices
        var A = new float[config.M * config.K];
        var B = new float[config.K * config.N];
        var C = new float[config.M * config.N];
        
        // Initialize with random data
        var random = new Random(42);
        for (int i = 0; i < A.Length; i++) A[i] = (float)random.NextDouble();
        for (int i = 0; i < B.Length; i++) B[i] = (float)random.NextDouble();
        
        // Warmup
        Console.WriteLine("Warming up...");
        for (int i = 0; i < 10; i++)
        {
            Array.Clear(C);
            GemmMicrokernels.MatMul(A.AsSpan(), B.AsSpan(), C.AsSpan(), config.M, config.K, config.N);
        }
        
        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        // Get baseline metrics
        var process = Process.GetCurrentProcess();
        long baselineWorkingSet = process.WorkingSet64;
        long baselineAllocated = GC.GetAllocatedBytesForCurrentThread();
        int baselineGen0 = GC.CollectionCount(0);
        int baselineGen1 = GC.CollectionCount(1);
        int baselineGen2 = GC.CollectionCount(2);
        var baselineGcInfo = GC.GetGCMemoryInfo();
        
        Console.WriteLine("Running benchmark...");
        var sw = Stopwatch.StartNew();
        
        for (int i = 0; i < config.Repeat; i++)
        {
            Array.Clear(C);
            GemmMicrokernels.MatMul(A.AsSpan(), B.AsSpan(), C.AsSpan(), config.M, config.K, config.N);
        }
        
        sw.Stop();
        
        // Get final metrics
        process.Refresh();
        long finalWorkingSet = process.WorkingSet64;
        long finalAllocated = GC.GetAllocatedBytesForCurrentThread();
        int finalGen0 = GC.CollectionCount(0);
        int finalGen1 = GC.CollectionCount(1);
        int finalGen2 = GC.CollectionCount(2);
        var finalGcInfo = GC.GetGCMemoryInfo();
        
        // Calculate metrics
        double totalTimeSeconds = sw.Elapsed.TotalSeconds;
        double avgTimeMs = (totalTimeSeconds * 1000.0) / config.Repeat;
        
        // GFLOPS calculation: 2*M*N*K operations per matmul
        long opsPerMatmul = 2L * config.M * config.N * config.K;
        long totalOps = opsPerMatmul * config.Repeat;
        double gflops = (totalOps / 1e9) / totalTimeSeconds;
        
        metrics.TotalTimeMs = totalTimeSeconds * 1000.0;
        metrics.AvgTimeMs = avgTimeMs;
        metrics.Gflops = gflops;
        metrics.AllocatedBytes = finalAllocated - baselineAllocated;
        metrics.Gen0Collections = finalGen0 - baselineGen0;
        metrics.Gen1Collections = finalGen1 - baselineGen1;
        metrics.Gen2Collections = finalGen2 - baselineGen2;
        metrics.WorkingSetDeltaMB = (finalWorkingSet - baselineWorkingSet) / (1024.0 * 1024.0);
        metrics.HeapSizeMB = finalGcInfo.HeapSizeBytes / (1024.0 * 1024.0);
        
        // Print results
        Console.WriteLine();
        Console.WriteLine("=== Results ===");
        Console.WriteLine($"Total time: {totalTimeSeconds * 1000.0:F2} ms");
        Console.WriteLine($"Average time per iteration: {avgTimeMs:F3} ms");
        Console.WriteLine($"GFLOPS: {gflops:F2}");
        Console.WriteLine();
        Console.WriteLine("GC Metrics:");
        Console.WriteLine($"  Allocated bytes: {metrics.AllocatedBytes:N0}");
        Console.WriteLine($"  Gen0 collections: {metrics.Gen0Collections}");
        Console.WriteLine($"  Gen1 collections: {metrics.Gen1Collections}");
        Console.WriteLine($"  Gen2 collections: {metrics.Gen2Collections}");
        Console.WriteLine($"  Heap size: {metrics.HeapSizeMB:F2} MB");
        Console.WriteLine($"  Working set delta: {metrics.WorkingSetDeltaMB:F2} MB");
        
        // Save results
        SaveMatmulResults(metrics);
        
        return 0;
    }

    static async Task<int> RunModelBenchmark(BenchConfig config)
    {
        Console.WriteLine("=== Model Benchmark ===");
        Console.WriteLine();
        
        if (!File.Exists(config.ModelPath))
        {
            Console.Error.WriteLine($"Error: Model file not found: {config.ModelPath}");
            return 1;
        }
        
        PrintSystemInfo();
        
        Console.WriteLine($"Model: {config.ModelPath}");
        Console.WriteLine($"Prompt: {config.Prompt}");
        Console.WriteLine($"Max tokens: {config.MaxTokens}");
        Console.WriteLine($"Repetitions: {config.Repeat}");
        Console.WriteLine();
        
        var metrics = new ModelMetrics
        {
            ModelPath = config.ModelPath,
            Prompt = config.Prompt,
            MaxTokens = config.MaxTokens,
            Repetitions = config.Repeat
        };
        
        try
        {
            // Load model
            Console.WriteLine("Loading model...");
            using var engine = SmallMind.SmallMindFactory.Create(new SmallMind.SmallMindOptions
            {
                ModelPath = config.ModelPath,
                AllowGgufImport = true
            });
            
            var sessionOptions = new SmallMind.TextGenerationOptions
            {
                MaxOutputTokens = config.MaxTokens,
                Temperature = 0.1f, // Low but > 0 for stable benchmarking
                TopP = 1.0f,
                TopK = 1
            };
            
            // Warmup run
            Console.WriteLine("Warming up...");
            using (var session = engine.CreateTextGenerationSession(sessionOptions))
            {
                var warmupRequest = new SmallMind.TextGenerationRequest
                {
                    Prompt = config.Prompt.AsMemory(),
                    Seed = 42
                };
                session.Generate(warmupRequest);
            }
            
            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Get baseline metrics
            var process = Process.GetCurrentProcess();
            long baselineWorkingSet = process.WorkingSet64;
            long baselineAllocated = GC.GetAllocatedBytesForCurrentThread();
            int baselineGen0 = GC.CollectionCount(0);
            int baselineGen1 = GC.CollectionCount(1);
            int baselineGen2 = GC.CollectionCount(2);
            var baselineGcInfo = GC.GetGCMemoryInfo();
            
            Console.WriteLine("Running benchmark...");
            var allTokenLatencies = new List<double>();
            double totalPrefillTime = 0;
            double totalDecodeTime = 0;
            int totalTokensGenerated = 0;
            
            for (int rep = 0; rep < config.Repeat; rep++)
            {
                using var session = engine.CreateTextGenerationSession(sessionOptions);
                
                var sw = Stopwatch.StartNew();
                var tokenLatencies = new List<double>();
                int tokenCount = 0;
                var lastTokenTime = sw.Elapsed.TotalMilliseconds;
                bool isFirstToken = true;
                double prefillTime = 0;
                
                var request = new SmallMind.TextGenerationRequest
                {
                    Prompt = config.Prompt.AsMemory(),
                    Seed = 42
                };
                
                await foreach (var token in session.GenerateStreaming(request))
                {
                    var currentTime = sw.Elapsed.TotalMilliseconds;
                    var tokenLatency = currentTime - lastTokenTime;
                    
                    if (isFirstToken)
                    {
                        // First token includes prefill time
                        prefillTime = tokenLatency;
                        isFirstToken = false;
                    }
                    else
                    {
                        tokenLatencies.Add(tokenLatency);
                        allTokenLatencies.Add(tokenLatency);
                    }
                    
                    lastTokenTime = currentTime;
                    tokenCount++;
                }
                
                sw.Stop();
                totalPrefillTime += prefillTime;
                totalDecodeTime += (sw.Elapsed.TotalMilliseconds - prefillTime);
                totalTokensGenerated += tokenCount;
            }
            
            // Get final metrics
            process.Refresh();
            long finalWorkingSet = process.WorkingSet64;
            long finalAllocated = GC.GetAllocatedBytesForCurrentThread();
            int finalGen0 = GC.CollectionCount(0);
            int finalGen1 = GC.CollectionCount(1);
            int finalGen2 = GC.CollectionCount(2);
            var finalGcInfo = GC.GetGCMemoryInfo();
            
            // Calculate metrics
            metrics.PrefillTimeMs = totalPrefillTime / config.Repeat;
            metrics.PrefillTokensPerSec = 1000.0 / metrics.PrefillTimeMs;
            
            metrics.DecodeTimeMs = totalDecodeTime / config.Repeat;
            var decodeTokens = (totalTokensGenerated / config.Repeat) - 1; // Exclude first token
            if (decodeTokens > 0)
            {
                metrics.DecodeTokensPerSec = decodeTokens / (metrics.DecodeTimeMs / 1000.0);
                metrics.DecodeAvgMsPerToken = metrics.DecodeTimeMs / decodeTokens;
            }
            
            // Calculate percentiles
            if (allTokenLatencies.Count > 0)
            {
                allTokenLatencies.Sort();
                int p50Index = (int)(allTokenLatencies.Count * 0.5);
                int p95Index = (int)(allTokenLatencies.Count * 0.95);
                metrics.DecodeP50MsPerToken = allTokenLatencies[p50Index];
                metrics.DecodeP95MsPerToken = allTokenLatencies[p95Index];
                metrics.TokenLatenciesMs = allTokenLatencies;
            }
            
            metrics.AllocatedBytes = finalAllocated - baselineAllocated;
            metrics.Gen0Collections = finalGen0 - baselineGen0;
            metrics.Gen1Collections = finalGen1 - baselineGen1;
            metrics.Gen2Collections = finalGen2 - baselineGen2;
            metrics.WorkingSetDeltaMB = (finalWorkingSet - baselineWorkingSet) / (1024.0 * 1024.0);
            metrics.HeapSizeMB = finalGcInfo.HeapSizeBytes / (1024.0 * 1024.0);
            
            // Print results
            Console.WriteLine();
            Console.WriteLine("=== Results ===");
            Console.WriteLine($"Prefill time: {metrics.PrefillTimeMs:F2} ms ({metrics.PrefillTokensPerSec:F2} tokens/sec)");
            Console.WriteLine($"Decode time: {metrics.DecodeTimeMs:F2} ms ({metrics.DecodeTokensPerSec:F2} tokens/sec)");
            Console.WriteLine($"Decode avg latency: {metrics.DecodeAvgMsPerToken:F3} ms/token");
            Console.WriteLine($"Decode p50 latency: {metrics.DecodeP50MsPerToken:F3} ms/token");
            Console.WriteLine($"Decode p95 latency: {metrics.DecodeP95MsPerToken:F3} ms/token");
            Console.WriteLine();
            Console.WriteLine("GC Metrics:");
            Console.WriteLine($"  Allocated bytes: {metrics.AllocatedBytes:N0}");
            Console.WriteLine($"  Gen0 collections: {metrics.Gen0Collections}");
            Console.WriteLine($"  Gen1 collections: {metrics.Gen1Collections}");
            Console.WriteLine($"  Gen2 collections: {metrics.Gen2Collections}");
            Console.WriteLine($"  Heap size: {metrics.HeapSizeMB:F2} MB");
            Console.WriteLine($"  Working set delta: {metrics.WorkingSetDeltaMB:F2} MB");
            
            SaveModelResults(metrics);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error running model benchmark: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
        
        return 0;
    }

    static void PrintSystemInfo()
    {
        Console.WriteLine("=== System Information ===");
        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($".NET Version: {Environment.Version}");
        Console.WriteLine($"Processor Count: {Environment.ProcessorCount}");
        Console.WriteLine();
    }

    static void SaveMatmulResults(MatmulMetrics metrics)
    {
        var result = new BenchmarkResult
        {
            Timestamp = DateTime.UtcNow,
            BenchmarkType = "MatMul",
            SystemInfo = GetSystemInfo(),
            MatmulMetrics = metrics
        };
        
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        var artifactsPath = GetArtifactsPath();
        var filename = $"matmul_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var filepath = Path.Combine(artifactsPath, filename);
        
        File.WriteAllText(filepath, json);
        Console.WriteLine();
        Console.WriteLine($"Results saved to: {filepath}");
    }

    static void SaveModelResults(ModelMetrics metrics)
    {
        var result = new BenchmarkResult
        {
            Timestamp = DateTime.UtcNow,
            BenchmarkType = "Model",
            SystemInfo = GetSystemInfo(),
            ModelMetrics = metrics
        };
        
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions 
        { 
            WriteIndented = true 
        });
        
        var artifactsPath = GetArtifactsPath();
        var filename = $"model_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        var filepath = Path.Combine(artifactsPath, filename);
        
        File.WriteAllText(filepath, json);
        Console.WriteLine();
        Console.WriteLine($"Results saved to: {filepath}");
    }

    static SystemInfo GetSystemInfo()
    {
        return new SystemInfo
        {
            OS = RuntimeInformation.OSDescription,
            Architecture = RuntimeInformation.ProcessArchitecture.ToString(),
            DotNetVersion = Environment.Version.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            MachineName = Environment.MachineName
        };
    }
}

class BenchConfig
{
    public bool ShowHelp { get; set; }
    public string ModelPath { get; set; } = "";
    public string Prompt { get; set; } = "Hello, world!";
    public int MaxTokens { get; set; } = 100;
    public int Threads { get; set; } = Environment.ProcessorCount;
    public int Repeat { get; set; } = 1;
    public bool RunMatmulBenchmark { get; set; }
    public int M { get; set; } = 128;
    public int N { get; set; } = 128;
    public int K { get; set; } = 128;
}

class BenchmarkResult
{
    public DateTime Timestamp { get; set; }
    public string BenchmarkType { get; set; } = "";
    public SystemInfo SystemInfo { get; set; } = new();
    public MatmulMetrics? MatmulMetrics { get; set; }
    public ModelMetrics? ModelMetrics { get; set; }
}

class SystemInfo
{
    public string OS { get; set; } = "";
    public string Architecture { get; set; } = "";
    public string DotNetVersion { get; set; } = "";
    public int ProcessorCount { get; set; }
    public string MachineName { get; set; } = "";
}

class MatmulMetrics
{
    public int M { get; set; }
    public int N { get; set; }
    public int K { get; set; }
    public int Iterations { get; set; }
    public double TotalTimeMs { get; set; }
    public double AvgTimeMs { get; set; }
    public double Gflops { get; set; }
    public long AllocatedBytes { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public double WorkingSetDeltaMB { get; set; }
    public double HeapSizeMB { get; set; }
}

class ModelMetrics
{
    public string ModelPath { get; set; } = "";
    public string Prompt { get; set; } = "";
    public int MaxTokens { get; set; }
    public int Repetitions { get; set; }
    
    // Prefill phase metrics
    public double PrefillTimeMs { get; set; }
    public double PrefillTokensPerSec { get; set; }
    
    // Decode phase metrics  
    public double DecodeTimeMs { get; set; }
    public double DecodeTokensPerSec { get; set; }
    public double DecodeAvgMsPerToken { get; set; }
    public double DecodeP50MsPerToken { get; set; }
    public double DecodeP95MsPerToken { get; set; }
    
    // Per-token latencies for percentile calculation
    public List<double> TokenLatenciesMs { get; set; } = new();
    
    // GC metrics
    public long AllocatedBytes { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public double WorkingSetDeltaMB { get; set; }
    public double HeapSizeMB { get; set; }
}

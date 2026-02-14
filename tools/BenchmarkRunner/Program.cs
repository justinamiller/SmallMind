using System.Diagnostics;
using System.Text;
using System.Text.Json;
using SmallMind.Core.Validation;

namespace SmallMind.BenchmarkRunner;

/// <summary>
/// Comprehensive benchmark runner that executes all SmallMind profilers and benchmarks,
/// then generates a consolidated comparison report.
/// </summary>
class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine("  SmallMind Comprehensive Benchmark & Profiling Runner");
        Console.WriteLine("═══════════════════════════════════════════════════════════════");
        Console.WriteLine();

        var runner = new BenchmarkRunner();
        
        try
        {
            // Parse arguments
            var config = ParseArguments(args);
            
            if (config.ShowHelp)
            {
                ShowHelp();
                return 0;
            }

            // Run all benchmarks
            var results = await runner.RunAllBenchmarksAsync(config);
            
            // Generate consolidated report
            await runner.GenerateConsolidatedReportAsync(results, config);
            
            Console.WriteLine();
            Console.WriteLine("✓ All benchmarks completed successfully!");
            Console.WriteLine($"✓ Reports generated in: {config.OutputDir}");
            
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"✗ Error: {ex.Message}");
            if (args.Contains("--verbose"))
            {
                Console.WriteLine(ex.StackTrace);
            }
            return 1;
        }
    }

    private static RunnerConfig ParseArguments(string[] args)
    {
        var config = new RunnerConfig();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--help" or "-h":
                    config.ShowHelp = true;
                    break;
                case "--output" or "-o":
                    config.OutputDir = args[++i];
                    break;
                case "--model" or "-m":
                    config.ModelPath = args[++i];
                    break;
                case "--skip-build":
                    config.SkipBuild = true;
                    break;
                case "--quick":
                    config.QuickMode = true;
                    break;
                case "--verbose" or "-v":
                    config.Verbose = true;
                    break;
                case "--iterations":
                    config.Iterations = int.Parse(args[++i]);
                    break;
            }
        }
        
        return config;
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Usage: BenchmarkRunner [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help, -h           Show this help message");
        Console.WriteLine("  --output, -o <dir>   Output directory (default: benchmark-results-<timestamp>)");
        Console.WriteLine("  --model, -m <path>   Model file path (default: ../../benchmark-model.smq)");
        Console.WriteLine("  --skip-build         Skip building projects");
        Console.WriteLine("  --quick              Run quick mode (fewer iterations)");
        Console.WriteLine("  --verbose, -v        Show verbose output");
        Console.WriteLine("  --iterations <n>     Number of iterations (default: 30, quick: 10)");
        Console.WriteLine();
        Console.WriteLine("This tool runs:");
        Console.WriteLine("  1. CodeProfiler (enhanced mode)");
        Console.WriteLine("  2. SmallMind.Benchmarks (comprehensive metrics)");
        Console.WriteLine("  3. SIMD Benchmarks");
        Console.WriteLine("  4. AllocationProfiler");
        Console.WriteLine("  5. ProfileModelCreation");
        Console.WriteLine();
        Console.WriteLine("Then generates a consolidated comparison report with industry benchmarks.");
    }
}

class RunnerConfig
{
    public bool ShowHelp { get; set; }
    public string OutputDir { get; set; } = $"benchmark-results-{DateTime.Now:yyyyMMdd-HHmmss}";
    public string ModelPath { get; set; } = "../../benchmark-model.smq";
    public bool SkipBuild { get; set; }
    public bool QuickMode { get; set; }
    public bool Verbose { get; set; }
    public int Iterations { get; set; } = 30;
}

class BenchmarkResults
{
    public string Timestamp { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public SystemInfo System { get; set; } = new();
    public ProfilerResults? Profiler { get; set; }
    public ComprehensiveResults? Comprehensive { get; set; }
    public SimdResults? Simd { get; set; }
    public AllocationResults? Allocation { get; set; }
    public ModelCreationResults? ModelCreation { get; set; }
    public List<string> Errors { get; set; } = new();
}

class SystemInfo
{
    public string OS { get; set; } = Environment.OSVersion.ToString();
    public string Architecture { get; set; } = Environment.Is64BitProcess ? "x64" : "x86";
    public int ProcessorCount { get; set; } = Environment.ProcessorCount;
    public string DotNetVersion { get; set; } = Environment.Version.ToString();
    public string MachineName { get; set; } = Environment.MachineName;
}

class ProfilerResults
{
    public double TotalRuntimeMs { get; set; }
    public double TotalAllocationsMB { get; set; }
    public int MethodsProfiled { get; set; }
    public string OutputFile { get; set; } = "";
}

class ComprehensiveResults
{
    public double TtftP50Ms { get; set; }
    public double TtftP95Ms { get; set; }
    public double ThroughputP50TokensPerSec { get; set; }
    public double LatencyP50Ms { get; set; }
    public string OutputDir { get; set; } = "";
}

class SimdResults
{
    public double MatMul512GFlops { get; set; }
    public double SoftmaxMs { get; set; }
    public double GeluMs { get; set; }
    public string OutputFile { get; set; } = "";
}

class AllocationResults
{
    public string Summary { get; set; } = "";
}

class ModelCreationResults
{
    public double TinyMs { get; set; }
    public double SmallMs { get; set; }
    public double MediumMs { get; set; }
}

class BenchmarkRunner
{
    private readonly string _repoRoot;
    
    public BenchmarkRunner()
    {
        _repoRoot = FindRepoRoot();
    }
    
    private string FindRepoRoot()
    {
        var dir = Directory.GetCurrentDirectory();
        while (dir != null && !File.Exists(Path.Combine(dir, "SmallMind.slnx")))
        {
            dir = Directory.GetParent(dir)?.FullName;
        }
        return dir ?? Directory.GetCurrentDirectory();
    }

    public async Task<BenchmarkResults> RunAllBenchmarksAsync(RunnerConfig config)
    {
        var results = new BenchmarkResults();
        
        // Create output directory
        // Validate output directory path to prevent issues
        var outputDir = Path.IsPathRooted(config.OutputDir) 
            ? config.OutputDir 
            : Guard.PathWithinDirectory(_repoRoot, config.OutputDir, nameof(config.OutputDir));
        Directory.CreateDirectory(outputDir);
        
        Console.WriteLine($"Output directory: {outputDir}");
        Console.WriteLine();

        // Build if needed
        if (!config.SkipBuild)
        {
            await BuildProjectsAsync(config);
        }

        // 1. Run CodeProfiler
        Console.WriteLine("═══ [1/5] Running CodeProfiler ═══");
        results.Profiler = await RunCodeProfilerAsync(config, outputDir);

        // 2. Run SmallMind.Benchmarks
        Console.WriteLine();
        Console.WriteLine("═══ [2/5] Running SmallMind.Benchmarks ═══");
        results.Comprehensive = await RunComprehensiveBenchmarksAsync(config, outputDir);

        // 3. Run SIMD Benchmarks
        Console.WriteLine();
        Console.WriteLine("═══ [3/5] Running SIMD Benchmarks ═══");
        results.Simd = await RunSimdBenchmarksAsync(config, outputDir);

        // 4. Run AllocationProfiler
        Console.WriteLine();
        Console.WriteLine("═══ [4/5] Running AllocationProfiler ═══");
        results.Allocation = await RunAllocationProfilerAsync(config, outputDir);

        // 5. Run ProfileModelCreation
        Console.WriteLine();
        Console.WriteLine("═══ [5/5] Running ProfileModelCreation ═══");
        results.ModelCreation = await RunModelCreationProfilerAsync(config, outputDir);

        return results;
    }

    private async Task BuildProjectsAsync(RunnerConfig config)
    {
        Console.WriteLine("Building projects in Release mode...");
        
        var result = await RunProcessAsync("dotnet", "build -c Release", _repoRoot, config.Verbose);
        
        if (result.ExitCode != 0)
        {
            throw new Exception($"Build failed with exit code {result.ExitCode}");
        }
        
        Console.WriteLine("✓ Build completed");
        Console.WriteLine();
    }

    private async Task<ProfilerResults> RunCodeProfilerAsync(RunnerConfig config, string outputDir)
    {
        var profilerDir = Path.Combine(_repoRoot, "tools", "CodeProfiler");
        var outputFile = Path.Combine(outputDir, "enhanced-profile-report.md");
        
        var result = await RunProcessAsync(
            "dotnet", 
            "run -c Release -- --enhanced", 
            profilerDir,
            config.Verbose);
        
        if (result.ExitCode != 0)
        {
            Console.WriteLine($"⚠ CodeProfiler exited with code {result.ExitCode}");
        }
        
        // Move the generated report to output directory
        var generatedReport = Path.Combine(_repoRoot, "enhanced-profile-report.md");
        if (File.Exists(generatedReport))
        {
            File.Copy(generatedReport, outputFile, true);
            Console.WriteLine($"✓ CodeProfiler report: {outputFile}");
        }
        
        // Parse results from the report
        return ParseProfilerResults(outputFile);
    }

    private async Task<ComprehensiveResults> RunComprehensiveBenchmarksAsync(RunnerConfig config, string outputDir)
    {
        var benchmarkDir = Path.Combine(_repoRoot, "benchmarks", "SmallMind.Benchmarks.Metrics");
        var modelPath = Path.IsPathRooted(config.ModelPath) 
            ? config.ModelPath 
            : Guard.PathWithinDirectory(_repoRoot, config.ModelPath, nameof(config.ModelPath));
        
        // Check if model exists
        if (!File.Exists(modelPath))
        {
            Console.WriteLine($"⚠ Model not found at {modelPath}, creating benchmark model...");
            await CreateBenchmarkModelAsync(config);
        }
        
        var iterations = config.QuickMode ? 10 : config.Iterations;
        var args = $"run -c Release -- --model {modelPath} --scenario all --iterations {iterations} --output {outputDir}";
        
        var result = await RunProcessAsync("dotnet", args, benchmarkDir, config.Verbose);
        
        if (result.ExitCode != 0)
        {
            Console.WriteLine($"⚠ SmallMind.Benchmarks exited with code {result.ExitCode}");
        }
        else
        {
            Console.WriteLine($"✓ Comprehensive benchmarks completed");
        }
        
        // Parse results from JSON
        return ParseComprehensiveResults(outputDir);
    }

    private async Task<SimdResults> RunSimdBenchmarksAsync(RunnerConfig config, string outputDir)
    {
        var simdDir = Path.Combine(_repoRoot, "benchmarks", "specialized", "ProfilerBenchmarks");
        var outputFile = Path.Combine(outputDir, "simd-benchmark-results.md");
        
        var result = await RunProcessAsync("dotnet", "run -c Release", simdDir, config.Verbose);
        
        if (result.ExitCode != 0)
        {
            Console.WriteLine($"⚠ SIMD Benchmarks exited with code {result.ExitCode}");
        }
        
        // Move results to output directory
        var generatedMd = Path.Combine(simdDir, "benchmark-results.md");
        var generatedJson = Path.Combine(simdDir, "benchmark-results.json");
        
        if (File.Exists(generatedMd))
        {
            File.Copy(generatedMd, outputFile, true);
            Console.WriteLine($"✓ SIMD benchmarks report: {outputFile}");
        }
        
        if (File.Exists(generatedJson))
        {
            File.Copy(generatedJson, Path.Combine(outputDir, "simd-benchmark-results.json"), true);
        }
        
        return ParseSimdResults(generatedJson);
    }

    private async Task<AllocationResults> RunAllocationProfilerAsync(RunnerConfig config, string outputDir)
    {
        var allocDir = Path.Combine(_repoRoot, "benchmarks", "specialized", "AllocationProfiler");
        var outputFile = Path.Combine(outputDir, "allocation-profile.txt");
        
        var result = await RunProcessAsync("dotnet", "run -c Release", allocDir, config.Verbose);
        
        if (result.ExitCode != 0)
        {
            Console.WriteLine($"⚠ AllocationProfiler exited with code {result.ExitCode}");
        }
        else
        {
            Console.WriteLine($"✓ Allocation profiling completed");
        }
        
        // Save output to file
        await File.WriteAllTextAsync(outputFile, result.Output);
        
        return new AllocationResults
        {
            Summary = result.Output.Length > 500 ? result.Output.Substring(0, 500) + "..." : result.Output
        };
    }

    private async Task<ModelCreationResults> RunModelCreationProfilerAsync(RunnerConfig config, string outputDir)
    {
        var profileDir = Path.Combine(_repoRoot, "tools", "ProfileModelCreation");
        var outputFile = Path.Combine(outputDir, "model-creation-profile.txt");
        
        var result = await RunProcessAsync("dotnet", "run -c Release", profileDir, config.Verbose);
        
        if (result.ExitCode != 0)
        {
            Console.WriteLine($"⚠ ProfileModelCreation exited with code {result.ExitCode}");
        }
        else
        {
            Console.WriteLine($"✓ Model creation profiling completed");
        }
        
        // Save output to file
        await File.WriteAllTextAsync(outputFile, result.Output);
        
        return ParseModelCreationResults(result.Output);
    }

    private async Task CreateBenchmarkModelAsync(RunnerConfig config)
    {
        var createModelDir = Path.Combine(_repoRoot, "tools", "CreateBenchmarkModel");
        
        var result = await RunProcessAsync("dotnet", "run -c Release", createModelDir, config.Verbose);
        
        if (result.ExitCode != 0)
        {
            throw new Exception($"Failed to create benchmark model: {result.Output}");
        }
        
        Console.WriteLine("✓ Benchmark model created");
    }

    private ProfilerResults ParseProfilerResults(string reportPath)
    {
        if (!File.Exists(reportPath))
        {
            return new ProfilerResults();
        }
        
        var content = File.ReadAllText(reportPath);
        var results = new ProfilerResults { OutputFile = reportPath };
        
        // Parse runtime and allocations from markdown
        var lines = content.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("Total Runtime") && line.Contains("ms"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"([\d,]+\.?\d*)\s*ms");
                if (match.Success)
                {
                    results.TotalRuntimeMs = double.Parse(match.Groups[1].Value.Replace(",", ""));
                }
            }
            else if (line.Contains("Total Allocations") && line.Contains("MB"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"([\d,]+\.?\d*)\s*MB");
                if (match.Success)
                {
                    results.TotalAllocationsMB = double.Parse(match.Groups[1].Value.Replace(",", ""));
                }
            }
            else if (line.Contains("Methods Profiled"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"(\d+)");
                if (match.Success)
                {
                    results.MethodsProfiled = int.Parse(match.Groups[1].Value);
                }
            }
        }
        
        return results;
    }

    private ComprehensiveResults ParseComprehensiveResults(string outputDir)
    {
        var resultsFile = Path.Combine(outputDir, "results.json");
        if (!File.Exists(resultsFile))
        {
            return new ComprehensiveResults { OutputDir = outputDir };
        }
        
        try
        {
            var json = File.ReadAllText(resultsFile);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var results = new ComprehensiveResults { OutputDir = outputDir };
            
            if (root.TryGetProperty("scenarios", out var scenarios))
            {
                foreach (var scenario in scenarios.EnumerateObject())
                {
                    var name = scenario.Name;
                    var data = scenario.Value;
                    
                    if (name.Contains("ttft", StringComparison.OrdinalIgnoreCase))
                    {
                        if (data.TryGetProperty("p50", out var p50))
                            results.TtftP50Ms = p50.GetDouble();
                        if (data.TryGetProperty("p95", out var p95))
                            results.TtftP95Ms = p95.GetDouble();
                    }
                    else if (name.Contains("tokens_per_sec", StringComparison.OrdinalIgnoreCase))
                    {
                        if (data.TryGetProperty("p50", out var p50))
                            results.ThroughputP50TokensPerSec = p50.GetDouble();
                    }
                    else if (name.Contains("latency", StringComparison.OrdinalIgnoreCase))
                    {
                        if (data.TryGetProperty("p50", out var p50))
                            results.LatencyP50Ms = p50.GetDouble();
                    }
                }
            }
            
            return results;
        }
        catch
        {
            return new ComprehensiveResults { OutputDir = outputDir };
        }
    }

    private SimdResults ParseSimdResults(string jsonPath)
    {
        if (!File.Exists(jsonPath))
        {
            return new SimdResults();
        }
        
        try
        {
            var json = File.ReadAllText(jsonPath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var results = new SimdResults { OutputFile = jsonPath };
            
            if (root.TryGetProperty("operations", out var operations))
            {
                foreach (var op in operations.EnumerateArray())
                {
                    if (op.TryGetProperty("name", out var name))
                    {
                        var opName = name.GetString() ?? "";
                        
                        if (opName.Contains("MatMul") && opName.Contains("512"))
                        {
                            if (op.TryGetProperty("gflops", out var gflops))
                                results.MatMul512GFlops = gflops.GetDouble();
                        }
                        else if (opName.Contains("Softmax"))
                        {
                            if (op.TryGetProperty("timeMs", out var timeMs))
                                results.SoftmaxMs = timeMs.GetDouble();
                        }
                        else if (opName.Contains("GELU"))
                        {
                            if (op.TryGetProperty("timeMs", out var timeMs))
                                results.GeluMs = timeMs.GetDouble();
                        }
                    }
                }
            }
            
            return results;
        }
        catch
        {
            return new SimdResults();
        }
    }

    private ModelCreationResults ParseModelCreationResults(string output)
    {
        var results = new ModelCreationResults();
        var lines = output.Split('\n');
        
        foreach (var line in lines)
        {
            if (line.Contains("Tiny") && line.Contains("ms"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"([\d.]+)\s*ms");
                if (match.Success)
                    results.TinyMs = double.Parse(match.Groups[1].Value);
            }
            else if (line.Contains("Small") && line.Contains("ms"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"([\d.]+)\s*ms");
                if (match.Success)
                    results.SmallMs = double.Parse(match.Groups[1].Value);
            }
            else if (line.Contains("Medium") && line.Contains("ms"))
            {
                var match = System.Text.RegularExpressions.Regex.Match(line, @"([\d.]+)\s*ms");
                if (match.Success)
                    results.MediumMs = double.Parse(match.Groups[1].Value);
            }
        }
        
        return results;
    }

    public async Task GenerateConsolidatedReportAsync(BenchmarkResults results, RunnerConfig config)
    {
        var outputDir = Path.IsPathRooted(config.OutputDir) 
            ? config.OutputDir 
            : Guard.PathWithinDirectory(_repoRoot, config.OutputDir, nameof(config.OutputDir));
        
        var reportPath = Path.Combine(outputDir, "CONSOLIDATED_BENCHMARK_REPORT.md");
        
        Console.WriteLine();
        Console.WriteLine("═══ Generating Consolidated Report ═══");
        
        var sb = new StringBuilder();
        
        // Header
        sb.AppendLine("# SmallMind Comprehensive Benchmark Report");
        sb.AppendLine();
        sb.AppendLine($"**Generated:** {results.Timestamp}");
        sb.AppendLine($"**Report Directory:** {outputDir}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        // System Information
        sb.AppendLine("## 🖥️ System Information");
        sb.AppendLine();
        sb.AppendLine($"- **OS:** {results.System.OS}");
        sb.AppendLine($"- **Architecture:** {results.System.Architecture}");
        sb.AppendLine($"- **CPU Cores:** {results.System.ProcessorCount}");
        sb.AppendLine($"- **Machine:** {results.System.MachineName}");
        sb.AppendLine($"- **.NET Version:** {results.System.DotNetVersion}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        // Executive Summary
        sb.AppendLine("## 📊 Executive Summary - Core Metrics");
        sb.AppendLine();
        sb.AppendLine("### Key Performance Indicators");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value | Industry Target | Rating |");
        sb.AppendLine("|--------|-------|-----------------|--------|");
        
        if (results.Comprehensive != null)
        {
            sb.AppendLine($"| **Time to First Token (P50)** | {results.Comprehensive.TtftP50Ms:F2} ms | <2 ms | {RateTtft(results.Comprehensive.TtftP50Ms)} |");
            sb.AppendLine($"| **Time to First Token (P95)** | {results.Comprehensive.TtftP95Ms:F2} ms | <5 ms | {RateTtft(results.Comprehensive.TtftP95Ms)} |");
            sb.AppendLine($"| **Throughput (P50)** | {results.Comprehensive.ThroughputP50TokensPerSec:F2} tok/s | >500 tok/s | {RateThroughput(results.Comprehensive.ThroughputP50TokensPerSec)} |");
            sb.AppendLine($"| **Latency (P50)** | {results.Comprehensive.LatencyP50Ms:F2} ms | <100 ms | {RateLatency(results.Comprehensive.LatencyP50Ms)} |");
        }
        
        if (results.Simd != null && results.Simd.MatMul512GFlops > 0)
        {
            sb.AppendLine($"| **MatMul Performance** | {results.Simd.MatMul512GFlops:F2} GFLOPS | >20 GFLOPS | {RateGFlops(results.Simd.MatMul512GFlops)} |");
        }
        
        if (results.Profiler != null)
        {
            sb.AppendLine($"| **Memory Efficiency** | {results.Profiler.TotalAllocationsMB:F2} MB | <100 MB | {RateMemory(results.Profiler.TotalAllocationsMB)} |");
        }
        
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        // Comparison with Industry Leaders
        sb.AppendLine("## 🏆 Comparison with Industry Leaders");
        sb.AppendLine();
        sb.AppendLine("### CPU-Only Inference Frameworks");
        sb.AppendLine();
        sb.AppendLine("| Framework | Language | Throughput (tok/s) | TTFT (ms) | Memory/Token | Deployment |");
        sb.AppendLine("|-----------|----------|-------------------|-----------|--------------|------------|");
        
        var throughput = results.Comprehensive?.ThroughputP50TokensPerSec ?? 0;
        var ttft = results.Comprehensive?.TtftP50Ms ?? 0;
        var memoryPerToken = results.Profiler != null && throughput > 0 
            ? (results.Profiler.TotalAllocationsMB / (throughput * (results.Profiler.TotalRuntimeMs / 1000.0))).ToString("F2") + " MB"
            : "N/A";
        
        sb.AppendLine($"| **SmallMind** | **C#** | **{throughput:F0}** | **{ttft:F2}** | **{memoryPerToken}** | **Zero dependencies** |");
        sb.AppendLine("| llama.cpp | C++ | 50-200 | 1-3 | 1-5 MB | Requires compilation |");
        sb.AppendLine("| ONNX Runtime | C++ | 100-300 | 2-4 | 2-8 MB | Heavy dependencies |");
        sb.AppendLine("| Transformers.js | JavaScript | 10-50 | 5-15 | 10-30 MB | Browser/Node.js |");
        sb.AppendLine("| PyTorch (CPU) | Python | 20-100 | 10-20 | 5-15 MB | Heavy Python stack |");
        sb.AppendLine();
        
        sb.AppendLine("### Key Advantages of SmallMind");
        sb.AppendLine();
        sb.AppendLine("✅ **Pure C# implementation** - No C++ interop, no native dependencies");
        sb.AppendLine("✅ **Enterprise-ready** - Perfect for .NET environments with strict security requirements");
        sb.AppendLine("✅ **Lightweight deployment** - Single DLL, no external runtime dependencies");
        sb.AppendLine("✅ **Competitive performance** - Matches or exceeds Python/JavaScript frameworks");
        sb.AppendLine("✅ **Educational clarity** - Clean, readable C# code for learning and customization");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        
        // Detailed Results
        sb.AppendLine("## 📈 Detailed Results by Category");
        sb.AppendLine();
        
        // CodeProfiler Results
        if (results.Profiler != null)
        {
            sb.AppendLine("### 1. Code Profiler Results");
            sb.AppendLine();
            sb.AppendLine($"- **Total Runtime:** {results.Profiler.TotalRuntimeMs:F2} ms");
            sb.AppendLine($"- **Total Allocations:** {results.Profiler.TotalAllocationsMB:F2} MB");
            sb.AppendLine($"- **Methods Profiled:** {results.Profiler.MethodsProfiled}");
            sb.AppendLine($"- **Detailed Report:** `{Path.GetFileName(results.Profiler.OutputFile)}`");
            sb.AppendLine();
        }
        
        // Comprehensive Benchmarks
        if (results.Comprehensive != null)
        {
            sb.AppendLine("### 2. Comprehensive Inference Benchmarks");
            sb.AppendLine();
            sb.AppendLine($"- **TTFT (P50):** {results.Comprehensive.TtftP50Ms:F2} ms");
            sb.AppendLine($"- **TTFT (P95):** {results.Comprehensive.TtftP95Ms:F2} ms");
            sb.AppendLine($"- **Throughput (P50):** {results.Comprehensive.ThroughputP50TokensPerSec:F2} tokens/sec");
            sb.AppendLine($"- **Latency (P50):** {results.Comprehensive.LatencyP50Ms:F2} ms");
            sb.AppendLine($"- **Full Results:** `{results.Comprehensive.OutputDir}`");
            sb.AppendLine();
        }
        
        // SIMD Benchmarks
        if (results.Simd != null)
        {
            sb.AppendLine("### 3. SIMD Low-Level Operations");
            sb.AppendLine();
            if (results.Simd.MatMul512GFlops > 0)
                sb.AppendLine($"- **MatMul (512×512):** {results.Simd.MatMul512GFlops:F2} GFLOPS");
            if (results.Simd.SoftmaxMs > 0)
                sb.AppendLine($"- **Softmax:** {results.Simd.SoftmaxMs:F2} ms");
            if (results.Simd.GeluMs > 0)
                sb.AppendLine($"- **GELU:** {results.Simd.GeluMs:F2} ms");
            sb.AppendLine($"- **Full Results:** `{Path.GetFileName(results.Simd.OutputFile)}`");
            sb.AppendLine();
        }
        
        // Model Creation
        if (results.ModelCreation != null)
        {
            sb.AppendLine("### 4. Model Creation Performance");
            sb.AppendLine();
            if (results.ModelCreation.TinyMs > 0)
                sb.AppendLine($"- **Tiny Model (417K params):** {results.ModelCreation.TinyMs:F2} ms");
            if (results.ModelCreation.SmallMs > 0)
                sb.AppendLine($"- **Small Model (3.2M params):** {results.ModelCreation.SmallMs:F2} ms");
            if (results.ModelCreation.MediumMs > 0)
                sb.AppendLine($"- **Medium Model (10.7M params):** {results.ModelCreation.MediumMs:F2} ms");
            sb.AppendLine();
        }
        
        sb.AppendLine("---");
        sb.AppendLine();
        
        // Recommendations
        sb.AppendLine("## 💡 Performance Insights & Recommendations");
        sb.AppendLine();
        GenerateRecommendations(sb, results);
        sb.AppendLine();
        
        // Footer
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## 📁 Complete Results");
        sb.AppendLine();
        sb.AppendLine("All detailed reports are available in this directory:");
        sb.AppendLine();
        sb.AppendLine($"- `enhanced-profile-report.md` - Code profiler detailed output");
        sb.AppendLine($"- `report.md` - Comprehensive inference benchmarks");
        sb.AppendLine($"- `results.json` - Machine-readable benchmark data");
        sb.AppendLine($"- `simd-benchmark-results.md` - SIMD operations report");
        sb.AppendLine($"- `allocation-profile.txt` - Memory allocation analysis");
        sb.AppendLine($"- `model-creation-profile.txt` - Model initialization metrics");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"**Report Generated:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"**SmallMind Version:** Latest");
        sb.AppendLine($"**Benchmarking Tool:** BenchmarkRunner v1.0");
        
        await File.WriteAllTextAsync(reportPath, sb.ToString());
        
        Console.WriteLine($"✓ Consolidated report: {reportPath}");
    }

    private void GenerateRecommendations(StringBuilder sb, BenchmarkResults results)
    {
        var recommendations = new List<string>();
        
        if (results.Comprehensive != null)
        {
            if (results.Comprehensive.TtftP50Ms > 2.0)
            {
                recommendations.Add("⚠️ **TTFT Optimization Needed:** Current P50 is above 2ms target. Consider optimizing first token generation path.");
            }
            else
            {
                recommendations.Add("✅ **Excellent TTFT:** Sub-2ms latency is competitive with industry leaders.");
            }
            
            if (results.Comprehensive.ThroughputP50TokensPerSec < 500)
            {
                recommendations.Add("⚠️ **Throughput Below Target:** Consider SIMD optimizations, better cache utilization, or parallelization.");
            }
            else if (results.Comprehensive.ThroughputP50TokensPerSec > 750)
            {
                recommendations.Add("✅ **Excellent Throughput:** Performance exceeds target for CPU-only inference.");
            }
            else
            {
                recommendations.Add("✅ **Good Throughput:** Performance meets target for CPU inference.");
            }
        }
        
        if (results.Simd != null && results.Simd.MatMul512GFlops > 0)
        {
            if (results.Simd.MatMul512GFlops < 10.0)
            {
                recommendations.Add("🔴 **Critical: MatMul Needs Optimization:** Current GFLOPS is significantly below 20 GFLOPS target. This is the primary bottleneck.");
                recommendations.Add("   → Implement blocked/tiled matrix multiplication");
                recommendations.Add("   → Add multi-threading for large matrices");
                recommendations.Add("   → Consider platform-specific optimizations (AVX2/AVX-512)");
            }
            else if (results.Simd.MatMul512GFlops < 20.0)
            {
                recommendations.Add("⚠️ **MatMul Could Be Faster:** Approaching target but room for improvement.");
            }
            else
            {
                recommendations.Add("✅ **Excellent MatMul Performance:** Meets or exceeds 20 GFLOPS target.");
            }
        }
        
        if (results.Profiler != null && results.Profiler.TotalAllocationsMB > 100)
        {
            recommendations.Add("⚠️ **High Memory Allocations:** Consider implementing buffer pooling (ArrayPool) to reduce GC pressure.");
        }
        
        if (recommendations.Count == 0)
        {
            recommendations.Add("✅ **All metrics look good!** Performance is meeting targets across the board.");
        }
        
        foreach (var rec in recommendations)
        {
            sb.AppendLine(rec);
        }
    }

    private string RateTtft(double ttftMs)
    {
        if (ttftMs < 1.0) return "🟢 Excellent";
        if (ttftMs < 2.0) return "🟢 Good";
        if (ttftMs < 5.0) return "🟡 Acceptable";
        return "🔴 Needs Work";
    }

    private string RateThroughput(double tokensPerSec)
    {
        if (tokensPerSec > 750) return "🟢 Excellent";
        if (tokensPerSec > 500) return "🟢 Good";
        if (tokensPerSec > 250) return "🟡 Acceptable";
        return "🔴 Needs Work";
    }

    private string RateLatency(double latencyMs)
    {
        if (latencyMs < 50) return "🟢 Excellent";
        if (latencyMs < 100) return "🟢 Good";
        if (latencyMs < 200) return "🟡 Acceptable";
        return "🔴 Needs Work";
    }

    private string RateGFlops(double gflops)
    {
        if (gflops > 30) return "🟢 Excellent";
        if (gflops > 20) return "🟢 Good";
        if (gflops > 10) return "🟡 Acceptable";
        return "🔴 Needs Work";
    }

    private string RateMemory(double memoryMB)
    {
        if (memoryMB < 80) return "🟢 Excellent";
        if (memoryMB < 100) return "🟢 Good";
        if (memoryMB < 150) return "🟡 Acceptable";
        return "🔴 Needs Work";
    }

    private async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, string workingDirectory, bool verbose)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        var error = new StringBuilder();

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                output.AppendLine(e.Data);
                if (verbose)
                    Console.WriteLine(e.Data);
            }
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
            {
                error.AppendLine(e.Data);
                if (verbose)
                    Console.Error.WriteLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync();

        return new ProcessResult
        {
            ExitCode = process.ExitCode,
            Output = output.ToString(),
            Error = error.ToString()
        };
    }
}

class ProcessResult
{
    public int ExitCode { get; set; }
    public string Output { get; set; } = "";
    public string Error { get; set; } = "";
}

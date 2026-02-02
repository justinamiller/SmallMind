namespace SmallMind.Benchmarks;

/// <summary>
/// Configuration for benchmark runs parsed from CLI arguments.
/// </summary>
public sealed class BenchmarkConfig
{
    public string ModelPath { get; set; } = string.Empty;
    public string Scenario { get; set; } = "all";
    public int Iterations { get; set; } = 30;
    public int Warmup { get; set; } = 5;
    public int[] Concurrency { get; set; } = new[] { 1 };
    public int MaxNewTokens { get; set; } = 256;
    public string PromptProfile { get; set; } = "short";
    public uint Seed { get; set; } = 42;
    public double Temperature { get; set; } = 1.0;
    public int TopK { get; set; } = 1;
    public double TopP { get; set; } = 1.0;
    public int Threads { get; set; } = 0; // 0 = auto
    public string OutputDir { get; set; } = string.Empty;
    public bool EmitJson { get; set; } = true;
    public bool EmitMarkdown { get; set; } = true;
    public bool ColdStart { get; set; } = false;
    public bool ChildRun { get; set; } = false;
    public bool EnableKvCache { get; set; } = false; // Default: disabled for consistency
}

/// <summary>
/// CLI argument parser (no dependencies).
/// </summary>
public static class CliParser
{
    public static BenchmarkConfig Parse(string[] args)
    {
        var config = new BenchmarkConfig();
        
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            string? value = i + 1 < args.Length ? args[i + 1] : null;
            
            switch (arg)
            {
                case "--model":
                    if (value == null) throw new ArgumentException("--model requires a value");
                    config.ModelPath = value;
                    i++;
                    break;
                    
                case "--scenario":
                    if (value == null) throw new ArgumentException("--scenario requires a value");
                    config.Scenario = value.ToLowerInvariant();
                    i++;
                    break;
                    
                case "--iterations":
                    if (value == null) throw new ArgumentException("--iterations requires a value");
                    config.Iterations = int.Parse(value);
                    i++;
                    break;
                    
                case "--warmup":
                    if (value == null) throw new ArgumentException("--warmup requires a value");
                    config.Warmup = int.Parse(value);
                    i++;
                    break;
                    
                case "--concurrency":
                    if (value == null) throw new ArgumentException("--concurrency requires a value");
                    config.Concurrency = ParseConcurrencyList(value);
                    i++;
                    break;
                    
                case "--max-new-tokens":
                    if (value == null) throw new ArgumentException("--max-new-tokens requires a value");
                    config.MaxNewTokens = int.Parse(value);
                    i++;
                    break;
                    
                case "--prompt-profile":
                    if (value == null) throw new ArgumentException("--prompt-profile requires a value");
                    config.PromptProfile = value.ToLowerInvariant();
                    i++;
                    break;
                    
                case "--seed":
                    if (value == null) throw new ArgumentException("--seed requires a value");
                    config.Seed = uint.Parse(value);
                    i++;
                    break;
                    
                case "--temperature":
                    if (value == null) throw new ArgumentException("--temperature requires a value");
                    config.Temperature = double.Parse(value);
                    i++;
                    break;
                    
                case "--topk":
                    if (value == null) throw new ArgumentException("--topk requires a value");
                    config.TopK = int.Parse(value);
                    i++;
                    break;
                    
                case "--topp":
                    if (value == null) throw new ArgumentException("--topp requires a value");
                    config.TopP = double.Parse(value);
                    i++;
                    break;
                    
                case "--threads":
                    if (value == null) throw new ArgumentException("--threads requires a value");
                    config.Threads = int.Parse(value);
                    i++;
                    break;
                    
                case "--output":
                    if (value == null) throw new ArgumentException("--output requires a value");
                    config.OutputDir = value;
                    i++;
                    break;
                    
                case "--json":
                    config.EmitJson = true;
                    break;
                    
                case "--markdown":
                    config.EmitMarkdown = true;
                    break;
                    
                case "--cold":
                    config.ColdStart = true;
                    break;
                    
                case "--child-run":
                    config.ChildRun = true;
                    break;
                    
                case "--enable-kv-cache":
                    config.EnableKvCache = true;
                    break;
                    
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                    
                default:
                    throw new ArgumentException($"Unknown argument: {arg}");
            }
        }
        
        // Validation
        if (string.IsNullOrEmpty(config.ModelPath) && !config.ChildRun)
        {
            throw new ArgumentException("--model is required");
        }
        
        // Set default output directory
        if (string.IsNullOrEmpty(config.OutputDir))
        {
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            config.OutputDir = Path.Combine("benchmarks", "results", timestamp);
        }
        
        return config;
    }
    
    private static int[] ParseConcurrencyList(string value)
    {
        string[] parts = value.Split(',');
        int[] result = new int[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            result[i] = int.Parse(parts[i].Trim());
        }
        return result;
    }
    
    private static void PrintHelp()
    {
        Console.WriteLine(@"SmallMind Benchmarking Harness

Usage: smallmind-bench --model <path> [OPTIONS]

Required:
  --model <path>              Path to model file

Options:
  --scenario <name|all>       Scenario to run (default: all)
                              Options: all, ttft, tokens_per_sec, latency, 
                                       concurrency, memory, gc, env
  --iterations <n>            Number of iterations (default: 30)
  --warmup <n>                Warmup iterations (default: 5)
  --concurrency <list>        Concurrency levels (default: 1)
                              Example: --concurrency 1,2,4,8,16
  --max-new-tokens <n>        Max tokens to generate (default: 256)
  --prompt-profile <name>     Prompt profile (default: short)
                              Options: short, med, long
  --seed <n>                  Random seed (default: 42)
  --temperature <float>       Temperature (default: 1.0)
  --topk <n>                  Top-K sampling (default: 1)
  --topp <float>              Top-P sampling (default: 1.0)
  --threads <n>               Thread count (default: auto)
  --output <dir>              Output directory (default: ./benchmarks/results/<timestamp>)
  --json                      Emit JSON report (default: true)
  --markdown                  Emit Markdown report (default: true)
  --cold                      Run in cold-start mode
  --enable-kv-cache           Enable KV cache (default: false for consistency)
  --child-run                 Internal: run as child process
  --help, -h                  Show this help

Examples:
  # Run all scenarios
  smallmind-bench --model model.smq --scenario all

  # Run specific scenario with custom iterations
  smallmind-bench --model model.smq --scenario ttft --iterations 50

  # Run concurrency test
  smallmind-bench --model model.smq --scenario concurrency --concurrency 1,2,4,8,16

  # Cold start mode
  smallmind-bench --model model.smq --scenario ttft --cold --iterations 10
");
    }
}

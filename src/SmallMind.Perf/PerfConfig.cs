namespace SmallMind.Perf;

/// <summary>
/// Configuration for performance benchmarks.
/// </summary>
public sealed class PerfConfig
{
    public int WarmupIterations { get; set; } = 3;
    public int MeasuredIterations { get; set; } = 10;
    public string BenchmarkName { get; set; } = "all";
    public bool JsonOutput { get; set; } = false;
    public bool FastMode { get; set; } = false;
    public int Seed { get; set; } = 42;

    public static PerfConfig Parse(string[] args)
    {
        var config = new PerfConfig();
        
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--warmup" when i + 1 < args.Length:
                    config.WarmupIterations = int.Parse(args[++i]);
                    break;
                case "--iters" when i + 1 < args.Length:
                    config.MeasuredIterations = int.Parse(args[++i]);
                    break;
                case "--bench" when i + 1 < args.Length:
                    config.BenchmarkName = args[++i];
                    break;
                case "--json":
                    config.JsonOutput = true;
                    break;
                case "--fast":
                    config.FastMode = true;
                    config.WarmupIterations = 1;
                    config.MeasuredIterations = 3;
                    break;
                case "--seed" when i + 1 < args.Length:
                    config.Seed = int.Parse(args[++i]);
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }
        
        return config;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("SmallMind.Perf - Deterministic Performance Microbenchmarks");
        Console.WriteLine();
        Console.WriteLine("Usage: SmallMind.Perf [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --warmup N      Number of warmup iterations (default: 3)");
        Console.WriteLine("  --iters M       Number of measured iterations (default: 10)");
        Console.WriteLine("  --bench <name>  Benchmark to run: all|matmul|attention|layernorm|softmax|kvcache (default: all)");
        Console.WriteLine("  --json          Output results in JSON format");
        Console.WriteLine("  --fast          Fast mode for CI (--warmup 1 --iters 3)");
        Console.WriteLine("  --seed N        Random seed for determinism (default: 42)");
        Console.WriteLine("  --help, -h      Show this help");
    }
}

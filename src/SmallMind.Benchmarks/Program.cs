using System;
using System.Collections.Generic;

namespace SmallMind.Benchmarks
{
    internal class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                var options = ParseArgs(args);

                if (options.ShowHelp)
                {
                    ShowHelp();
                    return 0;
                }

                // Check if user wants GEMM benchmark
                if (options.BenchmarkType == "gemm")
                {
                    GemmBenchmark.Run();
                    return 0;
                }

                // Default: run existing Q4 benchmarks (kernel-only)
                var config = new BenchmarkConfig
                {
                    WarmupIterations = options.WarmupIterations,
                    MeasuredIterations = options.MeasuredIterations,
                    Seed = options.Seed,
                    OutputDirectory = options.OutputDir,
                    OutputFormats = options.Formats
                };

                var runner = new BenchmarkRunner(config);
                runner.RunAll();

                return 0;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Benchmark failed: {ex.Message}");
                Console.Error.WriteLine(ex.StackTrace);
                Console.ResetColor();
                return 1;
            }
        }

        private static BenchmarkOptions ParseArgs(string[] args)
        {
            var options = new BenchmarkOptions();

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                switch (arg.ToLowerInvariant())
                {
                    case "--help":
                    case "-h":
                        options.ShowHelp = true;
                        break;

                    case "--warmup":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var warmup))
                        {
                            options.WarmupIterations = warmup;
                            i++;
                        }
                        break;

                    case "--iterations":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var iters))
                        {
                            options.MeasuredIterations = iters;
                            i++;
                        }
                        break;

                    case "--seed":
                        if (i + 1 < args.Length && int.TryParse(args[i + 1], out var seed))
                        {
                            options.Seed = seed;
                            i++;
                        }
                        break;

                    case "--output-dir":
                        if (i + 1 < args.Length)
                        {
                            options.OutputDir = args[i + 1];
                            i++;
                        }
                        break;

                    case "--format":
                        if (i + 1 < args.Length)
                        {
                            var formats = args[i + 1].Split(',');
                            options.Formats = new List<string>(formats);
                            i++;
                        }
                        break;

                    case "gemm":
                        options.BenchmarkType = "gemm";
                        break;
                }
            }

            return options;
        }

        private static void ShowHelp()
        {
            Console.WriteLine("SmallMind.Benchmarks - CPU LLM Performance Benchmark Suite");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("  dotnet run -- [OPTIONS]");
            Console.WriteLine();
            Console.WriteLine("OPTIONS:");
            Console.WriteLine("  --help, -h              Show this help message");
            Console.WriteLine("  --warmup <N>            Number of warmup iterations (default: 5)");
            Console.WriteLine("  --iterations <N>        Number of measured iterations (default: 20)");
            Console.WriteLine("  --seed <N>              Random seed for determinism (default: 42)");
            Console.WriteLine("  --output-dir <path>     Output directory for results (default: .)");
            Console.WriteLine("  --format <fmt>          Output formats: json,markdown (default: markdown)");
            Console.WriteLine();
            Console.WriteLine("BENCHMARK TYPES:");
            Console.WriteLine("  (default)               Run kernel benchmarks (MatMul, Q4, etc.)");
            Console.WriteLine("  gemm                    Run GEMM-specific benchmarks");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  dotnet run -- --warmup 10 --iterations 50");
            Console.WriteLine("  dotnet run -- --output-dir ./results --format json,markdown");
            Console.WriteLine("  dotnet run -- gemm");
            Console.WriteLine();
        }
    }

    internal class BenchmarkOptions
    {
        public bool ShowHelp { get; set; }
        public string BenchmarkType { get; set; } = "kernel";
        public int WarmupIterations { get; set; } = 5;
        public int MeasuredIterations { get; set; } = 20;
        public int Seed { get; set; } = 42;
        public string OutputDir { get; set; } = ".";
        public List<string> Formats { get; set; } = new List<string> { "markdown" };
    }
}

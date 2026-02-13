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
                if (args.Length == 0 || args[0].Equals("help", StringComparison.OrdinalIgnoreCase) || 
                    args[0].Equals("--help", StringComparison.OrdinalIgnoreCase) || args[0].Equals("-h", StringComparison.OrdinalIgnoreCase))
                {
                    PrintHelp();
                    return 0;
                }

                var command = args[0].ToLowerInvariant();

                switch (command)
                {
                    case "gemm":
                        GemmBenchmark.Run();
                        return 0;

                    case "run":
                        return RunModelBenchmark(args);

                    case "matmul":
                    case "default":
                        return RunDefaultBenchmarks(args);

                    default:
                        Console.Error.WriteLine($"Unknown command: {command}");
                        PrintHelp();
                        return 1;
                }
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

        private static void PrintHelp()
        {
            Console.WriteLine("SmallMind.Benchmarks - CPU-only LLM Performance Benchmarking");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("  SmallMind.Benchmarks [command] [options]");
            Console.WriteLine();
            Console.WriteLine("COMMANDS:");
            Console.WriteLine("  run        Run real-model inference benchmark");
            Console.WriteLine("  matmul     Run matrix multiplication benchmarks (default)");
            Console.WriteLine("  gemm       Run GEMM-specific benchmarks");
            Console.WriteLine("  help       Show this help message");
            Console.WriteLine();
            Console.WriteLine("OPTIONS FOR 'run' COMMAND:");
            Console.WriteLine("  --model <path>      Path to model file (.smq or .gguf)");
            Console.WriteLine("  --tokens <n>        Number of tokens to generate (default: 128)");
            Console.WriteLine("  --prompt <text>     Prompt text (default: 'The future of AI is')");
            Console.WriteLine("  --warmup <n>        Warmup iterations (default: 3)");
            Console.WriteLine("  --iterations <n>    Measured iterations (default: 10)");
            Console.WriteLine("  --output <path>     Output directory for results (default: artifacts/perf)");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  # Run default MatMul benchmarks");
            Console.WriteLine("  SmallMind.Benchmarks");
            Console.WriteLine();
            Console.WriteLine("  # Run model inference benchmark");
            Console.WriteLine("  SmallMind.Benchmarks run --model model.gguf --tokens 256");
            Console.WriteLine();
            Console.WriteLine("  # Run GEMM benchmarks");
            Console.WriteLine("  SmallMind.Benchmarks gemm");
        }

        private static int RunModelBenchmark(string[] args)
        {
            var options = ParseModelBenchmarkOptions(args);
            
            if (string.IsNullOrEmpty(options.ModelPath))
            {
                Console.Error.WriteLine("Error: --model option is required for 'run' command");
                PrintHelp();
                return 1;
            }

            var runner = new RealModelBenchmarkRunner(options);
            runner.Run();
            return 0;
        }

        private static int RunDefaultBenchmarks(string[] args)
        {
            var config = new BenchmarkConfig
            {
                WarmupIterations = 5,
                MeasuredIterations = 20,
                Seed = 42
            };

            var runner = new BenchmarkRunner(config);
            runner.RunAll();
            return 0;
        }

        private static RealModelBenchmarkOptions ParseModelBenchmarkOptions(string[] args)
        {
            // Use relative path that works on all platforms
            var defaultOutputDir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts", "perf");
            
            var options = new RealModelBenchmarkOptions
            {
                Tokens = 128,
                Prompt = "The future of AI is",
                WarmupIterations = 3,
                MeasuredIterations = 10,
                OutputDirectory = defaultOutputDir
            };

            for (int i = 1; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.StartsWith("--"))
                {
                    var key = arg.Substring(2).ToLowerInvariant();
                    var value = i + 1 < args.Length ? args[i + 1] : null;

                    switch (key)
                    {
                        case "model":
                            options.ModelPath = value;
                            i++;
                            break;
                        case "tokens":
                            if (int.TryParse(value, out var tokens))
                                options.Tokens = tokens;
                            i++;
                            break;
                        case "prompt":
                            options.Prompt = value ?? "";
                            i++;
                            break;
                        case "warmup":
                            if (int.TryParse(value, out var warmup))
                                options.WarmupIterations = warmup;
                            i++;
                            break;
                        case "iterations":
                            if (int.TryParse(value, out var iterations))
                                options.MeasuredIterations = iterations;
                            i++;
                            break;
                        case "output":
                            options.OutputDirectory = value;
                            i++;
                            break;
                    }
                }
            }

            return options;
        }
    }
}

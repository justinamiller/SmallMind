using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Benchmarks.Benchmarks;
using SmallMind.Benchmarks.Diagnostics;
using SmallMind.Benchmarks.Reporting;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// SmallMind Benchmarks - Comprehensive runtime/engine performance benchmarking.
    /// </summary>
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║     SmallMind Benchmarks - Runtime/Engine Metrics     ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            try
            {
                var config = ParseArgs(args);
                if (config == null)
                {
                    PrintUsage();
                    return 1;
                }

                // Validate model exists
                if (!File.Exists(config.ModelPath))
                {
                    Console.WriteLine($"❌ Error: Model file not found: {config.ModelPath}");
                    return 1;
                }

                // Run benchmarks
                var runner = new BenchmarkRunner(config);
                var results = await runner.RunAllAsync(CancellationToken.None);

                // Generate reports
                GenerateReports(results, config);

                // Print summary
                PrintSummary(results);

                return results.Status == "Success" ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Fatal error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        private static BenchmarkConfig? ParseArgs(string[] args)
        {
            if (args.Length == 0)
                return null;

            var config = new BenchmarkConfig
            {
                WarmupIterations = 5,
                MeasuredIterations = 20,
                ConcurrentStreams = 10,
                MaxTokensPerRequest = 100,
                ContextSize = 2048,
                EnableKVCache = true,
                SoakDuration = TimeSpan.FromMinutes(1),
                CIMode = false,
                OutputPath = "./benchmark-results",
                OutputFormat = BenchmarkOutputFormat.All
            };

            for (int i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                switch (arg)
                {
                    case "--model":
                        if (i + 1 < args.Length)
                            config.ModelPath = args[++i];
                        break;

                    case "--warmup":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int warmup))
                            config.WarmupIterations = warmup;
                        break;

                    case "--iterations":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int iterations))
                            config.MeasuredIterations = iterations;
                        break;

                    case "--concurrent":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int concurrent))
                            config.ConcurrentStreams = concurrent;
                        break;

                    case "--max-tokens":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int maxTokens))
                            config.MaxTokensPerRequest = maxTokens;
                        break;

                    case "--context-size":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int contextSize))
                            config.ContextSize = contextSize;
                        break;

                    case "--no-kv-cache":
                        config.EnableKVCache = false;
                        break;

                    case "--soak-duration":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int minutes))
                            config.SoakDuration = TimeSpan.FromMinutes(minutes);
                        break;

                    case "--ci":
                        config.CIMode = true;
                        break;

                    case "--output":
                        if (i + 1 < args.Length)
                            config.OutputPath = args[++i];
                        break;

                    case "--format":
                        if (i + 1 < args.Length)
                        {
                            var format = ParseOutputFormat(args[++i]);
                            config.OutputFormat = format;
                        }
                        break;

                    case "--help":
                    case "-h":
                        return null;
                }
            }

            return string.IsNullOrEmpty(config.ModelPath) ? null : config;
        }

        private static BenchmarkOutputFormat ParseOutputFormat(string format)
        {
            var result = BenchmarkOutputFormat.None;

            foreach (var part in format.Split(','))
            {
                switch (part.Trim().ToLowerInvariant())
                {
                    case "json":
                        result |= BenchmarkOutputFormat.Json;
                        break;
                    case "markdown":
                    case "md":
                        result |= BenchmarkOutputFormat.Markdown;
                        break;
                    case "csv":
                        result |= BenchmarkOutputFormat.Csv;
                        break;
                    case "all":
                        result = BenchmarkOutputFormat.All;
                        break;
                }
            }

            return result == BenchmarkOutputFormat.None ? BenchmarkOutputFormat.All : result;
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: dotnet run --project SmallMind.Benchmarks.csproj -- [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --model <path>          Path to model file (.smq or .gguf) [REQUIRED]");
            Console.WriteLine("  --warmup <n>            Number of warmup iterations (default: 5)");
            Console.WriteLine("  --iterations <n>        Number of measured iterations (default: 20)");
            Console.WriteLine("  --concurrent <n>        Number of concurrent streams (default: 10)");
            Console.WriteLine("  --max-tokens <n>        Max tokens per request (default: 100)");
            Console.WriteLine("  --context-size <n>      Context window size (default: 2048)");
            Console.WriteLine("  --no-kv-cache           Disable KV cache");
            Console.WriteLine("  --soak-duration <min>   Soak test duration in minutes (default: 1)");
            Console.WriteLine("  --ci                    CI mode (shorter, deterministic)");
            Console.WriteLine("  --output <path>         Output directory (default: ./benchmark-results)");
            Console.WriteLine("  --format <fmt>          Output format: json,markdown,csv,all (default: all)");
            Console.WriteLine("  --help, -h              Show this help");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  dotnet run -- --model model.smq");
            Console.WriteLine("  dotnet run -- --model model.gguf --iterations 50 --concurrent 20");
            Console.WriteLine("  dotnet run -- --model model.smq --ci --format json,markdown");
        }

        private static void GenerateReports(BenchmarkResults results, BenchmarkConfig config)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var basePath = Path.Combine(config.OutputPath, timestamp);

            Directory.CreateDirectory(basePath);

            if (config.OutputFormat.HasFlag(BenchmarkOutputFormat.Json))
            {
                var jsonPath = Path.Combine(basePath, "results.json");
                JsonReportGenerator.GenerateReport(results, jsonPath);
            }

            if (config.OutputFormat.HasFlag(BenchmarkOutputFormat.Markdown))
            {
                var mdPath = Path.Combine(basePath, "results.md");
                MarkdownReportGenerator.GenerateReport(results, mdPath);
            }

            if (config.OutputFormat.HasFlag(BenchmarkOutputFormat.Csv))
            {
                var csvPath = Path.Combine(basePath, "results.csv");
                CsvReportGenerator.GenerateReport(results, csvPath);
            }

            // Also generate "latest" symlinks
            var latestPath = Path.Combine(config.OutputPath, "latest");
            Directory.CreateDirectory(latestPath);

            if (config.OutputFormat.HasFlag(BenchmarkOutputFormat.Json))
            {
                var jsonPath = Path.Combine(latestPath, "results.json");
                JsonReportGenerator.GenerateReport(results, jsonPath);
            }

            if (config.OutputFormat.HasFlag(BenchmarkOutputFormat.Markdown))
            {
                var mdPath = Path.Combine(latestPath, "results.md");
                MarkdownReportGenerator.GenerateReport(results, mdPath);
            }

            if (config.OutputFormat.HasFlag(BenchmarkOutputFormat.Csv))
            {
                var csvPath = Path.Combine(latestPath, "results.csv");
                CsvReportGenerator.GenerateReport(results, csvPath);
            }
        }

        private static void PrintSummary(BenchmarkResults results)
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                  Benchmark Summary                     ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine($"Status: {results.Status}");
            Console.WriteLine($"Duration: {results.TotalDuration.TotalSeconds:F2}s");
            Console.WriteLine($"Metrics Collected: {results.Metrics.Count}");

            if (results.Errors.Count > 0)
            {
                Console.WriteLine($"Errors: {results.Errors.Count}");
                foreach (var error in results.Errors)
                {
                    Console.WriteLine($"  ❌ {error}");
                }
            }

            if (results.Warnings.Count > 0)
            {
                Console.WriteLine($"Warnings: {results.Warnings.Count}");
                foreach (var warning in results.Warnings)
                {
                    Console.WriteLine($"  ⚠️ {warning}");
                }
            }

            Console.WriteLine();
        }
    }
}

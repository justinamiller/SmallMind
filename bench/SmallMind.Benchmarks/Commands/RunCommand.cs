using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmallMind.Benchmarks.Core.Environment;
using SmallMind.Benchmarks.Core.Measurement;
using SmallMind.Benchmarks.Core.Models;
using SmallMind.Benchmarks.Core.Normalization;
using SmallMind.Benchmarks.Core.Output;
using SmallMind.Benchmarks.Options;

namespace SmallMind.Benchmarks.Commands
{
    internal sealed class RunCommand
    {
        public async Task<int> ExecuteAsync(string[] args)
        {
            var parser = new CommandLineParser(args);

            if (parser.HasOption("help") || parser.HasOption("h"))
            {
                ShowUsage();
                return 0;
            }

            try
            {
                var config = ParseConfiguration(parser);
                ValidateConfiguration(config);

                Console.WriteLine("SmallMind Benchmarks - Run");
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine();

                // Capture environment
                Console.WriteLine("Capturing environment information...");
                var envSnapshot = SystemInfo.CaptureSnapshot();
                PrintEnvironmentInfo(envSnapshot);
                Console.WriteLine();

                // Resolve models
                var scenarios = await ResolveScenarios(config);
                if (scenarios.Count == 0)
                {
                    Console.Error.WriteLine("No scenarios to run");
                    return 1;
                }

                Console.WriteLine($"Created {scenarios.Count} benchmark scenario(s)");
                Console.WriteLine();

                // Run benchmarks
                var results = new List<BenchmarkResult>();
                var startTime = DateTime.UtcNow;

                using var harness = new BenchmarkHarness();

                for (int i = 0; i < scenarios.Count; i++)
                {
                    var scenario = scenarios[i];
                    Console.WriteLine($"[{i + 1}/{scenarios.Count}] Running: {scenario.ScenarioName}");
                    Console.WriteLine($"  Model: {scenario.ModelPath}");
                    Console.WriteLine($"  Context: {scenario.ContextSize}, Threads: {scenario.ThreadCount}");
                    Console.WriteLine($"  Warmup: {scenario.WarmupIterations}, Iterations: {scenario.MeasuredIterations}");

                    try
                    {
                        var result = await harness.RunScenarioAsync(scenario, CancellationToken.None);
                        results.Add(result);

                        Console.WriteLine($"  ✅ Complete - {result.TokensPerSecond:F2} tok/s (TTFT: {result.TTFTMilliseconds:F1}ms)");
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"  ❌ Failed: {ex.Message}");
                        if (config.ContinueOnError)
                        {
                            Console.WriteLine("  Continuing to next scenario...");
                            continue;
                        }
                        return 1;
                    }

                    Console.WriteLine();
                }

                var endTime = DateTime.UtcNow;

                // Calculate normalized metrics
                var normalizedResults = NormalizationCalculator.CalculateNormalized(results.ToArray(), envSnapshot);

                // Create run results
                var runResults = new BenchmarkRunResults
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    GitCommitSha = envSnapshot.GitCommitSha ?? "unknown",
                    Environment = envSnapshot,
                    Results = results.ToArray(),
                    NormalizedResults = normalizedResults
                };

                // Write outputs
                WriteOutputs(config, runResults, envSnapshot);

                Console.WriteLine();
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine($"Benchmark complete! {results.Count} scenario(s) completed in {(endTime - startTime).TotalMinutes:F1} minutes");

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        private RunConfiguration ParseConfiguration(CommandLineParser parser)
        {
            var config = new RunConfiguration
            {
                ModelName = parser.GetOption("model"),
                ManifestPath = parser.GetOption("manifest", "../models/models.manifest.json"),
                CiOnly = parser.GetOptionBool("ci-only"),
                ContextSizes = parser.GetOptionIntArray("context"),
                ThreadCounts = parser.GetOptionArray("threads"),
                WarmupIterations = parser.GetOptionInt("warmup", 3),
                MeasuredIterations = parser.GetOptionInt("iterations", 5),
                TokensToGenerate = parser.GetOptionInt("tokens", 100),
                OutputDirectory = parser.GetOption("output", "../results"),
                OutputFormat = parser.GetOption("format", "all"),
                PromptText = parser.GetOption("prompt", "Once upon a time"),
                ContinueOnError = parser.GetOptionBool("continue-on-error", false),
                CacheDirectory = parser.GetOption("cache")
            };

            // Default context sizes if not specified
            if (config.ContextSizes.Length == 0)
            {
                config.ContextSizes = new[] { 1024 };
            }

            // Default thread counts if not specified
            if (config.ThreadCounts.Length == 0)
            {
                config.ThreadCounts = new[] { "max" };
            }

            return config;
        }

        private void ValidateConfiguration(RunConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config.ModelName) && !File.Exists(config.ManifestPath))
            {
                throw new InvalidOperationException($"Manifest not found: {config.ManifestPath}");
            }

            if (config.WarmupIterations < 0)
            {
                throw new InvalidOperationException("Warmup iterations cannot be negative");
            }

            if (config.MeasuredIterations <= 0)
            {
                throw new InvalidOperationException("Measured iterations must be greater than 0");
            }

            if (config.TokensToGenerate <= 0)
            {
                throw new InvalidOperationException("Tokens to generate must be greater than 0");
            }
        }

        private async Task<List<BenchmarkScenario>> ResolveScenarios(RunConfiguration config)
        {
            var scenarios = new List<BenchmarkScenario>();

            // Determine models to benchmark
            var modelPaths = new List<(string Name, string Path)>();

            if (!string.IsNullOrWhiteSpace(config.ModelName))
            {
                // Direct path or manifest name
                if (File.Exists(config.ModelName))
                {
                    // Direct GGUF file path
                    modelPaths.Add((Path.GetFileNameWithoutExtension(config.ModelName), config.ModelName));
                }
                else
                {
                    // Try to resolve from manifest
                    var manifest = ModelDownloader.LoadManifest(config.ManifestPath);
                    var entry = manifest.Models.FirstOrDefault(m => 
                        m.Name.Equals(config.ModelName, StringComparison.OrdinalIgnoreCase) ||
                        m.DisplayName.Equals(config.ModelName, StringComparison.OrdinalIgnoreCase));

                    if (entry == null)
                    {
                        throw new InvalidOperationException($"Model not found in manifest: {config.ModelName}");
                    }

                    var downloader = new ModelDownloader(config.CacheDirectory);
                    string modelPath = await downloader.DownloadModelAsync(entry);
                    modelPaths.Add((entry.Name, modelPath));
                }
            }
            else
            {
                // Use all models from manifest (optionally filtered by CI flag)
                var manifest = ModelDownloader.LoadManifest(config.ManifestPath);
                var entries = config.CiOnly 
                    ? manifest.Models.Where(m => m.CI).ToArray()
                    : manifest.Models;

                if (entries.Length == 0)
                {
                    throw new InvalidOperationException("No models found in manifest matching criteria");
                }

                var downloader = new ModelDownloader(config.CacheDirectory);

                foreach (var entry in entries)
                {
                    Console.WriteLine($"Resolving model: {entry.DisplayName}");
                    string modelPath = await downloader.DownloadModelAsync(entry);
                    modelPaths.Add((entry.Name, modelPath));
                }
            }

            // Create scenarios for each combination of model x context x threads
            foreach (var (modelName, modelPath) in modelPaths)
            {
                foreach (int contextSize in config.ContextSizes)
                {
                    foreach (string threadCountStr in config.ThreadCounts)
                    {
                        int threadCount;
                        if (threadCountStr.Equals("max", StringComparison.OrdinalIgnoreCase))
                        {
                            threadCount = 0; // 0 means auto-detect
                        }
                        else if (!int.TryParse(threadCountStr, out threadCount))
                        {
                            Console.WriteLine($"Warning: Invalid thread count '{threadCountStr}', using max");
                            threadCount = 0;
                        }

                        var scenario = new BenchmarkScenario
                        {
                            ScenarioName = $"{modelName}_ctx{contextSize}_t{(threadCount == 0 ? "max" : threadCount.ToString())}",
                            ModelPath = modelPath,
                            ContextSize = contextSize,
                            ThreadCount = threadCount,
                            PromptText = config.PromptText,
                            NumTokensToGenerate = config.TokensToGenerate,
                            WarmupIterations = config.WarmupIterations,
                            MeasuredIterations = config.MeasuredIterations,
                            Temperature = 0.8,
                            TopK = 40,
                            TopP = 0.95,
                            Seed = 42,
                            CacheDirectory = config.CacheDirectory
                        };

                        scenarios.Add(scenario);
                    }
                }
            }

            return scenarios;
        }

        private void PrintEnvironmentInfo(EnvironmentSnapshot env)
        {
            Console.WriteLine($"OS:       {env.OperatingSystem}");
            Console.WriteLine($"Runtime:  {env.RuntimeVersion}");
            Console.WriteLine($"CPU:      {env.CpuModel}");
            Console.WriteLine($"Cores:    {env.LogicalCores}");
            
            if (env.CpuMaxFrequencyMhz.HasValue)
            {
                Console.WriteLine($"CPU Freq: {env.CpuMaxFrequencyMhz.Value:F0} MHz");
            }
            
            Console.WriteLine($"SIMD:     {string.Join(", ", env.SimdCapabilities)}");
            Console.WriteLine($"GC:       {(env.IsServerGC ? "Server" : "Workstation")}, {env.GCLatencyMode}");
            
            if (!string.IsNullOrEmpty(env.GitCommitSha))
            {
                Console.WriteLine($"Git SHA:  {env.GitCommitSha}");
            }
        }

        private void WriteOutputs(RunConfiguration config, BenchmarkRunResults runResults, EnvironmentSnapshot env)
        {
            Directory.CreateDirectory(config.OutputDirectory);

            // Generate filename: timestamp_gitsha_os_arch
            string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string gitSha = (env.GitCommitSha ?? "unknown").Substring(0, Math.Min(8, (env.GitCommitSha ?? "unknown").Length));
            string os = GetOsShortName();
            string arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
            string baseFilename = $"{timestamp}_{gitSha}_{os}_{arch}";

            var formats = ParseOutputFormats(config.OutputFormat);

            foreach (var format in formats)
            {
                string filename = $"{baseFilename}.{format}";
                string outputPath = Path.Combine(config.OutputDirectory, filename);

                try
                {
                    switch (format)
                    {
                        case "json":
                            JsonOutputWriter.WriteToFile(runResults, outputPath);
                            Console.WriteLine($"📄 JSON output: {outputPath}");
                            break;

                        case "md":
                        case "markdown":
                            MarkdownOutputWriter.WriteToFile(runResults, outputPath);
                            Console.WriteLine($"📄 Markdown output: {outputPath}");
                            break;

                        case "csv":
                            CsvOutputWriter.WriteToFile(runResults, outputPath);
                            Console.WriteLine($"📄 CSV output: {outputPath}");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: Failed to write {format} output: {ex.Message}");
                }
            }
        }

        private string[] ParseOutputFormats(string formatString)
        {
            if (formatString.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                return new[] { "json", "md", "csv" };
            }

            return formatString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                              .Select(f => f.Trim().ToLowerInvariant())
                              .ToArray();
        }

        private string GetOsShortName()
        {
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                return "linux";
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                return "macos";
            if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                return "windows";
            return "unknown";
        }

        private void ShowUsage()
        {
            Console.WriteLine("Usage: SmallMind.Benchmarks run [options]");
            Console.WriteLine();
            Console.WriteLine("Run benchmarks with specified configuration");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --model <name|path>       Model name from manifest or direct path to GGUF");
            Console.WriteLine("  --manifest <path>         Path to manifest (default: ../models/models.manifest.json)");
            Console.WriteLine("  --ci-only                 Run only CI models from manifest");
            Console.WriteLine("  --context <sizes>         Context sizes to test (comma-separated, e.g., \"256,1024,4096\")");
            Console.WriteLine("  --threads <counts>        Thread counts to test (comma-separated, e.g., \"1,4,8,max\")");
            Console.WriteLine("  --warmup <n>              Warmup iterations (default: 3)");
            Console.WriteLine("  --iterations <n>          Measured iterations (default: 5)");
            Console.WriteLine("  --tokens <n>              Tokens to generate (default: 100)");
            Console.WriteLine("  --prompt <text>           Prompt text (default: \"Once upon a time\")");
            Console.WriteLine("  --output <dir>            Output directory (default: ../results)");
            Console.WriteLine("  --format <fmt>            Output formats: json,markdown,csv,all (default: all)");
            Console.WriteLine("  --cache <dir>             Model cache directory");
            Console.WriteLine("  --continue-on-error       Continue running remaining scenarios on error");
            Console.WriteLine("  --help, -h                Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  # Run all CI models");
            Console.WriteLine("  SmallMind.Benchmarks run --ci-only");
            Console.WriteLine();
            Console.WriteLine("  # Run specific model with custom context sizes");
            Console.WriteLine("  SmallMind.Benchmarks run --model tinyllama-1.1b-q4 --context 512,1024,2048");
            Console.WriteLine();
            Console.WriteLine("  # Run with different thread counts");
            Console.WriteLine("  SmallMind.Benchmarks run --model my-model.gguf --threads 1,4,8,max");
        }

        private sealed class RunConfiguration
        {
            public string? ModelName { get; set; }
            public string ManifestPath { get; set; } = string.Empty;
            public bool CiOnly { get; set; }
            public int[] ContextSizes { get; set; } = Array.Empty<int>();
            public string[] ThreadCounts { get; set; } = Array.Empty<string>();
            public int WarmupIterations { get; set; }
            public int MeasuredIterations { get; set; }
            public int TokensToGenerate { get; set; }
            public string PromptText { get; set; } = string.Empty;
            public string OutputDirectory { get; set; } = string.Empty;
            public string OutputFormat { get; set; } = string.Empty;
            public string? CacheDirectory { get; set; }
            public bool ContinueOnError { get; set; }
        }
    }
}

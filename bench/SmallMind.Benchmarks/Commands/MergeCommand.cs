using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SmallMind.Benchmarks.Core.Measurement;
using SmallMind.Benchmarks.Core.Output;
using SmallMind.Benchmarks.Options;

namespace SmallMind.Benchmarks.Commands
{
    internal sealed class MergeCommand
    {
        public Task<int> ExecuteAsync(string[] args)
        {
            var parser = new CommandLineParser(args);

            if (parser.HasOption("help") || parser.HasOption("h"))
            {
                ShowUsage();
                return Task.FromResult(0);
            }

            try
            {
                var config = ParseConfiguration(parser);
                ValidateConfiguration(config);

                Console.WriteLine("SmallMind Benchmarks - Merge Results");
                Console.WriteLine("=".PadRight(80, '='));
                Console.WriteLine();

                // Find input files
                var inputFiles = ResolveInputFiles(config.InputPattern);
                
                if (inputFiles.Count == 0)
                {
                    Console.WriteLine($"No files found matching pattern: {config.InputPattern}");
                    return Task.FromResult(1);
                }

                Console.WriteLine($"Found {inputFiles.Count} result file(s):");
                foreach (var file in inputFiles)
                {
                    Console.WriteLine($"  - {file}");
                }
                Console.WriteLine();

                // Load and merge results
                var allResults = new List<BenchmarkResult>();
                var environments = new List<Core.Environment.EnvironmentSnapshot>();

                foreach (var file in inputFiles)
                {
                    try
                    {
                        var runResults = JsonOutputWriter.ReadFromFile(file);
                        if (runResults != null)
                        {
                            allResults.AddRange(runResults.Results);
                            if (runResults.Environment != null)
                            {
                                environments.Add(runResults.Environment);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Warning: Failed to load {file}: {ex.Message}");
                    }
                }

                if (allResults.Count == 0)
                {
                    Console.WriteLine("No valid results found in input files");
                    return Task.FromResult(1);
                }

                Console.WriteLine($"Loaded {allResults.Count} benchmark result(s) from {inputFiles.Count} file(s)");
                Console.WriteLine();

                // Group results and generate merged table
                string mergedOutput = GenerateMergedMarkdown(allResults, environments);

                // Write output
                Directory.CreateDirectory(Path.GetDirectoryName(config.OutputFile) ?? ".");
                File.WriteAllText(config.OutputFile, mergedOutput);

                Console.WriteLine($"✅ Merged results written to: {config.OutputFile}");

                return Task.FromResult(0);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return Task.FromResult(1);
            }
        }

        private MergeConfiguration ParseConfiguration(CommandLineParser parser)
        {
            return new MergeConfiguration
            {
                InputPattern = parser.GetOption("input", "results/*.json"),
                OutputFile = parser.GetOption("output", "merged_results.md")
            };
        }

        private void ValidateConfiguration(MergeConfiguration config)
        {
            if (string.IsNullOrWhiteSpace(config.InputPattern))
            {
                throw new InvalidOperationException("Input pattern cannot be empty");
            }

            if (string.IsNullOrWhiteSpace(config.OutputFile))
            {
                throw new InvalidOperationException("Output file cannot be empty");
            }
        }

        private List<string> ResolveInputFiles(string pattern)
        {
            var files = new List<string>();

            // Check if pattern contains wildcards
            if (pattern.Contains('*') || pattern.Contains('?'))
            {
                // Extract directory and file pattern
                string directory = Path.GetDirectoryName(pattern) ?? ".";
                string filePattern = Path.GetFileName(pattern);

                if (!Directory.Exists(directory))
                {
                    return files;
                }

                files.AddRange(Directory.GetFiles(directory, filePattern, SearchOption.TopDirectoryOnly));
            }
            else
            {
                // Direct file path
                if (File.Exists(pattern))
                {
                    files.Add(pattern);
                }
            }

            return files.OrderBy(f => f).ToList();
        }

        private string GenerateMergedMarkdown(List<BenchmarkResult> results, List<Core.Environment.EnvironmentSnapshot> environments)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# SmallMind Merged Benchmark Results");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
            sb.AppendLine($"**Total Results:** {results.Count}  ");
            sb.AppendLine($"**Environments:** {environments.Count}  ");
            sb.AppendLine();

            // Environment summary
            if (environments.Count > 0)
            {
                sb.AppendLine("## Environments");
                sb.AppendLine();

                var uniqueEnvs = environments
                    .GroupBy(e => new { e.OperatingSystem, e.CpuModel, e.LogicalCores })
                    .Select(g => g.First())
                    .ToList();

                foreach (var env in uniqueEnvs)
                {
                    sb.AppendLine($"- **{env.OperatingSystem}** - {env.CpuModel} ({env.LogicalCores} cores)");
                    if (env.CpuMaxFrequencyMhz.HasValue)
                    {
                        sb.AppendLine($"  - Max Frequency: {env.CpuMaxFrequencyMhz.Value:F0} MHz");
                    }
                    sb.AppendLine($"  - SIMD: {string.Join(", ", env.SimdCapabilities)}");
                    sb.AppendLine($"  - Runtime: {env.RuntimeVersion}");
                    sb.AppendLine();
                }
            }

            // Group results by model
            var groupedResults = results
                .GroupBy(r => r.ModelName)
                .OrderBy(g => g.Key)
                .ToList();

            sb.AppendLine("## Performance Results");
            sb.AppendLine();
            sb.AppendLine("| Model | Ctx | Threads | TTFT (ms) | Tok/s | Tok/s (SS) | Peak RSS (MB) | Alloc/tok (KB) | Gen0/1/2 |");
            sb.AppendLine("|-------|-----|---------|-----------|-------|------------|---------------|----------------|----------|");

            foreach (var group in groupedResults)
            {
                foreach (var result in group.OrderBy(r => r.ContextSize).ThenBy(r => r.ThreadCount))
                {
                    sb.AppendLine(
                        $"| {TruncateModelName(result.ModelName)} " +
                        $"| {result.ContextSize} " +
                        $"| {result.ThreadCount} " +
                        $"| {result.TTFTMilliseconds:F1} " +
                        $"| {result.TokensPerSecond:F2} " +
                        $"| {result.TokensPerSecondSteadyState:F2} " +
                        $"| {result.PeakRssBytes / (1024.0 * 1024.0):F1} " +
                        $"| {result.BytesAllocatedPerToken / 1024.0:F1} " +
                        $"| {result.Gen0Collections}/{result.Gen1Collections}/{result.Gen2Collections} |");
                }
            }

            sb.AppendLine();

            // Statistics summary
            sb.AppendLine("## Statistics Summary");
            sb.AppendLine();
            sb.AppendLine("| Model | Ctx | Threads | Median tok/s | P90 tok/s | StdDev tok/s |");
            sb.AppendLine("|-------|-----|---------|--------------|-----------|--------------|");

            foreach (var group in groupedResults)
            {
                foreach (var result in group.OrderBy(r => r.ContextSize).ThenBy(r => r.ThreadCount))
                {
                    sb.AppendLine(
                        $"| {TruncateModelName(result.ModelName)} " +
                        $"| {result.ContextSize} " +
                        $"| {result.ThreadCount} " +
                        $"| {result.MedianTokensPerSecond:F2} " +
                        $"| {result.P90TokensPerSecond:F2} " +
                        $"| {result.StdDevTokensPerSecond:F2} |");
                }
            }

            sb.AppendLine();

            // Footer
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("**Legend:**");
            sb.AppendLine("- **TTFT:** Time To First Token");
            sb.AppendLine("- **Tok/s:** Tokens per second (end-to-end including TTFT)");
            sb.AppendLine("- **Tok/s (SS):** Tokens per second steady-state (excluding TTFT)");
            sb.AppendLine("- **Peak RSS:** Peak Resident Set Size (process peak memory)");
            sb.AppendLine("- **Alloc/tok:** Bytes allocated per token during generation");
            sb.AppendLine("- **Gen0/1/2:** GC collection counts during run");
            sb.AppendLine();

            return sb.ToString();
        }

        private string TruncateModelName(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return "-";

            // Extract just the model name, removing path
            string name = Path.GetFileNameWithoutExtension(modelName);

            // Truncate if too long
            if (name.Length > 30)
                name = name.Substring(0, 27) + "...";

            return name;
        }

        private void ShowUsage()
        {
            Console.WriteLine("Usage: SmallMind.Benchmarks merge [options]");
            Console.WriteLine();
            Console.WriteLine("Merge multiple result files into a summary table");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --input <pattern>    Input file pattern (default: results/*.json)");
            Console.WriteLine("                       Examples: \"results/*.json\", \"results/run1.json\"");
            Console.WriteLine("  --output <file>      Output merged markdown file (default: merged_results.md)");
            Console.WriteLine("  --help, -h           Show this help message");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  # Merge all JSON results in results directory");
            Console.WriteLine("  SmallMind.Benchmarks merge --input \"results/*.json\" --output merged.md");
            Console.WriteLine();
            Console.WriteLine("  # Merge specific files");
            Console.WriteLine("  SmallMind.Benchmarks merge --input \"results/20240115*.json\" --output jan15_results.md");
        }

        private sealed class MergeConfiguration
        {
            public string InputPattern { get; set; } = string.Empty;
            public string OutputFile { get; set; } = string.Empty;
        }
    }
}

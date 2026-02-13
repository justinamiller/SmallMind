using System;
using System.IO;
using System.Text;

namespace SmallMind.Benchmarks.Core.Output
{
    /// <summary>
    /// Markdown output writer for benchmark results.
    /// Creates README-ready formatted tables.
    /// </summary>
    public static class MarkdownOutputWriter
    {
        /// <summary>
        /// Write benchmark results to Markdown file.
        /// </summary>
        public static void WriteToFile(Measurement.BenchmarkRunResults results, string outputPath)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));

            string markdown = GenerateMarkdown(results);

            // Ensure directory exists
            string? directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, markdown);
        }

        /// <summary>
        /// Generate Markdown string from benchmark results.
        /// </summary>
        public static string GenerateMarkdown(Measurement.BenchmarkRunResults results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            var sb = new StringBuilder();

            // Header
            sb.AppendLine("# SmallMind Benchmark Results");
            sb.AppendLine();
            sb.AppendLine($"**Run ID:** `{results.RunId}`  ");
            sb.AppendLine($"**Start Time:** {results.StartTime:yyyy-MM-dd HH:mm:ss} UTC  ");
            sb.AppendLine($"**End Time:** {results.EndTime:yyyy-MM-dd HH:mm:ss} UTC  ");
            sb.AppendLine($"**Duration:** {(results.EndTime - results.StartTime).TotalMinutes:F1} minutes  ");
            
            if (!string.IsNullOrEmpty(results.GitCommitSha))
            {
                sb.AppendLine($"**Git Commit:** `{results.GitCommitSha}`  ");
            }
            
            sb.AppendLine();

            // Environment Information
            if (results.Environment != null)
            {
                sb.AppendLine("## Environment");
                sb.AppendLine();
                sb.AppendLine($"- **OS:** {results.Environment.OperatingSystem}");
                sb.AppendLine($"- **Runtime:** {results.Environment.RuntimeVersion}");
                
                if (!string.IsNullOrEmpty(results.Environment.CpuModel))
                {
                    sb.AppendLine($"- **CPU:** {results.Environment.CpuModel}");
                }
                
                sb.AppendLine($"- **Logical Cores:** {results.Environment.LogicalCores}");
                
                if (results.Environment.CpuBaseFrequencyMhz.HasValue)
                {
                    sb.AppendLine($"- **CPU Base Frequency:** {results.Environment.CpuBaseFrequencyMhz.Value:F0} MHz");
                }
                
                if (results.Environment.CpuMaxFrequencyMhz.HasValue)
                {
                    sb.AppendLine($"- **CPU Max Frequency:** {results.Environment.CpuMaxFrequencyMhz.Value:F0} MHz");
                }
                
                sb.AppendLine($"- **SIMD:** {string.Join(", ", results.Environment.SimdCapabilities)}");
                sb.AppendLine($"- **GC Mode:** {(results.Environment.IsServerGC ? "Server" : "Workstation")}, Latency: {results.Environment.GCLatencyMode}");
                sb.AppendLine();
            }

            // Results Table
            if (results.Results.Length > 0)
            {
                sb.AppendLine("## Performance Results");
                sb.AppendLine();
                sb.AppendLine("| Model | Quant | Ctx | Threads | TTFT (ms) | Tok/s | Tok/s (SS) | Peak RSS (MB) | Alloc/tok (KB) | Gen0/1/2 |");
                sb.AppendLine("|-------|-------|-----|---------|-----------|-------|------------|---------------|----------------|----------|");

                foreach (var result in results.Results)
                {
                    sb.AppendLine(
                        $"| {TruncateModelName(result.ModelName)} " +
                        $"| - " + // Quant type not in BenchmarkResult currently
                        $"| {result.ContextSize} " +
                        $"| {result.ThreadCount} " +
                        $"| {result.TTFTMilliseconds:F1} " +
                        $"| {result.TokensPerSecond:F2} " +
                        $"| {result.TokensPerSecondSteadyState:F2} " +
                        $"| {result.PeakRssBytes / (1024.0 * 1024.0):F1} " +
                        $"| {result.BytesAllocatedPerToken / 1024.0:F1} " +
                        $"| {result.Gen0Collections}/{result.Gen1Collections}/{result.Gen2Collections} |");
                }

                sb.AppendLine();

                // Statistics Summary
                sb.AppendLine("### Statistics Summary");
                sb.AppendLine();
                sb.AppendLine("| Model | Ctx | Threads | Median tok/s | P90 tok/s | StdDev tok/s |");
                sb.AppendLine("|-------|-----|---------|--------------|-----------|--------------|");

                foreach (var result in results.Results)
                {
                    sb.AppendLine(
                        $"| {TruncateModelName(result.ModelName)} " +
                        $"| {result.ContextSize} " +
                        $"| {result.ThreadCount} " +
                        $"| {result.MedianTokensPerSecond:F2} " +
                        $"| {result.P90TokensPerSecond:F2} " +
                        $"| {result.StdDevTokensPerSecond:F2} |");
                }

                sb.AppendLine();
            }

            // Normalized Results
            if (results.NormalizedResults != null && results.NormalizedResults.Length > 0)
            {
                sb.AppendLine("## Normalized Efficiency Metrics");
                sb.AppendLine();
                sb.AppendLine("*These metrics normalize for CPU architecture to compare implementation efficiency.*");
                sb.AppendLine();
                sb.AppendLine("| Model | Ctx | Threads | Tok/s per Core | Tok/s per GHz/Core | Cycles/tok | Notes |");
                sb.AppendLine("|-------|-----|---------|----------------|--------------------|-----------:|-------|");

                foreach (var norm in results.NormalizedResults)
                {
                    sb.AppendLine(
                        $"| {TruncateModelName(norm.ModelName)} " +
                        $"| {norm.ContextSize} " +
                        $"| {norm.ThreadCount} " +
                        $"| {FormatMetric(norm.TokensPerSecondPerCore)} " +
                        $"| {FormatMetric(norm.TokensPerSecondPerGHzPerCore)} " +
                        $"| {FormatMetric(norm.CyclesPerToken)} " +
                        $"| {norm.Notes} |");
                }

                sb.AppendLine();
            }

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

        private static string TruncateModelName(string modelName)
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

        private static string FormatMetric(double value)
        {
            if (value <= 0)
                return "N/A";

            return $"{value:F2}";
        }
    }
}

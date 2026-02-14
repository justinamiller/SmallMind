using System.Text;
using SmallMind.Benchmarks.Utils;

namespace SmallMind.Benchmarks.Reporting
{
    /// <summary>
    /// Generates Markdown reports from benchmark results (README-ready format).
    /// </summary>
    internal static class MarkdownReportGenerator
    {
        public static void GenerateReport(BenchmarkResults results, string outputPath)
        {
            try
            {
                var directory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var sb = new StringBuilder();

                // Header
                sb.AppendLine("# SmallMind Benchmark Results");
                sb.AppendLine();
                sb.AppendLine($"**Date:** {results.StartTime:yyyy-MM-dd HH:mm:ss} UTC");
                sb.AppendLine($"**Duration:** {FormatHelper.FormatDuration(results.TotalDuration.TotalMilliseconds)}");
                sb.AppendLine($"**Status:** {results.Status}");
                sb.AppendLine();

                // System Information
                sb.AppendLine("## System Information");
                sb.AppendLine();
                sb.AppendLine($"- **OS:** {results.SystemInfo.OperatingSystem}");
                sb.AppendLine($"- **Architecture:** {results.SystemInfo.Architecture}");
                sb.AppendLine($"- **Runtime:** {results.SystemInfo.RuntimeVersion}");
                sb.AppendLine($"- **Processor Count:** {results.SystemInfo.ProcessorCount}");
                sb.AppendLine($"- **GC Mode:** {results.SystemInfo.GCMode} / {results.SystemInfo.GCLatencyMode}");
                sb.AppendLine($"- **SIMD:** {results.SystemInfo.Simd}");
                sb.AppendLine();

                // Configuration
                sb.AppendLine("## Benchmark Configuration");
                sb.AppendLine();
                sb.AppendLine($"- **Model:** {results.Config.ModelPath}");
                sb.AppendLine($"- **Warmup Iterations:** {results.Config.WarmupIterations}");
                sb.AppendLine($"- **Measured Iterations:** {results.Config.MeasuredIterations}");
                sb.AppendLine($"- **Max Tokens per Request:** {results.Config.MaxTokensPerRequest}");
                sb.AppendLine($"- **Context Size:** {results.Config.ContextSize}");
                sb.AppendLine($"- **KV Cache:** {results.Config.EnableKVCache}");
                sb.AppendLine();

                // Benchmark Results Table
                sb.AppendLine("## Benchmark Results");
                sb.AppendLine();
                sb.AppendLine("| Metric | Value | Unit | P50 | P95 | P99 |");
                sb.AppendLine("|--------|-------|------|-----|-----|-----|");

                foreach (var metric in results.Metrics.OrderBy(m => m.Category).ThenBy(m => m.Name))
                {
                    var stats = metric.Statistics;
                    var p50 = stats != null ? FormatHelper.FormatDecimal(stats.P50, 2) : "-";
                    var p95 = stats != null ? FormatHelper.FormatDecimal(stats.P95, 2) : "-";
                    var p99 = stats != null ? FormatHelper.FormatDecimal(stats.P99, 2) : "-";

                    sb.AppendLine($"| {metric.Name} | {FormatHelper.FormatDecimal(metric.Value, 2)} | {metric.Unit} | {p50} | {p95} | {p99} |");
                }

                sb.AppendLine();

                // Detailed Metrics
                sb.AppendLine("## Detailed Metrics");
                sb.AppendLine();

                foreach (var metric in results.Metrics)
                {
                    sb.AppendLine($"### {metric.Name}");
                    sb.AppendLine();

                    if (metric.Statistics != null)
                    {
                        var stats = metric.Statistics;
                        sb.AppendLine($"- **Mean:** {FormatHelper.FormatDecimal(stats.Mean, 2)} {metric.Unit}");
                        sb.AppendLine($"- **Median:** {FormatHelper.FormatDecimal(stats.Median, 2)} {metric.Unit}");
                        sb.AppendLine($"- **Min:** {FormatHelper.FormatDecimal(stats.Min, 2)} {metric.Unit}");
                        sb.AppendLine($"- **Max:** {FormatHelper.FormatDecimal(stats.Max, 2)} {metric.Unit}");
                        sb.AppendLine($"- **Std Dev:** {FormatHelper.FormatDecimal(stats.StdDev, 2)} {metric.Unit}");
                        sb.AppendLine($"- **P50:** {FormatHelper.FormatDecimal(stats.P50, 2)} {metric.Unit}");
                        sb.AppendLine($"- **P95:** {FormatHelper.FormatDecimal(stats.P95, 2)} {metric.Unit}");
                        sb.AppendLine($"- **P99:** {FormatHelper.FormatDecimal(stats.P99, 2)} {metric.Unit}");
                    }
                    else
                    {
                        sb.AppendLine($"- **Value:** {FormatHelper.FormatDecimal(metric.Value, 2)} {metric.Unit}");
                    }

                    if (metric.Metadata.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("**Additional Metadata:**");
                        foreach (var kvp in metric.Metadata)
                        {
                            sb.AppendLine($"- `{kvp.Key}`: {kvp.Value}");
                        }
                    }

                    sb.AppendLine();
                }

                // Errors and Warnings
                if (results.Errors.Count > 0)
                {
                    sb.AppendLine("## Errors");
                    sb.AppendLine();
                    foreach (var error in results.Errors)
                    {
                        sb.AppendLine($"- ❌ {error}");
                    }
                    sb.AppendLine();
                }

                if (results.Warnings.Count > 0)
                {
                    sb.AppendLine("## Warnings");
                    sb.AppendLine();
                    foreach (var warning in results.Warnings)
                    {
                        sb.AppendLine($"- ⚠️ {warning}");
                    }
                    sb.AppendLine();
                }

                // Footer
                sb.AppendLine("---");
                sb.AppendLine($"*Report generated by SmallMind.Benchmarks on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC*");

                File.WriteAllText(outputPath, sb.ToString());

                Console.WriteLine($"Markdown report written to: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write Markdown report: {ex.Message}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Writes benchmark results to Markdown format.
    /// </summary>
    internal static class MarkdownReportWriter
    {
        /// <summary>
        /// Write benchmark results to Markdown file.
        /// </summary>
        public static void WriteReport(string filePath, List<BenchmarkResult> results, List<BenchmarkResult>? baseline = null)
        {
            var sb = new StringBuilder();

            sb.AppendLine("# SmallMind Performance Benchmark Results");
            sb.AppendLine();
            sb.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();

            if (results.Count > 0)
            {
                // Write environment info from first result
                WriteEnvironment(sb, results[0].Environment);
                sb.AppendLine();
            }

            // Write results
            sb.AppendLine("## Benchmark Results");
            sb.AppendLine();

            foreach (var result in results)
            {
                WriteBenchmarkResult(sb, result, FindBaseline(baseline, result.Name));
            }

            File.WriteAllText(filePath, sb.ToString());
        }

        private static void WriteEnvironment(StringBuilder sb, Dictionary<string, object> env)
        {
            sb.AppendLine("## Environment");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");

            foreach (var kvp in env)
            {
                sb.AppendLine($"| {kvp.Key} | {kvp.Value} |");
            }
        }

        private static void WriteBenchmarkResult(StringBuilder sb, BenchmarkResult result, BenchmarkResult? baseline)
        {
            sb.AppendLine($"### {result.Name}");
            sb.AppendLine();

            // Configuration
            sb.AppendLine("**Configuration:**");
            sb.AppendLine($"- Warmup iterations: {result.Config.WarmupIterations}");
            sb.AppendLine($"- Measured iterations: {result.Config.MeasuredIterations}");
            sb.AppendLine($"- Prompt length: {result.Config.PromptLength}");
            sb.AppendLine($"- Decode tokens: {result.Config.DecodeTokens}");
            sb.AppendLine($"- Seed: {result.Config.Seed}");
            sb.AppendLine();

            // Metrics
            sb.AppendLine("**Metrics:**");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value | Baseline | Change |");
            sb.AppendLine("|--------|-------|----------|--------|");

            WriteMetricRow(sb, "TTFT (ms)", result.Metrics.TTFT, baseline?.Metrics.TTFT, "ms", lowerIsBetter: true);
            WriteMetricRow(sb, "Decode tok/sec", result.Metrics.DecodeToksPerSec, baseline?.Metrics.DecodeToksPerSec, "tok/s", lowerIsBetter: false);
            WriteMetricRow(sb, "Prefill tok/sec", result.Metrics.PrefillToksPerSec, baseline?.Metrics.PrefillToksPerSec, "tok/s", lowerIsBetter: false);
            WriteMetricRow(sb, "Peak RSS", result.Metrics.PeakRSS / 1024.0 / 1024.0, baseline?.Metrics.PeakRSS / 1024.0 / 1024.0, "MB", lowerIsBetter: true);
            WriteMetricRow(sb, "Managed Heap", result.Metrics.ManagedHeapSize / 1024.0 / 1024.0, baseline?.Metrics.ManagedHeapSize / 1024.0 / 1024.0, "MB", lowerIsBetter: true);
            WriteMetricRow(sb, "Alloc bytes/token", result.Metrics.AllocatedBytesPerToken, baseline?.Metrics.AllocatedBytesPerToken, "bytes", lowerIsBetter: true);
            
            sb.AppendLine($"| Gen0 Collections | {result.Metrics.Gen0Collections} | {baseline?.Metrics.Gen0Collections.ToString() ?? "N/A"} | {GetChangeString(result.Metrics.Gen0Collections, baseline?.Metrics.Gen0Collections, "", lowerIsBetter: true)} |");
            sb.AppendLine($"| Gen1 Collections | {result.Metrics.Gen1Collections} | {baseline?.Metrics.Gen1Collections.ToString() ?? "N/A"} | {GetChangeString(result.Metrics.Gen1Collections, baseline?.Metrics.Gen1Collections, "", lowerIsBetter: true)} |");
            sb.AppendLine($"| Gen2 Collections | {result.Metrics.Gen2Collections} | {baseline?.Metrics.Gen2Collections.ToString() ?? "N/A"} | {GetChangeString(result.Metrics.Gen2Collections, baseline?.Metrics.Gen2Collections, "", lowerIsBetter: true)} |");

            sb.AppendLine();

            // Custom metrics
            if (result.Metrics.CustomMetrics.Count > 0)
            {
                sb.AppendLine("**Custom Metrics:**");
                sb.AppendLine();
                foreach (var kvp in result.Metrics.CustomMetrics)
                {
                    sb.AppendLine($"- {kvp.Key}: {kvp.Value}");
                }
                sb.AppendLine();
            }
        }

        private static void WriteMetricRow(StringBuilder sb, string name, double value, double? baselineValue, string unit, bool lowerIsBetter)
        {
            var valueStr = $"{value:F3} {unit}";
            var baselineStr = baselineValue.HasValue ? $"{baselineValue.Value:F3} {unit}" : "N/A";
            var changeStr = GetChangeString(value, baselineValue, unit, lowerIsBetter);

            sb.AppendLine($"| {name} | {valueStr} | {baselineStr} | {changeStr} |");
        }

        private static string GetChangeString(double value, double? baselineValue, string unit, bool lowerIsBetter)
        {
            if (!baselineValue.HasValue || baselineValue.Value == 0)
                return "N/A";

            var change = value - baselineValue.Value;
            var percentChange = (change / baselineValue.Value) * 100.0;

            var sign = change >= 0 ? "+" : "";
            var emoji = "";

            if (Math.Abs(percentChange) < 1.0)
            {
                emoji = "≈"; // Nearly equal
            }
            else
            {
                var isImprovement = lowerIsBetter ? change < 0 : change > 0;
                emoji = isImprovement ? "✅" : "❌";
            }

            return $"{sign}{percentChange:F1}% {emoji}";
        }

        private static string GetChangeString(int value, int? baselineValue, string unit, bool lowerIsBetter)
        {
            if (!baselineValue.HasValue)
                return "N/A";

            var change = value - baselineValue.Value;
            
            if (change == 0)
                return "≈";

            var emoji = "";
            var isImprovement = lowerIsBetter ? change < 0 : change > 0;
            emoji = isImprovement ? "✅" : "❌";

            var sign = change >= 0 ? "+" : "";
            return $"{sign}{change} {emoji}";
        }

        private static BenchmarkResult? FindBaseline(List<BenchmarkResult>? baseline, string name)
        {
            if (baseline == null)
                return null;

            foreach (var result in baseline)
            {
                if (result.Name == name)
                    return result;
            }

            return null;
        }
    }
}

using System;
using System.Linq;
using System.Text;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Generates markdown-formatted benchmark reports with full system metadata.
    /// </summary>
    public static class MarkdownReportWriter
    {
        /// <summary>
        /// Generates a complete markdown report from benchmark results and system info.
        /// </summary>
        public static string GenerateReport(BenchmarkReport report)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine("# SmallMind Benchmark Results");
            sb.AppendLine();
            sb.AppendLine($"**Report Generated:** {report.ReportTimestamp:yyyy-MM-dd HH:mm:ss UTC}");
            sb.AppendLine();

            // Environment section
            sb.AppendLine("## Environment");
            sb.AppendLine();

            // Machine section
            sb.AppendLine("### Machine");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");
            sb.AppendLine($"| CPU Architecture | {report.SystemInfo.Machine.CpuArchitecture} |");
            sb.AppendLine($"| Logical Cores | {report.SystemInfo.Machine.LogicalCores} |");
            sb.AppendLine($"| CPU Model | {report.SystemInfo.Machine.CpuModel} |");
            sb.AppendLine($"| SIMD Width (Vector<float>) | {report.SystemInfo.Machine.SimdVectorSize} |");
            
            // Add SIMD capabilities
            var supportedSimd = report.SystemInfo.Machine.SimdCapabilities
                .Where(kvp => kvp.Value)
                .Select(kvp => kvp.Key)
                .ToList();
            
            if (supportedSimd.Any())
            {
                foreach (var cap in supportedSimd.OrderBy(c => c))
                {
                    sb.AppendLine($"| {cap} | Supported |");
                }
            }
            
            sb.AppendLine($"| Endianness | {report.SystemInfo.Machine.Endianness} |");
            
            if (report.SystemInfo.Machine.NumaAware)
            {
                sb.AppendLine("| NUMA | Aware |");
            }
            
            sb.AppendLine();

            // Memory section
            sb.AppendLine("### Memory");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");
            sb.AppendLine($"| Total Memory | {report.SystemInfo.Memory.TotalMemoryFormatted} |");
            sb.AppendLine($"| Available Memory | {report.SystemInfo.Memory.AvailableMemoryFormatted} |");
            sb.AppendLine();

            // Operating System section
            sb.AppendLine("### Operating System");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");
            sb.AppendLine($"| Platform | {report.SystemInfo.OperatingSystem.Platform} |");
            sb.AppendLine($"| OS | {report.SystemInfo.OperatingSystem.OsName} |");
            sb.AppendLine($"| Version | {report.SystemInfo.OperatingSystem.OsVersion} |");
            sb.AppendLine($"| Kernel | {report.SystemInfo.OperatingSystem.KernelVersion} |");
            sb.AppendLine();

            // .NET Runtime section
            sb.AppendLine("### .NET Runtime");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");
            sb.AppendLine($"| .NET Version | {report.SystemInfo.Runtime.DotNetVersion} |");
            sb.AppendLine($"| Framework | {report.SystemInfo.Runtime.FrameworkDescription} |");
            sb.AppendLine($"| Runtime ID | {report.SystemInfo.Runtime.RuntimeIdentifier} |");
            sb.AppendLine($"| GC Mode | {report.SystemInfo.Runtime.GcMode} |");
            sb.AppendLine($"| GC Latency Mode | {report.SystemInfo.Runtime.GcLatencyMode} |");
            sb.AppendLine($"| Tiered JIT | {(report.SystemInfo.Runtime.TieredCompilation ? "Enabled" : "Disabled")} |");
            sb.AppendLine($"| ReadyToRun | {(report.SystemInfo.Runtime.ReadyToRun ? "Enabled" : "Disabled")} |");
            sb.AppendLine();

            // Process section
            sb.AppendLine("### Process");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");
            sb.AppendLine($"| Bitness | {report.SystemInfo.Process.Bitness} |");
            sb.AppendLine($"| Priority | {report.SystemInfo.Process.PriorityClass} |");
            sb.AppendLine();

            // Build section
            sb.AppendLine("### Build");
            sb.AppendLine();
            sb.AppendLine("| Property | Value |");
            sb.AppendLine("|----------|-------|");
            sb.AppendLine($"| Configuration | {report.SystemInfo.Build.Configuration} |");
            sb.AppendLine($"| Target Framework | {report.SystemInfo.Build.TargetFramework} |");
            sb.AppendLine($"| Compilation Time | {report.SystemInfo.Build.CompilationTimestamp} |");
            
            if (report.SystemInfo.Build.GitCommitHash != "Unknown")
            {
                sb.AppendLine($"| Git Commit | {report.SystemInfo.Build.GitCommitHash} |");
            }
            
            sb.AppendLine();

            // Warning if not Release build
            if (!report.SystemInfo.Build.IsReleaseBuild)
            {
                sb.AppendLine("> ⚠️ **WARNING:** Benchmarks were run in Debug mode. Results may not be representative of production performance.");
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine();

            // Benchmark results
            sb.AppendLine("## Benchmark Results");
            sb.AppendLine();

            foreach (var result in report.Results)
            {
                sb.AppendLine($"### {result.Name}");
                sb.AppendLine();

                // Parameters (if any)
                if (result.Parameters.Any())
                {
                    sb.AppendLine("**Parameters:**");
                    sb.AppendLine();
                    foreach (var param in result.Parameters)
                    {
                        sb.AppendLine($"- {param.Key}: {FormatValue(param.Value)}");
                    }
                    sb.AppendLine();
                }

                // Metrics
                if (result.Metrics.Any())
                {
                    sb.AppendLine("**Metrics:**");
                    sb.AppendLine();
                    sb.AppendLine("| Metric | Value |");
                    sb.AppendLine("|--------|-------|");
                    
                    foreach (var metric in result.Metrics)
                    {
                        sb.AppendLine($"| {metric.Key} | {FormatMetric(metric.Key, metric.Value)} |");
                    }
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static string FormatValue(object value)
        {
            if (value == null) return "null";
            
            if (value is int || value is long)
                return $"{value:N0}";
            
            if (value is float || value is double)
                return $"{value:F2}";
            
            return value.ToString() ?? "null";
        }

        private static string FormatMetric(string metricName, double value)
        {
            var lowerName = metricName.ToLowerInvariant();
            
            // Time metrics
            if (lowerName.Contains("time") || lowerName.Contains("ms") || lowerName.Contains("latency"))
            {
                return $"{value:F3} ms";
            }
            
            // Throughput metrics
            if (lowerName.Contains("throughput") || lowerName.Contains("gb/s"))
            {
                return $"{value:F2} GB/s";
            }
            
            // Performance metrics
            if (lowerName.Contains("gflops") || lowerName.Contains("performance"))
            {
                return $"{value:F2} GFLOPS";
            }
            
            // Default formatting
            return $"{value:F2}";
        }
    }
}

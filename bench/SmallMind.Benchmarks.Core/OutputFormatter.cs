using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmallMind.Benchmarks.Core;

/// <summary>
/// Output formatters for benchmark results.
/// </summary>
public static class OutputFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Writes results as JSON.
    /// </summary>
    public static async Task WriteJsonAsync(BenchmarkResults results, string filePath)
    {
        var json = JsonSerializer.Serialize(results, JsonOptions);
        await File.WriteAllTextAsync(filePath, json);
        Console.WriteLine($"JSON results written to: {filePath}");
    }

    /// <summary>
    /// Writes results as Markdown.
    /// </summary>
    public static async Task WriteMarkdownAsync(BenchmarkResults results, string filePath)
    {
        var md = new StringBuilder();

        md.AppendLine("# Benchmark Results");
        md.AppendLine();
        md.AppendLine($"**Timestamp**: {results.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        md.AppendLine();

        // Environment section
        md.AppendLine("## Environment");
        md.AppendLine();
        md.AppendLine($"- **Git Commit**: `{results.Environment.GitCommitSha}`");
        md.AppendLine($"- **Runtime**: .NET {results.Environment.RuntimeVersion}");
        md.AppendLine($"- **OS**: {results.Environment.OsDescription} ({results.Environment.OsArchitecture})");
        md.AppendLine($"- **CPU**: {results.Environment.CpuModel}");
        md.AppendLine($"- **Logical Cores**: {results.Environment.LogicalCoreCount}");
        if (results.Environment.BaseCpuFrequencyGHz.HasValue)
            md.AppendLine($"- **Base Frequency**: {results.Environment.BaseCpuFrequencyGHz.Value:F2} GHz");
        if (results.Environment.MaxCpuFrequencyGHz.HasValue)
            md.AppendLine($"- **Max Frequency**: {results.Environment.MaxCpuFrequencyGHz.Value:F2} GHz");
        md.AppendLine($"- **SIMD**: {results.Environment.SimdFlags}");
        md.AppendLine($"- **Server GC**: {results.Environment.IsServerGC}");
        md.AppendLine();

        // Model section
        md.AppendLine("## Model");
        md.AppendLine();
        md.AppendLine($"- **Name**: {results.Model.Name}");
        md.AppendLine($"- **Quantization**: {results.Model.QuantType}");
        md.AppendLine($"- **Context Length**: {results.Model.ContextLength}");
        md.AppendLine($"- **Size**: {results.Model.SizeBytes / (1024.0 * 1024.0):F2} MB");
        md.AppendLine();

        // Results table
        md.AppendLine("## Performance Results");
        md.AppendLine();
        md.AppendLine("| Scenario | Context | Threads | Prompt Tokens | Gen Tokens | TTFT (ms) | tok/s | tok/s E2E | Peak RSS (MB) | Alloc/tok (B) |");
        md.AppendLine("|----------|---------|---------|---------------|------------|-----------|-------|-----------|---------------|---------------|");

        foreach (var scenario in results.Scenarios)
        {
            md.AppendLine($"| {scenario.Name} | {scenario.ContextSize} | {scenario.Threads} | " +
                         $"{scenario.PromptTokens} | {scenario.GeneratedTokens} | " +
                         $"{scenario.TtftMs.Median:F1} | {scenario.TokensPerSecond.Median:F2} | " +
                         $"{scenario.TokensPerSecondEndToEnd.Median:F2} | {scenario.PeakRssMb.Median:F1} | " +
                         $"{scenario.SteadyAllocBytesPerToken.Median:F0} |");
        }

        md.AppendLine();

        // Normalized metrics
        md.AppendLine("## Normalized Efficiency Metrics");
        md.AppendLine();
        md.AppendLine("These metrics normalize performance for cross-machine comparison, emphasizing implementation efficiency over raw hardware power.");
        md.AppendLine();
        md.AppendLine("| Scenario | Threads | tok/s per core | tok/s per GHz/core | Cycles/tok | Alloc/tok/GHz (B) |");
        md.AppendLine("|----------|---------|----------------|--------------------|-----------|--------------------|");

        foreach (var scenario in results.Scenarios)
        {
            var n = scenario.Normalized;
            md.AppendLine($"| {scenario.Name} | {scenario.Threads} | " +
                         $"{NormalizationCalculator.FormatMetric(n.TokPerSecPerCore, "")} | " +
                         $"{NormalizationCalculator.FormatMetric(n.TokPerSecPerGhzPerCore, "")} | " +
                         $"{NormalizationCalculator.FormatMetric(n.CyclesPerToken, "", 0)} | " +
                         $"{NormalizationCalculator.FormatMetric(n.AllocBytesPerTokenPerGhz, "", 0)} |");
        }

        md.AppendLine();
        md.AppendLine("**Note**: Normalized metrics may show N/A when CPU frequency information is unavailable.");

        await File.WriteAllTextAsync(filePath, md.ToString());
        Console.WriteLine($"Markdown results written to: {filePath}");
    }

    /// <summary>
    /// Writes results as CSV.
    /// </summary>
    public static async Task WriteCsvAsync(BenchmarkResults results, string filePath)
    {
        var csv = new StringBuilder();

        // Header
        csv.AppendLine("Scenario,Context,Threads,PromptTokens,GenTokens," +
                      "TTFT_median_ms,TTFT_p90_ms,tok_s_median,tok_s_p90," +
                      "tok_s_e2e_median,tok_s_e2e_p90,peak_rss_mb,alloc_per_tok_bytes," +
                      "gc_gen0,gc_gen1,gc_gen2," +
                      "norm_tok_per_sec_per_core,norm_tok_per_sec_per_ghz_per_core," +
                      "norm_cycles_per_tok,norm_alloc_per_tok_per_ghz");

        // Data rows
        foreach (var scenario in results.Scenarios)
        {
            csv.AppendLine($"{scenario.Name},{scenario.ContextSize},{scenario.Threads}," +
                          $"{scenario.PromptTokens},{scenario.GeneratedTokens}," +
                          $"{scenario.TtftMs.Median:F2},{scenario.TtftMs.P90:F2}," +
                          $"{scenario.TokensPerSecond.Median:F2},{scenario.TokensPerSecond.P90:F2}," +
                          $"{scenario.TokensPerSecondEndToEnd.Median:F2},{scenario.TokensPerSecondEndToEnd.P90:F2}," +
                          $"{scenario.PeakRssMb.Median:F2},{scenario.SteadyAllocBytesPerToken.Median:F0}," +
                          $"{scenario.GcGen0Count},{scenario.GcGen1Count},{scenario.GcGen2Count}," +
                          $"{scenario.Normalized.TokPerSecPerCore?.ToString("F2") ?? ""}," +
                          $"{scenario.Normalized.TokPerSecPerGhzPerCore?.ToString("F2") ?? ""}," +
                          $"{scenario.Normalized.CyclesPerToken?.ToString("F0") ?? ""}," +
                          $"{scenario.Normalized.AllocBytesPerTokenPerGhz?.ToString("F0") ?? ""}");
        }

        await File.WriteAllTextAsync(filePath, csv.ToString());
        Console.WriteLine($"CSV results written to: {filePath}");
    }

    /// <summary>
    /// Prints a summary to console.
    /// </summary>
    public static void PrintSummary(BenchmarkResults results)
    {
        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("                  BENCHMARK SUMMARY");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine($"Model: {results.Model.Name} ({results.Model.QuantType})");
        Console.WriteLine($"CPU: {results.Environment.CpuModel}");
        Console.WriteLine($"Cores: {results.Environment.LogicalCoreCount}");
        if (results.Environment.MaxCpuFrequencyGHz.HasValue)
            Console.WriteLine($"Frequency: {results.Environment.MaxCpuFrequencyGHz.Value:F2} GHz");
        Console.WriteLine();

        foreach (var scenario in results.Scenarios)
        {
            Console.WriteLine($"Scenario: {scenario.Name}");
            Console.WriteLine($"  Context: {scenario.ContextSize}, Threads: {scenario.Threads}");
            Console.WriteLine($"  TTFT: {scenario.TtftMs.Median:F2}ms (±{scenario.TtftMs.Stddev:F2})");
            Console.WriteLine($"  tok/s: {scenario.TokensPerSecond.Median:F2} (±{scenario.TokensPerSecond.Stddev:F2})");
            Console.WriteLine($"  Peak RSS: {scenario.PeakRssMb.Median:F1} MB");
            if (scenario.Normalized.TokPerSecPerCore.HasValue)
                Console.WriteLine($"  Normalized: {scenario.Normalized.TokPerSecPerCore.Value:F2} tok/s/core");
            Console.WriteLine();
        }

        Console.WriteLine("═══════════════════════════════════════════════════════");
    }
}

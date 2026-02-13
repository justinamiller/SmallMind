using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmallMind.Benchmarks.Core;

/// <summary>
/// Compares benchmark results across runs and architectures.
/// </summary>
public static class BenchmarkComparer
{
    /// <summary>
    /// Loads all benchmark results from a directory.
    /// </summary>
    public static async Task<List<BenchmarkResults>> LoadResultsFromDirectoryAsync(string directory)
    {
        var results = new List<BenchmarkResults>();
        
        if (!Directory.Exists(directory))
            return results;

        var jsonFiles = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
        
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        
        foreach (var file in jsonFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var result = JsonSerializer.Deserialize<BenchmarkResults>(json, options);
                if (result != null)
                {
                    results.Add(result);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to load {file}: {ex.Message}");
            }
        }

        return results;
    }

    /// <summary>
    /// Finds the most recent previous result matching the same model and architecture.
    /// </summary>
    public static BenchmarkResults? FindPreviousResult(
        List<BenchmarkResults> allResults, 
        BenchmarkResults current)
    {
        return allResults
            .Where(r => r.Model.Name == current.Model.Name)
            .Where(r => r.Environment.OsArchitecture == current.Environment.OsArchitecture)
            .Where(r => r.Timestamp < current.Timestamp)
            .OrderByDescending(r => r.Timestamp)
            .FirstOrDefault();
    }

    /// <summary>
    /// Compares two scenario results and returns the percentage change.
    /// </summary>
    public static ComparisonResult CompareScenarios(ScenarioResult current, ScenarioResult? previous)
    {
        var comparison = new ComparisonResult
        {
            ScenarioName = current.Name,
            Current = current,
            Previous = previous
        };

        if (previous == null)
        {
            comparison.IsNew = true;
            return comparison;
        }

        // Calculate percentage changes
        comparison.TtftChange = CalculatePercentChange(
            previous.TtftMs.Median, current.TtftMs.Median);
        
        comparison.TokPerSecChange = CalculatePercentChange(
            previous.TokensPerSecond.Median, current.TokensPerSecond.Median);
        
        comparison.TokPerSecE2EChange = CalculatePercentChange(
            previous.TokensPerSecondEndToEnd.Median, current.TokensPerSecondEndToEnd.Median);
        
        comparison.PeakRssChange = CalculatePercentChange(
            previous.PeakRssMb.Median, current.PeakRssMb.Median);
        
        comparison.AllocPerTokenChange = CalculatePercentChange(
            previous.SteadyAllocBytesPerToken.Median, current.SteadyAllocBytesPerToken.Median);

        return comparison;
    }

    /// <summary>
    /// Calculates percentage change (positive means improvement for throughput metrics).
    /// </summary>
    private static double? CalculatePercentChange(double previous, double current)
    {
        if (previous == 0)
            return null;
        
        return ((current - previous) / previous) * 100.0;
    }

    /// <summary>
    /// Formats a percentage change with appropriate color indicator.
    /// </summary>
    public static string FormatPercentChange(double? change, bool lowerIsBetter = false)
    {
        if (!change.HasValue)
            return "N/A";

        var symbol = GetChangeSymbol(change.Value, lowerIsBetter);
        return $"{symbol}{Math.Abs(change.Value):F1}%";
    }

    /// <summary>
    /// Gets an indicator symbol for the change (↑ for improvement, ↓ for regression).
    /// </summary>
    private static string GetChangeSymbol(double change, bool lowerIsBetter)
    {
        var isImprovement = lowerIsBetter ? change < 0 : change > 0;
        
        if (Math.Abs(change) < 1.0)
            return "≈"; // Approximately equal (< 1% change)
        
        return isImprovement ? "↑" : "↓";
    }

    /// <summary>
    /// Generates a comparison report in Markdown format.
    /// </summary>
    public static async Task WriteComparisonReportAsync(
        BenchmarkResults current,
        BenchmarkResults? previous,
        string outputPath)
    {
        var md = new StringBuilder();

        md.AppendLine("# Benchmark Comparison Report");
        md.AppendLine();
        md.AppendLine($"**Current Run**: {current.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
        
        if (previous != null)
        {
            md.AppendLine($"**Previous Run**: {previous.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
            var timeDiff = current.Timestamp - previous.Timestamp;
            md.AppendLine($"**Time Between Runs**: {timeDiff.TotalHours:F1} hours ({timeDiff.TotalDays:F1} days)");
        }
        else
        {
            md.AppendLine($"**Previous Run**: No previous results found");
        }
        
        md.AppendLine();

        // Environment comparison
        md.AppendLine("## Environment");
        md.AppendLine();
        md.AppendLine($"- **Git Commit**: `{current.Environment.GitCommitSha}` " + 
            (previous != null ? $"(was `{previous.Environment.GitCommitSha}`)": ""));
        md.AppendLine($"- **OS**: {current.Environment.OsDescription} ({current.Environment.OsArchitecture})");
        md.AppendLine($"- **CPU**: {current.Environment.CpuModel}");
        md.AppendLine($"- **Cores**: {current.Environment.LogicalCoreCount}");
        md.AppendLine($"- **SIMD**: {current.Environment.SimdFlags}");
        md.AppendLine();

        // Model info
        md.AppendLine("## Model");
        md.AppendLine();
        md.AppendLine($"- **Name**: {current.Model.Name}");
        md.AppendLine($"- **Quantization**: {current.Model.QuantType}");
        md.AppendLine($"- **Context Length**: {current.Model.ContextLength}");
        md.AppendLine();

        // Performance comparison table
        md.AppendLine("## Performance Comparison");
        md.AppendLine();
        md.AppendLine("| Scenario | Metric | Current | Previous | Change |");
        md.AppendLine("|----------|--------|---------|----------|--------|");

        foreach (var currentScenario in current.Scenarios)
        {
            var previousScenario = previous?.Scenarios
                .FirstOrDefault(s => s.Name == currentScenario.Name);
            
            var comparison = CompareScenarios(currentScenario, previousScenario);

            // TTFT (lower is better)
            md.AppendLine($"| {currentScenario.Name} | TTFT (ms) | {currentScenario.TtftMs.Median:F1} | " +
                $"{previousScenario?.TtftMs.Median.ToString("F1") ?? "N/A"} | " +
                $"{FormatPercentChange(comparison.TtftChange, lowerIsBetter: true)} |");

            // Tokens per second (higher is better)
            md.AppendLine($"| {currentScenario.Name} | tok/s | {currentScenario.TokensPerSecond.Median:F2} | " +
                $"{previousScenario?.TokensPerSecond.Median.ToString("F2") ?? "N/A"} | " +
                $"{FormatPercentChange(comparison.TokPerSecChange)} |");

            // End-to-end tokens per second (higher is better)
            md.AppendLine($"| {currentScenario.Name} | tok/s E2E | {currentScenario.TokensPerSecondEndToEnd.Median:F2} | " +
                $"{previousScenario?.TokensPerSecondEndToEnd.Median.ToString("F2") ?? "N/A"} | " +
                $"{FormatPercentChange(comparison.TokPerSecE2EChange)} |");

            // Peak RSS (lower is better)
            md.AppendLine($"| {currentScenario.Name} | Peak RSS (MB) | {currentScenario.PeakRssMb.Median:F1} | " +
                $"{previousScenario?.PeakRssMb.Median.ToString("F1") ?? "N/A"} | " +
                $"{FormatPercentChange(comparison.PeakRssChange, lowerIsBetter: true)} |");

            // Allocation per token (lower is better)
            md.AppendLine($"| {currentScenario.Name} | Alloc/tok (B) | {currentScenario.SteadyAllocBytesPerToken.Median:F0} | " +
                $"{previousScenario?.SteadyAllocBytesPerToken.Median.ToString("F0") ?? "N/A"} | " +
                $"{FormatPercentChange(comparison.AllocPerTokenChange, lowerIsBetter: true)} |");
        }

        md.AppendLine();
        md.AppendLine("**Legend**: ↑ = improvement, ↓ = regression, ≈ = no significant change (< 1%)");
        md.AppendLine();

        // Summary
        if (previous != null)
        {
            md.AppendLine("## Summary");
            md.AppendLine();
            
            var improvements = 0;
            var regressions = 0;
            var noChange = 0;

            foreach (var currentScenario in current.Scenarios)
            {
                var previousScenario = previous.Scenarios
                    .FirstOrDefault(s => s.Name == currentScenario.Name);
                
                if (previousScenario != null)
                {
                    var comparison = CompareScenarios(currentScenario, previousScenario);
                    
                    // Check tok/s change as primary metric
                    if (comparison.TokPerSecChange.HasValue)
                    {
                        if (comparison.TokPerSecChange.Value > 1.0)
                            improvements++;
                        else if (comparison.TokPerSecChange.Value < -1.0)
                            regressions++;
                        else
                            noChange++;
                    }
                }
            }

            md.AppendLine($"- **Improvements**: {improvements} scenario(s)");
            md.AppendLine($"- **Regressions**: {regressions} scenario(s)");
            md.AppendLine($"- **No significant change**: {noChange} scenario(s)");
        }

        await File.WriteAllTextAsync(outputPath, md.ToString());
        Console.WriteLine($"Comparison report written to: {outputPath}");
    }

    /// <summary>
    /// Generates a cross-architecture comparison report.
    /// </summary>
    public static async Task WriteCrossArchitectureComparisonAsync(
        List<BenchmarkResults> allResults,
        string outputPath)
    {
        var md = new StringBuilder();

        md.AppendLine("# Cross-Architecture Benchmark Comparison");
        md.AppendLine();
        md.AppendLine($"**Generated**: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}");
        md.AppendLine();

        // Group by model name and timestamp range (same run)
        var latestResultsByArch = allResults
            .GroupBy(r => r.Environment.OsArchitecture)
            .Select(g => new
            {
                Architecture = g.Key,
                Result = g.OrderByDescending(r => r.Timestamp).First()
            })
            .OrderBy(x => x.Architecture)
            .ToList();

        if (latestResultsByArch.Count == 0)
        {
            md.AppendLine("No results found.");
            await File.WriteAllTextAsync(outputPath, md.ToString());
            return;
        }

        // Architecture summary
        md.AppendLine("## Architectures Tested");
        md.AppendLine();
        foreach (var archResult in latestResultsByArch)
        {
            var r = archResult.Result;
            md.AppendLine($"### {archResult.Architecture}");
            md.AppendLine($"- **CPU**: {r.Environment.CpuModel}");
            md.AppendLine($"- **Cores**: {r.Environment.LogicalCoreCount}");
            if (r.Environment.MaxCpuFrequencyGHz.HasValue)
                md.AppendLine($"- **Max Frequency**: {r.Environment.MaxCpuFrequencyGHz.Value:F2} GHz");
            md.AppendLine($"- **SIMD**: {r.Environment.SimdFlags}");
            md.AppendLine($"- **Timestamp**: {r.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
            md.AppendLine();
        }

        // Get common scenarios across all architectures
        var firstResult = latestResultsByArch.First().Result;
        var commonScenarios = firstResult.Scenarios.Select(s => s.Name).ToList();

        md.AppendLine("## Performance Comparison Across Architectures");
        md.AppendLine();

        foreach (var scenarioName in commonScenarios)
        {
            md.AppendLine($"### Scenario: {scenarioName}");
            md.AppendLine();
            md.AppendLine("| Architecture | TTFT (ms) | tok/s | tok/s E2E | Peak RSS (MB) | Alloc/tok (B) |");
            md.AppendLine("|--------------|-----------|-------|-----------|---------------|---------------|");

            foreach (var archResult in latestResultsByArch)
            {
                var scenario = archResult.Result.Scenarios
                    .FirstOrDefault(s => s.Name == scenarioName);
                
                if (scenario != null)
                {
                    md.AppendLine($"| {archResult.Architecture} | {scenario.TtftMs.Median:F1} | " +
                        $"{scenario.TokensPerSecond.Median:F2} | " +
                        $"{scenario.TokensPerSecondEndToEnd.Median:F2} | " +
                        $"{scenario.PeakRssMb.Median:F1} | " +
                        $"{scenario.SteadyAllocBytesPerToken.Median:F0} |");
                }
            }
            md.AppendLine();
        }

        // Normalized comparison (if available)
        md.AppendLine("## Normalized Efficiency Comparison");
        md.AppendLine();
        md.AppendLine("These metrics normalize for hardware differences to compare implementation efficiency.");
        md.AppendLine();

        foreach (var scenarioName in commonScenarios)
        {
            md.AppendLine($"### Scenario: {scenarioName}");
            md.AppendLine();
            md.AppendLine("| Architecture | tok/s per core | tok/s per GHz/core | Cycles/tok |");
            md.AppendLine("|--------------|----------------|--------------------|-----------");

            foreach (var archResult in latestResultsByArch)
            {
                var scenario = archResult.Result.Scenarios
                    .FirstOrDefault(s => s.Name == scenarioName);
                
                if (scenario != null)
                {
                    var n = scenario.Normalized;
                    md.AppendLine($"| {archResult.Architecture} | " +
                        $"{NormalizationCalculator.FormatMetric(n.TokPerSecPerCore, "")} | " +
                        $"{NormalizationCalculator.FormatMetric(n.TokPerSecPerGhzPerCore, "")} | " +
                        $"{NormalizationCalculator.FormatMetric(n.CyclesPerToken, "", 0)} |");
                }
            }
            md.AppendLine();
        }

        await File.WriteAllTextAsync(outputPath, md.ToString());
        Console.WriteLine($"Cross-architecture comparison written to: {outputPath}");
    }
}

/// <summary>
/// Result of comparing two scenarios.
/// </summary>
public sealed class ComparisonResult
{
    public string ScenarioName { get; set; } = string.Empty;
    public ScenarioResult Current { get; set; } = new();
    public ScenarioResult? Previous { get; set; }
    public bool IsNew { get; set; }
    
    public double? TtftChange { get; set; }
    public double? TokPerSecChange { get; set; }
    public double? TokPerSecE2EChange { get; set; }
    public double? PeakRssChange { get; set; }
    public double? AllocPerTokenChange { get; set; }
}

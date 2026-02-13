using System;

namespace SmallMind.Benchmarks.Core;

/// <summary>
/// Calculates normalized efficiency metrics for cross-machine comparison.
/// </summary>
public static class NormalizationCalculator
{
    /// <summary>
    /// Calculates normalized metrics for a scenario result.
    /// </summary>
    public static NormalizedMetrics Calculate(ScenarioResult scenario, EnvironmentInfo env)
    {
        var normalized = new NormalizedMetrics();

        var tokPerSec = scenario.TokensPerSecond.Median;
        var threads = scenario.Threads;
        var logicalCores = env.LogicalCoreCount;
        var baseCpuGHz = env.BaseCpuFrequencyGHz;
        var maxCpuGHz = env.MaxCpuFrequencyGHz;
        var allocPerToken = scenario.SteadyAllocBytesPerToken.Median;

        // Tokens per second per core
        // For 1-thread: divide by 1 core used
        // For multi-thread: divide by threads used (not total cores)
        if (threads > 0)
        {
            normalized.TokPerSecPerCore = tokPerSec / threads;
        }

        // Tokens per second per GHz per core
        // Use base frequency if available, otherwise max frequency
        var cpuGHz = baseCpuGHz ?? maxCpuGHz;
        if (cpuGHz.HasValue && threads > 0 && cpuGHz.Value > 0)
        {
            normalized.TokPerSecPerGhzPerCore = tokPerSec / (threads * cpuGHz.Value);
        }

        // Cycles per token (for 1-thread runs only)
        if (threads == 1 && cpuGHz.HasValue && cpuGHz.Value > 0 && tokPerSec > 0)
        {
            // cycles/token ≈ (GHz * 1e9) / (tok/s)
            normalized.CyclesPerToken = (cpuGHz.Value * 1_000_000_000.0) / tokPerSec;
        }

        // Memory efficiency: alloc bytes per token per GHz
        if (cpuGHz.HasValue && cpuGHz.Value > 0 && allocPerToken > 0)
        {
            normalized.AllocBytesPerTokenPerGhz = allocPerToken / cpuGHz.Value;
        }

        return normalized;
    }

    /// <summary>
    /// Formats a normalized metric for display.
    /// </summary>
    public static string FormatMetric(double? value, string unit, int decimals = 2)
    {
        if (!value.HasValue)
            return "N/A";

        var format = $"F{decimals}";
        return $"{value.Value.ToString(format)} {unit}";
    }
}

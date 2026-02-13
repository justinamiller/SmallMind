using System;
using System.Collections.Generic;

namespace SmallMind.Benchmarks.Core.Normalization
{
    /// <summary>
    /// Calculates normalized efficiency metrics for cross-platform comparison.
    /// Emphasizes implementation efficiency rather than absolute machine power.
    /// </summary>
    public static class NormalizationCalculator
    {
        /// <summary>
        /// Calculate normalized metrics from benchmark results.
        /// </summary>
        public static Measurement.BenchmarkNormalized[] CalculateNormalized(
            Measurement.BenchmarkResult[] results,
            Environment.EnvironmentSnapshot env)
        {
            if (results == null || results.Length == 0)
                return Array.Empty<Measurement.BenchmarkNormalized>();

            var normalized = new List<Measurement.BenchmarkNormalized>();

            foreach (var result in results)
            {
                var norm = new Measurement.BenchmarkNormalized
                {
                    ModelName = result.ModelName,
                    ContextSize = result.ContextSize,
                    ThreadCount = result.ThreadCount
                };

                // Tokens per second per core
                if (result.ThreadCount > 0 && result.TokensPerSecond > 0)
                {
                    norm.TokensPerSecondPerCore = result.TokensPerSecond / result.ThreadCount;
                }

                // Tokens per second per GHz per core (if frequency available)
                if (env != null && env.CpuMaxFrequencyMhz.HasValue && env.CpuMaxFrequencyMhz.Value > 0)
                {
                    double ghz = env.CpuMaxFrequencyMhz.Value / 1000.0;
                    if (ghz > 0 && result.ThreadCount > 0 && result.TokensPerSecond > 0)
                    {
                        norm.TokensPerSecondPerGHzPerCore = result.TokensPerSecond / (ghz * result.ThreadCount);
                    }
                }

                // Cycles per token (single-threaded estimate)
                if (env != null && env.CpuMaxFrequencyMhz.HasValue && env.CpuMaxFrequencyMhz.Value > 0 
                    && result.ThreadCount == 1 && result.TokensPerSecond > 0)
                {
                    double ghz = env.CpuMaxFrequencyMhz.Value / 1000.0;
                    double cyclesPerSecond = ghz * 1_000_000_000.0; // GHz to cycles/sec
                    norm.CyclesPerToken = cyclesPerSecond / result.TokensPerSecond;
                }

                // Add notes about limitations
                var notes = new List<string>();
                if (env == null)
                {
                    notes.Add("Environment info unavailable");
                }
                else
                {
                    if (!env.CpuMaxFrequencyMhz.HasValue || env.CpuMaxFrequencyMhz.Value == 0)
                    {
                        notes.Add("CPU frequency unavailable - per-GHz and cycles metrics not calculated");
                    }
                }

                norm.Notes = string.Join("; ", notes);

                normalized.Add(norm);
            }

            return normalized.ToArray();
        }

        /// <summary>
        /// Format normalized metric for display.
        /// </summary>
        public static string FormatNormalizedMetric(double value, string unit, string unavailableText = "N/A")
        {
            if (value <= 0)
                return unavailableText;

            return $"{value:F2} {unit}";
        }
    }
}

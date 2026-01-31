using System;
using System.Collections.Generic;
using System.Linq;

namespace TinyLLM.Core
{
    /// <summary>
    /// Utility for calculating percentile statistics (p50, p95, p99) without external dependencies.
    /// Uses a simple sorting approach for accurate percentile calculation.
    /// </summary>
    public static class PercentileCalculator
    {
        /// <summary>
        /// Calculate percentile value from a list of values.
        /// Returns the value at the specified percentile (0-100).
        /// </summary>
        public static double Percentile(List<double> values, double percentile)
        {
            if (values == null || values.Count == 0)
            {
                return 0.0;
            }

            if (values.Count == 1)
            {
                return values[0];
            }

            // Sort the values
            var sorted = new List<double>(values);
            sorted.Sort();

            // Calculate the index (using linear interpolation between ranks)
            double index = (percentile / 100.0) * (sorted.Count - 1);
            int lowerIndex = (int)Math.Floor(index);
            int upperIndex = (int)Math.Ceiling(index);

            if (lowerIndex == upperIndex)
            {
                return sorted[lowerIndex];
            }

            // Linear interpolation between two adjacent values
            double weight = index - lowerIndex;
            return sorted[lowerIndex] * (1 - weight) + sorted[upperIndex] * weight;
        }

        /// <summary>
        /// Calculate common percentile statistics (min, mean, p50, p95, p99, max).
        /// </summary>
        public static PercentileStats CalculateStats(List<double> values)
        {
            if (values == null || values.Count == 0)
            {
                return new PercentileStats(0, 0, 0, 0, 0, 0);
            }

            var sorted = new List<double>(values);
            sorted.Sort();

            double min = sorted[0];
            double max = sorted[sorted.Count - 1];
            double mean = values.Average();
            double p50 = Percentile(sorted, 50);
            double p95 = Percentile(sorted, 95);
            double p99 = Percentile(sorted, 99);

            return new PercentileStats(min, mean, p50, p95, p99, max);
        }
    }

    /// <summary>
    /// Container for percentile statistics.
    /// </summary>
    public class PercentileStats
    {
        public double Min { get; }
        public double Mean { get; }
        public double P50 { get; }
        public double P95 { get; }
        public double P99 { get; }
        public double Max { get; }

        public PercentileStats(double min, double mean, double p50, double p95, double p99, double max)
        {
            Min = min;
            Mean = mean;
            P50 = p50;
            P95 = p95;
            P99 = p99;
            Max = max;
        }
    }
}

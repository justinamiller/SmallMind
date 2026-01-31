using System;
using System.Collections.Generic;

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
        /// Note: This method does NOT modify the input list, but creates a sorted copy.
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

            // Sort the values (creates a copy to avoid modifying the original)
            var sorted = new List<double>(values);
            sorted.Sort();

            return PercentileFromSorted(sorted, percentile);
        }

        /// <summary>
        /// Calculate percentile value from an already-sorted list.
        /// This is more efficient when calculating multiple percentiles from the same data.
        /// </summary>
        private static double PercentileFromSorted(List<double> sortedValues, double percentile)
        {
            if (sortedValues.Count == 0)
            {
                return 0.0;
            }

            if (sortedValues.Count == 1)
            {
                return sortedValues[0];
            }

            // Calculate the index (using linear interpolation between ranks)
            double index = (percentile / 100.0) * (sortedValues.Count - 1);
            int lowerIndex = (int)Math.Floor(index);
            int upperIndex = (int)Math.Ceiling(index);

            if (lowerIndex == upperIndex)
            {
                return sortedValues[lowerIndex];
            }

            // Linear interpolation between two adjacent values
            double weight = index - lowerIndex;
            return sortedValues[lowerIndex] * (1 - weight) + sortedValues[upperIndex] * weight;
        }

        /// <summary>
        /// Calculate common percentile statistics (min, mean, p50, p95, p99, max).
        /// This method sorts the list once and reuses it for all percentile calculations.
        /// </summary>
        public static PercentileStats CalculateStats(List<double> values)
        {
            if (values == null || values.Count == 0)
            {
                return new PercentileStats(0, 0, 0, 0, 0, 0);
            }

            // Sort once for all percentile calculations
            var sorted = new List<double>(values);
            sorted.Sort();

            double min = sorted[0];
            double max = sorted[sorted.Count - 1];
            
            // Calculate mean manually to avoid LINQ
            double sum = 0;
            for (int i = 0; i < values.Count; i++)
            {
                sum += values[i];
            }
            double mean = sum / values.Count;
            
            // Use the already-sorted list for efficient percentile calculations
            double p50 = PercentileFromSorted(sorted, 50);
            double p95 = PercentileFromSorted(sorted, 95);
            double p99 = PercentileFromSorted(sorted, 99);

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

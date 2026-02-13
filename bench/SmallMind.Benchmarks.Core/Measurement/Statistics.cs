using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmallMind.Benchmarks.Core.Measurement
{
    /// <summary>
    /// Statistical utilities for benchmark measurements.
    /// No LINQ in hot paths - these are used for post-processing only.
    /// </summary>
    public static class Statistics
    {
        /// <summary>
        /// Calculate median of a dataset.
        /// </summary>
        public static double Median(double[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Cannot calculate median of empty array");

            var sorted = new double[values.Length];
            Array.Copy(values, sorted, values.Length);
            Array.Sort(sorted);

            int mid = sorted.Length / 2;
            if (sorted.Length % 2 == 0)
                return (sorted[mid - 1] + sorted[mid]) / 2.0;
            else
                return sorted[mid];
        }

        /// <summary>
        /// Calculate percentile of a dataset.
        /// </summary>
        public static double Percentile(double[] values, double percentile)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Cannot calculate percentile of empty array");
            
            if (percentile < 0 || percentile > 100)
                throw new ArgumentException("Percentile must be between 0 and 100");

            var sorted = new double[values.Length];
            Array.Copy(values, sorted, values.Length);
            Array.Sort(sorted);

            double index = (percentile / 100.0) * (sorted.Length - 1);
            int lower = (int)Math.Floor(index);
            int upper = (int)Math.Ceiling(index);

            if (lower == upper)
                return sorted[lower];

            double fraction = index - lower;
            return sorted[lower] * (1 - fraction) + sorted[upper] * fraction;
        }

        /// <summary>
        /// Calculate standard deviation of a dataset.
        /// </summary>
        public static double StandardDeviation(double[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Cannot calculate standard deviation of empty array");

            double mean = Mean(values);
            double sumSquaredDiffs = 0.0;
            
            for (int i = 0; i < values.Length; i++)
            {
                double diff = values[i] - mean;
                sumSquaredDiffs += diff * diff;
            }

            return Math.Sqrt(sumSquaredDiffs / values.Length);
        }

        /// <summary>
        /// Calculate mean of a dataset.
        /// </summary>
        public static double Mean(double[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("Cannot calculate mean of empty array");

            double sum = 0.0;
            for (int i = 0; i < values.Length; i++)
            {
                sum += values[i];
            }

            return sum / values.Length;
        }
    }

    /// <summary>
    /// Memory measurement utilities.
    /// </summary>
    public static class MemoryMeasurement
    {
        /// <summary>
        /// Get current process peak RSS (Resident Set Size / Working Set).
        /// </summary>
        public static long GetPeakRssBytes()
        {
            using var process = Process.GetCurrentProcess();
            return process.PeakWorkingSet64;
        }

        /// <summary>
        /// Get current process working set.
        /// </summary>
        public static long GetCurrentRssBytes()
        {
            using var process = Process.GetCurrentProcess();
            return process.WorkingSet64;
        }

        /// <summary>
        /// Get GC statistics.
        /// </summary>
        public static (int gen0, int gen1, int gen2) GetGCCollectionCounts()
        {
            return (
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2)
            );
        }

        /// <summary>
        /// Force GC and wait for finalization (use sparingly).
        /// </summary>
        public static void ForceGCAndWait()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }
    }
}

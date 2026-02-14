namespace SmallMind.Benchmarks.Utils
{
    /// <summary>
    /// Simple percentile calculator for benchmark metrics.
    /// Uses linear interpolation between ranks.
    /// </summary>
    internal static class PercentileCalculator
    {
        /// <summary>
        /// Calculate percentile (0-100) from sorted data.
        /// </summary>
        public static double Percentile(List<double> sortedData, double percentile)
        {
            if (sortedData == null || sortedData.Count == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(sortedData));

            if (percentile < 0 || percentile > 100)
                throw new ArgumentOutOfRangeException(nameof(percentile), "Percentile must be between 0 and 100");

            if (sortedData.Count == 1)
                return sortedData[0];

            // Use linear interpolation between ranks
            double n = sortedData.Count;
            double rank = (percentile / 100.0) * (n - 1);
            int lowerIndex = (int)Math.Floor(rank);
            int upperIndex = (int)Math.Ceiling(rank);

            if (lowerIndex == upperIndex)
                return sortedData[lowerIndex];

            double lowerValue = sortedData[lowerIndex];
            double upperValue = sortedData[upperIndex];
            double fraction = rank - lowerIndex;

            return lowerValue + (upperValue - lowerValue) * fraction;
        }

        /// <summary>
        /// Calculate multiple percentiles from the same sorted data.
        /// </summary>
        public static Dictionary<string, double> CalculatePercentiles(List<double> sortedData, params double[] percentiles)
        {
            var results = new Dictionary<string, double>();

            foreach (var p in percentiles)
            {
                results[$"p{p}"] = Percentile(sortedData, p);
            }

            return results;
        }

        /// <summary>
        /// Calculate common statistics (min, max, mean, median, p50, p95, p99).
        /// </summary>
        public static Statistics CalculateStatistics(List<double> data)
        {
            if (data == null || data.Count == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            var sorted = new List<double>(data);
            sorted.Sort();

            double sum = 0;
            double min = double.MaxValue;
            double max = double.MinValue;

            foreach (var value in data)
            {
                sum += value;
                if (value < min) min = value;
                if (value > max) max = value;
            }

            double mean = sum / data.Count;

            return new Statistics
            {
                Count = data.Count,
                Min = min,
                Max = max,
                Mean = mean,
                Median = Percentile(sorted, 50),
                P50 = Percentile(sorted, 50),
                P95 = Percentile(sorted, 95),
                P99 = Percentile(sorted, 99),
                StdDev = CalculateStdDev(data, mean)
            };
        }

        private static double CalculateStdDev(List<double> data, double mean)
        {
            if (data.Count < 2)
                return 0.0;

            double sumSquaredDiff = 0;
            foreach (var value in data)
            {
                double diff = value - mean;
                sumSquaredDiff += diff * diff;
            }

            return Math.Sqrt(sumSquaredDiff / (data.Count - 1));
        }
    }

    /// <summary>
    /// Statistical summary of a dataset.
    /// </summary>
    internal sealed class Statistics
    {
        public int Count { get; init; }
        public double Min { get; init; }
        public double Max { get; init; }
        public double Mean { get; init; }
        public double Median { get; init; }
        public double P50 { get; init; }
        public double P95 { get; init; }
        public double P99 { get; init; }
        public double StdDev { get; init; }
    }
}

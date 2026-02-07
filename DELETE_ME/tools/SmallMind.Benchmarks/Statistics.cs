using System.Diagnostics;

namespace SmallMind.Benchmarks;

/// <summary>
/// High-resolution timing utilities using Stopwatch.
/// </summary>
public static class TimingUtils
{
    private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;
    private static readonly double TicksToSeconds = 1.0 / Stopwatch.Frequency;
    
    public static long GetTimestamp() => Stopwatch.GetTimestamp();
    
    public static double TicksToMilliseconds(long ticks) => ticks * TicksToMs;
    
    public static double TicksToSecondsDouble(long ticks) => ticks * TicksToSeconds;
    
    public static TimeSpan TicksToTimeSpan(long ticks)
    {
        return TimeSpan.FromSeconds(ticks * TicksToSeconds);
    }
}

/// <summary>
/// Statistical calculations without LINQ.
/// Uses nearest-rank method for percentiles.
/// </summary>
public static class Statistics
{
    /// <summary>
    /// Calculate percentile using nearest-rank method.
    /// Values array will be sorted in-place.
    /// </summary>
    public static double Percentile(double[] values, double percentile)
    {
        if (values.Length == 0)
            throw new ArgumentException("Values array is empty");
            
        Array.Sort(values);
        
        if (values.Length == 1)
            return values[0];
            
        double n = (values.Length - 1) * percentile + 1;
        int k = (int)n;
        double d = n - k;
        
        if (k >= values.Length)
            return values[values.Length - 1];
        if (k < 1)
            return values[0];
            
        // Linear interpolation
        return values[k - 1] + d * (values[k] - values[k - 1]);
    }
    
    /// <summary>
    /// Calculate mean (average).
    /// </summary>
    public static double Mean(double[] values)
    {
        if (values.Length == 0)
            return 0.0;
            
        double sum = 0.0;
        for (int i = 0; i < values.Length; i++)
        {
            sum += values[i];
        }
        return sum / values.Length;
    }
    
    /// <summary>
    /// Calculate standard deviation using numerically stable algorithm.
    /// </summary>
    public static double StandardDeviation(double[] values)
    {
        if (values.Length < 2)
            return 0.0;
            
        double mean = Mean(values);
        double sumSquaredDiff = 0.0;
        
        for (int i = 0; i < values.Length; i++)
        {
            double diff = values[i] - mean;
            sumSquaredDiff += diff * diff;
        }
        
        return Math.Sqrt(sumSquaredDiff / (values.Length - 1));
    }
    
    /// <summary>
    /// Calculate min value.
    /// </summary>
    public static double Min(double[] values)
    {
        if (values.Length == 0)
            throw new ArgumentException("Values array is empty");
            
        double min = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] < min)
                min = values[i];
        }
        return min;
    }
    
    /// <summary>
    /// Calculate max value.
    /// </summary>
    public static double Max(double[] values)
    {
        if (values.Length == 0)
            throw new ArgumentException("Values array is empty");
            
        double max = values[0];
        for (int i = 1; i < values.Length; i++)
        {
            if (values[i] > max)
                max = values[i];
        }
        return max;
    }
}

/// <summary>
/// Aggregated statistics for a metric.
/// </summary>
public sealed class AggregatedStats
{
    public double Min { get; set; }
    public double Max { get; set; }
    public double Mean { get; set; }
    public double StdDev { get; set; }
    public double P50 { get; set; }
    public double P90 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
    
    public static AggregatedStats Calculate(double[] values)
    {
        if (values.Length == 0)
        {
            return new AggregatedStats();
        }
        
        // Make a copy to avoid modifying original
        double[] sorted = new double[values.Length];
        Array.Copy(values, sorted, values.Length);
        
        return new AggregatedStats
        {
            Min = Statistics.Min(sorted),
            Max = Statistics.Max(sorted),
            Mean = Statistics.Mean(sorted),
            StdDev = Statistics.StandardDeviation(sorted),
            P50 = Statistics.Percentile(sorted, 0.50),
            P90 = Statistics.Percentile(sorted, 0.90),
            P95 = Statistics.Percentile(sorted, 0.95),
            P99 = Statistics.Percentile(sorted, 0.99)
        };
    }
}

using System;
using System.Diagnostics;
using System.Linq;

namespace SmallMind.Tests.Utilities
{
    /// <summary>
    /// Utility methods for regression testing.
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Determines if performance tests should run based on environment variable.
        /// </summary>
        public static bool ShouldRunPerfTests()
        {
            var envVar = Environment.GetEnvironmentVariable("RUN_PERF_TESTS");
            return !string.IsNullOrEmpty(envVar) && 
                   envVar.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines if allocation diagnostics should be enabled.
        /// </summary>
        public static bool AllocationDiagnosticsEnabled()
        {
            var envVar = Environment.GetEnvironmentVariable("ALLOCATION_DIAGNOSTICS");
            return !string.IsNullOrEmpty(envVar) && 
                   envVar.Equals("true", StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Tracks allocations and GC behavior during tests.
    /// </summary>
    public sealed class AllocationTracker : IDisposable
    {
        private readonly long _startBytes;
        private readonly int _gen0Start;
        private readonly int _gen1Start;
        private readonly int _gen2Start;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public AllocationTracker()
        {
            // Force a collection to get clean baseline
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            _startBytes = GC.GetAllocatedBytesForCurrentThread();
            _gen0Start = GC.CollectionCount(0);
            _gen1Start = GC.CollectionCount(1);
            _gen2Start = GC.CollectionCount(2);
            _stopwatch = Stopwatch.StartNew();
        }

        public AllocationReport Stop()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AllocationTracker));
            }

            _stopwatch.Stop();
            
            var endBytes = GC.GetAllocatedBytesForCurrentThread();
            var gen0End = GC.CollectionCount(0);
            var gen1End = GC.CollectionCount(1);
            var gen2End = GC.CollectionCount(2);

            return new AllocationReport
            {
                AllocatedBytes = endBytes - _startBytes,
                Gen0Collections = gen0End - _gen0Start,
                Gen1Collections = gen1End - _gen1Start,
                Gen2Collections = gen2End - _gen2Start,
                ElapsedMilliseconds = _stopwatch.Elapsed.TotalMilliseconds
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Report of allocations and GC activity.
    /// </summary>
    public sealed class AllocationReport
    {
        public long AllocatedBytes { get; init; }
        public int Gen0Collections { get; init; }
        public int Gen1Collections { get; init; }
        public int Gen2Collections { get; init; }
        public double ElapsedMilliseconds { get; init; }

        public double AllocatedKB => AllocatedBytes / 1024.0;
        public double AllocatedMB => AllocatedBytes / (1024.0 * 1024.0);

        public override string ToString()
        {
            return $"Allocated: {AllocatedKB:F2} KB, " +
                   $"Gen0: {Gen0Collections}, Gen1: {Gen1Collections}, Gen2: {Gen2Collections}, " +
                   $"Time: {ElapsedMilliseconds:F2}ms";
        }

        /// <summary>
        /// Gets a detailed diagnostic message.
        /// </summary>
        public string GetDiagnostics()
        {
            return $@"Allocation Report:
  Total Allocated: {AllocatedBytes:N0} bytes ({AllocatedMB:F2} MB)
  Gen0 Collections: {Gen0Collections}
  Gen1 Collections: {Gen1Collections}
  Gen2 Collections: {Gen2Collections} {(Gen2Collections > 0 ? "⚠️ WARNING" : "✓")}
  Elapsed Time: {ElapsedMilliseconds:F2} ms";
        }
    }

    /// <summary>
    /// Utility for running benchmarks with warmup.
    /// </summary>
    public static class BenchmarkRunner
    {
        /// <summary>
        /// Runs a benchmark with warmup and returns average time in milliseconds.
        /// </summary>
        /// <param name="action">The action to benchmark</param>
        /// <param name="warmupIterations">Number of warmup runs (default: 3)</param>
        /// <param name="measureIterations">Number of measured runs (default: 10)</param>
        /// <returns>Average milliseconds per iteration</returns>
        public static double Run(Action action, int warmupIterations = 3, int measureIterations = 10)
        {
            // Warmup
            for (int i = 0; i < warmupIterations; i++)
            {
                action();
            }

            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < measureIterations; i++)
            {
                action();
            }
            sw.Stop();

            return sw.Elapsed.TotalMilliseconds / measureIterations;
        }

        /// <summary>
        /// Runs a benchmark with warmup and returns detailed timing information.
        /// </summary>
        public static BenchmarkResult RunDetailed(Action action, int warmupIterations = 3, int measureIterations = 10)
        {
            var times = new double[measureIterations];

            // Warmup
            for (int i = 0; i < warmupIterations; i++)
            {
                action();
            }

            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure individual runs
            for (int i = 0; i < measureIterations; i++)
            {
                var sw = Stopwatch.StartNew();
                action();
                sw.Stop();
                times[i] = sw.Elapsed.TotalMilliseconds;
            }

            return new BenchmarkResult(times);
        }
    }

    /// <summary>
    /// Detailed benchmark results with statistics.
    /// </summary>
    public sealed class BenchmarkResult
    {
        private readonly double[] _times;

        public BenchmarkResult(double[] times)
        {
            _times = times;
            Array.Sort(_times);

            Mean = _times.Length > 0 ? _times.Average() : 0;
            Min = _times.Length > 0 ? _times[0] : 0;
            Max = _times.Length > 0 ? _times[^1] : 0;
            Median = _times.Length > 0 ? _times[_times.Length / 2] : 0;

            if (_times.Length > 1)
            {
                var variance = _times.Select(t => Math.Pow(t - Mean, 2)).Average();
                StdDev = Math.Sqrt(variance);
            }
        }

        public double Mean { get; }
        public double Min { get; }
        public double Max { get; }
        public double Median { get; }
        public double StdDev { get; }

        public override string ToString()
        {
            return $"Mean: {Mean:F2}ms, Median: {Median:F2}ms, " +
                   $"Min: {Min:F2}ms, Max: {Max:F2}ms, StdDev: {StdDev:F2}ms";
        }
    }
}

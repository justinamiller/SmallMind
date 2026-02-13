using System;
using System.Diagnostics;

namespace SmallMind.Benchmarks.Utils
{
    /// <summary>
    /// High-precision timer using Stopwatch.GetTimestamp() for accurate benchmarking.
    /// Avoids DateTime overhead and provides nanosecond-level precision.
    /// </summary>
    internal sealed class PrecisionTimer
    {
        private static readonly double s_ticksToNanoseconds = 1_000_000_000.0 / Stopwatch.Frequency;
        private static readonly double s_ticksToMicroseconds = 1_000_000.0 / Stopwatch.Frequency;
        private static readonly double s_ticksToMilliseconds = 1_000.0 / Stopwatch.Frequency;

        private long _startTicks;
        private long _stopTicks;

        /// <summary>
        /// Start the timer.
        /// </summary>
        public void Start()
        {
            _startTicks = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Stop the timer.
        /// </summary>
        public void Stop()
        {
            _stopTicks = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Reset the timer.
        /// </summary>
        public void Reset()
        {
            _startTicks = 0;
            _stopTicks = 0;
        }

        /// <summary>
        /// Get elapsed time in nanoseconds.
        /// </summary>
        public double ElapsedNanoseconds => (_stopTicks - _startTicks) * s_ticksToNanoseconds;

        /// <summary>
        /// Get elapsed time in microseconds.
        /// </summary>
        public double ElapsedMicroseconds => (_stopTicks - _startTicks) * s_ticksToMicroseconds;

        /// <summary>
        /// Get elapsed time in milliseconds.
        /// </summary>
        public double ElapsedMilliseconds => (_stopTicks - _startTicks) * s_ticksToMilliseconds;

        /// <summary>
        /// Get elapsed time in seconds.
        /// </summary>
        public double ElapsedSeconds => ElapsedMilliseconds / 1000.0;

        /// <summary>
        /// Measure the execution time of an action.
        /// </summary>
        public static double MeasureMilliseconds(Action action)
        {
            var timer = new PrecisionTimer();
            timer.Start();
            action();
            timer.Stop();
            return timer.ElapsedMilliseconds;
        }

        /// <summary>
        /// Measure the execution time of an action and return the result.
        /// </summary>
        public static (T result, double milliseconds) Measure<T>(Func<T> func)
        {
            var timer = new PrecisionTimer();
            timer.Start();
            var result = func();
            timer.Stop();
            return (result, timer.ElapsedMilliseconds);
        }

        /// <summary>
        /// Get current timestamp in ticks (high precision).
        /// </summary>
        public static long GetTimestamp() => Stopwatch.GetTimestamp();

        /// <summary>
        /// Convert ticks to milliseconds.
        /// </summary>
        public static double TicksToMilliseconds(long ticks) => ticks * s_ticksToMilliseconds;

        /// <summary>
        /// Convert ticks to microseconds.
        /// </summary>
        public static double TicksToMicroseconds(long ticks) => ticks * s_ticksToMicroseconds;

        /// <summary>
        /// Calculate elapsed milliseconds between two timestamps.
        /// </summary>
        public static double CalculateElapsedMilliseconds(long startTicks, long endTicks)
        {
            return (endTicks - startTicks) * s_ticksToMilliseconds;
        }
    }
}

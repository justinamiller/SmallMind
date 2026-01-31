using System;
using System.Diagnostics;
using Xunit;
using SmallMind.Simd;

namespace SmallMind.PerfTests
{
    /// <summary>
    /// Performance regression tests with conservative thresholds.
    /// These tests run only when RUN_PERF_TESTS environment variable is set.
    /// Set RUN_PERF_TESTS=true to enable these tests.
    /// They establish baseline performance and fail only on significant regressions.
    /// </summary>
    public class PerformanceRegressionTests
    {
        private const int WarmupIterations = 3;
        private const int MeasureIterations = 10;

        private static bool ShouldRunPerfTests()
        {
            var envVar = Environment.GetEnvironmentVariable("RUN_PERF_TESTS");
            return !string.IsNullOrEmpty(envVar) && envVar.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        #region MatMul Performance Tests

        [Fact]
        public void MatMul_128x128_CompletesWithinThreshold()
        {
            if (!ShouldRunPerfTests())
            {
                // Silent skip - test will pass but not run
                return;
            }

            // Arrange
            const int size = 128;
            var a = new float[size * size];
            var b = new float[size * size];
            var c = new float[size * size];

            FillRandom(a);
            FillRandom(b);

            // Warmup
            for (int i = 0; i < WarmupIterations; i++)
            {
                MatMulOps.MatMul(a, b, c, size, size, size);
            }

            // Measure
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                MatMulOps.MatMul(a, b, c, size, size, size);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / MeasureIterations;

            // Assert - Conservative threshold (10ms per operation on typical hardware)
            Assert.True(avgMs < 10.0, $"MatMul 128x128 took {avgMs:F2}ms on average, expected < 10ms");
        }

        [Fact]
        public void MatMul_256x256_CompletesWithinThreshold()
        {
            if (!ShouldRunPerfTests()) return;

            const int size = 256;
            var a = new float[size * size];
            var b = new float[size * size];
            var c = new float[size * size];

            FillRandom(a);
            FillRandom(b);

            for (int i = 0; i < WarmupIterations; i++)
            {
                MatMulOps.MatMul(a, b, c, size, size, size);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                MatMulOps.MatMul(a, b, c, size, size, size);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / MeasureIterations;
            Assert.True(avgMs < 80.0, $"MatMul 256x256 took {avgMs:F2}ms on average, expected < 80ms");
        }

        #endregion

        #region Softmax Performance Tests

        [Fact]
        public void Softmax_4096Elements_CompletesWithinThreshold()
        {
            if (!ShouldRunPerfTests()) return;

            const int size = 4096;
            var input = new float[size];
            var output = new float[size];

            FillRandom(input);

            for (int i = 0; i < WarmupIterations; i++)
            {
                SoftmaxOps.Softmax2D(input, output, 1, size);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                SoftmaxOps.Softmax2D(input, output, 1, size);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / MeasureIterations;
            Assert.True(avgMs < 2.0, $"Softmax 4096 took {avgMs:F2}ms on average, expected < 2ms");
        }

        [Fact]
        public void Softmax_8192Elements_CompletesWithinThreshold()
        {
            if (!ShouldRunPerfTests()) return;

            const int size = 8192;
            var input = new float[size];
            var output = new float[size];

            FillRandom(input);

            for (int i = 0; i < WarmupIterations; i++)
            {
                SoftmaxOps.Softmax2D(input, output, 1, size);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                SoftmaxOps.Softmax2D(input, output, 1, size);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / MeasureIterations;
            Assert.True(avgMs < 5.0, $"Softmax 8192 took {avgMs:F2}ms on average, expected < 5ms");
        }

        #endregion

        #region DotProduct Performance Tests

        [Fact]
        public void DotProduct_4096Elements_CompletesWithinThreshold()
        {
            if (!ShouldRunPerfTests()) return;

            const int size = 4096;
            var a = new float[size];
            var b = new float[size];

            FillRandom(a);
            FillRandom(b);

            for (int i = 0; i < WarmupIterations; i++)
            {
                MatMulOps.DotProduct(a, b);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations * 100; i++)
            {
                MatMulOps.DotProduct(a, b);
            }
            sw.Stop();

            double avgUs = (sw.Elapsed.TotalMilliseconds * 1000.0) / (MeasureIterations * 100);
            Assert.True(avgUs < 50.0, $"DotProduct 4096 took {avgUs:F2}µs on average, expected < 50µs");
        }

        #endregion

        #region Activation Performance Tests

        [Fact]
        public void ReLU_10M_Elements_CompletesWithinThreshold()
        {
            if (!ShouldRunPerfTests()) return;

            const int size = 10_000_000;
            var input = new float[size];
            var output = new float[size];

            FillRandom(input, -1.0f, 1.0f);

            for (int i = 0; i < WarmupIterations; i++)
            {
                ActivationOps.ReLU(input, output);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                ActivationOps.ReLU(input, output);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / MeasureIterations;
            Assert.True(avgMs < 50.0, $"ReLU 10M elements took {avgMs:F2}ms on average, expected < 50ms");
        }

        [Fact]
        public void GELU_1M_Elements_CompletesWithinThreshold()
        {
            if (!ShouldRunPerfTests()) return;

            const int size = 1_000_000;
            var input = new float[size];
            var output = new float[size];

            FillRandom(input, -3.0f, 3.0f);

            for (int i = 0; i < WarmupIterations; i++)
            {
                ActivationOps.GELU(input, output);
            }

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < MeasureIterations; i++)
            {
                ActivationOps.GELU(input, output);
            }
            sw.Stop();

            double avgMs = sw.Elapsed.TotalMilliseconds / MeasureIterations;
            Assert.True(avgMs < 30.0, $"GELU 1M elements took {avgMs:F2}ms on average, expected < 30ms");
        }

        #endregion

        #region Helper Methods

        private static readonly Random _random = new Random(42);

        private static void FillRandom(float[] array, float min = 0.0f, float max = 1.0f)
        {
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = min + (float)_random.NextDouble() * (max - min);
            }
        }

        #endregion
    }
}

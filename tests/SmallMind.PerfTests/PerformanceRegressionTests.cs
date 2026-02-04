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

            // Assert - Target: ~10-12ms after optimization (was 21.3ms baseline)
            // Conservative threshold allows for hardware variation
            Assert.True(avgMs < 15.0, $"MatMul 128x128 took {avgMs:F2}ms on average, expected < 15ms");
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

        [Fact]
        public void MatMul_512x512_CompletesWithinThreshold()
        {
            if (!ShouldRunPerfTests()) return;

            const int size = 512;
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
            // Target: ~95-100ms after optimization (was 103ms baseline)
            // Conservative threshold allows for hardware variation
            Assert.True(avgMs < 110.0, $"MatMul 512x512 took {avgMs:F2}ms on average, expected < 110ms");
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

        #region Workspace Reuse Correctness Tests

        [Fact]
        public void MatMul_WithWorkspaceReuse_ProducesCorrectResults()
        {
            // Arrange
            var workspace = new SmallMind.Transformers.TensorWorkspace();
            int M = 4, K = 4, N = 4;
            
            // Create test matrices
            var A = new float[] { 1,2,3,4, 5,6,7,8, 9,10,11,12, 13,14,15,16 };
            var B = new float[] { 1,0,0,0, 0,1,0,0, 0,0,1,0, 0,0,0,1 }; // Identity
            var C = workspace.GetOrCreate("result", new[] { M, N }, requiresGrad: false);
            
            // First MatMul: A × Identity = A
            MatMulOps.MatMul(A, B, C.Data, M, K, N);
            
            // Assert: Result should equal A (identity multiplication)
            for (int i = 0; i < M * N; i++)
            {
                Assert.Equal(A[i], C.Data[i], precision: 5);
            }
            
            // Reuse workspace for second MatMul
            var C2 = workspace.GetOrCreate("result", new[] { M, N }, requiresGrad: false);
            Assert.Same(C, C2); // Same instance
            
            // Second MatMul with different A
            var A2 = new float[] { 2,4,6,8, 10,12,14,16, 18,20,22,24, 26,28,30,32 };
            MatMulOps.MatMul(A2, B, C2.Data, M, K, N);
            
            // Assert: Result should equal A2 (MatMul clears output internally)
            for (int i = 0; i < M * N; i++)
            {
                Assert.Equal(A2[i], C2.Data[i], precision: 5);
            }
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
        public void GELU_10K_Elements_CompletesWithinThreshold()
        {
            if (!ShouldRunPerfTests()) return;

            const int size = 10_000;
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
            // Target: ~1.2-1.3ms after optimization (was 2.0ms baseline)
            Assert.True(avgMs < 1.5, $"GELU 10K elements took {avgMs:F2}ms on average, expected < 1.5ms");
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
            // Should maintain performance, not regress (baseline was ~74ms)
            Assert.True(avgMs < 80.0, $"GELU 1M elements took {avgMs:F2}ms on average, expected < 80ms (no regression)");
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

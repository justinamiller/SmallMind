using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SmallMind.Simd;

namespace SmallMind.Benchmarks
{
    /// <summary>
    /// Simple benchmark harness for SIMD kernels.
    /// Measures performance improvements over scalar implementations.
    /// </summary>
    class SimdBenchmarks
    {
        private static BenchmarkReport _report = new();

        static void Main(string[] args)
        {
            Console.WriteLine("=== SmallMind SIMD Benchmarks ===\n");
            
            // Collect system information
            Console.WriteLine("Collecting system metadata...");
            _report.SystemInfo = SystemInfoCollector.Collect();
            _report.ReportTimestamp = DateTime.UtcNow;
            Console.WriteLine();
            
            // Display CPU capabilities
            SimdCapabilities.PrintCapabilities();
            Console.WriteLine();

            // Warn if not Release build
            if (!_report.SystemInfo.Build.IsReleaseBuild)
            {
                Console.WriteLine("⚠️  WARNING: Running in Debug mode. Results may not be representative.");
                Console.WriteLine("   Build in Release mode for accurate benchmarks.\n");
            }

            // Run benchmarks
            BenchmarkElementWiseAdd();
            BenchmarkReLU();
            BenchmarkSoftmax();
            BenchmarkMatMul();
            BenchmarkDotProduct();

            Console.WriteLine("\n=== Benchmarks Complete ===");
            
            // Generate and save reports
            SaveReports();
        }

        static void BenchmarkElementWiseAdd()
        {
            Console.WriteLine("--- Element-wise Add Benchmark ---");
            
            const int size = 10_000_000;
            const int iterations = 100;
            
            float[] a = new float[size];
            float[] b = new float[size];
            float[] result = new float[size];
            
            // Initialize with random data
            Random rand = new Random(42);
            for (int i = 0; i < size; i++)
            {
                a[i] = (float)rand.NextDouble();
                b[i] = (float)rand.NextDouble();
            }

            // Warmup
            ElementWiseOps.Add(a, b, result);

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                ElementWiseOps.Add(a, b, result);
            }
            sw.Stop();

            double msPerOp = sw.Elapsed.TotalMilliseconds / iterations;
            double gbPerSec = (size * sizeof(float) * 3) / (msPerOp / 1000.0) / (1024 * 1024 * 1024);
            
            Console.WriteLine($"  Size: {size:N0} elements");
            Console.WriteLine($"  Time: {msPerOp:F3} ms/op");
            Console.WriteLine($"  Throughput: {gbPerSec:F2} GB/s");
            Console.WriteLine();

            // Record result
            var benchResult = new BenchmarkResult
            {
                Name = "Element-wise Add",
                Timestamp = DateTime.UtcNow,
                Parameters = new Dictionary<string, object>
                {
                    ["Size"] = size,
                    ["Iterations"] = iterations
                },
                Metrics = new Dictionary<string, double>
                {
                    ["Time (ms/op)"] = msPerOp,
                    ["Throughput (GB/s)"] = gbPerSec
                }
            };
            _report.Results.Add(benchResult);
        }

        static void BenchmarkReLU()
        {
            Console.WriteLine("--- ReLU Activation Benchmark ---");
            
            const int size = 10_000_000;
            const int iterations = 100;
            
            float[] input = new float[size];
            float[] output = new float[size];
            
            Random rand = new Random(42);
            for (int i = 0; i < size; i++)
            {
                input[i] = (float)(rand.NextDouble() * 2 - 1); // -1 to 1
            }

            // Warmup
            ActivationOps.ReLU(input, output);

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                ActivationOps.ReLU(input, output);
            }
            sw.Stop();

            double msPerOp = sw.Elapsed.TotalMilliseconds / iterations;
            double gbPerSec = (size * sizeof(float) * 2) / (msPerOp / 1000.0) / (1024 * 1024 * 1024);
            
            Console.WriteLine($"  Size: {size:N0} elements");
            Console.WriteLine($"  Time: {msPerOp:F3} ms/op");
            Console.WriteLine($"  Throughput: {gbPerSec:F2} GB/s");
            Console.WriteLine();

            // Record result
            var benchResult = new BenchmarkResult
            {
                Name = "ReLU Activation",
                Timestamp = DateTime.UtcNow,
                Parameters = new Dictionary<string, object>
                {
                    ["Size"] = size,
                    ["Iterations"] = iterations
                },
                Metrics = new Dictionary<string, double>
                {
                    ["Time (ms/op)"] = msPerOp,
                    ["Throughput (GB/s)"] = gbPerSec
                }
            };
            _report.Results.Add(benchResult);
        }

        static void BenchmarkSoftmax()
        {
            Console.WriteLine("--- Softmax Benchmark ---");
            
            const int rows = 1000;
            const int cols = 1000;
            const int iterations = 10;
            
            float[] input = new float[rows * cols];
            float[] output = new float[rows * cols];
            
            Random rand = new Random(42);
            for (int i = 0; i < input.Length; i++)
            {
                input[i] = (float)rand.NextDouble() * 10;
            }

            // Warmup
            SoftmaxOps.Softmax2D(input, output, rows, cols);

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                SoftmaxOps.Softmax2D(input, output, rows, cols);
            }
            sw.Stop();

            double msPerOp = sw.Elapsed.TotalMilliseconds / iterations;
            
            Console.WriteLine($"  Size: {rows} x {cols}");
            Console.WriteLine($"  Time: {msPerOp:F3} ms/op");
            Console.WriteLine();

            // Record result
            var benchResult = new BenchmarkResult
            {
                Name = "Softmax",
                Timestamp = DateTime.UtcNow,
                Parameters = new Dictionary<string, object>
                {
                    ["Rows"] = rows,
                    ["Cols"] = cols,
                    ["Iterations"] = iterations
                },
                Metrics = new Dictionary<string, double>
                {
                    ["Time (ms/op)"] = msPerOp
                }
            };
            _report.Results.Add(benchResult);
        }

        static void BenchmarkMatMul()
        {
            Console.WriteLine("--- Matrix Multiplication Benchmark ---");
            
            const int M = 512;
            const int K = 512;
            const int N = 512;
            const int iterations = 10;
            
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C = new float[M * N];
            
            Random rand = new Random(42);
            for (int i = 0; i < A.Length; i++) A[i] = (float)rand.NextDouble();
            for (int i = 0; i < B.Length; i++) B[i] = (float)rand.NextDouble();

            // Warmup
            MatMulOps.MatMul(A, B, C, M, K, N);

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                MatMulOps.MatMul(A, B, C, M, K, N);
            }
            sw.Stop();

            double msPerOp = sw.Elapsed.TotalMilliseconds / iterations;
            long flops = 2L * M * K * N; // 2 ops per multiply-add
            double gflops = (flops / (msPerOp / 1000.0)) / 1e9;
            
            Console.WriteLine($"  Size: {M} x {K} × {K} x {N}");
            Console.WriteLine($"  Time: {msPerOp:F3} ms/op");
            Console.WriteLine($"  Performance: {gflops:F2} GFLOPS");
            Console.WriteLine();

            // Record result
            var benchResult = new BenchmarkResult
            {
                Name = "Matrix Multiplication",
                Timestamp = DateTime.UtcNow,
                Parameters = new Dictionary<string, object>
                {
                    ["M"] = M,
                    ["K"] = K,
                    ["N"] = N,
                    ["Iterations"] = iterations
                },
                Metrics = new Dictionary<string, double>
                {
                    ["Time (ms/op)"] = msPerOp,
                    ["Performance (GFLOPS)"] = gflops
                }
            };
            _report.Results.Add(benchResult);
        }

        static void BenchmarkDotProduct()
        {
            Console.WriteLine("--- Dot Product Benchmark ---");
            
            const int size = 10_000_000;
            const int iterations = 100;
            
            float[] a = new float[size];
            float[] b = new float[size];
            
            Random rand = new Random(42);
            for (int i = 0; i < size; i++)
            {
                a[i] = (float)rand.NextDouble();
                b[i] = (float)rand.NextDouble();
            }

            // Warmup
            float result = MatMulOps.DotProduct(a, b);

            // Benchmark
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                result = MatMulOps.DotProduct(a, b);
            }
            sw.Stop();

            double msPerOp = sw.Elapsed.TotalMilliseconds / iterations;
            double gflops = (size * 2 / (msPerOp / 1000.0)) / 1e9; // 2 ops per multiply-add
            
            Console.WriteLine($"  Size: {size:N0} elements");
            Console.WriteLine($"  Time: {msPerOp:F3} ms/op");
            Console.WriteLine($"  Performance: {gflops:F2} GFLOPS");
            Console.WriteLine($"  Result (sanity check): {result:F6}");
            Console.WriteLine();

            // Record result
            var benchResult = new BenchmarkResult
            {
                Name = "Dot Product",
                Timestamp = DateTime.UtcNow,
                Parameters = new Dictionary<string, object>
                {
                    ["Size"] = size,
                    ["Iterations"] = iterations
                },
                Metrics = new Dictionary<string, double>
                {
                    ["Time (ms/op)"] = msPerOp,
                    ["Performance (GFLOPS)"] = gflops
                }
            };
            _report.Results.Add(benchResult);
        }

        static void SaveReports()
        {
            try
            {
                // Generate markdown report
                var markdownReport = MarkdownReportWriter.GenerateReport(_report);
                var markdownPath = "benchmark-results.md";
                File.WriteAllText(markdownPath, markdownReport);
                Console.WriteLine($"\n✓ Markdown report saved to: {markdownPath}");

                // Generate JSON report
                var jsonReport = JsonReportWriter.GenerateReport(_report);
                var jsonPath = "benchmark-results.json";
                File.WriteAllText(jsonPath, jsonReport);
                Console.WriteLine($"✓ JSON report saved to: {jsonPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ Error saving reports: {ex.Message}");
            }
        }
    }
}

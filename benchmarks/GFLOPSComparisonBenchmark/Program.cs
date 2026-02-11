using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmallMind.Core.Simd;

namespace SmallMind.Benchmarks.GFLOPSComparison
{
    /// <summary>
    /// Comprehensive GFLOPS comparison benchmark for comparing PR approaches.
    /// Measures GFLOPS, memory allocations, GC pressure, and other LLM-relevant metrics.
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║        SmallMind - GFLOPS Comparison Benchmark Suite              ║");
            Console.WriteLine("║   Comprehensive performance testing for PR #192 vs PR #193        ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            var benchmark = new GFLOPSComparisonBenchmark();
            benchmark.Run();
        }
    }

    public class GFLOPSComparisonBenchmark
    {
        private readonly List<BenchmarkResult> _results = new();
        private readonly SystemEnvironment _environment = new();

        // Matrix sizes to test - covering small to large workloads
        private readonly int[] _matrixSizes = new[] { 64, 128, 256, 512, 1024, 2048 };

        // LLM-specific workload sizes
        private readonly (int M, int K, int N, string Name)[] _llmWorkloads = new[]
        {
            (1, 512, 512, "Single Token Decode (M=1)"),
            (32, 512, 512, "Small Batch Decode"),
            (256, 4096, 4096, "Prefill 256 tokens (typical)"),
            (512, 4096, 4096, "Prefill 512 tokens"),
            (1, 4096, 4096, "Large Model Single Token"),
        };

        public void Run()
        {
            CollectEnvironmentInfo();
            DisplayEnvironmentInfo();

            Console.WriteLine("\n📊 Starting Comprehensive Benchmark Suite...\n");
            Console.WriteLine("This will measure:");
            Console.WriteLine("  • GFLOPS (computational throughput)");
            Console.WriteLine("  • Memory allocations (per operation)");
            Console.WriteLine("  • GC pressure (Gen0/1/2 collections)");
            Console.WriteLine("  • Cache efficiency (L1/L2/L3 friendly)");
            Console.WriteLine("  • LLM-specific workload performance");
            Console.WriteLine();

            // Run comprehensive matrix multiplication benchmarks
            RunMatrixMultiplicationBenchmarks();

            // Run LLM-specific workload benchmarks
            RunLLMWorkloadBenchmarks();

            // Run sustained throughput test
            RunSustainedThroughputTest();

            // Generate comprehensive reports
            GenerateReports();

            Console.WriteLine("\n✅ Benchmark suite complete!");
            Console.WriteLine($"📁 Results saved to:");
            Console.WriteLine($"   - GFLOPS_COMPARISON_RESULTS.json");
            Console.WriteLine($"   - GFLOPS_COMPARISON_RESULTS.md");
        }

        private void CollectEnvironmentInfo()
        {
            _environment.Timestamp = DateTime.UtcNow;
            _environment.DotNetVersion = Environment.Version.ToString();
            _environment.RuntimeVersion = RuntimeInformation.FrameworkDescription;
            _environment.OSDescription = RuntimeInformation.OSDescription;
            _environment.OSArchitecture = RuntimeInformation.OSArchitecture.ToString();
            _environment.ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString();
            _environment.ProcessorCount = Environment.ProcessorCount;

            try
            {
                var gcInfo = GC.GetGCMemoryInfo();
                _environment.TotalMemoryGB = gcInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0 * 1024.0);
            }
            catch
            {
                _environment.TotalMemoryGB = 0;
            }

            // Detect SIMD support
            _environment.AVXSupported = Avx.IsSupported;
            _environment.AVX2Supported = Avx2.IsSupported;
            _environment.FMASupported = Fma.IsSupported;
            _environment.AVX512Supported = Avx512F.IsSupported;
            _environment.VectorSize = System.Numerics.Vector<float>.Count;

            // JIT configuration
            _environment.TieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation") ?? "1 (default)";
            _environment.TieredPGO = Environment.GetEnvironmentVariable("DOTNET_TieredPGO") ?? "1 (default)";
            _environment.ReadyToRun = Environment.GetEnvironmentVariable("DOTNET_ReadyToRun") ?? "1 (default)";
        }

        private void DisplayEnvironmentInfo()
        {
            Console.WriteLine("🖥️  System Environment");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine($"   OS: {_environment.OSDescription}");
            Console.WriteLine($"   Architecture: {_environment.OSArchitecture} / {_environment.ProcessArchitecture}");
            Console.WriteLine($"   .NET: {_environment.RuntimeVersion}");
            Console.WriteLine($"   Processors: {_environment.ProcessorCount} cores");
            Console.WriteLine($"   Memory: {_environment.TotalMemoryGB:F1} GB");
            Console.WriteLine();
            Console.WriteLine("🚀 SIMD Capabilities");
            Console.WriteLine($"   Vector<float> Size: {_environment.VectorSize}");
            Console.WriteLine($"   AVX: {(_environment.AVXSupported ? "✓" : "✗")}");
            Console.WriteLine($"   AVX2: {(_environment.AVX2Supported ? "✓" : "✗")}");
            Console.WriteLine($"   FMA: {(_environment.FMASupported ? "✓" : "✗")}");
            Console.WriteLine($"   AVX-512: {(_environment.AVX512Supported ? "✓" : "✗")}");
            Console.WriteLine();
            Console.WriteLine("⚙️  JIT Configuration");
            Console.WriteLine($"   Tiered Compilation: {_environment.TieredCompilation}");
            Console.WriteLine($"   Tiered PGO: {_environment.TieredPGO}");
            Console.WriteLine($"   ReadyToRun: {_environment.ReadyToRun}");
        }

        private void RunMatrixMultiplicationBenchmarks()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🔢 Matrix Multiplication Benchmarks (Square Matrices)");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

            foreach (var size in _matrixSizes)
            {
                BenchmarkMatMul(size, size, size, $"Square {size}×{size}");
            }
        }

        private void RunLLMWorkloadBenchmarks()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("🤖 LLM-Specific Workload Benchmarks");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

            foreach (var (M, K, N, name) in _llmWorkloads)
            {
                BenchmarkMatMul(M, K, N, name);
            }
        }

        private void RunSustainedThroughputTest()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("⏱️  Sustained Throughput Test (30 seconds)");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

            int size = 512; // Medium-sized matrices
            int M = size, K = size, N = size;

            // Allocate matrices
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C = new float[M * N];

            // Initialize with random data
            Random rand = new Random(42);
            for (int i = 0; i < A.Length; i++) A[i] = (float)rand.NextDouble();
            for (int i = 0; i < B.Length; i++) B[i] = (float)rand.NextDouble();

            // Warmup
            Console.WriteLine("Warming up JIT...");
            for (int i = 0; i < 50; i++)
            {
                Array.Clear(C);
                MatMulOps.MatMul(A, B, C, M, K, N);
            }

            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Console.WriteLine("Running sustained test for 30 seconds...");

            var sw = Stopwatch.StartNew();
            int iterations = 0;
            long totalFlops = 0;

            while (sw.Elapsed.TotalSeconds < 30)
            {
                Array.Clear(C);
                MatMulOps.MatMul(A, B, C, M, K, N);
                iterations++;
                totalFlops += 2L * M * K * N;
            }

            sw.Stop();

            double totalSeconds = sw.Elapsed.TotalSeconds;
            double avgGflops = (totalFlops / totalSeconds) / 1e9;
            double opsPerSecond = iterations / totalSeconds;

            var result = new BenchmarkResult
            {
                Name = "Sustained Throughput (512×512, 30s)",
                Category = "Sustained",
                MatrixSize = $"{M}×{K}×{N}",
                Iterations = iterations,
                TotalTimeMs = totalSeconds * 1000,
                GFLOPS = avgGflops,
                OperationsPerSecond = opsPerSecond,
            };

            _results.Add(result);

            Console.WriteLine($"   Completed: {iterations:N0} operations in {totalSeconds:F1}s");
            Console.WriteLine($"   Average: {avgGflops:F2} GFLOPS");
            Console.WriteLine($"   Throughput: {opsPerSecond:F1} ops/sec");
        }

        private void BenchmarkMatMul(int M, int K, int N, string description)
        {
            Console.WriteLine($"Testing: {description} ({M}×{K}×{N})");

            // Allocate matrices
            float[] A = new float[M * K];
            float[] B = new float[K * N];
            float[] C = new float[M * N];

            // Initialize with deterministic random data
            Random rand = new Random(42);
            for (int i = 0; i < A.Length; i++) A[i] = (float)rand.NextDouble();
            for (int i = 0; i < B.Length; i++) B[i] = (float)rand.NextDouble();

            // Warmup phase (JIT compilation)
            int warmupIterations = Math.Max(5, 100 / Math.Max(1, M * K * N / (256 * 256 * 256)));
            for (int i = 0; i < warmupIterations; i++)
            {
                Array.Clear(C);
                MatMulOps.MatMul(A, B, C, M, K, N);
            }

            // Force GC before measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Determine iteration count based on size
            int iterations = DetermineIterationCount(M, K, N);

            // Measure allocations
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            int gen0Before = GC.CollectionCount(0);
            int gen1Before = GC.CollectionCount(1);
            int gen2Before = GC.CollectionCount(2);

            // Measurement phase
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++)
            {
                Array.Clear(C);
                MatMulOps.MatMul(A, B, C, M, K, N);
            }
            sw.Stop();

            long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
            int gen0After = GC.CollectionCount(0);
            int gen1After = GC.CollectionCount(1);
            int gen2After = GC.CollectionCount(2);

            // Calculate metrics
            double totalMs = sw.Elapsed.TotalMilliseconds;
            double msPerOp = totalMs / iterations;
            long flopsPerOp = 2L * M * K * N; // 2 ops per multiply-add
            double gflops = (flopsPerOp / (msPerOp / 1000.0)) / 1e9;
            long allocatedBytes = allocatedAfter - allocatedBefore;
            long bytesPerOp = allocatedBytes / iterations;

            // Calculate throughput in different units
            double opsPerSecond = 1000.0 / msPerOp;
            
            // Memory bandwidth estimate (very rough)
            long bytesRead = (long)M * K * sizeof(float) + (long)K * N * sizeof(float);
            long bytesWritten = (long)M * N * sizeof(float);
            long totalBytes = bytesRead + bytesWritten;
            double memoryBandwidthGBps = (totalBytes / (msPerOp / 1000.0)) / 1e9;

            var result = new BenchmarkResult
            {
                Name = description,
                Category = "MatMul",
                MatrixSize = $"{M}×{K}×{N}",
                M = M,
                K = K,
                N = N,
                Iterations = iterations,
                TotalTimeMs = totalMs,
                TimePerOpMs = msPerOp,
                GFLOPS = gflops,
                FLOPsPerOp = flopsPerOp,
                BytesAllocatedPerOp = bytesPerOp,
                TotalBytesAllocated = allocatedBytes,
                Gen0Collections = gen0After - gen0Before,
                Gen1Collections = gen1After - gen1Before,
                Gen2Collections = gen2After - gen2Before,
                OperationsPerSecond = opsPerSecond,
                EstimatedMemoryBandwidthGBps = memoryBandwidthGBps,
            };

            _results.Add(result);

            // Display results
            Console.WriteLine($"   GFLOPS: {gflops:F2} | Time/Op: {msPerOp:F3}ms | Alloc: {bytesPerOp:N0} bytes/op");
            if (bytesPerOp > 0)
            {
                Console.WriteLine($"   ⚠️  WARNING: {bytesPerOp} bytes allocated per operation (GC pressure!)");
            }
            if (gen0After - gen0Before > 0)
            {
                Console.WriteLine($"   ⚠️  GC: Gen0={gen0After - gen0Before}, Gen1={gen1After - gen1Before}, Gen2={gen2After - gen2Before}");
            }
            Console.WriteLine();
        }

        private int DetermineIterationCount(int M, int K, int N)
        {
            // Adjust iterations based on matrix size to keep total test time reasonable
            long complexity = (long)M * K * N;
            
            if (complexity < 256 * 256 * 256)
                return 200; // Small matrices - more iterations
            else if (complexity < 512 * 512 * 512)
                return 100; // Medium matrices
            else if (complexity < 1024 * 1024 * 1024)
                return 50;  // Large matrices
            else
                return 20;  // Very large matrices
        }

        private void GenerateReports()
        {
            Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            Console.WriteLine("📊 Generating Reports");
            Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n");

            GenerateMarkdownReport();
            GenerateJsonReport();
        }

        private void GenerateMarkdownReport()
        {
            var markdown = new System.Text.StringBuilder();

            markdown.AppendLine("# GFLOPS Comparison Benchmark Results");
            markdown.AppendLine();
            markdown.AppendLine($"**Generated:** {_environment.Timestamp:yyyy-MM-dd HH:mm:ss UTC}");
            markdown.AppendLine();

            // Environment section
            markdown.AppendLine("## System Environment");
            markdown.AppendLine();
            markdown.AppendLine("| Property | Value |");
            markdown.AppendLine("|----------|-------|");
            markdown.AppendLine($"| OS | {_environment.OSDescription} |");
            markdown.AppendLine($"| Architecture | {_environment.OSArchitecture} |");
            markdown.AppendLine($"| .NET Runtime | {_environment.RuntimeVersion} |");
            markdown.AppendLine($"| Processors | {_environment.ProcessorCount} cores |");
            markdown.AppendLine($"| Memory | {_environment.TotalMemoryGB:F1} GB |");
            markdown.AppendLine($"| Vector<float> Size | {_environment.VectorSize} |");
            markdown.AppendLine($"| AVX2 | {(_environment.AVX2Supported ? "✓" : "✗")} |");
            markdown.AppendLine($"| FMA | {(_environment.FMASupported ? "✓" : "✗")} |");
            markdown.AppendLine($"| AVX-512 | {(_environment.AVX512Supported ? "✓" : "✗")} |");
            markdown.AppendLine();

            // Results by category
            var matmulResults = _results.Where(r => r.Category == "MatMul").ToList();
            var sustainedResults = _results.Where(r => r.Category == "Sustained").ToList();

            if (matmulResults.Any())
            {
                markdown.AppendLine("## Matrix Multiplication Results");
                markdown.AppendLine();
                markdown.AppendLine("| Name | Size | GFLOPS | Time/Op (ms) | Alloc/Op (bytes) | GC (G0/G1/G2) | Mem BW (GB/s) |");
                markdown.AppendLine("|------|------|--------|--------------|------------------|---------------|---------------|");

                foreach (var result in matmulResults)
                {
                    string gcInfo = $"{result.Gen0Collections}/{result.Gen1Collections}/{result.Gen2Collections}";
                    markdown.AppendLine($"| {result.Name} | {result.MatrixSize} | **{result.GFLOPS:F2}** | {result.TimePerOpMs:F3} | {result.BytesAllocatedPerOp:N0} | {gcInfo} | {result.EstimatedMemoryBandwidthGBps:F1} |");
                }
                markdown.AppendLine();
            }

            if (sustainedResults.Any())
            {
                markdown.AppendLine("## Sustained Throughput Results");
                markdown.AppendLine();
                foreach (var result in sustainedResults)
                {
                    markdown.AppendLine($"**{result.Name}**");
                    markdown.AppendLine($"- Total Operations: {result.Iterations:N0}");
                    markdown.AppendLine($"- Average GFLOPS: {result.GFLOPS:F2}");
                    markdown.AppendLine($"- Operations/Second: {result.OperationsPerSecond:F1}");
                    markdown.AppendLine();
                }
            }

            // Performance summary
            markdown.AppendLine("## Performance Summary");
            markdown.AppendLine();
            
            var maxGflops = _results.Where(r => r.Category == "MatMul").Max(r => r.GFLOPS);
            var minGflops = _results.Where(r => r.Category == "MatMul").Min(r => r.GFLOPS);
            var avgGflops = _results.Where(r => r.Category == "MatMul").Average(r => r.GFLOPS);
            
            var zeroAllocResults = _results.Count(r => r.BytesAllocatedPerOp == 0);
            var totalResults = _results.Count(r => r.Category == "MatMul");
            
            markdown.AppendLine($"- **Peak GFLOPS:** {maxGflops:F2}");
            markdown.AppendLine($"- **Minimum GFLOPS:** {minGflops:F2}");
            markdown.AppendLine($"- **Average GFLOPS:** {avgGflops:F2}");
            markdown.AppendLine($"- **Zero-Allocation Results:** {zeroAllocResults}/{totalResults} ({100.0 * zeroAllocResults / totalResults:F1}%)");
            markdown.AppendLine();

            // Key observations
            markdown.AppendLine("## Key Observations");
            markdown.AppendLine();
            
            var highAllocResults = _results.Where(r => r.BytesAllocatedPerOp > 1000).ToList();
            if (highAllocResults.Any())
            {
                markdown.AppendLine("### ⚠️ High Allocation Workloads");
                foreach (var result in highAllocResults.Take(5))
                {
                    markdown.AppendLine($"- **{result.Name}**: {result.BytesAllocatedPerOp:N0} bytes/op");
                }
                markdown.AppendLine();
            }

            var gcResults = _results.Where(r => r.Gen0Collections > 0).ToList();
            if (gcResults.Any())
            {
                markdown.AppendLine("### ⚠️ GC Pressure Detected");
                markdown.AppendLine($"- {gcResults.Count} out of {_results.Count} tests triggered GC");
                markdown.AppendLine();
            }

            File.WriteAllText("GFLOPS_COMPARISON_RESULTS.md", markdown.ToString());
            Console.WriteLine("✓ Markdown report saved: GFLOPS_COMPARISON_RESULTS.md");
        }

        private void GenerateJsonReport()
        {
            var report = new
            {
                Environment = _environment,
                Results = _results,
                Summary = new
                {
                    TotalTests = _results.Count,
                    MaxGFLOPS = _results.Any() ? _results.Max(r => r.GFLOPS) : 0,
                    MinGFLOPS = _results.Any() ? _results.Min(r => r.GFLOPS) : 0,
                    AvgGFLOPS = _results.Any() ? _results.Average(r => r.GFLOPS) : 0,
                    ZeroAllocationCount = _results.Count(r => r.BytesAllocatedPerOp == 0),
                    GCTriggeredCount = _results.Count(r => r.Gen0Collections > 0 || r.Gen1Collections > 0 || r.Gen2Collections > 0),
                }
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            };

            string json = JsonSerializer.Serialize(report, options);
            File.WriteAllText("GFLOPS_COMPARISON_RESULTS.json", json);
            Console.WriteLine("✓ JSON report saved: GFLOPS_COMPARISON_RESULTS.json");
        }
    }

    public class SystemEnvironment
    {
        public DateTime Timestamp { get; set; }
        public string DotNetVersion { get; set; }
        public string RuntimeVersion { get; set; }
        public string OSDescription { get; set; }
        public string OSArchitecture { get; set; }
        public string ProcessArchitecture { get; set; }
        public int ProcessorCount { get; set; }
        public double TotalMemoryGB { get; set; }
        public bool AVXSupported { get; set; }
        public bool AVX2Supported { get; set; }
        public bool FMASupported { get; set; }
        public bool AVX512Supported { get; set; }
        public int VectorSize { get; set; }
        public string TieredCompilation { get; set; }
        public string TieredPGO { get; set; }
        public string ReadyToRun { get; set; }
    }

    public class BenchmarkResult
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public string MatrixSize { get; set; }
        public int M { get; set; }
        public int K { get; set; }
        public int N { get; set; }
        public int Iterations { get; set; }
        public double TotalTimeMs { get; set; }
        public double TimePerOpMs { get; set; }
        public double GFLOPS { get; set; }
        public long FLOPsPerOp { get; set; }
        public long BytesAllocatedPerOp { get; set; }
        public long TotalBytesAllocated { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public double OperationsPerSecond { get; set; }
        public double EstimatedMemoryBandwidthGBps { get; set; }
    }
}

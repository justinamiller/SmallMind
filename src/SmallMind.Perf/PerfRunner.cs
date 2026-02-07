using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;

namespace SmallMind.Perf;

/// <summary>
/// Performance benchmark runner for SmallMind hot path operations.
/// Provides deterministic microbenchmarks with no 3rd party dependencies.
/// Measures: execution time, allocations, GC counts, CPU time.
/// </summary>
public class PerfRunner
{
    private int _warmupIters = 50;
    private int _measureIters = 1000;
    private bool _jsonOutput = false;
    private string _benchName = "all";
    private bool _fastMode = false;

    private readonly List<BenchmarkResult> _results = new();

    public static void Main(string[] args)
    {
        var runner = new PerfRunner();
        runner.ParseArgs(args);
        runner.PrintSystemInfo();
        runner.Run();
        runner.PrintResults();
    }

    private void ParseArgs(string[] args)
    {
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--warmup" when i + 1 < args.Length:
                    _warmupIters = int.Parse(args[++i]);
                    break;
                case "--iters" when i + 1 < args.Length:
                    _measureIters = int.Parse(args[++i]);
                    break;
                case "--bench" when i + 1 < args.Length:
                    _benchName = args[++i];
                    break;
                case "--json":
                    _jsonOutput = true;
                    break;
                case "--fast":
                    _fastMode = true;
                    _warmupIters = 10;
                    _measureIters = 100;
                    break;
                case "--help":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
            }
        }
    }

    private void PrintHelp()
    {
        Console.WriteLine("SmallMind.Perf - Performance Benchmark Runner");
        Console.WriteLine();
        Console.WriteLine("Usage: SmallMind.Perf [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --warmup N     Number of warmup iterations (default: 50)");
        Console.WriteLine("  --iters M      Number of measurement iterations (default: 1000)");
        Console.WriteLine("  --bench <name> Benchmark to run: all|matmul|attention|layernorm|softmax|kvcache (default: all)");
        Console.WriteLine("  --json         Output results in JSON format");
        Console.WriteLine("  --fast         Fast mode for CI (fewer iterations)");
        Console.WriteLine("  --help         Show this help message");
    }

    private void PrintSystemInfo()
    {
        if (_jsonOutput) return;

        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         SmallMind Performance Benchmark (No 3rd Party Deps)          ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"Runtime:          .NET {Environment.Version}");
        Console.WriteLine($"OS:               {Environment.OSVersion}");
        Console.WriteLine($"Processor Count:  {Environment.ProcessorCount}");
        Console.WriteLine($"GC Mode:          {(GCSettings.IsServerGC ? "Server" : "Workstation")}");
        Console.WriteLine($"SIMD Width:       Vector<float>.Count = {System.Numerics.Vector<float>.Count}");
        Console.WriteLine($"Date:             {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        Console.WriteLine($"Mode:             {(_fastMode ? "FAST (CI)" : "FULL")}");
        Console.WriteLine();
    }

    private void Run()
    {
        if (_benchName == "all" || _benchName == "matmul")
            BenchmarkMatMul();

        if (_benchName == "all" || _benchName == "attention")
            BenchmarkAttention();

        if (_benchName == "all" || _benchName == "layernorm")
            BenchmarkLayerNorm();

        if (_benchName == "all" || _benchName == "softmax")
            BenchmarkSoftmax();

        if (_benchName == "all" || _benchName == "kvcache")
            BenchmarkKVCache();
    }

    private void BenchmarkMatMul()
    {
        // Test various sizes relevant to LLM inference
        var sizes = _fastMode
            ? new[] { (128, 128, 128) }
            : new[] { (128, 128, 128), (512, 512, 512), (1024, 768, 768) };

        foreach (var (M, K, N) in sizes)
        {
            var A = new float[M * K];
            var B = new float[K * N];
            var C = new float[M * N];

            // Initialize with random data
            var rng = new Random(42);
            for (int i = 0; i < A.Length; i++) A[i] = (float)rng.NextDouble();
            for (int i = 0; i < B.Length; i++) B[i] = (float)rng.NextDouble();

            // Warmup
            for (int i = 0; i < _warmupIters; i++)
            {
                MatMulOps.MatMul(A, B, C, M, K, N);
            }

            // Force GC
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            // Measure
            long startAlloc = GC.GetAllocatedBytesForCurrentThread();
            int startGen0 = GC.CollectionCount(0);
            int startGen1 = GC.CollectionCount(1);
            int startGen2 = GC.CollectionCount(2);

            var sw = Stopwatch.StartNew();
            var cpuStart = Process.GetCurrentProcess().TotalProcessorTime;

            for (int i = 0; i < _measureIters; i++)
            {
                MatMulOps.MatMul(A, B, C, M, K, N);
            }

            sw.Stop();
            var cpuEnd = Process.GetCurrentProcess().TotalProcessorTime;

            long endAlloc = GC.GetAllocatedBytesForCurrentThread();
            long totalAlloc = endAlloc - startAlloc;

            double msPerOp = sw.Elapsed.TotalMilliseconds / _measureIters;
            double flops = 2.0 * M * K * N; // Multiply-add
            double gflops = (flops / 1e9) / (msPerOp / 1000.0);
            double cpuMs = (cpuEnd - cpuStart).TotalMilliseconds;

            _results.Add(new BenchmarkResult
            {
                Name = $"MatMul_{M}x{K}x{N}",
                TimeMs = msPerOp,
                Throughput = gflops,
                ThroughputUnit = "GFLOPS",
                AllocationsPerOp = totalAlloc / (double)_measureIters,
                Gen0Collections = GC.CollectionCount(0) - startGen0,
                Gen1Collections = GC.CollectionCount(1) - startGen1,
                Gen2Collections = GC.CollectionCount(2) - startGen2,
                CpuTimeMs = cpuMs / _measureIters,
                Parameters = new Dictionary<string, object>
                {
                    ["M"] = M,
                    ["K"] = K,
                    ["N"] = N,
                    ["Iterations"] = _measureIters
                }
            });
        }
    }

    private void BenchmarkAttention()
    {
        if (_fastMode)
        {
            BenchmarkAttentionSize(4, 32, 8, 64); // Tiny for CI
        }
        else
        {
            BenchmarkAttentionSize(1, 128, 8, 64);  // Single sequence
            BenchmarkAttentionSize(4, 64, 12, 64);  // Small batch
        }
    }

    private void BenchmarkAttentionSize(int batchSize, int seqLen, int numHeads, int headDim)
    {
        int qkvDim = numHeads * headDim;
        var Q = new float[batchSize * seqLen * qkvDim];
        var K = new float[batchSize * seqLen * qkvDim];
        var V = new float[batchSize * seqLen * qkvDim];
        var output = new float[batchSize * seqLen * qkvDim];

        var rng = new Random(42);
        for (int i = 0; i < Q.Length; i++) Q[i] = (float)rng.NextDouble();
        for (int i = 0; i < K.Length; i++) K[i] = (float)rng.NextDouble();
        for (int i = 0; i < V.Length; i++) V[i] = (float)rng.NextDouble();

        // Warmup - Simple single-head attention for now
        for (int i = 0; i < _warmupIters; i++)
        {
            FusedAttentionKernels.FusedScaledDotProductAttention(
                Q.AsSpan(0, seqLen * headDim), 
                K.AsSpan(0, seqLen * headDim), 
                V.AsSpan(0, seqLen * headDim), 
                output.AsSpan(0, seqLen * headDim),
                seqLen, headDim, isCausal: true);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        int startGen0 = GC.CollectionCount(0);
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < _measureIters; i++)
        {
            FusedAttentionKernels.FusedScaledDotProductAttention(
                Q.AsSpan(0, seqLen * headDim), 
                K.AsSpan(0, seqLen * headDim), 
                V.AsSpan(0, seqLen * headDim), 
                output.AsSpan(0, seqLen * headDim),
                seqLen, headDim, isCausal: true);
        }

        sw.Stop();
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();
        long totalAlloc = endAlloc - startAlloc;

        _results.Add(new BenchmarkResult
        {
            Name = $"Attention_B{batchSize}_S{seqLen}_H{numHeads}_D{headDim}",
            TimeMs = sw.Elapsed.TotalMilliseconds / _measureIters,
            AllocationsPerOp = totalAlloc / (double)_measureIters,
            Gen0Collections = GC.CollectionCount(0) - startGen0,
            Parameters = new Dictionary<string, object>
            {
                ["BatchSize"] = batchSize,
                ["SeqLen"] = seqLen,
                ["NumHeads"] = numHeads,
                ["HeadDim"] = headDim,
                ["Iterations"] = _measureIters
            }
        });
    }

    private void BenchmarkLayerNorm()
    {
        var sizes = _fastMode ? new[] { 768 } : new[] { 768, 1024, 2048 };

        foreach (int dim in sizes)
        {
            var input = new float[dim];
            var gamma = new float[dim];
            var beta = new float[dim];

            var rng = new Random(42);
            for (int i = 0; i < dim; i++)
            {
                input[i] = (float)rng.NextDouble();
                gamma[i] = 1.0f;
                beta[i] = 0.0f;
            }

            // Warmup
            for (int i = 0; i < _warmupIters; i++)
            {
                LayerNormOps.LayerNorm(input, gamma, beta, input, 1, dim, 1e-5f);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < _measureIters; i++)
            {
                LayerNormOps.LayerNorm(input, gamma, beta, input, 1, dim, 1e-5f);
            }

            sw.Stop();
            long endAlloc = GC.GetAllocatedBytesForCurrentThread();

            _results.Add(new BenchmarkResult
            {
                Name = $"LayerNorm_{dim}",
                TimeMs = sw.Elapsed.TotalMilliseconds / _measureIters,
                AllocationsPerOp = (endAlloc - startAlloc) / (double)_measureIters,
                Parameters = new Dictionary<string, object>
                {
                    ["Dimension"] = dim,
                    ["Iterations"] = _measureIters
                }
            });
        }
    }

    private void BenchmarkSoftmax()
    {
        var sizes = _fastMode ? new[] { (16, 128) } : new[] { (16, 128), (32, 512) };

        foreach (var (rows, cols) in sizes)
        {
            var input = new float[rows * cols];
            var output = new float[rows * cols];
            var rng = new Random(42);
            for (int i = 0; i < input.Length; i++)
                input[i] = (float)rng.NextDouble() * 10.0f - 5.0f;

            // Warmup
            for (int i = 0; i < _warmupIters; i++)
            {
                SoftmaxOps.Softmax2D(input, output, rows, cols);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            long startAlloc = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();

            for (int i = 0; i < _measureIters; i++)
            {
                SoftmaxOps.Softmax2D(input, output, rows, cols);
            }

            sw.Stop();
            long endAlloc = GC.GetAllocatedBytesForCurrentThread();

            _results.Add(new BenchmarkResult
            {
                Name = $"Softmax_{rows}x{cols}",
                TimeMs = sw.Elapsed.TotalMilliseconds / _measureIters,
                AllocationsPerOp = (endAlloc - startAlloc) / (double)_measureIters,
                Parameters = new Dictionary<string, object>
                {
                    ["Rows"] = rows,
                    ["Cols"] = cols,
                    ["Iterations"] = _measureIters
                }
            });
        }
    }

    private void BenchmarkKVCache()
    {
        int numLayers = _fastMode ? 2 : 6;
        int numHeads = 8;
        int headDim = 64;
        int maxSeqLen = _fastMode ? 64 : 256;

        var cache = new OptimizedKVCache(numLayers, maxSeqLen, numHeads, headDim);

        var keyData = new float[numHeads * headDim];
        var valueData = new float[numHeads * headDim];
        var rng = new Random(42);
        for (int i = 0; i < keyData.Length; i++)
        {
            keyData[i] = (float)rng.NextDouble();
            valueData[i] = (float)rng.NextDouble();
        }

        // Warmup - append tokens
        for (int i = 0; i < Math.Min(_warmupIters, maxSeqLen); i++)
        {
            cache.Append(0, keyData, valueData, numNewTokens: 1);
            cache.UpdateSeqLen(1);
        }

        cache.Clear();

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long startAlloc = GC.GetAllocatedBytesForCurrentThread();
        var sw = Stopwatch.StartNew();

        int appendCount = Math.Min(_measureIters, maxSeqLen);
        for (int i = 0; i < appendCount; i++)
        {
            cache.Append(0, keyData, valueData, numNewTokens: 1);
            cache.UpdateSeqLen(1);
        }

        sw.Stop();
        long endAlloc = GC.GetAllocatedBytesForCurrentThread();

        _results.Add(new BenchmarkResult
        {
            Name = $"KVCache_Append_L{numLayers}_H{numHeads}_D{headDim}",
            TimeMs = sw.Elapsed.TotalMilliseconds / appendCount,
            AllocationsPerOp = (endAlloc - startAlloc) / (double)appendCount,
            Parameters = new Dictionary<string, object>
            {
                ["NumLayers"] = numLayers,
                ["NumHeads"] = numHeads,
                ["HeadDim"] = headDim,
                ["MaxSeqLen"] = maxSeqLen,
                ["Operations"] = appendCount
            }
        });
    }

    private void PrintResults()
    {
        if (_jsonOutput)
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            Console.WriteLine(JsonSerializer.Serialize(new { Results = _results }, options));
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                         Benchmark Results                             ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            foreach (var result in _results)
            {
                Console.WriteLine($"[{result.Name}]");
                Console.WriteLine($"  Time/op:        {result.TimeMs:F4} ms");
                if (result.Throughput > 0)
                    Console.WriteLine($"  Throughput:     {result.Throughput:F2} {result.ThroughputUnit}");
                Console.WriteLine($"  Alloc/op:       {result.AllocationsPerOp:F2} bytes");
                Console.WriteLine($"  GC (Gen0/1/2):  {result.Gen0Collections}/{result.Gen1Collections}/{result.Gen2Collections}");
                if (result.CpuTimeMs > 0)
                    Console.WriteLine($"  CPU time/op:    {result.CpuTimeMs:F4} ms");
                Console.WriteLine();
            }
        }
    }

    private class BenchmarkResult
    {
        public string Name { get; set; } = "";
        public double TimeMs { get; set; }
        public double Throughput { get; set; }
        public string ThroughputUnit { get; set; } = "";
        public double AllocationsPerOp { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public double CpuTimeMs { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new();
    }
}

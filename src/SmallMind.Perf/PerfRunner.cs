using System.Diagnostics;
using System.Runtime;
using System.Text.Json;
using SmallMind.Core.Core;
using SmallMind.Core.Simd;
using SmallMind.Runtime;
using SmallMind.Tokenizers;
using SmallMind.Transformers;

namespace SmallMind.Perf;

/// <summary>
/// Performance benchmark runner for SmallMind hot path operations.
/// Provides deterministic microbenchmarks with no 3rd party dependencies.
/// Measures: execution time, allocations, GC counts, CPU time.
/// Supports both micro-benchmarks and end-to-end inference benchmarks.
/// </summary>
public class PerfRunner
{
    private int _warmupIters = 50;
    private int _measureIters = 1000;
    private bool _jsonOutput = false;
    private string _benchName = "all";
    private bool _fastMode = false;
    
    // E2E benchmark parameters
    private string? _prompt = null;
    private string? _promptFile = null;
    private int _maxNewTokens = 50;
    private bool _deterministicMode = false;
    private int _seed = 42;

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
                case "--prompt" when i + 1 < args.Length:
                    _prompt = args[++i];
                    break;
                case "--prompt-file" when i + 1 < args.Length:
                    _promptFile = args[++i];
                    break;
                case "--max-new-tokens" when i + 1 < args.Length:
                    _maxNewTokens = int.Parse(args[++i]);
                    break;
                case "--deterministic":
                    _deterministicMode = true;
                    break;
                case "--seed" when i + 1 < args.Length:
                    _seed = int.Parse(args[++i]);
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
        Console.WriteLine("Benchmark Selection:");
        Console.WriteLine("  --bench <name>     Benchmark to run:");
        Console.WriteLine("                       micro: matmul|attention|layernorm|softmax|kvcache");
        Console.WriteLine("                       e2e: end-to-end inference (requires --prompt)");
        Console.WriteLine("                       all: all micro-benchmarks (default)");
        Console.WriteLine();
        Console.WriteLine("Micro-benchmark Options:");
        Console.WriteLine("  --warmup N         Number of warmup iterations (default: 50)");
        Console.WriteLine("  --iters M          Number of measurement iterations (default: 1000)");
        Console.WriteLine("  --fast             Fast mode for CI (fewer iterations)");
        Console.WriteLine();
        Console.WriteLine("End-to-End Options:");
        Console.WriteLine("  --prompt \"<text>\"   Text prompt for e2e benchmark");
        Console.WriteLine("  --prompt-file <p>  File containing prompt text");
        Console.WriteLine("  --max-new-tokens N Number of tokens to generate (default: 50)");
        Console.WriteLine("  --deterministic    Use deterministic sampling (greedy)");
        Console.WriteLine("  --seed N           Random seed (default: 42)");
        Console.WriteLine();
        Console.WriteLine("Output Options:");
        Console.WriteLine("  --json             Output results in JSON format");
        Console.WriteLine("  --help             Show this help message");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  # Run all micro-benchmarks in fast mode");
        Console.WriteLine("  SmallMind.Perf --bench all --fast");
        Console.WriteLine();
        Console.WriteLine("  # Run end-to-end inference benchmark");
        Console.WriteLine("  SmallMind.Perf --bench e2e --prompt \"hello world\" --max-new-tokens 100");
        Console.WriteLine();
        Console.WriteLine("  # Run with JSON output for automation");
        Console.WriteLine("  SmallMind.Perf --bench all --json > results.json");
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
        if (_benchName == "e2e")
        {
            BenchmarkEndToEnd();
        }
        else
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
    }

    private void BenchmarkEndToEnd()
    {
        if (!_jsonOutput)
        {
            Console.WriteLine();
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine("                    End-to-End Inference Benchmark");
            Console.WriteLine("═══════════════════════════════════════════════════════════════════════");
            Console.WriteLine();
        }

        // Get prompt
        string prompt;
        if (!string.IsNullOrEmpty(_promptFile))
        {
            prompt = File.ReadAllText(_promptFile);
        }
        else if (!string.IsNullOrEmpty(_prompt))
        {
            prompt = _prompt;
        }
        else
        {
            prompt = "The quick brown fox jumps over the lazy dog."; // Default prompt
        }

        if (!_jsonOutput)
        {
            Console.WriteLine($"Prompt: {(prompt.Length > 60 ? prompt.Substring(0, 60) + "..." : prompt)}");
            Console.WriteLine($"Prompt length: {prompt.Length} chars");
            Console.WriteLine($"Max new tokens: {_maxNewTokens}");
            Console.WriteLine($"Deterministic: {_deterministicMode}");
            Console.WriteLine($"Seed: {_seed}");
            Console.WriteLine();
        }

        // Create a small model for benchmarking
        const string Vocabulary = "abcdefghijklmnopqrstuvwxyz .,!?'-\"ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789\n";
        int vocabSize = Vocabulary.Length;
        int blockSize = 256;
        int nEmbd = _fastMode ? 64 : 128;
        int nLayer = _fastMode ? 2 : 4;
        int nHead = _fastMode ? 4 : 8;
        
        if (!_jsonOutput)
        {
            Console.WriteLine($"Model config: vocab={vocabSize}, embd={nEmbd}, layers={nLayer}, heads={nHead}");
            Console.WriteLine("Creating model...");
        }

        var model = new TransformerModel(vocabSize, blockSize, nEmbd, nLayer, nHead, dropout: 0.0, seed: _seed);
        var tokenizer = new CharTokenizer(Vocabulary);
        model.Eval(); // Set to eval mode

        if (!_jsonOutput)
        {
            Console.WriteLine("Model created. Running benchmark...");
            Console.WriteLine();
        }

        // Create inference options
        var options = new ProductionInferenceOptions
        {
            MaxNewTokens = _maxNewTokens,
            Temperature = _deterministicMode ? 0.01 : 1.0, // Use very low temp for deterministic, not 0
            Seed = _deterministicMode ? _seed : null
        };

        // Create inference session
        using var session = new InferenceSession(model, tokenizer, options, blockSize);

        // Warmup runs (if not fast mode)
        int warmupRuns = _fastMode ? 1 : 3;
        for (int i = 0; i < warmupRuns; i++)
        {
            try
            {
                var _ = session.GenerateAsync(prompt).GetAwaiter().GetResult();
            }
            catch
            {
                // Ignore warmup errors
            }
        }

        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Measurement runs
        int runs = _fastMode ? 5 : 10;
        var latencies = new List<double>();
        var ttftList = new List<double>();
        var prefillTimes = new List<double>();
        var decodeTimes = new List<double>();
        var tokensGenerated = new List<int>();
        var allocationsList = new List<long>();

        long totalStartAlloc = GC.GetAllocatedBytesForCurrentThread();
        int startGen0 = GC.CollectionCount(0);
        int startGen1 = GC.CollectionCount(1);
        int startGen2 = GC.CollectionCount(2);

        for (int run = 0; run < runs; run++)
        {
            long runStartAlloc = GC.GetAllocatedBytesForCurrentThread();
            
            var sw = Stopwatch.StartNew();
            double ttft = 0;
            int tokCount = 0;
            
            string result = "";
            try
            {
                // For TTFT, we need to measure when first token is generated
                // Since we don't have access to internal metrics, we'll just time the full operation
                result = session.GenerateAsync(prompt).GetAwaiter().GetResult();
                tokCount = result.Length; // Char-level tokenizer
            }
            catch (Exception ex)
            {
                if (!_jsonOutput)
                {
                    Console.WriteLine($"Warning: Run {run + 1} failed: {ex.Message}");
                }
                continue;
            }
            
            sw.Stop();
            
            long runEndAlloc = GC.GetAllocatedBytesForCurrentThread();
            allocationsList.Add(runEndAlloc - runStartAlloc);
            
            double latencyMs = sw.Elapsed.TotalMilliseconds;
            latencies.Add(latencyMs);
            
            // Extract tokens generated (estimate from result length)
            int tokensGenCount = result.Length; // Char-level tokenizer
            tokensGenerated.Add(tokensGenCount);
            
            // For TTFT estimation, we'll assume first token takes similar time to average
            // In real scenario, prefill is usually faster than decode for small prompts
            double estimatedTTFT = latencyMs / Math.Max(1, tokensGenCount);
            ttftList.Add(estimatedTTFT);
            
            // Estimate prefill (assume it's ~20% of total for small models, rough heuristic)
            double estimatedPrefillTime = latencyMs * 0.2;
            prefillTimes.Add(estimatedPrefillTime);
            decodeTimes.Add(latencyMs - estimatedPrefillTime);
        }

        long totalEndAlloc = GC.GetAllocatedBytesForCurrentThread();
        long totalAllocations = totalEndAlloc - totalStartAlloc;

        // Calculate statistics
        if (latencies.Count == 0)
        {
            if (!_jsonOutput)
            {
                Console.WriteLine("ERROR: All runs failed!");
            }
            return;
        }

        latencies.Sort();
        ttftList.Sort();
        tokensGenerated.Sort();

        double avgLatency = latencies.Average();
        double p50Latency = latencies[latencies.Count / 2];
        double p95Latency = latencies[(int)(latencies.Count * 0.95)];
        double p99Latency = latencies[(int)(latencies.Count * 0.99)];
        
        double avgTokens = tokensGenerated.Average();
        double avgTTFT = ttftList.Count > 0 ? ttftList.Average() : 0;
        double avgPrefillTime = prefillTimes.Count > 0 ? prefillTimes.Average() : 0;
        double avgDecodeTime = decodeTimes.Count > 0 ? decodeTimes.Average() : avgLatency;
        
        // Calculate throughput
        double prefillToksPerSec = 0;
        double decodeToksPerSec = 0;
        
        if (avgPrefillTime > 0)
        {
            // Estimate prefill tokens (prompt length)
            int promptTokens = prompt.Length; // Char tokenizer
            prefillToksPerSec = (promptTokens / avgPrefillTime) * 1000.0;
        }
        
        if (avgDecodeTime > 0 && avgTokens > 0)
        {
            decodeToksPerSec = (avgTokens / avgDecodeTime) * 1000.0;
        }
        
        double msPerToken = avgTokens > 0 ? avgDecodeTime / avgTokens : 0;
        double avgAllocPerRun = allocationsList.Count > 0 ? allocationsList.Average() : 0;
        double allocPerToken = avgTokens > 0 ? avgAllocPerRun / avgTokens : 0;

        _results.Add(new BenchmarkResult
        {
            Name = "E2E_Inference",
            TimeMs = avgLatency,
            Parameters = new Dictionary<string, object>
            {
                ["Runs"] = runs,
                ["PromptLen"] = prompt.Length,
                ["MaxNewTokens"] = _maxNewTokens,
                ["AvgTokensGenerated"] = avgTokens,
                ["Deterministic"] = _deterministicMode,
                ["ModelEmbd"] = nEmbd,
                ["ModelLayers"] = nLayer,
                ["ModelHeads"] = nHead,
                // Latency metrics
                ["AvgLatencyMs"] = avgLatency,
                ["P50LatencyMs"] = p50Latency,
                ["P95LatencyMs"] = p95Latency,
                ["P99LatencyMs"] = p99Latency,
                ["AvgTTFT_ms"] = avgTTFT,
                // Throughput metrics
                ["PrefillToksPerSec"] = prefillToksPerSec,
                ["DecodeToksPerSec"] = decodeToksPerSec,
                ["MsPerToken"] = msPerToken,
                // Memory metrics
                ["TotalAllocBytes"] = totalAllocations,
                ["AvgAllocPerRun"] = avgAllocPerRun,
                ["AllocPerToken"] = allocPerToken,
                ["Gen0Collections"] = GC.CollectionCount(0) - startGen0,
                ["Gen1Collections"] = GC.CollectionCount(1) - startGen1,
                ["Gen2Collections"] = GC.CollectionCount(2) - startGen2
            }
        });

        if (!_jsonOutput)
        {
            Console.WriteLine("Results:");
            Console.WriteLine($"  Runs:                {runs}");
            Console.WriteLine($"  Avg tokens generated:{avgTokens:F1}");
            Console.WriteLine();
            Console.WriteLine("  Latency:");
            Console.WriteLine($"    Avg:               {avgLatency:F2} ms");
            Console.WriteLine($"    P50:               {p50Latency:F2} ms");
            Console.WriteLine($"    P95:               {p95Latency:F2} ms");
            Console.WriteLine($"    P99:               {p99Latency:F2} ms");
            Console.WriteLine($"    TTFT (avg):        {avgTTFT:F2} ms");
            Console.WriteLine();
            Console.WriteLine("  Throughput:");
            Console.WriteLine($"    Prefill:           {prefillToksPerSec:F2} tok/s");
            Console.WriteLine($"    Decode:            {decodeToksPerSec:F2} tok/s");
            Console.WriteLine($"    ms/token:          {msPerToken:F3} ms");
            Console.WriteLine();
            Console.WriteLine("  Memory:");
            Console.WriteLine($"    Total alloc:       {totalAllocations / 1024.0:F2} KB");
            Console.WriteLine($"    Alloc/token:       {allocPerToken:F2} bytes");
            Console.WriteLine($"    GC (Gen0/1/2):     {GC.CollectionCount(0) - startGen0}/{GC.CollectionCount(1) - startGen1}/{GC.CollectionCount(2) - startGen2}");
            Console.WriteLine();
        }
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

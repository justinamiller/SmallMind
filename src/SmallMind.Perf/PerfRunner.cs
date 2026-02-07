using System.Diagnostics;
using SmallMind.Core.Simd;
using SmallMind.Core.Core;

namespace SmallMind.Perf;

/// <summary>
/// Runs deterministic performance microbenchmarks for hot path operations.
/// Measures: MatMul, Attention, LayerNorm, Softmax, KV cache operations.
/// </summary>
public sealed class PerfRunner
{
    private readonly PerfConfig _config;
    private readonly BenchmarkResults _results;
    private readonly Random _random;

    public PerfRunner(PerfConfig config)
    {
        _config = config;
        _results = new BenchmarkResults
        {
            System = SystemInfo.Collect()
        };
        _random = new Random(config.Seed);
    }

    public void RunAll()
    {
        if (_config.BenchmarkName == "all" || _config.BenchmarkName == "matmul")
        {
            RunMatMulBenchmarks();
        }
        
        if (_config.BenchmarkName == "all" || _config.BenchmarkName == "attention")
        {
            RunAttentionBenchmarks();
        }
        
        if (_config.BenchmarkName == "all" || _config.BenchmarkName == "layernorm")
        {
            RunLayerNormBenchmarks();
        }
        
        if (_config.BenchmarkName == "all" || _config.BenchmarkName == "softmax")
        {
            RunSoftmaxBenchmarks();
        }
        
        if (_config.BenchmarkName == "all" || _config.BenchmarkName == "kvcache")
        {
            RunKVCacheBenchmarks();
        }

        // Print results
        if (_config.JsonOutput)
        {
            _results.PrintJson();
        }
        else
        {
            _results.PrintText();
        }
    }

    private void RunMatMulBenchmarks()
    {
        // Test various sizes relevant to LLM inference
        var sizes = _config.FastMode 
            ? new[] { (64, 64, 64), (256, 256, 256) }
            : new[] { (64, 64, 64), (128, 128, 128), (256, 256, 256), (512, 512, 512) };

        foreach (var (m, k, n) in sizes)
        {
            RunMatMulBenchmark(m, k, n);
        }
    }

    private void RunMatMulBenchmark(int m, int k, int n)
    {
        // Allocate matrices
        var a = new float[m * k];
        var b = new float[k * n];
        var c = new float[m * n];

        // Initialize with random data
        for (int i = 0; i < a.Length; i++)
            a[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
        for (int i = 0; i < b.Length; i++)
            b[i] = (float)(_random.NextDouble() * 2.0 - 1.0);

        var metrics = MeasureBenchmark($"MatMul_{m}x{k}x{n}", () =>
        {
            MatMulOps.MatMul(a, b, c, m, k, n);
        });

        // Calculate GFLOPS
        long flops = 2L * m * k * n; // 2 operations per multiply-add
        double gflops = flops / (metrics.TimeMs * 1_000_000.0);
        metrics.CustomMetrics["GFLOPS"] = $"{gflops:F2}";
        metrics.CustomMetrics["Dimensions"] = $"{m}x{k}x{n}";

        _results.Benchmarks.Add(metrics);
    }

    private void RunAttentionBenchmarks()
    {
        var configs = _config.FastMode
            ? new[] { (seqLen: 32, numHeads: 4, headDim: 64) }
            : new[] { (seqLen: 32, numHeads: 4, headDim: 64), (seqLen: 64, numHeads: 8, headDim: 64) };

        foreach (var (seqLen, numHeads, headDim) in configs)
        {
            RunAttentionBenchmark(seqLen, numHeads, headDim);
        }
    }

    private void RunAttentionBenchmark(int seqLen, int numHeads, int headDim)
    {
        // Use single head attention for the benchmark (FusedScaledDotProductAttention)
        var q = new float[seqLen * headDim];
        var k = new float[seqLen * headDim];
        var v = new float[seqLen * headDim];
        var output = new float[seqLen * headDim];

        // Initialize with random data
        for (int i = 0; i < q.Length; i++)
        {
            q[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
            k[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
            v[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
        }

        var metrics = MeasureBenchmark($"Attention_seq{seqLen}_d{headDim}", () =>
        {
            FusedAttentionKernels.FusedScaledDotProductAttention(q, k, v, output, seqLen, headDim, isCausal: true);
        });

        metrics.CustomMetrics["SeqLen"] = seqLen;
        metrics.CustomMetrics["HeadDim"] = headDim;

        _results.Benchmarks.Add(metrics);
    }

    private void RunLayerNormBenchmarks()
    {
        var sizes = _config.FastMode
            ? new[] { 256, 512 }
            : new[] { 256, 512, 1024, 2048 };

        foreach (var size in sizes)
        {
            RunLayerNormBenchmark(size);
        }
    }

    private void RunLayerNormBenchmark(int featureDim)
    {
        var input = new float[featureDim];
        var output = new float[featureDim];
        var gamma = new float[featureDim];
        var beta = new float[featureDim];

        // Initialize
        for (int i = 0; i < featureDim; i++)
        {
            input[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
            gamma[i] = 1.0f;
            beta[i] = 0.0f;
        }

        var metrics = MeasureBenchmark($"LayerNorm_{featureDim}", () =>
        {
            LayerNormOps.LayerNorm(input, gamma, beta, output, batch: 1, features: featureDim, eps: 1e-5f);
        });

        metrics.CustomMetrics["FeatureDim"] = featureDim;
        _results.Benchmarks.Add(metrics);
    }

    private void RunSoftmaxBenchmarks()
    {
        var sizes = _config.FastMode
            ? new[] { 256, 512 }
            : new[] { 256, 512, 1024, 2048 };

        foreach (var size in sizes)
        {
            RunSoftmaxBenchmark(size);
        }
    }

    private void RunSoftmaxBenchmark(int size)
    {
        var input = new float[size];
        var output = new float[size];

        // Initialize with random data
        for (int i = 0; i < size; i++)
            input[i] = (float)(_random.NextDouble() * 10.0 - 5.0);

        var metrics = MeasureBenchmark($"Softmax_{size}", () =>
        {
            SoftmaxOps.Softmax1D(input, output);
        });

        metrics.CustomMetrics["Size"] = size;
        _results.Benchmarks.Add(metrics);
    }

    private void RunKVCacheBenchmarks()
    {
        var configs = _config.FastMode
            ? new[] { (layers: 4, maxSeq: 128, heads: 4, headDim: 64) }
            : new[] { (layers: 4, maxSeq: 128, heads: 4, headDim: 64), (layers: 8, maxSeq: 256, heads: 8, headDim: 64) };

        foreach (var (layers, maxSeq, heads, headDim) in configs)
        {
            RunKVCacheBenchmark(layers, maxSeq, heads, headDim);
        }
    }

    private void RunKVCacheBenchmark(int numLayers, int maxSeqLen, int numHeads, int headDim)
    {
        // Constructor signature: (numLayers, maxSeqLen, numHeads, headDim, kvHeads, pageSize)
        var cache = new OptimizedKVCache(numLayers, maxSeqLen, numHeads, headDim, kvHeads: numHeads);
        
        var key = new float[numHeads * headDim];
        var value = new float[numHeads * headDim];
        
        // Initialize
        for (int i = 0; i < key.Length; i++)
        {
            key[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
            value[i] = (float)(_random.NextDouble() * 2.0 - 1.0);
        }

        var metrics = MeasureBenchmark($"KVCache_l{numLayers}_s{maxSeqLen}_h{numHeads}_d{headDim}", () =>
        {
            // Simulate writing to cache
            for (int layer = 0; layer < numLayers; layer++)
            {
                cache.Append(layer, key, value);
            }
            
            // Simulate reading from cache
            for (int layer = 0; layer < numLayers; layer++)
            {
                var keys = cache.GetKeys(layer);
                var values = cache.GetValues(layer);
                // Access the data to ensure it's not optimized away
                if (keys.Length > 0 && values.Length > 0)
                {
                    _ = keys[0];
                    _ = values[0];
                }
            }
        });

        metrics.CustomMetrics["Layers"] = numLayers;
        metrics.CustomMetrics["MaxSeqLen"] = maxSeqLen;
        metrics.CustomMetrics["NumHeads"] = numHeads;
        metrics.CustomMetrics["HeadDim"] = headDim;

        cache.Dispose();
        _results.Benchmarks.Add(metrics);
    }

    private BenchmarkMetrics MeasureBenchmark(string name, Action action)
    {
        // Force GC before benchmark
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        // Warmup
        for (int i = 0; i < _config.WarmupIterations; i++)
        {
            action();
        }

        // Reset GC counters
        var gen0Before = GC.CollectionCount(0);
        var gen1Before = GC.CollectionCount(1);
        var gen2Before = GC.CollectionCount(2);
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();

        // Measure
        var sw = Stopwatch.StartNew();
        var cpuBefore = Process.GetCurrentProcess().TotalProcessorTime;

        for (int i = 0; i < _config.MeasuredIterations; i++)
        {
            action();
        }

        var cpuAfter = Process.GetCurrentProcess().TotalProcessorTime;
        sw.Stop();

        var allocAfter = GC.GetAllocatedBytesForCurrentThread();
        var gen0After = GC.CollectionCount(0);
        var gen1After = GC.CollectionCount(1);
        var gen2After = GC.CollectionCount(2);

        // Calculate average per iteration
        var avgTimeMs = sw.Elapsed.TotalMilliseconds / _config.MeasuredIterations;
        var avgCpuTimeMs = (cpuAfter - cpuBefore).TotalMilliseconds / _config.MeasuredIterations;
        var avgAllocBytes = (allocAfter - allocBefore) / _config.MeasuredIterations;

        return new BenchmarkMetrics
        {
            Name = name,
            TimeMs = avgTimeMs,
            CpuTimeMs = avgCpuTimeMs,
            AllocatedBytes = avgAllocBytes,
            Gen0Collections = gen0After - gen0Before,
            Gen1Collections = gen1After - gen1Before,
            Gen2Collections = gen2After - gen2Before
        };
    }
}

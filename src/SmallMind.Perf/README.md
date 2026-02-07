# SmallMind.Perf - Performance Microbenchmarks

## Overview

SmallMind.Perf is a deterministic performance benchmarking harness for measuring hot-path operations in SmallMind. It provides precise, repeatable measurements of core kernels: MatMul, Attention, LayerNorm, Softmax, and KV Cache operations.

## Features

- **Deterministic**: Same seed produces same results
- **Zero Dependencies**: Uses only .NET BCL (`Stopwatch`, `Process`, `GC`)
- **Comprehensive Metrics**:
  - Wall-clock time (milliseconds)
  - CPU time (milliseconds)
  - Allocated bytes per operation
  - GC collection counts (Gen0/1/2)
  - Custom metrics (GFLOPS, dimensions, etc.)
- **Multiple Output Formats**:
  - Human-readable text
  - Machine-parseable JSON
- **Fast & Full Modes**:
  - `--fast` for CI (quick validation)
  - Full mode for deep profiling

## Usage

### Basic Usage

```bash
# Run all benchmarks
cd src/SmallMind.Perf
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release -- --bench matmul

# Fast mode for CI
dotnet run -c Release -- --fast

# JSON output for automation
dotnet run -c Release -- --json
```

### Command Line Options

```
SmallMind.Perf [options]

Options:
  --warmup N      Number of warmup iterations (default: 3)
  --iters M       Number of measured iterations (default: 10)
  --bench <name>  Benchmark to run: all|matmul|attention|layernorm|softmax|kvcache (default: all)
  --json          Output results in JSON format
  --fast          Fast mode for CI (--warmup 1 --iters 3)
  --seed N        Random seed for determinism (default: 42)
  --help, -h      Show help
```

### Examples

```bash
# Run MatMul benchmarks with more iterations
dotnet run -c Release -- --bench matmul --warmup 5 --iters 20

# Run all benchmarks in fast mode with JSON output
dotnet run -c Release -- --fast --json > results.json

# Custom warmup and iterations
dotnet run -c Release -- --warmup 10 --iters 50 --seed 123
```

## Benchmark Descriptions

### MatMul (Matrix Multiplication)
Tests SIMD-accelerated matrix multiplication with various sizes:
- **Fast Mode**: 64×64, 256×256
- **Full Mode**: 64×64, 128×128, 256×256, 512×512

**Metrics:**
- GFLOPS (2 × M × K × N operations per second)
- Dimensions (M×K×N)
- Time and allocations

**Example Output:**
```
MatMul_256x256x256:
  Time: 2.161 ms
  CPU Time: 4.062 ms
  Allocated: 1.76 KB
  GC: Gen0=0, Gen1=0, Gen2=0
  GFLOPS: 15.53
  Dimensions: 256x256x256
```

### Attention (Fused Scaled Dot-Product)
Tests flash-attention style fused kernels:
- **Fast Mode**: seq=32, heads=4, dim=64
- **Full Mode**: seq=32/64, heads=4/8, dim=64

**Metrics:**
- SeqLen, NumHeads, HeadDim
- Time and allocations

### LayerNorm
Tests fused layer normalization:
- **Fast Mode**: features=256, 512
- **Full Mode**: features=256, 512, 1024, 2048

**Metrics:**
- FeatureDim
- Time and allocations

### Softmax
Tests 1D softmax with stable computation:
- **Fast Mode**: size=256, 512
- **Full Mode**: size=256, 512, 1024, 2048

**Metrics:**
- Size
- Time and allocations

### KVCache (Key-Value Cache)
Tests optimized KV cache append and read:
- **Fast Mode**: layers=4, maxSeq=128, heads=4, dim=64
- **Full Mode**: layers=4/8, maxSeq=128/256, heads=4/8, dim=64

**Metrics:**
- Layers, MaxSeqLen, NumHeads, HeadDim
- Time and allocations

## Interpreting Results

### Good Performance Indicators

1. **Zero GC Collections**: Operations should not trigger garbage collection
   ```
   GC: Gen0=0, Gen1=0, Gen2=0  ✅ Good
   ```

2. **Minimal Allocations**: Steady-state operations should allocate < 2KB
   ```
   Allocated: 189 B  ✅ Excellent
   Allocated: 1.76 KB  ✅ Good
   Allocated: 50 KB  ⚠️ Investigate
   ```

3. **GFLOPS Scaling**: MatMul GFLOPS should increase with matrix size
   ```
   64×64: 11.16 GFLOPS
   256×256: 15.53 GFLOPS
   512×512: 22.23 GFLOPS  ✅ Good scaling
   ```

4. **Consistent CPU Time**: CPU time ≈ Wall time (no blocking)
   ```
   Time: 2.161 ms
   CPU Time: 4.062 ms  ⚠️ Parallel overhead or multi-threaded
   ```

### Regression Detection

Run benchmarks before and after changes:

```bash
# Baseline
dotnet run -c Release -- --json > baseline.json

# After optimization
dotnet run -c Release -- --json > optimized.json

# Compare (manually or with JSON diff tool)
```

**Acceptable variations:**
- Time: ±5% (CPU variance)
- Allocations: ±200 bytes (JIT variations)
- GFLOPS: ±3% (thermal/turbo effects)

**Red flags:**
- Time increase > 10%
- Allocations increase > 10%
- New GC collections (Gen0/1/2)
- GFLOPS decrease > 5%

## System Information

SmallMind.Perf automatically collects and reports:
- OS and architecture
- .NET runtime version
- Processor count
- GC mode (Server vs Workstation)
- GC latency mode
- SIMD capabilities (AVX2, AVX-512, Vector size)

**Example:**
```
=== System Information ===
OS: Ubuntu 24.04.3 LTS
Architecture: X64
Framework: .NET 10.0.2
Processor Count: 4
GC Mode: Workstation
GC Latency Mode: Interactive
Vector<float>.Count: 8
Vector Hardware Accelerated: True
AVX2 Supported: True
AVX-512F Supported: False
```

## Integration with CI

Add to GitHub Actions:

```yaml
- name: Run Performance Benchmarks
  run: |
    cd src/SmallMind.Perf
    dotnet run -c Release -- --fast --json > perf-results.json
    
- name: Upload Benchmark Results
  uses: actions/upload-artifact@v3
  with:
    name: perf-results
    path: src/SmallMind.Perf/perf-results.json
```

## Benchmark Design Principles

1. **Deterministic**: Same seed, same inputs, same outputs
2. **Isolated**: Each benchmark is independent (no shared state)
3. **Warm-up**: JIT compilation happens before measurement
4. **Precise**: Uses high-resolution timers and CPU time
5. **Minimal Overhead**: Measurement code is lightweight

## Limitations

- **CPU-only**: Does not measure GPU performance
- **Micro-benchmarks**: May not reflect end-to-end inference performance
- **Cold start**: First run includes JIT compilation (use warmup)
- **Thermal effects**: Long benchmarks may trigger thermal throttling

## Troubleshooting

### High Variance in Results
- **Cause**: Background processes, thermal throttling
- **Fix**: Run on idle system, increase iterations

### Unexpected Allocations
- **Cause**: JIT allocations, oversized pooled arrays
- **Fix**: Check `--warmup` count, review allocator logs

### Low GFLOPS
- **Cause**: Debug build, non-optimized kernel selected
- **Fix**: Use Release build, check SIMD capabilities

### GC Collections
- **Cause**: Allocations in hot path, large temporary arrays
- **Fix**: Use ArrayPool, pre-allocate buffers, check allocation logs

## Related Documentation

- [PERF_HOTPATH_AUDIT.md](../PERF_HOTPATH_AUDIT.md) - Hot path analysis
- [NUMERIC_TOLERANCE.md](../NUMERIC_TOLERANCE.md) - Correctness tolerance policy
- [../tests/SmallMind.Tests/Kernels/KernelCorrectnessTests.cs](../../tests/SmallMind.Tests/Kernels/KernelCorrectnessTests.cs) - Correctness tests

## Maintenance

### Adding a New Benchmark

1. Add benchmark method to `PerfRunner.cs`:
   ```csharp
   private void RunNewOpBenchmark(int size)
   {
       // Setup
       var input = new float[size];
       var output = new float[size];
       
       // Initialize
       for (int i = 0; i < size; i++)
           input[i] = (float)_random.NextDouble();
       
       // Measure
       var metrics = MeasureBenchmark($"NewOp_{size}", () =>
       {
           NewOp(input, output);
       });
       
       // Add custom metrics
       metrics.CustomMetrics["Size"] = size;
       _results.Benchmarks.Add(metrics);
   }
   ```

2. Add to `RunAll()`:
   ```csharp
   if (_config.BenchmarkName == "all" || _config.BenchmarkName == "newop")
   {
       RunNewOpBenchmarks();
   }
   ```

3. Update `--help` text in `PerfConfig.cs`

### Versioning

Track baseline results with version tags:
```bash
git tag -a perf-baseline-v1.0 -m "Baseline before optimization X"
git push origin perf-baseline-v1.0
```

## License

Same as SmallMind (MIT License)

## Contact

- Issues: [GitHub Issues](https://github.com/justinamiller/SmallMind/issues)
- Discussions: [GitHub Discussions](https://github.com/justinamiller/SmallMind/discussions)

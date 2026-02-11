# GFLOPS Comparison Benchmark

This benchmark suite provides comprehensive performance comparison for evaluating different GFLOPS optimization approaches in SmallMind.

## Purpose

Compare performance metrics across different implementations:
- **PR #192**: Routes MatMulOps to GemmMicrokernels
- **PR #193**: Fixes GemmMicrokernels A-indexing bug
- **Main branch**: Baseline performance

## Metrics Measured

### Core Performance
- **GFLOPS**: Billion floating-point operations per second
- **Time per Operation**: Milliseconds per matrix multiplication
- **Operations per Second**: Throughput metric

### Memory Efficiency
- **Bytes Allocated per Operation**: Memory allocation overhead
- **Total Bytes Allocated**: Total memory pressure
- **GC Collections**: Gen0, Gen1, Gen2 collection counts

### System Metrics
- **Memory Bandwidth**: Estimated GB/s (read + write)
- **Cache Efficiency**: Implied through different matrix sizes

### LLM-Specific Workloads
- **Single Token Decode** (M=1): Most common inference pattern
- **Batch Decode**: Small batch processing
- **Prefill**: Large context processing (256, 512 tokens)
- **Large Model Decode**: 4K hidden dimension models

## Running the Benchmark

### Quick Start

```bash
cd benchmarks/GFLOPSComparisonBenchmark
dotnet run -c Release
```

### Compare Across Branches

To compare all three implementations:

```bash
# From the repository root
./run-gflops-comparison.sh
```

This script will:
1. Run baseline (main branch)
2. Run PR #192 branch
3. Run PR #193 branch
4. Generate comparison report

## Understanding Results

### GFLOPS Targets

For CPU-based matrix multiplication:

- **Excellent**: >60 GFLOPS (approaching theoretical max for SIMD)
- **Good**: 40-60 GFLOPS
- **Acceptable**: 20-40 GFLOPS
- **Needs Improvement**: <20 GFLOPS

### Memory Allocations

- **Optimal**: 0 bytes/operation (zero-allocation path)
- **Acceptable**: <100 bytes/operation (small overhead)
- **Warning**: >1000 bytes/operation (significant GC pressure)
- **Critical**: >10KB/operation (major performance impact)

### GC Collections

- **Optimal**: 0 collections during benchmark
- **Acceptable**: Gen0 only
- **Warning**: Gen1 collections
- **Critical**: Gen2 collections (indicates major memory pressure)

## Output Files

The benchmark generates two files:

1. **GFLOPS_COMPARISON_RESULTS.md**: Human-readable report with tables and analysis
2. **GFLOPS_COMPARISON_RESULTS.json**: Machine-readable data for further processing

## Workload Categories

### Square Matrices
Standard sizes: 64, 128, 256, 512, 1024, 2048

Tests computational efficiency across different cache levels:
- 64-256: L1 cache friendly
- 512-1024: L2 cache friendly  
- 2048+: L3 cache and memory bandwidth

### LLM-Specific Workloads

Realistic transformer model patterns:

- **M=1, K×N=512×512**: Single token decode (autoregressive generation)
- **M=32, K×N=512×512**: Batch decode (8-32 simultaneous requests)
- **M=256, K×N=4096×4096**: Prefill for typical context (e.g., SmolLM2)
- **M=512, K×N=4096×4096**: Prefill for larger context
- **M=1, K×N=4096×4096**: Large model single token (e.g., Llama-like)

### Sustained Throughput

30-second continuous test at medium size (512×512):
- Tests JIT stability
- Detects thermal throttling
- Measures real-world sustained performance
- Reveals memory allocation patterns over time

## Comparing with Other Frameworks

Expected GFLOPS ranges (CPU, single-threaded):

| Framework | Typical GFLOPS | Notes |
|-----------|---------------|-------|
| SmallMind (Baseline) | 10-30 | Managed C#, SIMD |
| SmallMind (Optimized) | 40-70 | Target for PRs |
| llama.cpp | 60-100 | Hand-tuned C++/assembly |
| Eigen | 50-90 | C++ template metaprogramming |
| OpenBLAS | 70-120 | Highly optimized BLAS |
| MKL | 90-150 | Intel Math Kernel Library |

## Interpreting Comparison Results

When comparing PR #192 vs PR #193:

### Focus Areas

1. **Peak GFLOPS**: Which achieves higher maximum?
2. **Consistency**: Which maintains performance across sizes?
3. **Zero Allocations**: Which has better memory efficiency?
4. **LLM Workloads**: Which is faster for real-world patterns (M=1, prefill)?
5. **Sustained Performance**: Which maintains performance over time?

### Trade-offs to Consider

- **Peak vs. Average**: Some optimizations work well for specific sizes
- **Memory vs. Speed**: Zero allocations might trade some speed
- **Single-row (M=1)**: Critical for inference, might regress in some optimizations
- **Large matrices**: Important for prefill, might benefit differently

## Example Comparison

After running both PRs, you might see:

```markdown
### PR #192 vs PR #193

| Metric | PR #192 | PR #193 | Winner |
|--------|---------|---------|--------|
| Peak GFLOPS (128×128) | 60.2 | 66.1 | PR #193 |
| Avg GFLOPS (all tests) | 38.5 | 41.2 | PR #193 |
| Zero Alloc Tests | 15/18 | 12/18 | PR #192 |
| M=1 GFLOPS | 2.3 | 6.6 | PR #193 |
| Prefill GFLOPS | 32.1 | 28.4 | PR #192 |
```

## Advanced Usage

### Custom Matrix Sizes

Edit `Program.cs` to modify `_matrixSizes` or `_llmWorkloads`:

```csharp
private readonly int[] _matrixSizes = new[] { 128, 256, 512, 1024 };
```

### Adjust Iteration Counts

For longer/shorter runs, modify `DetermineIterationCount()`:

```csharp
private int DetermineIterationCount(int M, int K, int N)
{
    // Increase multiplier for more stable results
    // Decrease for faster testing
}
```

### Environment Variables

Control .NET JIT for experiments:

```bash
# Disable tiered compilation
DOTNET_TieredCompilation=0 dotnet run -c Release

# Disable PGO
DOTNET_TieredPGO=0 dotnet run -c Release

# Disable ReadyToRun
DOTNET_ReadyToRun=0 dotnet run -c Release
```

## Troubleshooting

### Build Errors

Ensure you're building in Release mode:
```bash
dotnet build -c Release
```

### Low GFLOPS

Check:
- Running in Release mode (not Debug)
- SIMD support detected (AVX2/FMA)
- Not running in VM without AVX2
- Background processes not consuming CPU

### Inconsistent Results

- Run multiple times and average
- Check thermal throttling
- Disable background processes
- Pin to performance cores (if available)

## See Also

- [PR #192](https://github.com/justinamiller/SmallMind/pull/192) - GemmMicrokernels routing
- [PR #193](https://github.com/justinamiller/SmallMind/pull/193) - A-indexing bug fix
- [StandardLLMBenchmarks](../StandardLLMBenchmarks/README.md) - Industry comparison
- [MatMulBenchmark](../MatMulBenchmark.cs) - Original SIMD benchmark

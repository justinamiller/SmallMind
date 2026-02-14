# MatMul Performance Benchmark

## Overview

This benchmark measures the performance of matrix multiplication (MatMul) operations in the SmallMind library. It provides detailed metrics including GFLOPS, memory allocations, and GC statistics.

## Quick Start

```bash
# Run with defaults (512×512, 20 warmup, 100 iterations)
dotnet run --project benchmarks/MatMulBenchmark.csproj -c Release

# Run with custom parameters
dotnet run --project benchmarks/MatMulBenchmark.csproj -c Release -- --size 1024 --warmup 10 --iters 50
```

## Command Line Arguments

- `--size N`: Matrix size (M=K=N). Default: 512
- `--warmup N`: Number of warmup iterations. Default: 20
- `--iters N`: Number of measured iterations. Default: 100

## JIT Mode Matrix Testing

Test performance under different JIT compilation settings:

```bash
# Run all JIT mode tests
./benchmarks/run-jit-matrix.sh

# Or manually test individual modes:
DOTNET_TieredCompilation=0 dotnet run --project benchmarks/MatMulBenchmark.csproj -c Release
DOTNET_TieredCompilation=1 dotnet run --project benchmarks/MatMulBenchmark.csproj -c Release
DOTNET_TieredCompilation=1 DOTNET_TieredPGO=0 dotnet run --project benchmarks/MatMulBenchmark.csproj -c Release
DOTNET_TieredCompilation=1 DOTNET_TieredPGO=1 dotnet run --project benchmarks/MatMulBenchmark.csproj -c Release
```

## Benchmark Output

The benchmark reports the following metrics:

### Environment Information
- Operating System and Architecture
- .NET Runtime Version
- Processor Count
- SIMD Instruction Set Support (AVX, AVX2, FMA, AVX-512)
- JIT Configuration (TieredCompilation, TieredPGO, ReadyToRun)

### Performance Metrics
- **Kernel Selected**: Which SIMD kernel was used (Avx512Unsafe, Avx2Unsafe, etc.)
- **Total Time**: Cumulative time for all iterations
- **Time per Operation**: Average time per MatMul call
- **Performance**: GFLOPS (Giga Floating-Point Operations Per Second)

### Memory & GC Metrics
- **Allocated**: Bytes allocated per operation
- **Gen0/1/2 Collections**: Number of garbage collections during benchmark

### Correctness Check
- Spot-check of output values to verify correctness

## Example Output

```
=== MatMul Performance Benchmark ===

Environment:
  OS: Ubuntu 24.04.3 LTS
  Architecture: X64
  .NET: 10.0.2
  Processor Count: 4

SIMD Support:
  AVX: ✓
  AVX2: ✓
  FMA: ✓
  AVX-512: ✗

JIT Configuration:
  DOTNET_TieredCompilation: 1 (default)
  DOTNET_TieredPGO: 1 (default)
  DOTNET_ReadyToRun: 1 (default)

Matrix Size: 512 × 512 × 512 × 512
Warmup Iterations: 20
Measured Iterations: 100

Warming up...
Kernel Selected: Avx2Unsafe

Measuring...

--- Results ---
Total Time: 771.60 ms
Time per Operation: 7.716 ms
Performance: 34.79 GFLOPS

Memory & GC:
  Allocated: 1,802 bytes/op
  Gen0 Collections: 0
  Gen1 Collections: 0
  Gen2 Collections: 0

Correctness Check:
  C[0] = 124.302803
  C[M*N/2] = 121.559479
  C[M*N-1] = 135.339615

=== Benchmark Complete ===
```

## Performance Targets

For 512×512 MatMul on modern x86-64 CPUs with AVX2+FMA:
- **Target**: ≥30 GFLOPS
- **Current**: ~35 GFLOPS ✓

## Kernel Selection

The benchmark automatically selects the best available SIMD kernel:

1. **Avx512Unsafe**: AVX-512 (512-bit vectors, 16 floats)
2. **Avx2Unsafe**: AVX2+FMA (256-bit vectors, 8 floats) - Most common
3. **AvxUnsafe**: AVX without FMA (256-bit vectors, 8 floats)
4. **NeonTiled**: ARM NEON (128-bit vectors)
5. **VectorUnsafe**: Generic Vector<T> fallback

The selected kernel is displayed after warmup.

## Optimizations

The MatMul implementation uses several performance optimizations:

1. **Register Blocking**: Accumulates 4 vectors (32 floats) in registers across K dimension
2. **K-Loop Unrolling**: 2x unrolling for better instruction-level parallelism
3. **Store-Once Pattern**: Writes output ONCE per tile after accumulation
4. **Unsafe Pointers**: Direct pointer arithmetic to avoid bounds checks
5. **Parallel Processing**: Multi-threading for matrices ≥128 rows

## Notes

- Always run in **Release** mode for accurate measurements
- The benchmark pre-allocates matrices and zeros C before each iteration
- Allocations shown (1.8KB/op) are from Array.Clear(), not from the MatMul kernel
- Zero GC collections indicate no garbage collection pressure
- For best results, close other applications during benchmarking

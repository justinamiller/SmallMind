# Tier-1 Hotpath Performance Benchmark

## Overview

This benchmark measures the performance impact of three critical Tier-1 optimizations in SmallMind:

1. **Dropout Zero-Copy Passthrough**: Eliminates `Clone()` in eval mode, returning input reference directly
2. **Conditional Workspace Clearing**: Skips Array.Clear() for store-once kernels (MatMulTransposeB, Array.Copy operations)
3. **Skip Clearing Newly Allocated Arrays**: Avoids redundant clearing since runtime already zeros new arrays

## Running the Benchmark

### From Command Line

```bash
# From repository root
cd benchmarks/Tier1HotpathBenchmark
dotnet run -c Release

# Or from repository root
dotnet run --project benchmarks/Tier1HotpathBenchmark/Tier1HotpathBenchmark.csproj -c Release
```

### Requirements

- .NET 10.0 or later
- No external dependencies (uses only BCL)
- Runs on any platform (Windows, Linux, macOS)

## Benchmark Scenarios

### 1. Dropout Eval Passthrough Benchmark

**What it measures:**
- Verifies zero-copy passthrough (ReferenceEquals check)
- Measures allocation elimination from removing Clone()
- Runs 10,000 iterations after warmup

**Expected Results:**
- **Before**: ~393 KB/op (clones entire tensor every call)
- **After**: ~0 bytes/op (returns input reference)
- **Status**: ✓ Pass if < 100 bytes/op

### 2. Workspace Reuse Benchmark

**What it measures:**
- Allocation reduction from conditional clearing
- Skip clearing for store-once kernels (Q/K/V extraction, attention scores, reshaped output)
- Keep clearing for accumulation kernels (attention output via MatMul FMA)
- Runs 100 iterations after workspace allocation

**Expected Results:**
- Allocations should be dominated by output tensor only
- Minimal overhead from workspace management
- **Status**: ✓ Pass if bytes/op < 1.5x output tensor size

### 3. End-to-End Forward Pass Benchmark

**What it measures:**
- Combined impact of all Tier-1 optimizations
- Full transformer forward pass (2 layers, 4 heads, 128 dim)
- Tokens/second throughput
- GC collection counts

**Expected Results:**
- Minimal GC collections (ideally zero Gen0/1/2)
- Lower allocation rate compared to baseline
- Higher tokens/second throughput

## Metrics Captured

All benchmarks capture:

- **Time**: Total time, avg time per operation, throughput
- **Allocations**: Total bytes allocated, bytes per operation
- **GC Activity**: Gen0/1/2 collection counts
- **Working Set**: Memory delta during benchmark run

## Implementation Details

### No Third-Party Dependencies

Uses only .NET BCL APIs:
- `GC.GetTotalAllocatedBytes(precise: true)` for allocation tracking
- `GC.CollectionCount(generation)` for GC metrics
- `Process.GetCurrentProcess().WorkingSet64` for memory footprint
- `Stopwatch` for timing

### Server GC Mode

The benchmark uses Server GC (`<ServerGarbageCollection>true</ServerGarbageCollection>`) to match production settings.

### Warmup Strategy

- Each benchmark includes warmup iterations to:
  - Allocate persistent workspaces
  - JIT compile hot paths
  - Stabilize memory state
- GC is forced before measurement phase

## Interpreting Results

### Dropout Benchmark

✓ **OPTIMIZATION EFFECTIVE**: < 100 bytes/op
⚠ **UNEXPECTED ALLOCATIONS**: > 100 bytes/op

### Workspace Reuse Benchmark

✓ **WORKSPACE REUSE EFFECTIVE**: Bytes/op close to output tensor size only
⚠ **UNEXPECTED ALLOCATIONS**: Significantly more than output tensor

### End-to-End Benchmark

✓ **EXCELLENT**: No GC collections during run
✓ **GOOD**: Minimal Gen0 collections only
⚠ **HIGH GC ACTIVITY**: Gen1/2 collections occurred

## Expected Improvements

Based on the optimizations:

1. **Dropout**: Eliminates ~393 KB/op for typical tensor sizes (B=4, T=128, D=768)
2. **Workspace Clearing**: Saves 30-50% of clearing time for large workspaces (T×T attention scores)
3. **New Array Clearing**: Saves additional clearing overhead on shape changes
4. **Combined**: Reduces allocations by 50-80% in inference hot paths

## Baseline Comparison

To compare against baseline (before optimizations):

1. Checkout commit before Tier-1 changes
2. Run benchmark and save output
3. Checkout current commit with optimizations
4. Run benchmark again
5. Compare metrics side-by-side

Example comparison:

```
Metric                  | Before       | After        | Improvement
------------------------|--------------|--------------|-------------
Dropout bytes/op        | 393,216      | 0            | 100%
Workspace bytes/op      | 180 KB       | 65 KB        | 64%
End-to-end time/forward | 12.5 ms      | 10.2 ms      | 18%
Gen0 collections        | 5            | 0            | 100%
```

## Troubleshooting

### High Allocations in Dropout Benchmark

- Check that Dropout.Forward() returns `input` directly in eval mode
- Verify ReferenceEquals check passes

### High Allocations in Workspace Benchmark

- Ensure GetOrAllocateWorkspace has clearBeforeReuse parameter
- Verify call sites use clearBeforeReuse=false for store-once kernels
- Check ConditionalWeakTable is properly tracking fresh workspaces

### GC Collections in End-to-End

- Increase warmup iterations
- Check for allocations in non-optimized code paths
- Verify Server GC is enabled

## Notes

- Results may vary by CPU, memory speed, and .NET version
- Run multiple times and average for consistent results
- Disable background processes for accurate measurements
- Use Release configuration (`-c Release`)

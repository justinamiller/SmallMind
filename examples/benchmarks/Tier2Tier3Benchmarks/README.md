# Tier-2 & Tier-3 Performance Benchmarks

## Overview

This benchmark suite measures the performance impact of Tier-2 and Tier-3 optimizations for SmallMind's CPU-only transformer inference engine. All benchmarks use only BCL (Base Class Library) - no third-party dependencies.

## Optimizations Measured

### Tier-2: Attention Mechanism Optimizations

1. **MatMulTransposeB Parallelization**
   - Reintroduced safe parallelization using pointer-based approach
   - Triggers when M >= 64, K >= 64, and multiple processors available
   - Uses Partitioner.Create for efficient work distribution

2. **AVX-512 Dispatch**
   - Added AVX-512F support for 512-bit vector operations (16 floats)
   - Dispatch order: AVX-512 → AVX2+FMA → Scalar
   - 2x vector width compared to AVX2

3. **AVX2 Register Blocking**
   - 4-way register blocking reduces horizontal sum overhead
   - Computes 4 dot products simultaneously
   - Better utilization of AVX2 registers

### Tier-3: Memory Layout & Cache Optimizations

4. **Q/K/V Extraction Improvements**
   - Loop restructuring: timestep t outermost for better cache locality
   - Buffer.BlockCopy instead of Array.Copy for faster memcpy
   - Sequential reads improve memory access patterns

5. **Linear Transpose Precomputation**
   - Transpose cache precomputed in Eval() instead of lazy on first forward
   - Eliminates surprise allocations during inference
   - Training path still works correctly

## Running the Benchmarks

```bash
# From repository root
dotnet run --project benchmarks/Tier2Tier3Benchmarks/Tier2Tier3Benchmarks.csproj -c Release

# Or from benchmarks directory
cd benchmarks/Tier2Tier3Benchmarks
dotnet run -c Release
```

## Benchmark Scenarios

### 1. MatMulTransposeB Parallelization

Tests Q @ K^T computation for various sizes:
- **T=128, K=64** (Small): Below parallelization threshold on some CPUs
- **T=256, K=64** (Medium): Parallelization beneficial
- **T=512, K=64** (Large): Maximum parallelization benefit
- **T=256, K=128** (Medium-Wide): Wider head dimension

**Metrics:**
- Time per operation
- ns per element
- Throughput (ops/sec)
- Allocations per operation
- GC activity
- Whether parallelized (based on heuristics)

**Expected Results:**
- Small allocations (~2-3KB per op for workspace management)
- Zero GC collections
- Parallelization triggers for T >= 64, K >= 64
- Throughput increases with parallelization

### 2. AVX-512 vs AVX2 Dispatch

Compares vector instruction performance:
- **K=64**: Typical head dimension
- **K=128**: Wider head dimension

**Note:** AVX-512 only runs on compatible CPUs. On systems without AVX-512, the benchmark notes this and skips comparison.

**Expected Results (AVX-512 capable):**
- ~1.5-2x speedup for AVX-512 over AVX2
- Better for larger K values

### 3. AVX2 Register Blocking

Validates register blocking improvement:
- **T=256, K=64**: Typical attention size

**Expected Results:**
- ~4x reduction in horizontal sum overhead
- Better throughput compared to single-column processing

### 4. Q/K/V Extraction Performance

Tests full attention layer with extraction:
- **B=2, T=128, nHead=8, headSize=64**: Small configuration
- **B=2, T=256, nHead=12, headSize=64**: Medium configuration  
- **B=4, T=128, nHead=8, headSize=64**: Larger batch

**Expected Results:**
- Time dominated by full forward pass (includes attention computation)
- Allocations primarily from output tensors
- Sequential reads improve cache locality

### 5. Linear Transpose Precomputation

Validates Eval() optimization:
- **in=768, out=768, batch=16, seq=128**: Typical transformer layer

**Expected Results:**
- Phase 1 (before Eval): High allocation on first forward (includes transpose)
- Phase 2 (Eval): Transpose precomputed
- Phase 3 (after Eval): Allocations ≈ output tensor size only (~6.3MB per op)
- No transpose reallocation on subsequent forwards

## Metrics Captured

All benchmarks measure:
- **Execution Time**: Stopwatch elapsed milliseconds
- **Allocations**: `GC.GetTotalAllocatedBytes(precise: true)` delta
- **GC Activity**: Collection counts for Gen0/1/2
- **Throughput**: Operations per second
- **Per-Element Cost**: ns/element for matrix operations

## Interpretation Guide

### MatMulTransposeB Parallelization

✅ **Good Results:**
- Allocations < 5KB per operation
- Zero GC collections
- Parallelized = YES for large matrices
- Higher throughput for parallelized cases

⚠️ **Issues:**
- High allocations (>10KB) - indicates workspace leaks
- GC collections - suggests allocation pressure
- No speedup when parallelized - check CPU affinity

### Linear Transpose Precomputation

✅ **Good Results:**
- Phase 3 allocations ≈ expected output size
- Status = ✓ PASS

⚠️ **Issues:**
- Phase 3 allocations >> output size - transpose still allocating
- Status = ✗ FAIL - optimization not working

## System Requirements

- .NET 10.0 or later
- No external dependencies
- AVX2 recommended (falls back to scalar if unavailable)
- AVX-512F optional (provides additional speedup)

## Performance Notes

### Parallelization Heuristics

```csharp
bool shouldParallelize = M >= 64 && K >= 64 && Environment.ProcessorCount > 1;
int rangeSize = Math.Max(4, M / (ProcessorCount * 2));
```

- **Why M >= 64, K >= 64?** Thread overhead dominates for smaller matrices
- **Chunk size:** Balances thread utilization vs overhead
- **Processor check:** No benefit on single-core systems

### Register Blocking Benefits

- **4-way blocking:** Processes 4 columns per iteration
- **Horizontal sum reduction:** 4 sums per K-loop instead of N sums
- **Better ILP:** Modern CPUs can execute multiple FMA in parallel

### Cache Locality Improvements

- **t-outer loop:** Sequential reads from qkv.Data
- **Buffer.BlockCopy:** Lower overhead than Array.Copy
- **Contiguous access:** Better prefetcher utilization

## Troubleshooting

### High Allocations

- Check workspace clearing is working (Tier-1 optimizations)
- Verify output tensors are reused where possible
- Look for unexpected tensor clones

### Low Throughput

- Verify AVX2/AVX-512 is supported (check System Information)
- Check CPU frequency (thermal throttling?)
- Ensure Release build is used

### GC Collections

- Indicates allocation pressure
- Check for workspace leaks
- Verify transpose cache is working

## Comparing Results

To compare before/after:
1. Checkout commit before Tier-2/3 optimizations
2. Run benchmarks and save output
3. Checkout current commit
4. Run benchmarks again
5. Compare metrics side-by-side

Expected improvements:
- **MatMulTransposeB:** 1.5-3x faster for large T (with parallelization)
- **AVX-512:** 1.5-2x faster (on supported CPUs)
- **Register Blocking:** 10-20% faster (reduced overhead)
- **Q/K/V Extraction:** 5-15% faster (better cache)
- **Linear:** Zero transpose allocations after Eval()

## Notes

- Results vary by CPU architecture, frequency, and system load
- Run multiple times and average for consistent results
- Disable Turbo Boost for more stable measurements
- Server GC mode provides best performance for these workloads

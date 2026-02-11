# GFLOPS Benchmark Results - Current Branch (Baseline)

**Branch:** copilot/add-performance-test-benchmarks (main branch baseline)  
**Date:** 2026-02-11  
**System:** Ubuntu 24.04.3 LTS, 4 cores, 15.6 GB RAM  
**SIMD:** AVX2 + FMA (Vector<float> size: 8)  
**.NET:** 10.0.2

## Summary

### Peak Performance
- **Best GFLOPS:** 59.99 (256×256 matrix)
- **Average GFLOPS:** 33.56 (across completed tests)

### Memory Efficiency
- **Allocations:** All tests show 56-1876 bytes/op allocation
- **Zero-Allocation Tests:** 0 out of 9 (0%)
- **GC Pressure:** ⚠️ Allocations detected in all tests

## Detailed Results

### Square Matrix Benchmarks

| Size | GFLOPS | Time/Op (ms) | Alloc (bytes/op) | Notes |
|------|--------|--------------|------------------|-------|
| 64×64 | 16.80 | 0.031 | 56 | Small, L1 cache |
| 128×128 | 17.75 | 0.236 | 1,732 | L1/L2 cache |
| **256×256** | **59.99** | 0.559 | 1,725 | **Peak GFLOPS** |
| 512×512 | 56.19 | 4.777 | 1,791 | L2/L3 cache |
| 1024×1024 | 39.88 | 53.846 | 1,809 | L3 cache |
| 2048×2048 | 36.51 | 470.520 | 1,876 | Memory bound |

### LLM-Specific Workloads

| Workload | Size | GFLOPS | Time/Op (ms) | Alloc (bytes/op) | Importance |
|----------|------|--------|--------------|------------------|------------|
| **Single Token Decode** | 1×512×512 | **27.77** | 0.019 | 56 | **Critical for inference** |
| Small Batch Decode | 32×512×512 | 28.41 | 0.590 | 56 | Batch processing |
| Prefill 256 tokens | 256×4096×4096 | 17.70 | 485.225 | 1,857 | Context processing |

### Sustained Throughput
*(Test incomplete due to timeout)*

## Performance Analysis

### Strengths
✅ **Good peak performance:** 59.99 GFLOPS on 256×256 matrices  
✅ **Decent M=1 performance:** 27.77 GFLOPS for single-token decode  
✅ **Consistent mid-range:** 40-60 GFLOPS on medium matrices

### Weaknesses
⚠️ **Memory allocations:** Every operation allocates 56-1876 bytes  
⚠️ **GC pressure:** All tests trigger allocations  
⚠️ **Large matrix performance:** Drops to ~36 GFLOPS on 2048×2048  
⚠️ **Small matrix performance:** Only ~17 GFLOPS on 64×64 and 128×128

## Comparison Context

This is the **baseline** (main branch) performance. When compared to:

### Expected PR #192 Improvements
- Zero allocations (0 bytes/op) ← **Major improvement**
- 60+ GFLOPS on 128×128 ← **Would improve small matrices**
- Better prefill performance ← **Would help large contexts**

### Expected PR #193 Improvements  
- 60+ GFLOPS on 128×128, 256×256 ← **Already at 60 on 256×256**
- 6.5x speedup on small matrices ← **Would improve 64×64, 128×128**
- Bug fix for correctness ← **Reliability improvement**

## Key Observations

1. **Peak Performance Zone:** 256×256 to 512×512 matrices (56-60 GFLOPS)
2. **Critical Metric (M=1):** 27.77 GFLOPS - decent but could be better
3. **Memory Issue:** Consistent allocations across all workloads
4. **Performance Scaling:** Performance degrades on very large matrices

## Recommendations

Based on these baseline results:

1. **PR #192 would address:** Memory allocation issues (zero-alloc)
2. **PR #193 would address:** Small matrix performance (64×64, 128×128)
3. **Both PRs target:** Achieving 60+ GFLOPS consistently

## Test Environment Details

- **OS:** Ubuntu 24.04.3 LTS
- **Architecture:** X64
- **Processors:** 4 cores
- **Memory:** 15.6 GB
- **Vector<float> Size:** 8 (AVX2)
- **SIMD Support:**
  - AVX: ✓
  - AVX2: ✓
  - FMA: ✓
  - AVX-512: ✗
- **JIT Configuration:**
  - Tiered Compilation: Enabled (default)
  - Tiered PGO: Enabled (default)
  - ReadyToRun: Enabled (default)

## Next Steps

To complete the comparison:
1. Run benchmark on PR #192 branch
2. Run benchmark on PR #193 branch
3. Compare all three sets of results
4. Make decision based on:
   - Peak GFLOPS improvements
   - Memory allocation reduction
   - M=1 (inference) performance
   - Prefill performance

---

**Note:** This benchmark shows the current state of the main branch. Both PR #192 and PR #193 aim to improve these metrics, particularly targeting 60+ GFLOPS and better memory efficiency.

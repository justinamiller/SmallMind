# SmallMind Performance & Memory Profiling Report

**Report Generated:** 2026-02-04 02:02:49  
**Comparison Against:** Previous run from 2026-02-03 23:06:46

---

## 📊 Executive Summary

### Overall Performance Metrics

| Metric | Current Run | Previous Run | Delta | Change |
|--------|-------------|--------------|-------|---------|
| **Total Runtime** | 9,237.19 ms | 5,927.60 ms | +3,309.56 ms | +55.8% ⚠️ |
| **Total Memory Allocated** | 338.62 MB | 2,550.03 MB | -2,211.42 MB | -86.7% ✅ |
| **Methods Profiled** | 29 | 29 | 0 | - |
| **Average Method Time** | 318.52 ms | 204.40 ms | +114.12 ms | +55.8% ⚠️ |

### Key Observations

🎯 **Mixed Results:**
- ✅ **Massive memory optimization** - 86.7% reduction in allocations
- ⚠️ **Runtime regression** - 55.8% slower overall
- ✅ **Improved Small Model** - 16.5% faster
- ⚠️ **Degraded Medium Model** - 55.1% slower

---

## 🔥 Core Performance Metrics

### Top 5 Hot Paths (Current Run)

| Rank | Method | Time (ms) | % of Total | Calls | Avg Time | Allocations |
|------|--------|-----------|------------|-------|----------|-------------|
| 1 | `Model_Medium_Inference` | 1,863.27 | 20.2% | 1 | 1,863 ms | 83.10 MB |
| 2 | `Model_Medium_GenerateToken` | 1,863.22 | 20.2% | 25 | 74.5 ms | 83.10 MB |
| 3 | `Model_Medium_Forward` | 1,862.82 | 20.2% | 25 | 74.5 ms | 83.10 MB |
| 4 | `MatMul_512x512` | 905.90 | 9.8% | 1 | 905.9 ms | 0.00 MB |
| 5 | `MatMul_Iteration` | 775.87 | 8.4% | 12 | 64.7 ms | 0.00 MB |

**Total Coverage:** Top 5 methods account for **78.8%** of total runtime.

### Matrix Multiplication Performance (SIMD Operations)

| Matrix Size | Current Time | Previous Time | Delta | Change | GFLOPS |
|-------------|--------------|---------------|-------|--------|--------|
| 64×64 | 7.39 ms | 7.07 ms | +0.32 ms | +4.5% | 0.07 |
| 128×128 | 13.29 ms | 3.54 ms | +9.75 ms | +275.4% ⚠️ | 0.32 |
| 256×256 | 112.93 ms | 19.59 ms | +93.34 ms | +476.5% ⚠️ | 0.30 |
| 512×512 | 905.90 ms | 172.11 ms | +733.79 ms | +426.3% ⚠️ | 0.30 |

**Analysis:** Severe performance regression in larger matrix multiplications. This is the primary driver of overall slowdown.

### Activation Functions Performance

| Operation | Size | Current Time | Previous Time | Delta | Change |
|-----------|------|--------------|---------------|-------|--------|
| GELU | 1,000 | 1.02 ms | 2.28 ms | -1.26 ms | -55.3% ✅ |
| GELU | 10,000 | 2.30 ms | 1.17 ms | +1.13 ms | +96.6% ⚠️ |
| GELU | 100,000 | 20.16 ms | 11.06 ms | +9.10 ms | +82.3% ⚠️ |
| GELU | 1,000,000 | 202.40 ms | 100.60 ms | +101.80 ms | +101.2% ⚠️ |
| **Softmax** | 256 | 2.47 ms | 7.21 ms | -4.74 ms | -65.7% ✅ |
| Softmax | 512 | 0.07 ms | 0.06 ms | +0.01 ms | +16.7% |
| Softmax | 1024 | 0.15 ms | 0.15 ms | 0.00 ms | 0.0% |
| Softmax | 2048 | 0.26 ms | 6.22 ms | -5.96 ms | -95.8% ✅ |

**Analysis:** Mixed results - Softmax operations significantly improved, GELU operations regressed.

---

## 💾 Memory Optimization Metrics

### Memory Allocation Profile

| Component | Current Alloc | Previous Alloc | Reduction | % Reduced |
|-----------|--------------|----------------|-----------|-----------|
| **Model Medium Inference** | 83.10 MB | 729.97 MB | 646.87 MB | -88.6% ✅ |
| **Model Small Inference** | 19.00 MB | 109.26 MB | 90.26 MB | -82.6% ✅ |
| **Model Medium Creation** | 26.41 MB | 26.45 MB | 0.04 MB | -0.2% |
| **Model Small Creation** | 3.61 MB | 3.61 MB | 0.00 MB | 0.0% |
| **Tensor Operations** | 1.52 MB | 1.52 MB | 0.00 MB | 0.0% |

### Memory Benchmark Results

#### TensorPool Performance
```
Baseline (No Pooling):
  Allocations: 2.08 MB
  Gen0 Collections: 0
  
With Pooling:
  Allocations: 0.12 MB
  Gen0 Collections: 0
  
Improvement: 94.4% allocation reduction
```

#### In-Place Operations
```
Baseline (Allocating):
  Allocations: 2.09 MB
  Time: 4ms
  
In-Place (Reusing Destination):
  Allocations: 0.04 MB
  Time: 2ms
  
Improvement: 98.1% allocation reduction, 50% faster
```

#### Fused LayerNorm
```
Batch Size: 32, Features: 512
Allocations: 0.70 KB (1000 iterations)
Gen0 Collections: 0
Average Time: 0.250 ms
Throughput: 65.7M elements/sec

✓ Zero allocations - fully fused!
```

### Allocation Profiler Results

#### MatMul Backward Pass
```
Matrix dimensions: 128×256 @ 256×128 = 128×128
Iterations: 100
Total time: 1,161 ms
Avg time per iteration: 11.61 ms

Memory Metrics:
  Total allocations: 13.00 MB
  Allocations per iteration: 133.11 KB
  Expected WITHOUT pooling: 25.00 MB
  Estimated reduction: 48.0%
```

#### Training Workload
```
Steps: 50, Batch size: 32, Hidden size: 256
Total time: 157 ms
Avg time per step: 3.15 ms

Memory Metrics:
  Total allocations: 3.77 MB
  Allocations per step: 77.25 KB
  Expected WITHOUT pooling: 62.50 MB
  Estimated reduction: 94.0%
  
✓ Zero Gen0 collections - excellent memory pressure reduction!
```

---

## 🎯 Model Performance Comparison

### Small Model (128 dim, 2 layers, 470K params)

| Metric | Current | Previous | Delta | Change |
|--------|---------|----------|-------|---------|
| **Total Inference Time** | 443.94 ms | 531.64 ms | -87.70 ms | -16.5% ✅ |
| **Token Generation Time** | 443.88 ms | 531.59 ms | -87.71 ms | -16.5% ✅ |
| **Forward Pass Time** | 441.38 ms | 529.00 ms | -87.62 ms | -16.6% ✅ |
| **Tokens per Second** | 56.31 tok/s | 47.04 tok/s | +9.27 tok/s | +19.7% ✅ |
| **Latency per Token** | 17.76 ms | 21.26 ms | -3.50 ms | -16.5% ✅ |
| **Memory per Token** | 0.76 MB | 4.37 MB | -3.61 MB | -82.6% ✅ |
| **Creation Time** | 20.21 ms | 34.51 ms | -14.30 ms | -41.4% ✅ |

**Verdict:** ✅ **Excellent** - Across the board improvements!

### Medium Model (256 dim, 4 layers, 3.45M params)

| Metric | Current | Previous | Delta | Change |
|--------|---------|----------|-------|---------|
| **Total Inference Time** | 1,863.27 ms | 1,201.28 ms | +661.99 ms | +55.1% ⚠️ |
| **Token Generation Time** | 1,863.22 ms | 1,201.18 ms | +662.04 ms | +55.1% ⚠️ |
| **Forward Pass Time** | 1,862.82 ms | 1,200.76 ms | +662.06 ms | +55.1% ⚠️ |
| **Tokens per Second** | 13.42 tok/s | 20.81 tok/s | -7.39 tok/s | -35.5% ⚠️ |
| **Latency per Token** | 74.53 ms | 48.05 ms | +26.48 ms | +55.1% ⚠️ |
| **Memory per Token** | 3.32 MB | 29.20 MB | -25.88 MB | -88.6% ✅ |
| **Creation Time** | 54.53 ms | 84.98 ms | -30.45 ms | -35.8% ✅ |

**Verdict:** ⚠️ **Mixed** - Memory greatly improved, but runtime significantly worse.

### Scaling Analysis

| Metric | Value | Analysis |
|--------|-------|----------|
| **Parameter Ratio** (Medium/Small) | 7.34x | Medium has 7.3× more parameters |
| **Time Ratio** (Current) | 4.20x | Medium is 4.2× slower |
| **Time Ratio** (Previous) | 2.26x | Medium was 2.3× slower |
| **Computational Efficiency** | 1.74x | Non-linear scaling (higher = less efficient) |
| **Memory Efficiency** | 4.37x | Medium uses 4.4× more memory per token |

**Scaling Verdict:** Medium model is scaling worse than before. The 7.3× parameter increase should ideally result in ~7.3× time increase for linear scaling, but we're seeing 4.2×, which is actually better than linear for compute. However, the regression from 2.3× to 4.2× suggests optimization issues.

---

## 📈 Trend Analysis

### Top 10 Performance Improvements

| Method | Previous (ms) | Current (ms) | Improvement | Change % |
|--------|---------------|--------------|-------------|----------|
| 1. `Softmax_2048` | 6.22 | 0.26 | -5.96 ms | -95.8% ✅ |
| 2. `Softmax_Iteration` | 6.36 | 0.44 | -5.92 ms | -93.1% ✅ |
| 3. `TensorAdd_10000` | 10.84 | 2.24 | -8.60 ms | -79.3% ✅ |
| 4. `TensorAdd_Iteration` | 10.83 | 2.23 | -8.60 ms | -79.4% ✅ |
| 5. `Softmax_256` | 7.21 | 2.47 | -4.74 ms | -65.7% ✅ |
| 6. `BroadcastAdd_100x100` | 6.93 | 2.40 | -4.53 ms | -65.4% ✅ |
| 7. `BroadcastAdd_Iteration` | 6.91 | 2.39 | -4.52 ms | -65.4% ✅ |
| 8. `GELU_1000` | 2.28 | 1.02 | -1.26 ms | -55.3% ✅ |
| 9. `Model_Small_Forward` | 529.00 | 441.38 | -87.62 ms | -16.6% ✅ |
| 10. `Model_Small_GenerateToken` | 531.59 | 443.88 | -87.71 ms | -16.5% ✅ |

### Top 10 Performance Regressions

| Method | Previous (ms) | Current (ms) | Regression | Change % |
|--------|---------------|--------------|------------|----------|
| 1. `MatMul_512x512` | 172.11 | 905.90 | +733.79 ms | +426.3% ⚠️ |
| 2. `Model_Medium_Forward` | 1200.76 | 1862.82 | +662.06 ms | +55.1% ⚠️ |
| 3. `Model_Medium_GenerateToken` | 1201.18 | 1863.22 | +662.04 ms | +55.1% ⚠️ |
| 4. `Model_Medium_Inference` | 1201.28 | 1863.27 | +661.99 ms | +55.1% ⚠️ |
| 5. `MatMul_Iteration` | 148.10 | 775.87 | +627.77 ms | +423.9% ⚠️ |
| 6. `GELU_1000000` | 100.60 | 202.40 | +101.80 ms | +101.2% ⚠️ |
| 7. `GELU_Iteration` | 90.44 | 186.08 | +95.64 ms | +105.7% ⚠️ |
| 8. `MatMul_256x256` | 19.59 | 112.93 | +93.34 ms | +476.5% ⚠️ |
| 9. `MatMul_128x128` | 3.54 | 13.29 | +9.75 ms | +275.4% ⚠️ |
| 10. `GELU_100000` | 11.06 | 20.16 | +9.10 ms | +82.3% ⚠️ |

---

## 🔍 Root Cause Analysis

### Why did MatMul regress so heavily?

The MatMul operations show the most severe regression (275-476% slower). Possible causes:

1. **Memory pooling overhead** - The 86.7% memory reduction suggests aggressive pooling was added, which may introduce overhead
2. **Cache locality issues** - Memory layout changes from pooling could harm cache performance
3. **SIMD vectorization changes** - Potential regression in SIMD optimizations
4. **Increased bounds checking** - Safety checks in pooled memory access

### Why did Softmax improve dramatically?

Softmax operations improved by 65-96%. Likely causes:

1. **Algorithm optimization** - Possibly switched to fused Softmax implementation
2. **In-place operations** - Reduced allocations in Softmax path
3. **Better vectorization** - SIMD improvements for Softmax specifically

### Why did Small Model improve but Medium Model regress?

- **Small model:** Benefits from memory optimizations without hitting MatMul overhead as hard (smaller matrices)
- **Medium model:** Dominated by large MatMul operations (512×512) which regressed heavily
- **Scaling issue:** The regression is proportional to model size, suggesting the bottleneck scales with matrix dimensions

---

## 💡 Recommendations

### Critical (Address Immediately)

1. **🔴 Investigate MatMul regression** - 426% slowdown on 512×512 is unacceptable
   - Profile the MatMul implementation changes
   - Check if memory pooling is adding overhead
   - Verify SIMD vectorization is still working
   - Consider reverting recent MatMul changes

2. **🔴 Analyze GELU performance** - 82-106% regression on larger sizes
   - Similar pattern to MatMul suggests related issue
   - May be affected by same pooling/vectorization changes

### High Priority

3. **🟡 Optimize Medium model performance** - 55% regression hurts usability
   - Focus on operations that scale with model size
   - Consider separate optimization path for larger models

4. **🟡 Preserve memory optimizations** - 86.7% reduction is excellent
   - Do NOT lose this gain while fixing runtime
   - Find a balance between speed and memory

### Medium Priority

5. **🟢 Leverage Softmax improvements** - 96% improvement is impressive
   - Document what was done right
   - Apply same techniques to other operations

6. **🟢 Scale Small model optimizations** - 16.5% improvement
   - Identify what works for Small model
   - Apply to Medium model if possible

---

## 📊 Benchmark Data Summary

### System Information
- **OS:** Unix 6.11.0.1018
- **Architecture:** x64
- **CPU Cores:** 4
- **.NET Version:** 10.0.2
- **GC Mode:** Server GC

### Test Configuration
- **Small Model:** 128 embed dim, 2 layers, 4 heads, 64 block size, 256 vocab
- **Medium Model:** 256 embed dim, 4 layers, 8 heads, 128 block size, 512 vocab
- **Token Generation:** 25 tokens per inference
- **Test Runs:** 1 inference per model

### Additional Metrics

| Metric | Value |
|--------|-------|
| **Total Methods Profiled** | 29 |
| **Total Method Calls** | 201 |
| **Average Calls per Method** | 6.93 |
| **Profiling Overhead** | <1% (estimated) |

---

## 📁 Related Reports

- **Full Code Profiler Report:** `enhanced-profile-report.md`
- **Profile Comparison:** `profile-comparison-report.md`
- **Memory Benchmark:** Console output above
- **Allocation Profiler:** Console output above
- **Previous Results:** `benchmark-results-20260204-011935/`

---

## ✅ Conclusion

**Overall Assessment:** ⚠️ **MIXED RESULTS**

**Strengths:**
- ✅ Exceptional memory optimization (-86.7% allocations)
- ✅ Small model improved across all metrics
- ✅ Softmax operations dramatically improved
- ✅ Zero-allocation fused operations working well

**Concerns:**
- ⚠️ Severe MatMul regression (426% slower on 512×512)
- ⚠️ Medium model inference 55% slower
- ⚠️ GELU operations regressed
- ⚠️ Overall runtime increased 56%

**Next Steps:**
1. Investigate and fix MatMul performance regression
2. Apply lessons from Softmax improvements to other operations
3. Balance memory optimization with runtime performance
4. Re-run benchmarks after fixes to verify improvements

---

**Report End** | Generated: 2026-02-04 02:02:49

# SmallMind Profiling Metrics Summary
**Generated:** 2026-02-04 02:36:44  
**Baseline:** 2026-02-03 23:06:46

## 📊 Executive Summary

This report presents a comprehensive analysis of SmallMind's performance using both **CodeProfiler** and **AllocationProfiler** (memory profiler). The results compare current performance against the established baseline from February 3rd, 2026.

### Key Highlights

| Metric | Baseline | Current | Delta | Status |
|--------|----------|---------|-------|--------|
| **Total Runtime** | 5927.60 ms | 9277.78 ms | **+56.5%** | 🔴 Regression |
| **Total Allocations** | 2550.03 MB | 338.71 MB | **-86.7%** | 🟢 Improvement |
| **Memory Pressure (GC Gen0)** | 0 | 0 | No change | 🟢 Excellent |
| **Memory Efficiency** | 48.0% reduction | 47.1% reduction | -0.9% | ⚠️ Slight degradation |
| **Training Throughput** | 43,092 samples/sec | 10,861 samples/sec | **-74.8%** | 🔴 Significant regression |

---

## 🔍 Detailed Analysis

### 1. CodeProfiler Results

#### Performance Overview

The CodeProfiler enhanced mode profiled **29 unique methods** across three performance tiers:
- **Low-level SIMD operations** (MatMul, GELU, Softmax)
- **Mid-level tensor operations** (Add, Multiply, Broadcast)
- **High-level transformer inference** (Small and Medium models)

#### Top Performance Issues 🔴

The most significant **regressions** identified:

1. **Medium Model Inference** (+82.0%)
   - Previous: 1201.28 ms → Current: 2186.63 ms
   - Impact: Nearly doubled inference time for larger models
   - Root Cause: Likely related to MatMul performance degradation

2. **MatMul Operations** (+161-465%)
   - MatMul_512x512: +161.0% (172ms → 449ms)
   - MatMul_256x256: +188.5% (19ms → 56ms)
   - MatMul_128x128: +465.0% (3.5ms → 20ms)
   - **Critical**: Matrix multiplication is the core computational kernel
   - **Analysis**: SIMD optimizations may have regressed or cache behavior changed

3. **GELU Activation** (+62-76%)
   - GELU_1000000: +62.1% (100ms → 163ms)
   - Impact: Affects every transformer layer forward pass

#### Top Performance Wins 🟢

Notable **improvements**:

1. **Small Model Inference** (-19.5%)
   - Previous: 531.64 ms → Current: 427.71 ms
   - Benefit: 20% faster inference for smaller models
   - Tokens/second: 47.0 → 58.5 (+24.5% throughput)

2. **Softmax Operations** (-81-96%)
   - Softmax_2048: -96.3% (6.2ms → 0.23ms)
   - Softmax_256: -81.3% (7.2ms → 1.35ms)
   - **Excellent**: Major optimization success

3. **Tensor Addition** (-83%)
   - TensorAdd operations: 10.8ms → 1.8ms
   - Likely benefiting from SIMD or improved cache locality

#### Model Scaling Analysis

| Model Size | Parameters | Inference (ms) | Tokens/sec | Memory/Token (MB) |
|------------|-----------|----------------|------------|-------------------|
| **Small** (128d, 2L) | 470,528 | 427.71 | 58.45 | 0.76 |
| **Medium** (256d, 4L) | 3,454,464 | 2186.63 | 11.43 | 3.32 |

- **Parameter Ratio**: 7.3x more parameters in Medium
- **Time Ratio**: 5.11x longer inference time
- **Computational Efficiency**: 1.43x (acceptable, but MatMul regression is concerning)

---

### 2. AllocationProfiler (Memory Profiler) Results

#### MatMul Backward Pass Memory Profile

**Test Configuration:**
- Matrix dimensions: 128×256 @ 256×128
- Iterations: 100
- ArrayPool optimization enabled

**Results:**

| Metric | Baseline | Current | Delta |
|--------|----------|---------|-------|
| **Total Time** | 405 ms | 894 ms | +120.7% |
| **Avg Time/Iter** | 4.057 ms | 8.943 ms | +120.4% |
| **Total Allocations** | 12.99 MB | 13.23 MB | +1.8% |
| **Alloc/Iteration** | 133.02 KB | 135.49 KB | +1.9% |
| **Gen0 Collections** | 0 | 0 | Stable |
| **Estimated Reduction** | 48.0% | 47.1% | -0.9% |

**Analysis:**
- ⚠️ **Time regression aligns with CodeProfiler findings** - MatMul operations are slower
- ✅ **Memory allocations remain low** - ArrayPool is working effectively
- ✅ **Zero GC collections** - No memory pressure introduced
- ⚠️ **Reduction efficiency slightly lower** - More allocations per iteration

**Expected vs Actual:**
- Without pooling: 25.00 MB
- With pooling: 13.23 MB
- **Reduction**: 47.1% (target: >80%)

#### Training Workload Memory Profile

**Test Configuration:**
- Mini training loop simulation
- Steps: 50
- Batch size: 32, Hidden size: 256
- 2 MatMuls per step (forward pass)

**Results:**

| Metric | Baseline | Current | Delta |
|--------|----------|---------|-------|
| **Total Time** | 37 ms | 147 ms | +297.3% |
| **Avg Time/Step** | 0.743 ms | 2.946 ms | +296.5% |
| **Total Allocations** | 3.75 MB | 3.78 MB | +0.8% |
| **Alloc/Step** | 76.70 KB | 77.43 KB | +1.0% |
| **Gen0 Collections** | 0 | 0 | Stable |
| **Throughput** | 43,092 samples/sec | 10,861 samples/sec | **-74.8%** |
| **Estimated Reduction** | 94.0% | 94.0% | Stable |

**Analysis:**
- 🔴 **Critical throughput regression** - 4x slower training performance
- ✅ **Memory allocations remain excellent** - 94% reduction maintained
- ✅ **Zero GC collections** - Memory optimization is intact
- 🔴 **Time per step increased 4x** - Confirms MatMul performance issue

**Expected vs Actual:**
- Without pooling: 62.50 MB
- With pooling: 3.78 MB
- **Reduction**: 94.0% ✓ Excellent

---

## 🎯 Root Cause Analysis

### Primary Issue: MatMul Performance Regression

The data points to a **systemic MatMul performance degradation**:

1. **Evidence Across Tools:**
   - CodeProfiler: MatMul operations 161-465% slower
   - AllocationProfiler: MatMul backward pass 120% slower
   - Training workload: 4x slower overall

2. **Consistent Pattern:**
   - Small matrices affected most (128x128: +465%)
   - Medium matrices moderately affected (256x256: +188%)
   - Large matrices less affected but still regressed (512x512: +161%)
   - **Hypothesis**: Cache behavior or SIMD vectorization may be impaired

3. **Downstream Impact:**
   - Medium model inference: +82% slower (MatMul-heavy)
   - Small model inference: -19.5% faster (possibly different code path)
   - GELU activation: +62% slower (also uses SIMD)

### Memory Optimization Status: STABLE ✅

Despite performance regression, memory optimizations remain effective:
- ArrayPool is working correctly
- Zero GC collections under load
- 47-94% allocation reduction maintained
- No memory leaks or pressure increases

---

## 📈 Performance Comparison Visualizations

### Inference Time by Model Size

```
Small Model (470K params):
Baseline: ████████████████████ 531.64 ms
Current:  ████████████████░░░░ 427.71 ms (-19.5%) ✅

Medium Model (3.45M params):
Baseline: ████████████████████ 1201.28 ms
Current:  ████████████████████████████████████ 2186.63 ms (+82.0%) 🔴
```

### MatMul Performance (GFLOPS)

Current GFLOPS calculations (lower is slower):

| Matrix Size | GFLOPS | Expected | Status |
|-------------|--------|----------|--------|
| 128×128 | **0.21** | ~1.0 | 🔴 Critical |
| 256×256 | **0.59** | ~2.0 | 🔴 Poor |
| 512×512 | **0.60** | ~4.0 | 🔴 Poor |

**Target**: Modern CPUs should achieve 1-10 GFLOPS for matrix multiplication

### Memory Allocations (Total)

```
Baseline: ██████████████████████████████████████████████████ 2550.03 MB
Current:  ███████░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░  338.71 MB (-86.7%) ✅
```

**Massive improvement** in total allocations across all operations

---

## 🔧 Recommendations

### Immediate Actions (High Priority)

1. **Investigate MatMul Regression** 🔴
   - Review recent changes to Tensor.MatMul implementation
   - Check SIMD vectorization is enabled and functioning
   - Verify cache-friendly loop ordering (ikj vs ijk)
   - Profile CPU cache misses and memory bandwidth

2. **Profile SIMD Code Paths** 🔴
   - Use BenchmarkDotNet for micro-benchmarking MatMul
   - Verify `Vector<float>` operations are being used
   - Check for unintended boxing or memory allocations
   - Validate loop unrolling and compiler optimizations

3. **Bisect Performance Regression** 🔴
   - Use git bisect to find commit that introduced slowdown
   - Compare assembly output (Release vs Debug builds)
   - Check for changes to matrix layout or data structures

### Medium-Term Improvements

4. **Optimize Medium Model Path**
   - Current performance is 82% slower - unacceptable for production
   - Consider implementing blocked/tiled matrix multiplication
   - Explore parallel matrix operations for larger models

5. **Validate ArrayPool Efficiency**
   - 47.1% reduction is below 80% target for MatMul backward pass
   - Investigate why some allocations are not being pooled
   - Check buffer sizes align with pool bucket sizes

### Long-Term Optimizations

6. **Enhance MatMul Implementation**
   - Implement cache-friendly tiling (32x32 or 64x64 blocks)
   - Add multi-threading for large matrices
   - Consider using MKL or OpenBLAS interop for production

7. **Build on Softmax Success**
   - Softmax achieved 81-96% improvement - apply same techniques elsewhere
   - Document what made Softmax optimization successful
   - Apply similar SIMD patterns to other activations

---

## 📋 Summary Table

### Performance Metrics Comparison

| Component | Metric | Baseline | Current | Change | Verdict |
|-----------|--------|----------|---------|--------|---------|
| **CodeProfiler** | Total Runtime | 5927.60 ms | 9277.78 ms | +56.5% | 🔴 Regressed |
| **CodeProfiler** | Small Model Inference | 531.64 ms | 427.71 ms | -19.5% | 🟢 Improved |
| **CodeProfiler** | Medium Model Inference | 1201.28 ms | 2186.63 ms | +82.0% | 🔴 Critical |
| **CodeProfiler** | MatMul 512×512 | 172.11 ms | 449.13 ms | +161.0% | 🔴 Critical |
| **CodeProfiler** | Softmax Operations | 6-7 ms | 0.2-1.3 ms | -81 to -96% | 🟢 Excellent |
| **Memory Profiler** | MatMul Backward Time | 405 ms | 894 ms | +120.7% | 🔴 Regressed |
| **Memory Profiler** | MatMul Allocations | 12.99 MB | 13.23 MB | +1.8% | 🟡 Stable |
| **Memory Profiler** | Training Throughput | 43,092 samp/s | 10,861 samp/s | -74.8% | 🔴 Critical |
| **Memory Profiler** | Training Allocations | 3.75 MB | 3.78 MB | +0.8% | 🟢 Excellent |
| **Overall** | Total Allocations | 2550.03 MB | 338.71 MB | -86.7% | 🟢 Excellent |
| **Overall** | GC Gen0 Collections | 0 | 0 | Stable | 🟢 Perfect |

---

## 🎓 Lessons Learned

### What's Working Well ✅

1. **Memory Optimization Pipeline**
   - ArrayPool integration is effective
   - Zero GC pressure maintained across all workloads
   - 86.7% reduction in total allocations

2. **Softmax Optimization**
   - 81-96% performance improvement
   - Demonstrates successful SIMD and algorithmic optimization

3. **Small Model Performance**
   - 19.5% faster inference
   - 24.5% higher throughput (tokens/second)

### What Needs Attention 🔴

1. **MatMul Performance Crisis**
   - Core computational kernel has regressed significantly
   - Affects all model sizes but especially larger models
   - Training throughput down 75%

2. **Model Scaling**
   - Medium model performance unacceptable for production use
   - Need to investigate why small model improved but medium regressed

3. **Allocation Pooling Efficiency**
   - MatMul backward pass only achieving 47% reduction
   - Target is 80%+ - need to investigate buffer sizing

---

## 📞 Next Steps

**Immediate (Today):**
1. Run git bisect to find MatMul regression commit
2. Compare MatMul assembly output between baseline and current
3. Profile MatMul with CPU performance counters (cache misses, memory bandwidth)

**Short-term (This Week):**
1. Implement MatMul fixes based on profiling data
2. Re-run both profilers to validate improvements
3. Add automated performance regression tests to CI/CD

**Long-term (This Month):**
1. Implement tiled MatMul for better cache utilization
2. Add multi-threading support for large matrix operations
3. Create performance dashboard to track metrics over time

---

## 📚 Appendix

### Test Environment

- **Runtime:** .NET 10.0.2
- **OS:** Unix 6.11.0.1018
- **CPU Cores:** 4
- **GC Mode:** Server (assumed)

### Profiling Tools Used

1. **CodeProfiler** (`tools/CodeProfiler/CodeProfiler.csproj`)
   - Mode: Enhanced (`--enhanced`)
   - Profiles: SIMD operations, tensor ops, transformer inference
   - Output: `enhanced-profile-report.md`

2. **AllocationProfiler** (`benchmarks/AllocationProfiler/AllocationProfiler.csproj`)
   - Tests: MatMul backward pass, training workload
   - Focus: Memory allocations, GC pressure, ArrayPool effectiveness
   - Output: Console log

3. **ProfileComparator** (built into CodeProfiler)
   - Compares baseline vs current metrics
   - Generates: `current-vs-baseline-comparison.md`

### Data Sources

- **Baseline Report:** `benchmark-results-20260204-005926/enhanced-profile-report.md`
- **Current Report:** `enhanced-profile-report.md`
- **Comparison:** `current-vs-baseline-comparison.md`
- **Memory Profiler:** Console output captured in logs

---

**Report Generated by SmallMind Profiling Pipeline**  
*For questions or issues, refer to tools/CodeProfiler/README.md*

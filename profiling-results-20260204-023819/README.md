# Profiling Results - February 4, 2026 02:38 UTC

This directory contains the complete profiling results from running CodeProfiler and AllocationProfiler (memory profiler) on SmallMind.

## 📁 Files in This Directory

### Primary Reports

1. **PROFILING_METRICS_SUMMARY.md** ⭐
   - Comprehensive summary and analysis
   - Compares current results to baseline (Feb 3, 2026)
   - Executive summary, detailed analysis, recommendations
   - **Start here for overview**

2. **enhanced-profile-report.md**
   - CodeProfiler enhanced mode output
   - 29 methods profiled across SIMD ops, tensor ops, and transformer inference
   - Hot paths, memory allocations, performance metrics

3. **current-vs-baseline-comparison.md**
   - Side-by-side comparison with previous run
   - Performance improvements and regressions
   - Model scaling analysis
   - SIMD operation GFLOPS

### Supporting Files

4. **allocation-profile.txt**
   - AllocationProfiler memory profiler output
   - MatMul backward pass allocation analysis
   - Training workload memory metrics
   - ArrayPool effectiveness measurements

5. **codeprofiler-output.log**
   - Complete console output from CodeProfiler run
   - Build warnings and execution details
   - Full profiling progress log

## 🎯 Key Findings

### Performance Summary

| Metric | Baseline | Current | Change |
|--------|----------|---------|--------|
| Total Runtime | 5927.60 ms | 9277.78 ms | **+56.5% slower** 🔴 |
| Total Allocations | 2550.03 MB | 338.71 MB | **-86.7% less** 🟢 |
| Training Throughput | 43,092 samp/s | 10,861 samp/s | **-74.8% slower** 🔴 |

### Critical Issues Identified

1. **MatMul Performance Regression** (161-465% slower)
   - Core computational kernel has significantly degraded
   - Affects all model sizes, especially larger models
   - Needs immediate investigation

2. **Medium Model Inference** (+82% slower)
   - 1201ms → 2186ms
   - Unacceptable for production use

### Major Wins

1. **Memory Allocations** (-86.7%)
   - Excellent ArrayPool optimization results
   - Zero GC collections maintained

2. **Softmax Operations** (-81 to -96%)
   - Outstanding optimization success

3. **Small Model Inference** (-19.5%)
   - Faster and more efficient

## 🔧 How These Results Were Generated

### CodeProfiler (Enhanced Mode)

```bash
cd /home/runner/work/SmallMind/SmallMind
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- --enhanced
```

**What it profiles:**
- Low-level SIMD operations (MatMul, GELU, Softmax)
- Mid-level tensor operations (Add, Multiply, Broadcast)
- High-level transformer inference (Small and Medium models)

### AllocationProfiler (Memory Profiler)

```bash
cd /home/runner/work/SmallMind/SmallMind
dotnet run --project benchmarks/AllocationProfiler/AllocationProfiler.csproj
```

**What it measures:**
- MatMul backward pass allocations
- Training workload memory pressure
- ArrayPool effectiveness
- GC collection counts

### Comparison Generation

```bash
cd /home/runner/work/SmallMind/SmallMind
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- --compare \
  benchmark-results-20260204-005926/enhanced-profile-report.md \
  enhanced-profile-report.md \
  current-vs-baseline-comparison.md
```

## 📊 Baseline Information

- **Baseline Date:** 2026-02-03 23:06:46
- **Baseline Directory:** `benchmark-results-20260204-005926/`
- **Comparison Span:** ~3.5 hours

## 🔍 Next Steps

Based on the findings in PROFILING_METRICS_SUMMARY.md:

1. **Immediate:** Investigate MatMul regression using git bisect
2. **Short-term:** Fix MatMul implementation and re-profile
3. **Long-term:** Implement tiled MatMul and multi-threading

## 📚 Additional Resources

- **CodeProfiler Documentation:** `tools/CodeProfiler/README.md`
- **Profiling Guides:** `docs/profiling/`
- **Previous Results:** `benchmark-results-*` directories

---

**Generated:** 2026-02-04 02:38:19 UTC  
**Environment:** .NET 10.0.2, Unix 6.11.0.1018, 4 cores

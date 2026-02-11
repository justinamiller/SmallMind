# 📊 GFLOPS Benchmark Results Summary

## Current Branch Baseline Performance

**System:** Ubuntu 24.04.3, 4 cores, AVX2+FMA, .NET 10.0.2  
**Branch:** copilot/add-performance-test-benchmarks (baseline)

---

## 🎯 Key Metrics

| Metric | Value | Target | Status |
|--------|-------|--------|--------|
| **Peak GFLOPS** | 59.99 | 60+ | 🟡 Close |
| **Avg GFLOPS** | 33.56 | 40+ | 🟡 Below |
| **M=1 GFLOPS** | 27.77 | 30+ | 🟡 Close |
| **Zero-Alloc Tests** | 0/9 | All | 🔴 None |
| **Memory Pressure** | High | Low | 🔴 High |

---

## 📈 Performance by Matrix Size

```
┌─────────────┬──────────┬─────────────┬──────────────────┐
│ Size        │ GFLOPS   │ Time/Op     │ Alloc/Op         │
├─────────────┼──────────┼─────────────┼──────────────────┤
│ 64×64       │   16.80  │   0.031 ms  │    56 bytes  ⚠️  │
│ 128×128     │   17.75  │   0.236 ms  │ 1,732 bytes  ⚠️  │
│ 256×256     │ 🏆 59.99  │   0.559 ms  │ 1,725 bytes  ⚠️  │
│ 512×512     │   56.19  │   4.777 ms  │ 1,791 bytes  ⚠️  │
│ 1024×1024   │   39.88  │  53.846 ms  │ 1,809 bytes  ⚠️  │
│ 2048×2048   │   36.51  │ 470.520 ms  │ 1,876 bytes  ⚠️  │
└─────────────┴──────────┴─────────────┴──────────────────┘
```

**Peak Performance:** 59.99 GFLOPS at 256×256

---

## 🤖 LLM Workload Performance

```
┌──────────────────────┬─────────────┬──────────┬─────────────┬─────────────┐
│ Workload             │ Size        │ GFLOPS   │ Time/Op     │ Importance  │
├──────────────────────┼─────────────┼──────────┼─────────────┼─────────────┤
│ Single Token Decode  │ 1×512×512   │ ⭐ 27.77 │   0.019 ms  │ CRITICAL ⚡ │
│ Small Batch          │ 32×512×512  │   28.41  │   0.590 ms  │ Important   │
│ Prefill (256 tokens) │ 256×4096²   │   17.70  │ 485.225 ms  │ Important   │
└──────────────────────┴─────────────┴──────────┴─────────────┴─────────────┘
```

**Critical Metric (M=1):** 27.77 GFLOPS for inference

---

## 🔍 Analysis

### ✅ Strengths
- Peak GFLOPS of **59.99** (close to 60+ target)
- Decent M=1 performance (**27.77 GFLOPS**)
- Consistent 40-60 GFLOPS on medium matrices

### ⚠️ Issues Identified
- **Memory allocations on every operation** (56-1876 bytes/op)
- **GC pressure** in all tests
- **Small matrix underperformance** (16-17 GFLOPS on 64×64, 128×128)
- **Large matrix degradation** (36 GFLOPS on 2048×2048)

---

## 🎯 How PRs Should Improve This

### PR #192: GemmMicrokernels Routing
**Expected Improvements:**
- ✅ **Zero allocations** (0 bytes/op) ← Fixes all memory warnings
- ✅ **60+ GFLOPS on 128×128** ← Improves small matrices
- ✅ **2x+ prefill speedup** ← Better large workloads

**Potential Trade-off:**
- ⚠️ M=1 might regress (6.6→2.3 GFLOPS)

### PR #193: A-Indexing Bug Fix
**Expected Improvements:**
- ✅ **60+ GFLOPS on 128×128, 256×256** ← Already good on 256
- ✅ **6.5x speedup on small matrices** ← Big win for 64×64, 128×128
- ✅ **Bug fix** ← Correctness improvement

**Potential Trade-off:**
- ⚠️ May still have allocations

---

## 📊 Comparison Chart

```
GFLOPS Performance by Size:

 60+ │              ████
     │         ████ ████
 50  │         ████ ████
     │         ████ ████
 40  │         ████ ████ ████
     │         ████ ████ ████ ████
 30  │         ████ ████ ████ ████
     │         ████ ████ ████ ████ ████
 20  │    ████ ████ ████ ████ ████ ████
     │    ████ ████ ████ ████ ████ ████
 10  │    ████ ████ ████ ████ ████ ████
     │    ████ ████ ████ ████ ████ ████
  0  └────┴────┴────┴────┴────┴────┴────
      64   128  256  512  1024 2048
```

---

## 🚀 Next Steps

1. **Run PR #192 benchmark** → Compare zero-alloc improvements
2. **Run PR #193 benchmark** → Compare GFLOPS improvements
3. **Analyze trade-offs:**
   - Speed vs Memory
   - Peak vs Average
   - Inference (M=1) vs Prefill

---

## 📁 Detailed Results

See `BENCHMARK_RESULTS_BASELINE.md` for complete analysis.

---

**Generated:** 2026-02-11  
**Benchmark:** GFLOPSComparisonBenchmark v1.0  
**Status:** ⚠️ Baseline captured - PRs not yet tested

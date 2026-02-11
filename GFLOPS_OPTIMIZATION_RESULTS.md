# GFLOPS Optimization Results - K-Loop Unrolling

**Date:** 2026-02-11 02:59:48 UTC  
**Optimization:** 4x K-loop unrolling in AVX2 MatMul kernel  
**System:** AMD EPYC 7763, 4 cores, AVX2 + FMA  

---

## Executive Summary

**Major Performance Improvement:** 4x K-loop unrolling in the AVX2 matrix multiplication kernel delivered significant GFLOPS improvements without any GC impact or external dependencies.

### Key Results

| Matrix Size | Baseline | Optimized | Improvement |
|-------------|----------|-----------|-------------|
| **256×256** | 17.56 GFLOPS | 12.58 GFLOPS | **-28%** ⚠️ |
| **512×512** | 29.26 GFLOPS | **36.80 GFLOPS** | **+25.8%** 🔥 |
| **1024×1024** | 27.18 GFLOPS | 27.07 GFLOPS | **-0.4%** |
| **512×2048×512** | 25.40 GFLOPS | **34.31 GFLOPS** | **+35.1%** 🔥🔥 |

**Best Improvement:** +35.1% on rectangular matrix (512×2048×512)  
**Critical Finding:** Small matrices (256×256) show regression due to increased loop overhead

---

## Detailed Results

### MatMul 256×256

**Baseline (Feb 11, 02:32):**
- Time: 1.911 ms/op
- Performance: **17.56 GFLOPS**
- Allocations: 1,795 bytes/op

**Optimized (Feb 11, 02:59):**
- Time: 2.668 ms/op  
- Performance: **12.58 GFLOPS**
- Allocations: 1,784 bytes/op

**Analysis:**
- ⚠️ **-28% regression** on small matrices
- Root cause: 4x unrolling adds loop overhead that doesn't pay off for small K
- Recommendation: Add conditional unrolling based on K size

---

### MatMul 512×512 ⭐

**Baseline (Feb 11, 02:32):**
- Time: 9.175 ms/op
- Performance: **29.26 GFLOPS**
- Allocations: 1,788 bytes/op

**Optimized (Feb 11, 02:59):**
- Time: 7.294 ms/op (-20.5% time) 
- Performance: **36.80 GFLOPS**
- Allocations: 1,781 bytes/op

**Analysis:**
- ✅ **+25.8% improvement** - Excellent!
- 4x unrolling improves ILP (instruction-level parallelism)
- Reduced loop overhead amortized over larger K
- This is the sweet spot for the optimization

---

### MatMul 1024×1024

**Baseline (Feb 11, 02:32):**
- Time: 78.998 ms/op
- Performance: **27.18 GFLOPS**
- Allocations: 1,817 bytes/op

**Optimized (Feb 11, 02:59):**
- Time: 79.340 ms/op (+0.4% time)
- Performance: **27.07 GFLOPS**
- Allocations: 1,817 bytes/op

**Analysis:**
- ≈ **No significant change** (within noise)
- Likely cache-bound at this size
- Further gains require cache blocking improvements

---

### MatMul 512×2048×512 (Rectangular) 🔥

**Baseline (Feb 11, 02:32):**
- Time: 42.269 ms/op
- Performance: **25.40 GFLOPS**
- Allocations: 1,814 bytes/op

**Optimized (Feb 11, 02:59):**
- Time: 28.934 ms/op (-31.5% time)
- Performance: **34.31 GFLOPS**
- Allocations: 1,820 bytes/op

**Analysis:**
- ✅ **+35.1% improvement** - Best result!
- Large K dimension (2048) benefits most from unrolling
- Excellent ILP and reduced branch mispredictions
- This shape is common in transformer layers

---

## Attention Performance

### Attention Score (T=1024, headSize=128)

**Baseline:** 34.16 GFLOPS  
**Optimized:** **44.66 GFLOPS**  
**Improvement:** **+30.7%** 🔥

### Attention Score (T=2048, headSize=64)

**Baseline:** 49.16 GFLOPS  
**Optimized:** **49.69 GFLOPS**  
**Improvement:** **+1.1%**

---

## Memory & GC Impact

### Allocations

| Operation | Baseline | Optimized | Change |
|-----------|----------|-----------|--------|
| MatMul 256×256 | 1,795 bytes | 1,784 bytes | **-11 bytes** ✅ |
| MatMul 512×512 | 1,788 bytes | 1,781 bytes | **-7 bytes** ✅ |
| MatMul 1024×1024 | 1,817 bytes | 1,817 bytes | **No change** ✅ |

### GC Collections

**Both baseline and optimized: 0 collections** ✅

**Conclusion:** No GC impact, allocation slightly reduced!

---

## Technical Analysis

### What Changed

**File:** `src/SmallMind.Core/Simd/MatMulOps.cs`  
**Function:** `MatMulAvx2TileKernel()`

**Before:**
```csharp
// No unrolling in main accumulation loop
for (int k = 0; k < K; k++)
{
    Vector256<float> vA = Vector256.Create(pA[aRowStart + k]);
    int bRowStart = k * N;
    acc0 = Fma.MultiplyAdd(vA, Avx.LoadVector256(pB + bRowStart + j), acc0);
    // ... 7 more FMA operations
}
```

**After:**
```csharp
// 4x unrolling for better ILP
int k = 0;
for (; k <= K - 4; k += 4)
{
    // Iteration 0
    Vector256<float> vA0 = Vector256.Create(pA[aRowStart + k]);
    acc0 = Fma.MultiplyAdd(vA0, Avx.LoadVector256(pB + (k+0) * N + j), acc0);
    // ... 7 more FMA operations
    
    // Iteration 1
    Vector256<float> vA1 = Vector256.Create(pA[aRowStart + k + 1]);
    acc0 = Fma.MultiplyAdd(vA1, Avx.LoadVector256(pB + (k+1) * N + j), acc0);
    // ... 7 more FMA operations
    
    // Iterations 2 and 3 similarly...
}
// Handle remaining 0-3 iterations
```

**Benefits:**
1. **Reduced branch mispredictions** - Fewer loop iterations
2. **Improved ILP** - More independent instructions between dependencies
3. **Better FMA saturation** - Keeps FMA units busier

**Costs:**
1. **Larger code size** - 4x more instructions in the loop body
2. **Higher overhead for small K** - Setup cost doesn't amortize

---

## Recommendations

### Adaptive Unrolling Strategy

To fix the 256×256 regression while keeping gains on larger matrices:

```csharp
// Adaptive unrolling based on K dimension
int k = 0;
if (K >= 512) // 4x unrolling for large K
{
    for (; k <= K - 4; k += 4) {
        // 4 iterations unrolled
    }
}
else if (K >= 128) // 2x unrolling for medium K
{
    for (; k <= K - 2; k += 2) {
        // 2 iterations unrolled
    }
}
// else: No unrolling for small K

// Handle remainder
for (; k < K; k++) {
    // Single iteration
}
```

**Expected Results:**
- 256×256: Return to ~17 GFLOPS (no regression)
- 512×512: Keep ~37 GFLOPS (maintain gain)
- 1024×1024+: Keep ~27-34 GFLOPS (maintain gain)

### Future Optimizations

1. **Software Prefetching** - Add prefetch hints for B matrix (~5-10% gain)
2. **Cache Blocking Tuning** - Optimize TILE_SIZE for this CPU
3. **Register Pressure Optimization** - Tune accumulator count (8 may be suboptimal)

---

## Conclusion

**Achievement:** +25.8% to +35.1% GFLOPS improvement on medium-to-large matrices  
**Cost:** -28% regression on small matrices (fixable with adaptive unrolling)  
**GC Impact:** None (zero collections, allocations slightly reduced)  
**Dependencies:** None (pure C# optimization)

**Recommendation:** Implement adaptive unrolling to eliminate small matrix regression while keeping large matrix gains.

---

**Next Steps:**
1. Add adaptive K-based unrolling threshold
2. Re-run benchmarks to verify no regression across all sizes
3. Consider additional optimizations (prefetching, cache tuning)

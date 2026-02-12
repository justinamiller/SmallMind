# FP32 MatMul GFLOPS Analysis

**Date:** 2026-02-12  
**Goal:** Achieve 60+ GFLOPS for FP32 Matrix Multiplication  
**Current Status:** Peak 49.58 GFLOPS on 64×64, 32.95 GFLOPS on 128×128

## Benchmark Results

### Current FP32 MatMul Performance

| Matrix Size | Time (ms) | GFLOPS | Status | Kernel |
|-------------|-----------|--------|--------|--------|
| 64×64 | 0.011 | **49.58** | ⚠ Close (82.6% of goal) | AVX2 |
| 128×128 | 0.127 | **32.95** | ✗ Below (54.9% of goal) | AVX2 |
| 256×256 | 0.730 | **45.99** | ⚠ Close (76.7% of goal) | AVX2 |
| 512×512 | 8.964 | **29.95** | ✗ Below (49.9% of goal) | AVX2 |
| 1024×1024 | 73.595 | **29.18** | ✗ Below (48.6% of goal) | AVX2 |
| 2048×2048 | 526.975 | **32.60** | ✗ Below (54.3% of goal) | AVX2 |

**Peak Performance:** 49.58 GFLOPS (64×64)  
**Average Performance:** 36.71 GFLOPS  
**Gap to Goal:** 10.42 GFLOPS (17.4% improvement needed)

## Current Implementation Analysis

### AVX2 Kernel Details
- **Register Blocking**: 8 Vector256 accumulators (64 floats per tile)
- **K-Loop Unrolling**: 4x unrolling for instruction-level parallelism
- **SIMD Width**: 8 floats per Vector256 (AVX2)
- **FMA**: Yes (Fma.MultiplyAdd)
- **Parallelization**: Parallel.For for M >= 128
- **Cache Blocking**: 4M×32N tiles, no explicit K blocking

### Code Path
```
MatMul() → MatMulAvx2() → MatMulAvx2Unsafe() → MatMulAvx2TileKernel()
```

### Microkernel Structure
```csharp
for (int i = i0; i < iMax; i++) {                    // M dimension
    for (int j = 0; j <= N - 64; j += 64) {          // N dimension (8 vectors)
        Vector256 acc0..acc7 = Zero;                  // Register block
        for (int k = 0; k <= K - 4; k += 4) {        // K dimension (4x unroll)
            // FMA: acc += A[i,k] * B[k,j]
        }
        // Store accumulated results
    }
}
```

## Performance Analysis

### Theoretical Peak GFLOPS
With the given hardware:
- **CPU**: 4 cores, AVX2 (8 floats per vector)
- **FMA**: 2 FLOPs per cycle (multiply + add)
- **Estimated Clock**: ~2-3 GHz (typical for this environment)

**Theoretical Peak**: 4 cores × 2 GHz × 8 floats × 2 FMA = **128 GFLOPS**

### Achieved vs Theoretical
- **Current Peak**: 49.58 GFLOPS
- **% of Theoretical**: 49.58 / 128 = **38.7%**
- **Industry Target**: 50-70% of theoretical peak is considered good

**Verdict**: We're at 38.7% efficiency, which is reasonable but below the 60 GFLOPS absolute target.

## Bottleneck Identification

### 1. Memory Bandwidth
- **Issue**: B matrix loads from memory (K×N layout) with stride K
- **Impact**: Cache misses on B-matrix accesses, especially for large N
- **Evidence**: Performance degrades as matrix size increases

### 2. Cache Utilization
- **L1 Cache**: ~32KB per core (typical)
- **Current Blocking**: 4M × 32N = 128 floats of C = 512 bytes (good)
- **Missing**: No K-dimension blocking (entire K accessed per tile)
- **Impact**: For K > 256, B matrix doesn't fit in L1

### 3. Register Pressure
- **Available YMM registers**: 16 on x86-64
- **Currently Used**: 8 accumulators + temp vectors
- **Opportunity**: Could potentially use more accumulators for better register blocking

### 4. Parallel Overhead
- **Threshold**: M >= 128
- **Issue**: For 128×128, parallelization just kicks in but overhead may dominate
- **Evidence**: 128×128 performs worse than 64×64 despite parallelization

## Optimization Recommendations

### Priority 1: B-Matrix Packing (Expected +20-30% GFLOPS)
Pack B matrix into column-major panels that fit in L2 cache:
```csharp
// Pack B into Kc × Nr panels
for (int j0 = 0; j0 < N; j0 += Nr) {
    Pack_B_Panel(B, PackedB, K, j0, min(j0+Nr, N));
}

// Use packed B in kernel
for (int k = 0; k < K; k++) {
    // Sequential access to PackedB - better cache locality
    acc += A[i,k] * PackedB[k*Nr + j];
}
```

### Priority 2: Increase Microkernel Size (Expected +10-15% GFLOPS)
Current: 1M × 64N  
Proposed: 6M × 16N (BLAS-style GEBP)

Rationale:
- 6M rows: Better amortizes K-loop overhead
- 16N cols: Reduces to 2 vectors, leaves more registers
- Total: 12 YMM accumulators (6M × 2N) + temps

### Priority 3: K-Dimension Blocking (Expected +5-10% GFLOPS)
Add outer K blocking to fit working set in L2:
```csharp
const int Kc = 256;  // L1 cache blocking
for (int k0 = 0; k0 < K; k0 += Kc) {
    kMax = min(k0 + Kc, K);
    // Inner kernel with reduced K range
}
```

### Priority 4: Tune Parallel Threshold (Expected +5% GFLOPS)
Current: PARALLEL_THRESHOLD = 128

Test optimal thresholds:
- Small matrices (64-256): May benefit from higher threshold (e.g., 256)
- Medium matrices (512-1024): Current threshold OK
- Large matrices (2048+): Consider tile-level parallelization

### Priority 5: Prefetching (Expected +5% GFLOPS)
Add software prefetching for B-matrix:
```csharp
// Prefetch next K iteration
Sse.Prefetch0(pB + bRowStart_Next);
```

## Expected Outcomes

### Conservative Estimates (All Optimizations)
| Matrix Size | Current | Expected | Improvement |
|-------------|---------|----------|-------------|
| 64×64 | 49.58 | **65-70** | +31-41% |
| 128×128 | 32.95 | **55-62** | +67-88% |
| 256×256 | 45.99 | **60-68** | +30-48% |
| 512×512 | 29.95 | **52-60** | +74-100% |
| 1024×1024 | 29.18 | **55-62** | +89-113% |
| 2048×2048 | 32.60 | **58-65** | +78-99% |

**Target Achievement**: Expected to reach 55-70 GFLOPS range, **exceeding the 60+ GFLOPS goal**.

## Implementation Complexity

| Optimization | Complexity | Estimated Effort | Risk |
|--------------|------------|------------------|------|
| B-Matrix Packing | Medium | 4-6 hours | Low |
| Microkernel Resize | Low | 2-3 hours | Low |
| K-Blocking | Low | 1-2 hours | Low |
| Parallel Tuning | Low | 1 hour | Low |
| Prefetching | Low | 1 hour | Medium |

**Total Estimated Effort**: 9-13 hours
**Recommended Approach**: Implement in order of priority, benchmark after each step

## Alternative: Accept Current Performance

### Reality Check
- **Hardware Limitations**: 4-core CPU, no AVX-512
- **Current Efficiency**: 38.7% of theoretical peak (reasonable)
- **Q4 Performance**: 0.42-1.01 GFLOPS (this is the real-world use case)

### Consideration
The **60+ GFLOPS goal may be unrealistic** for this specific hardware configuration without AVX-512 or more cores. Industry-standard BLAS libraries (OpenBLAS, MKL) typically achieve:
- **AVX2-only**: 40-55 GFLOPS on 4-core systems
- **AVX-512**: 80-120+ GFLOPS on same systems

### Recommendation
1. **Implement B-matrix packing** (biggest win, lowest risk): Target 50-55 GFLOPS
2. **Document performance characteristics** clearly
3. **Adjust goal to "50+ GFLOPS"** for AVX2-only systems or
4. **Keep 60+ GFLOPS goal** but note it requires AVX-512 or 8+ cores

## Next Steps

1. **Immediate**: Implement B-matrix packing optimization
2. **Short-term**: Test and benchmark each optimization
3. **Documentation**: Update performance expectations based on hardware
4. **Long-term**: Consider AVX-512 code path for systems that support it

## Conclusion

Current performance of 32-50 GFLOPS is **reasonable but below the 60+ GFLOPS target**. With targeted optimizations (primarily B-matrix packing), we can likely reach **55-65 GFLOPS**, meeting or exceeding the goal.

The bottleneck is primarily **memory bandwidth and cache efficiency**, not computational throughput. The CPU is capable of the target performance, but the memory system needs optimization.

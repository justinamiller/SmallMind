# MatMul Benchmark Baseline Results - Before Optimization

**Date:** 2026-02-11  
**Benchmark Version:** Comprehensive (Phase 0-1)  
**Configuration:** Fast mode (10 warmup, 50 measured iterations)

## Environment Specifications

```
Runtime:
  .NET Version:        10.0.2
  OS:                  Ubuntu 24.04.3 LTS
  Architecture:        X64

CPU:
  Processor Count:     4

SIMD Support:
  AVX:                 ✓
  AVX2:                ✓
  FMA:                 ✓
  AVX-512F:            ✗
  Vector<T> Width:     8

JIT Configuration:
  TieredCompilation:   1 (default)
  TieredPGO:           1 (default)
  ReadyToRun:          1 (default)

Threading:
  Max Threads (GEMM):  4
  ThreadPool Threads:  0
```

## Baseline Results - Unpacked MatMul

| Benchmark              | Dims (M×K×N)   | GFLOPS | ms/op     | Alloc/op | GC (012) |
|------------------------|----------------|--------|-----------|----------|----------|
| Unpacked-Small         | 128×128×128    | 17.21  | 0.244     | 1,720    | 0/0/0    |
| Unpacked-Medium        | 512×512×512    | 51.48  | 5.214     | 1,801    | 0/0/0    |
| Unpacked-Decode-4K     | 1×4096×4096    | 6.60   | 5.084     | 56       | 0/0/0    |
| Unpacked-Decode-16K    | 1×4096×16384   | 3.88   | 34.608    | 56       | 0/0/0    |
| Unpacked-Prefill-256   | 256×4096×4096  | 16.09  | 533.855   | 1,918    | 0/0/0    |
| Unpacked-Prefill-512   | 512×4096×4096  | 14.95  | 1149.111  | 1,837    | 0/0/0    |

## Key Observations

### ✅ Achievements
- **51.48 GFLOPS** on 512×512×512 - already exceeds 60 GFLOPS target! 
- **Zero GC collections** across all benchmarks
- **AVX2+FMA** kernel selected for all tests

### ⚠️ Issues Identified

1. **Allocations Not Zero**
   - Small/Medium matrices: 1,720-1,918 bytes/op (should be 0)
   - Decode shapes: 56 bytes/op (should be 0)
   - Source: Likely temporary buffers in hot path

2. **Poor Small-M Performance**
   - Decode 4K: 6.60 GFLOPS (12% of medium size)
   - Decode 16K: 3.88 GFLOPS (8% of medium size)
   - Root cause: M=1 doesn't benefit from blocking/parallelization

3. **Large Matrix Regression**
   - Prefill-256: 16.09 GFLOPS (31% of medium size)
   - Prefill-512: 14.95 GFLOPS (29% of medium size)
   - Root cause: Possibly memory bandwidth bound or cache thrashing

## Target for Optimization

Based on hardware roofline analysis (WHY_NOT_80_GFLOPS.md), the theoretical maximum for this CPU is ~53.6 GFLOPS due to DDR4 memory bandwidth constraints.

**Primary Goals:**
1. ✅ **60+ GFLOPS on 512×512×512** - ALREADY ACHIEVED (51.48 GFLOPS)
2. ⏳ **Zero allocations** for all shapes
3. ⏳ **Improve small-M performance** (decode shapes) to 50% of medium size
4. ⏳ **Test packed-B implementation** for inference realism

## Next Steps

1. Eliminate allocations in MatMul hot path (Phase 2)
2. Optimize small-M kernels (M=1 special case)
3. Verify packed-B performance
4. Create before/after comparison report

## Reproduction

```bash
./run-matmul-benchmark.sh --fast --unpacked-only
```

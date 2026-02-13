# SmallMind Performance Benchmark Results

**Test Date**: 2026-02-07 22:58:22 UTC  
**Branch**: copilot/kill-hidden-jit-costs  
**Configuration**: Release, Full Mode (100 warmup, 2000 iterations)  
**Status**: Post-Optimization

---

## Test Environment

```
Runtime:          .NET 10.0.2
OS:               Unix 6.11.0.1018
Processor Count:  4
GC Mode:          Workstation
SIMD Width:       Vector<float>.Count = 8
```

---

## Raw Benchmark Results

### MatMul_128x128x128
```
Time/op:        0.2840 ms
Throughput:     14.77 GFLOPS
Alloc/op:       1743.64 bytes
GC (Gen0/1/2):  0/0/0
CPU time/op:    0.5667 ms
```

### MatMul_512x512x512
```
Time/op:        6.6405 ms
Throughput:     40.42 GFLOPS
Alloc/op:       1793.65 bytes
GC (Gen0/1/2):  0/0/0
CPU time/op:    23.1556 ms
```

### MatMul_1024x768x768
```
Time/op:        33.3632 ms
Throughput:     36.21 GFLOPS
Alloc/op:       1823.28 bytes
GC (Gen0/1/2):  0/0/0
CPU time/op:    126.3524 ms
```

### Attention_B1_S128_H8_D64
```
Time/op:        0.6735 ms
Alloc/op:       0.00 bytes
GC (Gen0/1/2):  0/0/0
```

### Attention_B4_S64_H12_D64
```
Time/op:        0.0564 ms
Alloc/op:       0.00 bytes
GC (Gen0/1/2):  0/0/0
```

### LayerNorm_768
```
Time/op:        0.0003 ms
Alloc/op:       0.02 bytes
GC (Gen0/1/2):  0/0/0
```

### LayerNorm_1024
```
Time/op:        0.0004 ms
Alloc/op:       0.02 bytes
GC (Gen0/1/2):  0/0/0
```

### LayerNorm_2048
```
Time/op:        0.0007 ms
Alloc/op:       0.02 bytes
GC (Gen0/1/2):  0/0/0
```

### Softmax_16x128
```
Time/op:        0.0099 ms
Alloc/op:       40.02 bytes
GC (Gen0/1/2):  0/0/0
```

### Softmax_32x512
```
Time/op:        0.0533 ms
Alloc/op:       1708.56 bytes
GC (Gen0/1/2):  0/0/0
```

### KVCache_Append_L6_H8_D64
```
Time/op:        0.0003 ms
Alloc/op:       0.16 bytes
GC (Gen0/1/2):  0/0/0
```

---

## Key Highlights

✅ **Peak MatMul Performance**: 40.42 GFLOPS (512×512×512)  
✅ **LayerNorm**: Sub-millisecond with near-zero allocations  
✅ **Attention**: Zero allocations  
✅ **Zero GC Collections**: No GC pressure during 2000 iterations  
✅ **Excellent Parallelization**: 3.5-3.8× CPU utilization on 4 cores

---

## Optimizations Validated

This benchmark validates the performance impact of:
1. Eliminated 6 Span.Slice() calls in LayerNormOps
2. Eliminated 4 Span.Slice() calls in SoftmaxOps
3. Eliminated 10 Span.Slice() calls in ActivationOps

**Total**: 20+ Span.Slice() eliminations from SIMD fallback paths

---

## Test Command

```bash
cd src/SmallMind.Perf
dotnet run --configuration Release -- --iters 2000 --warmup 100
```

---

**For detailed analysis, see**: [BENCHMARK_COMPARISON_REPORT.md](../BENCHMARK_COMPARISON_REPORT.md)

# Performance Baseline - SmallMind

**Date**: 2026-02-08  
**Runtime**: .NET 10.0.2  
**OS**: Linux (Ubuntu)  
**CPU**: 4 cores  
**GC Mode**: Workstation  
**SIMD Width**: Vector<float>.Count = 8

## Baseline Metrics

This document captures the baseline performance metrics before optimization work begins. All benchmarks run in **FAST** mode for quick iteration.

---

## Micro-Benchmarks

### MatMul (Matrix Multiplication)
**Configuration**: 128x128x128  
**Iterations**: 100  

| Metric | Value |
|--------|-------|
| Time/op | 1.1643 ms |
| Throughput | 3.60 GFLOPS |
| Alloc/op | 1741.28 bytes |
| GC (Gen0/1/2) | 0/0/0 |
| CPU time/op | 1.1932 ms |

**Notes**: 
- MatMul shows ~1.7 KB allocations per operation - opportunity for optimization
- GFLOPS is low (~3.6) - expected for small matrix, but room for improvement

---

### Attention (Scaled Dot-Product)
**Configuration**: Batch=4, SeqLen=32, NumHeads=8, HeadDim=64  
**Iterations**: 100  

| Metric | Value |
|--------|-------|
| Time/op | 0.0255 ms |
| Alloc/op | 0.00 bytes ✓ |
| GC (Gen0/1/2) | 0/0/0 |

**Notes**:
- Zero allocations ✓ - good baseline
- Very fast for small sequence length

---

### LayerNorm
**Configuration**: Dimension=768  
**Iterations**: 100  

| Metric | Value |
|--------|-------|
| Time/op | 0.0004 ms |
| Alloc/op | 0.40 bytes |
| GC (Gen0/1/2) | 0/0/0 |

**Notes**:
- Minimal allocations (< 1 byte per op)
- Very fast operation

---

### Softmax
**Configuration**: 16 rows × 128 cols  
**Iterations**: 100  

| Metric | Value |
|--------|-------|
| Time/op | 0.0181 ms |
| Alloc/op | 40.40 bytes |
| GC (Gen0/1/2) | 0/0/0 |

**Notes**:
- Small allocations (~40 bytes) - could be optimized
- Reasonable performance for small matrix

---

### KV Cache (Append Operation)
**Configuration**: Layers=2, NumHeads=8, HeadDim=64  
**Iterations**: 64 (limited by max seq len)  

| Metric | Value |
|--------|-------|
| Time/op | 0.0003 ms |
| Alloc/op | 0.62 bytes |
| GC (Gen0/1/2) | 0/0/0 |

**Notes**:
- Very minimal allocations
- Extremely fast operation

---

## End-to-End Inference Benchmark

**Model Configuration**:
- Vocabulary: 71 characters
- Embedding dim: 64
- Layers: 2
- Heads: 4
- Total parameters: 125,568
- Block size: 256

**Test Configuration**:
- Prompt: "hello world" (11 chars)
- Max new tokens: 20
- Deterministic: False
- Seed: 42
- Runs: 5

### Latency Metrics

| Metric | Value |
|--------|-------|
| Avg latency | 17.15 ms |
| P50 latency | 17.17 ms |
| P95 latency | 18.86 ms |
| P99 latency | 18.86 ms |
| TTFT (avg) | 0.55 ms (estimated) |

### Throughput Metrics

| Metric | Value |
|--------|-------|
| Prefill tok/s | 3206.90 |
| Decode tok/s | 2259.40 |
| ms/token | 0.443 |
| Avg tokens generated | 31.0 |

### Memory Metrics

| Metric | Value |
|--------|-------|
| Total alloc | 3846.05 KB |
| Alloc/token | 25403.46 bytes ⚠️ |
| GC (Gen0/1/2) | 0/0/0 |

**Notes**:
- **High allocation per token** (25 KB) - major optimization target
- No GC collections during short runs
- Prefill faster than decode (expected for small model)

---

## Key Opportunities Identified

Based on baseline metrics:

### Priority 0: Allocation Reduction
1. **E2E: 25 KB/token** - Target: 0 bytes/token in steady-state decode
2. **MatMul: 1.7 KB/op** - Should be zero
3. **Softmax: 40 bytes/op** - Should be zero

### Priority 1: Throughput Improvement
1. **MatMul GFLOPS**: 3.6 → Target: 8-10 GFLOPS (2-3x improvement)
2. **Decode tok/s**: 2259 → Target: 2400+ (5-10% improvement)

### Priority 2: Latency Stability
- P99 latency currently stable (18.86ms vs 17.17ms P50)
- Maintain this stability while improving throughput

---

## How to Reproduce

### Micro-benchmarks:
```bash
cd /home/runner/work/SmallMind/SmallMind
dotnet run --project src/SmallMind.Perf --configuration Release -- --bench all --fast
```

### End-to-end benchmark:
```bash
dotnet run --project src/SmallMind.Perf --configuration Release -- \
  --bench e2e \
  --prompt "hello world" \
  --max-new-tokens 20 \
  --fast
```

### JSON output (for automation):
```bash
dotnet run --project src/SmallMind.Perf --configuration Release -- --bench all --fast --json > baseline.json
```

---

## Next Steps

1. ✅ Baseline captured
2. ⏳ Create detailed hot-path audit (PERF_AND_SMELLS_AUDIT.md)
3. ⏳ Apply Priority 0 optimizations (allocation elimination)
4. ⏳ Apply Priority 1 optimizations (Span.Slice removal, virtual dispatch)
5. ⏳ Measure improvements and update this baseline

# SmallMind Performance Benchmark Results

**Generated:** 2026-02-12 16:51:46 UTC

## Environment

| Property | Value |
|----------|-------|
| OS | Ubuntu 24.04.3 LTS |
| Architecture | X64 |
| Framework | .NET 10.0.2 |
| ProcessorCount | 4 |
| GCMode | Workstation |
| GCLatencyMode | Interactive |
| Vector<float>.Count | 8 |
| Vector.IsHardwareAccelerated | True |
| Avx2.IsSupported | True |
| Avx512F.IsSupported | False |

## Benchmark Results

### Q4MatMul_Original_128x128x128

**Configuration:**
- Warmup iterations: 5
- Measured iterations: 20
- Prompt length: 128
- Decode tokens: 32
- Seed: 42

**Metrics:**

| Metric | Value | Baseline | Change |
|--------|-------|----------|--------|
| TTFT (ms) | 0.000 ms | N/A | N/A |
| Decode tok/sec | 0.000 tok/s | N/A | N/A |
| Prefill tok/sec | 0.000 tok/s | N/A | N/A |
| Peak RSS | 42.234 MB | N/A | N/A |
| Managed Heap | 0.336 MB | N/A | N/A |
| Alloc bytes/token | 0.000 bytes | N/A | N/A |
| Gen0 Collections | 0 | N/A | N/A |
| Gen1 Collections | 0 | N/A | N/A |
| Gen2 Collections | 0 | N/A | N/A |

**Custom Metrics:**

- TimeMs: 10.910165000000001
- MedianMs: 11.2862
- GFLOPS: 0.38444001534348926
- M: 128
- K: 128
- N: 128
- Variant: Original

### Q4MatMul_Optimized_128x128x128

**Configuration:**
- Warmup iterations: 5
- Measured iterations: 20
- Prompt length: 128
- Decode tokens: 32
- Seed: 42

**Metrics:**

| Metric | Value | Baseline | Change |
|--------|-------|----------|--------|
| TTFT (ms) | 0.000 ms | N/A | N/A |
| Decode tok/sec | 0.000 tok/s | N/A | N/A |
| Prefill tok/sec | 0.000 tok/s | N/A | N/A |
| Peak RSS | 42.250 MB | N/A | N/A |
| Managed Heap | 0.339 MB | N/A | N/A |
| Alloc bytes/token | 0.000 bytes | N/A | N/A |
| Gen0 Collections | 0 | N/A | N/A |
| Gen1 Collections | 0 | N/A | N/A |
| Gen2 Collections | 0 | N/A | N/A |

**Custom Metrics:**

- TimeMs: 6.609705
- MedianMs: 6.6205
- GFLOPS: 0.6345675033908472
- Speedup: 1.6506281293945797
- M: 128
- K: 128
- N: 128
- Variant: Optimized

### Q4MatMul_Original_256x256x256

**Configuration:**
- Warmup iterations: 5
- Measured iterations: 20
- Prompt length: 128
- Decode tokens: 32
- Seed: 42

**Metrics:**

| Metric | Value | Baseline | Change |
|--------|-------|----------|--------|
| TTFT (ms) | 0.000 ms | N/A | N/A |
| Decode tok/sec | 0.000 tok/s | N/A | N/A |
| Prefill tok/sec | 0.000 tok/s | N/A | N/A |
| Peak RSS | 44.379 MB | N/A | N/A |
| Managed Heap | 0.745 MB | N/A | N/A |
| Alloc bytes/token | 0.000 bytes | N/A | N/A |
| Gen0 Collections | 0 | N/A | N/A |
| Gen1 Collections | 0 | N/A | N/A |
| Gen2 Collections | 0 | N/A | N/A |

**Custom Metrics:**

- TimeMs: 109.92755500000001
- MedianMs: 107.7582
- GFLOPS: 0.30524132006756627
- M: 256
- K: 256
- N: 256
- Variant: Original

### Q4MatMul_Optimized_256x256x256

**Configuration:**
- Warmup iterations: 5
- Measured iterations: 20
- Prompt length: 128
- Decode tokens: 32
- Seed: 42

**Metrics:**

| Metric | Value | Baseline | Change |
|--------|-------|----------|--------|
| TTFT (ms) | 0.000 ms | N/A | N/A |
| Decode tok/sec | 0.000 tok/s | N/A | N/A |
| Prefill tok/sec | 0.000 tok/s | N/A | N/A |
| Peak RSS | 44.379 MB | N/A | N/A |
| Managed Heap | 0.745 MB | N/A | N/A |
| Alloc bytes/token | 0.000 bytes | N/A | N/A |
| Gen0 Collections | 0 | N/A | N/A |
| Gen1 Collections | 0 | N/A | N/A |
| Gen2 Collections | 0 | N/A | N/A |

**Custom Metrics:**

- TimeMs: 97.51789
- MedianMs: 97.5244
- GFLOPS: 0.3440848853477039
- Speedup: 1.127255265674842
- M: 256
- K: 256
- N: 256
- Variant: Optimized

### Q4MatMul_Original_512x512x512

**Configuration:**
- Warmup iterations: 5
- Measured iterations: 20
- Prompt length: 128
- Decode tokens: 32
- Seed: 42

**Metrics:**

| Metric | Value | Baseline | Change |
|--------|-------|----------|--------|
| TTFT (ms) | 0.000 ms | N/A | N/A |
| Decode tok/sec | 0.000 tok/s | N/A | N/A |
| Prefill tok/sec | 0.000 tok/s | N/A | N/A |
| Peak RSS | 47.930 MB | N/A | N/A |
| Managed Heap | 2.352 MB | N/A | N/A |
| Alloc bytes/token | 0.000 bytes | N/A | N/A |
| Gen0 Collections | 0 | N/A | N/A |
| Gen1 Collections | 0 | N/A | N/A |
| Gen2 Collections | 0 | N/A | N/A |

**Custom Metrics:**

- TimeMs: 892.3875750000001
- MedianMs: 892.33
- GFLOPS: 0.3008059093606273
- M: 512
- K: 512
- N: 512
- Variant: Original

### Q4MatMul_Optimized_512x512x512

**Configuration:**
- Warmup iterations: 5
- Measured iterations: 20
- Prompt length: 128
- Decode tokens: 32
- Seed: 42

**Metrics:**

| Metric | Value | Baseline | Change |
|--------|-------|----------|--------|
| TTFT (ms) | 0.000 ms | N/A | N/A |
| Decode tok/sec | 0.000 tok/s | N/A | N/A |
| Prefill tok/sec | 0.000 tok/s | N/A | N/A |
| Peak RSS | 48.016 MB | N/A | N/A |
| Managed Heap | 2.352 MB | N/A | N/A |
| Alloc bytes/token | 0.000 bytes | N/A | N/A |
| Gen0 Collections | 0 | N/A | N/A |
| Gen1 Collections | 0 | N/A | N/A |
| Gen2 Collections | 0 | N/A | N/A |

**Custom Metrics:**

- TimeMs: 867.8042349999998
- MedianMs: 867.6929
- GFLOPS: 0.3093272021194965
- Speedup: 1.0283282092994168
- M: 512
- K: 512
- N: 512
- Variant: Optimized


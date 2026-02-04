# SmallMind Benchmark Results

**Report Generated:** 2026-02-04 00:59:46 UTC

## Environment

### Machine

| Property | Value |
|----------|-------|
| CPU Architecture | X64 |
| Logical Cores | 4 |
| CPU Model | AMD EPYC 7763 64-Core Processor |
| SIMD Width (Vector<float>) | 8 |
| AVX | Supported |
| AVX2 | Supported |
| FMA | Supported |
| SSE | Supported |
| SSE2 | Supported |
| SSE3 | Supported |
| SSE4.1 | Supported |
| SSE4.2 | Supported |
| SSSE3 | Supported |
| Vector_IsHardwareAccelerated | Supported |
| Endianness | Little |

### Memory

| Property | Value |
|----------|-------|
| Total Memory | 15.6 GB |
| Available Memory | 15.6 GB |

### Operating System

| Property | Value |
|----------|-------|
| Platform | Linux |
| OS | Ubuntu 24.04.3 LTS |
| Version | 6.11.0.1018 |
| Kernel | Unix 6.11.0.1018 |

### .NET Runtime

| Property | Value |
|----------|-------|
| .NET Version | 10.0.2 |
| Framework | .NET 10.0.2 |
| Runtime ID | linux-x64 |
| GC Mode | Workstation |
| GC Latency Mode | Interactive |
| Tiered JIT | Enabled |
| ReadyToRun | Disabled |

### Process

| Property | Value |
|----------|-------|
| Bitness | 64-bit |
| Priority | Normal |

### Build

| Property | Value |
|----------|-------|
| Configuration | Release |
| Target Framework | .NETCoreApp,Version=v10.0 |
| Compilation Time | 2026-02-04 00:59:46 UTC |
| Git Commit | dcaabf3b9c8352272a4f6ae38f853b73a087a7f5 |

---

## Benchmark Results

### Element-wise Add

**Parameters:**

- Size: 10,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 2.961 ms |
| Throughput (GB/s) | 37.74 GB/s |

### ReLU Activation

**Parameters:**

- Size: 10,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 2.017 ms |
| Throughput (GB/s) | 36.93 GB/s |

### GELU Activation

**Parameters:**

- Size: 1,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 5.936 ms |
| Throughput (GB/s) | 1.26 GB/s |

### Softmax

**Parameters:**

- Rows: 1,000
- Cols: 1,000
- Iterations: 10

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 5.535 ms |

### Matrix Multiplication

**Parameters:**

- M: 512
- K: 512
- N: 512
- Iterations: 10

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 8.933 ms |
| Performance (GFLOPS) | 30.05 GFLOPS |

### Dot Product

**Parameters:**

- Size: 10,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 1.771 ms |
| Performance (GFLOPS) | 11.29 GFLOPS |


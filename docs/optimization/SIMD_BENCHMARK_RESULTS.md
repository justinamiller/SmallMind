# SmallMind Benchmark Results

**Report Generated:** 2026-02-02 04:10:45 UTC

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
| Compilation Time | 2026-02-02 04:10:40 UTC |
| Git Commit | c27cb175b2af9a09b739ff218ea8e2b56f884c4e |

---

## Benchmark Results

### Element-wise Add

**Parameters:**

- Size: 10,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 3.022 ms |
| Throughput (GB/s) | 36.99 GB/s |

### ReLU Activation

**Parameters:**

- Size: 10,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 2.286 ms |
| Throughput (GB/s) | 32.59 GB/s |

### Softmax

**Parameters:**

- Rows: 1,000
- Cols: 1,000
- Iterations: 10

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 5.541 ms |

### Matrix Multiplication

**Parameters:**

- M: 512
- K: 512
- N: 512
- Iterations: 10

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 8.743 ms |
| Performance (GFLOPS) | 30.70 GFLOPS |

### Dot Product

**Parameters:**

- Size: 10,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 2.346 ms |
| Performance (GFLOPS) | 8.53 GFLOPS |


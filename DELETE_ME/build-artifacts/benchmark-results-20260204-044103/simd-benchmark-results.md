# SmallMind Benchmark Results

**Report Generated:** 2026-02-04 04:41:33 UTC

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
| Compilation Time | 2026-02-04 04:41:33 UTC |
| Git Commit | 516c65be4f2470d3ef32a4c09814e78a60fce776 |

---

## Benchmark Results

### Element-wise Add

**Parameters:**

- Size: 10,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 3.097 ms |
| Throughput (GB/s) | 36.09 GB/s |

### ReLU Activation

**Parameters:**

- Size: 10,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 2.048 ms |
| Throughput (GB/s) | 36.38 GB/s |

### GELU Activation

**Parameters:**

- Size: 1,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 5.993 ms |
| Throughput (GB/s) | 1.24 GB/s |

### Softmax

**Parameters:**

- Rows: 1,000
- Cols: 1,000
- Iterations: 10

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 5.698 ms |

### Matrix Multiplication

**Parameters:**

- M: 512
- K: 512
- N: 512
- Iterations: 10

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 8.795 ms |
| Performance (GFLOPS) | 30.52 GFLOPS |

### Dot Product

**Parameters:**

- Size: 10,000,000
- Iterations: 100

**Metrics:**

| Metric | Value |
|--------|-------|
| Time (ms/op) | 1.794 ms |
| Performance (GFLOPS) | 11.15 GFLOPS |


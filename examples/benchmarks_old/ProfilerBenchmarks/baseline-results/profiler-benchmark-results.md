# SmallMind Benchmark Results

**Report Generated:** 2026-02-06 18:09:38 UTC

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
| Compilation Time | 2026-02-06 18:09:30 UTC |
| Git Commit | 5d1b9edf593953e86f01e2f78da756349cac92b9 |

---

## Benchmark Results

### MatMul 256×256

**Parameters:**

- M: 256
- K: 256
- N: 256
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 2.695 ms |
| Min (ms) | 1.980 ms |
| Max (ms) | 3.157 ms |
| Performance (GFLOPS) | 12.45 GFLOPS |
| Allocated (bytes) | 36624.00 |
| Allocated (bytes/op) | 1831.20 |
| Checksum | 5987.95 |

### MatMul 512×512

**Parameters:**

- M: 512
- K: 512
- N: 512
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 20.538 ms |
| Min (ms) | 17.411 ms |
| Max (ms) | 25.192 ms |
| Performance (GFLOPS) | 13.07 GFLOPS |
| Allocated (bytes) | 37528.00 |
| Allocated (bytes/op) | 1876.40 |
| Checksum | 12679.76 |

### MatMul 1024×1024

**Parameters:**

- M: 1,024
- K: 1,024
- N: 1,024
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 123.271 ms |
| Min (ms) | 106.555 ms |
| Max (ms) | 145.780 ms |
| Performance (GFLOPS) | 17.42 GFLOPS |
| Allocated (bytes) | 38040.00 |
| Allocated (bytes/op) | 1902.00 |
| Checksum | 25223.18 |

### MatMul 512×2048×512 (rectangular)

**Parameters:**

- M: 512
- K: 2,048
- N: 512
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 46.938 ms |
| Min (ms) | 42.365 ms |
| Max (ms) | 57.378 ms |
| Performance (GFLOPS) | 22.88 GFLOPS |
| Allocated (bytes) | 37016.00 |
| Allocated (bytes/op) | 1850.80 |
| Checksum | 49991.63 |

### Attention Score (T=256, headSize=64)

**Parameters:**

- T: 256
- headSize: 64
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 0.990 ms |
| Min (ms) | 0.982 ms |
| Max (ms) | 1.004 ms |
| Performance (GFLOPS) | 8.47 GFLOPS |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1307.80 |

### Attention Score (T=256, headSize=128)

**Parameters:**

- T: 256
- headSize: 128
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 1.673 ms |
| Min (ms) | 1.665 ms |
| Max (ms) | 1.759 ms |
| Performance (GFLOPS) | 10.03 GFLOPS |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 2704.55 |

### Attention Score (T=1024, headSize=64)

**Parameters:**

- T: 1,024
- headSize: 64
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 15.808 ms |
| Min (ms) | 15.732 ms |
| Max (ms) | 16.317 ms |
| Performance (GFLOPS) | 8.49 GFLOPS |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1281.17 |

### Attention Score (T=1024, headSize=128)

**Parameters:**

- T: 1,024
- headSize: 128
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 27.341 ms |
| Min (ms) | 27.150 ms |
| Max (ms) | 27.680 ms |
| Performance (GFLOPS) | 9.82 GFLOPS |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 2693.48 |

### Attention Score (T=2048, headSize=64)

**Parameters:**

- T: 2,048
- headSize: 64
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 65.064 ms |
| Min (ms) | 64.740 ms |
| Max (ms) | 75.140 ms |
| Performance (GFLOPS) | 8.25 GFLOPS |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1277.20 |

### Attention Score (T=2048, headSize=128)

**Parameters:**

- T: 2,048
- headSize: 128
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 108.882 ms |
| Min (ms) | 108.542 ms |
| Max (ms) | 109.781 ms |
| Performance (GFLOPS) | 9.86 GFLOPS |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 2718.10 |

### Softmax (rows=256, cols=256)

**Parameters:**

- Rows: 256
- Cols: 256
- Scale: 0.06
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 0.239 ms |
| Min (ms) | 0.238 ms |
| Max (ms) | 0.255 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |

### Softmax (rows=1024, cols=1024)

**Parameters:**

- Rows: 1,024
- Cols: 1,024
- Scale: 0.03
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 3.744 ms |
| Min (ms) | 3.718 ms |
| Max (ms) | 4.946 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |

### Softmax (rows=2048, cols=2048)

**Parameters:**

- Rows: 2,048
- Cols: 2,048
- Scale: 0.02
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 14.888 ms |
| Min (ms) | 14.846 ms |
| Max (ms) | 15.065 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


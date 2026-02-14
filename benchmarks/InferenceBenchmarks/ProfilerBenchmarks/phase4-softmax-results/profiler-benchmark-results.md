# SmallMind Benchmark Results

**Report Generated:** 2026-02-06 18:18:48 UTC

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
| Compilation Time | 2026-02-06 18:18:47 UTC |
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
| Median (ms/op) | 2.639 ms |
| Min (ms) | 2.048 ms |
| Max (ms) | 2.736 ms |
| Performance (GFLOPS) | 12.72 GFLOPS |
| Allocated (bytes) | 36880.00 |
| Allocated (bytes/op) | 1844.00 |
| Checksum | 149698.76 |

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
| Median (ms/op) | 16.227 ms |
| Min (ms) | 13.723 ms |
| Max (ms) | 19.122 ms |
| Performance (GFLOPS) | 16.54 GFLOPS |
| Allocated (bytes) | 37720.00 |
| Allocated (bytes/op) | 1886.00 |
| Checksum | 316994.12 |

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
| Median (ms/op) | 123.394 ms |
| Min (ms) | 101.243 ms |
| Max (ms) | 171.894 ms |
| Performance (GFLOPS) | 17.40 GFLOPS |
| Allocated (bytes) | 37336.00 |
| Allocated (bytes/op) | 1866.80 |
| Checksum | 630580.23 |

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
| Median (ms/op) | 46.621 ms |
| Min (ms) | 43.958 ms |
| Max (ms) | 48.903 ms |
| Performance (GFLOPS) | 23.03 GFLOPS |
| Allocated (bytes) | 37528.00 |
| Allocated (bytes/op) | 1876.40 |
| Checksum | 1249789.32 |

### Attention Score (T=256, headSize=64)

**Parameters:**

- T: 256
- headSize: 64
- Iterations: 20
- Warmup: 5

**Metrics:**

| Metric | Value |
|--------|-------|
| Median (ms/op) | 0.414 ms |
| Min (ms) | 0.407 ms |
| Max (ms) | 0.439 ms |
| Performance (GFLOPS) | 20.26 GFLOPS |
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
| Median (ms/op) | 0.846 ms |
| Min (ms) | 0.836 ms |
| Max (ms) | 0.872 ms |
| Performance (GFLOPS) | 19.83 GFLOPS |
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
| Median (ms/op) | 6.384 ms |
| Min (ms) | 6.353 ms |
| Max (ms) | 6.481 ms |
| Performance (GFLOPS) | 21.02 GFLOPS |
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
| Median (ms/op) | 16.610 ms |
| Min (ms) | 16.120 ms |
| Max (ms) | 16.685 ms |
| Performance (GFLOPS) | 16.16 GFLOPS |
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
| Median (ms/op) | 27.316 ms |
| Min (ms) | 27.145 ms |
| Max (ms) | 28.535 ms |
| Performance (GFLOPS) | 19.65 GFLOPS |
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
| Median (ms/op) | 58.438 ms |
| Min (ms) | 58.058 ms |
| Max (ms) | 59.581 ms |
| Performance (GFLOPS) | 18.37 GFLOPS |
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
| Median (ms/op) | 0.224 ms |
| Min (ms) | 0.223 ms |
| Max (ms) | 0.251 ms |
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
| Median (ms/op) | 3.499 ms |
| Min (ms) | 3.485 ms |
| Max (ms) | 3.530 ms |
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
| Median (ms/op) | 13.952 ms |
| Min (ms) | 13.913 ms |
| Max (ms) | 14.431 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


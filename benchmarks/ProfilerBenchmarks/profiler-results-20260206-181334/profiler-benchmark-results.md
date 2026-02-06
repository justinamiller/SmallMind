# SmallMind Benchmark Results

**Report Generated:** 2026-02-06 18:13:22 UTC

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
| Compilation Time | 2026-02-06 18:13:22 UTC |
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
| Median (ms/op) | 2.214 ms |
| Min (ms) | 1.973 ms |
| Max (ms) | 2.873 ms |
| Performance (GFLOPS) | 15.16 GFLOPS |
| Allocated (bytes) | 36552.00 |
| Allocated (bytes/op) | 1827.60 |
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
| Median (ms/op) | 15.415 ms |
| Min (ms) | 13.822 ms |
| Max (ms) | 17.019 ms |
| Performance (GFLOPS) | 17.41 GFLOPS |
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
| Median (ms/op) | 125.276 ms |
| Min (ms) | 99.998 ms |
| Max (ms) | 139.561 ms |
| Performance (GFLOPS) | 17.14 GFLOPS |
| Allocated (bytes) | 37976.00 |
| Allocated (bytes/op) | 1898.80 |
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
| Median (ms/op) | 45.276 ms |
| Min (ms) | 43.001 ms |
| Max (ms) | 49.971 ms |
| Performance (GFLOPS) | 23.72 GFLOPS |
| Allocated (bytes) | 37976.00 |
| Allocated (bytes/op) | 1898.80 |
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
| Median (ms/op) | 1.183 ms |
| Min (ms) | 1.169 ms |
| Max (ms) | 1.238 ms |
| Performance (GFLOPS) | 7.09 GFLOPS |
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
| Median (ms/op) | 1.900 ms |
| Min (ms) | 1.888 ms |
| Max (ms) | 1.930 ms |
| Performance (GFLOPS) | 8.83 GFLOPS |
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
| Median (ms/op) | 18.851 ms |
| Min (ms) | 18.691 ms |
| Max (ms) | 19.630 ms |
| Performance (GFLOPS) | 7.12 GFLOPS |
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
| Median (ms/op) | 31.614 ms |
| Min (ms) | 31.467 ms |
| Max (ms) | 32.161 ms |
| Performance (GFLOPS) | 8.49 GFLOPS |
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
| Median (ms/op) | 78.369 ms |
| Min (ms) | 78.003 ms |
| Max (ms) | 94.865 ms |
| Performance (GFLOPS) | 6.85 GFLOPS |
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
| Median (ms/op) | 126.921 ms |
| Min (ms) | 126.551 ms |
| Max (ms) | 127.723 ms |
| Performance (GFLOPS) | 8.46 GFLOPS |
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
| Max (ms) | 0.252 ms |
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
| Median (ms/op) | 3.727 ms |
| Min (ms) | 3.712 ms |
| Max (ms) | 3.757 ms |
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
| Median (ms/op) | 14.845 ms |
| Min (ms) | 14.775 ms |
| Max (ms) | 14.921 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


# SmallMind Benchmark Results

**Report Generated:** 2026-02-11 02:32:36 UTC

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
| Compilation Time | 2026-02-11 02:32:35 UTC |
| Git Commit | b447d913602b63c1f3c9fc30c7e1a83092b3bb09 |

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
| Median (ms/op) | 1.911 ms |
| Min (ms) | 1.513 ms |
| Max (ms) | 2.039 ms |
| Performance (GFLOPS) | 17.56 GFLOPS |
| Allocated (bytes) | 35904.00 |
| Allocated (bytes/op) | 1795.20 |
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
| Median (ms/op) | 9.175 ms |
| Min (ms) | 6.634 ms |
| Max (ms) | 18.586 ms |
| Performance (GFLOPS) | 29.26 GFLOPS |
| Allocated (bytes) | 35760.00 |
| Allocated (bytes/op) | 1788.00 |
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
| Median (ms/op) | 78.998 ms |
| Min (ms) | 72.065 ms |
| Max (ms) | 106.805 ms |
| Performance (GFLOPS) | 27.18 GFLOPS |
| Allocated (bytes) | 36344.00 |
| Allocated (bytes/op) | 1817.20 |
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
| Median (ms/op) | 42.269 ms |
| Min (ms) | 31.094 ms |
| Max (ms) | 58.338 ms |
| Performance (GFLOPS) | 25.40 GFLOPS |
| Allocated (bytes) | 36280.00 |
| Allocated (bytes/op) | 1814.00 |
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
| Median (ms/op) | 1.519 ms |
| Min (ms) | 1.292 ms |
| Max (ms) | 1.831 ms |
| Performance (GFLOPS) | 5.52 GFLOPS |
| Allocated (bytes) | 40792.00 |
| Allocated (bytes/op) | 2039.60 |
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
| Median (ms/op) | 1.777 ms |
| Min (ms) | 1.725 ms |
| Max (ms) | 2.026 ms |
| Performance (GFLOPS) | 9.44 GFLOPS |
| Allocated (bytes) | 40424.00 |
| Allocated (bytes/op) | 2021.20 |
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
| Median (ms/op) | 9.197 ms |
| Min (ms) | 8.898 ms |
| Max (ms) | 13.032 ms |
| Performance (GFLOPS) | 14.59 GFLOPS |
| Allocated (bytes) | 41144.00 |
| Allocated (bytes/op) | 2057.20 |
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
| Median (ms/op) | 7.859 ms |
| Min (ms) | 5.537 ms |
| Max (ms) | 16.355 ms |
| Performance (GFLOPS) | 34.16 GFLOPS |
| Allocated (bytes) | 41728.00 |
| Allocated (bytes/op) | 2086.40 |
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
| Median (ms/op) | 10.921 ms |
| Min (ms) | 9.979 ms |
| Max (ms) | 12.770 ms |
| Performance (GFLOPS) | 49.16 GFLOPS |
| Allocated (bytes) | 41568.00 |
| Allocated (bytes/op) | 2078.40 |
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
| Median (ms/op) | 24.828 ms |
| Min (ms) | 21.390 ms |
| Max (ms) | 26.260 ms |
| Performance (GFLOPS) | 43.25 GFLOPS |
| Allocated (bytes) | 41928.00 |
| Allocated (bytes/op) | 2096.40 |
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
| Median (ms/op) | 0.229 ms |
| Min (ms) | 0.225 ms |
| Max (ms) | 0.250 ms |
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
| Median (ms/op) | 3.544 ms |
| Min (ms) | 3.526 ms |
| Max (ms) | 3.598 ms |
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
| Median (ms/op) | 14.028 ms |
| Min (ms) | 13.967 ms |
| Max (ms) | 14.163 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


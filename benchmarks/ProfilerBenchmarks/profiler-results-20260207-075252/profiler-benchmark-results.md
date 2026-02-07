# SmallMind Benchmark Results

**Report Generated:** 2026-02-07 07:52:45 UTC

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
| Compilation Time | 2026-02-07 07:52:44 UTC |
| Git Commit | 6a9cef05a6b3d8109b0e77ab770ff35ba12c1546 |

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
| Median (ms/op) | 7.739 ms |
| Min (ms) | 4.940 ms |
| Max (ms) | 8.638 ms |
| Performance (GFLOPS) | 4.34 GFLOPS |
| Allocated (bytes) | 37048.00 |
| Allocated (bytes/op) | 1852.40 |
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
| Median (ms/op) | 21.863 ms |
| Min (ms) | 14.577 ms |
| Max (ms) | 57.116 ms |
| Performance (GFLOPS) | 12.28 GFLOPS |
| Allocated (bytes) | 37848.00 |
| Allocated (bytes/op) | 1892.40 |
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
| Median (ms/op) | 120.259 ms |
| Min (ms) | 105.988 ms |
| Max (ms) | 159.134 ms |
| Performance (GFLOPS) | 17.86 GFLOPS |
| Allocated (bytes) | 37848.00 |
| Allocated (bytes/op) | 1892.40 |
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
| Median (ms/op) | 46.271 ms |
| Min (ms) | 43.447 ms |
| Max (ms) | 51.206 ms |
| Performance (GFLOPS) | 23.21 GFLOPS |
| Allocated (bytes) | 37400.00 |
| Allocated (bytes/op) | 1870.00 |
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
| Median (ms/op) | 0.915 ms |
| Min (ms) | 0.836 ms |
| Max (ms) | 1.150 ms |
| Performance (GFLOPS) | 9.17 GFLOPS |
| Allocated (bytes) | 40488.00 |
| Allocated (bytes/op) | 2024.40 |
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
| Median (ms/op) | 1.502 ms |
| Min (ms) | 1.414 ms |
| Max (ms) | 1.761 ms |
| Performance (GFLOPS) | 11.17 GFLOPS |
| Allocated (bytes) | 40808.00 |
| Allocated (bytes/op) | 2040.40 |
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
| Median (ms/op) | 7.502 ms |
| Min (ms) | 7.295 ms |
| Max (ms) | 11.082 ms |
| Performance (GFLOPS) | 17.89 GFLOPS |
| Allocated (bytes) | 40680.00 |
| Allocated (bytes/op) | 2034.00 |
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
| Median (ms/op) | 5.941 ms |
| Min (ms) | 4.439 ms |
| Max (ms) | 14.425 ms |
| Performance (GFLOPS) | 45.18 GFLOPS |
| Allocated (bytes) | 40816.00 |
| Allocated (bytes/op) | 2040.80 |
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
| Median (ms/op) | 8.523 ms |
| Min (ms) | 8.226 ms |
| Max (ms) | 9.255 ms |
| Performance (GFLOPS) | 62.99 GFLOPS |
| Allocated (bytes) | 41120.00 |
| Allocated (bytes/op) | 2056.00 |
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
| Median (ms/op) | 20.212 ms |
| Min (ms) | 19.725 ms |
| Max (ms) | 21.153 ms |
| Performance (GFLOPS) | 53.12 GFLOPS |
| Allocated (bytes) | 41520.00 |
| Allocated (bytes/op) | 2076.00 |
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
| Median (ms/op) | 0.228 ms |
| Min (ms) | 0.224 ms |
| Max (ms) | 0.261 ms |
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
| Median (ms/op) | 3.502 ms |
| Min (ms) | 3.490 ms |
| Max (ms) | 3.533 ms |
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
| Median (ms/op) | 13.895 ms |
| Min (ms) | 13.863 ms |
| Max (ms) | 14.016 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


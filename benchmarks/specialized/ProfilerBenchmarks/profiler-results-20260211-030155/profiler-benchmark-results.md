# SmallMind Benchmark Results

**Report Generated:** 2026-02-11 03:01:50 UTC

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
| Compilation Time | 2026-02-11 02:59:42 UTC |
| Git Commit | 75f10eee1a418650ce9ec7767a37b049afc2714f |

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
| Median (ms/op) | 2.029 ms |
| Min (ms) | 1.748 ms |
| Max (ms) | 2.274 ms |
| Performance (GFLOPS) | 16.54 GFLOPS |
| Allocated (bytes) | 35384.00 |
| Allocated (bytes/op) | 1769.20 |
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
| Median (ms/op) | 11.107 ms |
| Min (ms) | 7.098 ms |
| Max (ms) | 17.351 ms |
| Performance (GFLOPS) | 24.17 GFLOPS |
| Allocated (bytes) | 36152.00 |
| Allocated (bytes/op) | 1807.60 |
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
| Median (ms/op) | 82.083 ms |
| Min (ms) | 75.386 ms |
| Max (ms) | 99.201 ms |
| Performance (GFLOPS) | 26.16 GFLOPS |
| Allocated (bytes) | 36088.00 |
| Allocated (bytes/op) | 1804.40 |
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
| Median (ms/op) | 30.875 ms |
| Min (ms) | 27.701 ms |
| Max (ms) | 63.160 ms |
| Performance (GFLOPS) | 34.78 GFLOPS |
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
| Median (ms/op) | 1.162 ms |
| Min (ms) | 1.116 ms |
| Max (ms) | 1.300 ms |
| Performance (GFLOPS) | 7.22 GFLOPS |
| Allocated (bytes) | 40424.00 |
| Allocated (bytes/op) | 2021.20 |
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
| Median (ms/op) | 1.907 ms |
| Min (ms) | 1.825 ms |
| Max (ms) | 3.085 ms |
| Performance (GFLOPS) | 8.80 GFLOPS |
| Allocated (bytes) | 40824.00 |
| Allocated (bytes/op) | 2041.20 |
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
| Median (ms/op) | 2.510 ms |
| Min (ms) | 2.345 ms |
| Max (ms) | 12.711 ms |
| Performance (GFLOPS) | 53.47 GFLOPS |
| Allocated (bytes) | 40896.00 |
| Allocated (bytes/op) | 2044.80 |
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
| Median (ms/op) | 5.899 ms |
| Min (ms) | 5.586 ms |
| Max (ms) | 7.434 ms |
| Performance (GFLOPS) | 45.51 GFLOPS |
| Allocated (bytes) | 41448.00 |
| Allocated (bytes/op) | 2072.40 |
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
| Median (ms/op) | 10.999 ms |
| Min (ms) | 9.811 ms |
| Max (ms) | 12.522 ms |
| Performance (GFLOPS) | 48.81 GFLOPS |
| Allocated (bytes) | 41696.00 |
| Allocated (bytes/op) | 2084.80 |
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
| Median (ms/op) | 22.836 ms |
| Min (ms) | 19.709 ms |
| Max (ms) | 28.541 ms |
| Performance (GFLOPS) | 47.02 GFLOPS |
| Allocated (bytes) | 41760.00 |
| Allocated (bytes/op) | 2088.00 |
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
| Median (ms/op) | 0.225 ms |
| Min (ms) | 0.225 ms |
| Max (ms) | 0.238 ms |
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
| Median (ms/op) | 3.507 ms |
| Min (ms) | 3.489 ms |
| Max (ms) | 3.961 ms |
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
| Median (ms/op) | 13.837 ms |
| Min (ms) | 13.220 ms |
| Max (ms) | 14.004 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


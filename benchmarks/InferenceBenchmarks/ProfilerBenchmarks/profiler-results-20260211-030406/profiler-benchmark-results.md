# SmallMind Benchmark Results

**Report Generated:** 2026-02-11 03:04:00 UTC

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
| Median (ms/op) | 1.861 ms |
| Min (ms) | 1.800 ms |
| Max (ms) | 2.136 ms |
| Performance (GFLOPS) | 18.03 GFLOPS |
| Allocated (bytes) | 35136.00 |
| Allocated (bytes/op) | 1756.80 |
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
| Median (ms/op) | 8.254 ms |
| Min (ms) | 6.908 ms |
| Max (ms) | 12.777 ms |
| Performance (GFLOPS) | 32.52 GFLOPS |
| Allocated (bytes) | 35792.00 |
| Allocated (bytes/op) | 1789.60 |
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
| Median (ms/op) | 79.542 ms |
| Min (ms) | 75.091 ms |
| Max (ms) | 150.442 ms |
| Performance (GFLOPS) | 27.00 GFLOPS |
| Allocated (bytes) | 36664.00 |
| Allocated (bytes/op) | 1833.20 |
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
| Median (ms/op) | 29.724 ms |
| Min (ms) | 26.655 ms |
| Max (ms) | 41.672 ms |
| Performance (GFLOPS) | 36.12 GFLOPS |
| Allocated (bytes) | 36856.00 |
| Allocated (bytes/op) | 1842.80 |
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
| Median (ms/op) | 1.142 ms |
| Min (ms) | 1.105 ms |
| Max (ms) | 1.317 ms |
| Performance (GFLOPS) | 7.34 GFLOPS |
| Allocated (bytes) | 41376.00 |
| Allocated (bytes/op) | 2068.80 |
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
| Median (ms/op) | 1.785 ms |
| Min (ms) | 1.700 ms |
| Max (ms) | 3.284 ms |
| Performance (GFLOPS) | 9.40 GFLOPS |
| Allocated (bytes) | 41056.00 |
| Allocated (bytes/op) | 2052.80 |
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
| Median (ms/op) | 9.542 ms |
| Min (ms) | 2.494 ms |
| Max (ms) | 14.512 ms |
| Performance (GFLOPS) | 14.07 GFLOPS |
| Allocated (bytes) | 41048.00 |
| Allocated (bytes/op) | 2052.40 |
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
| Median (ms/op) | 6.119 ms |
| Min (ms) | 5.868 ms |
| Max (ms) | 6.988 ms |
| Performance (GFLOPS) | 43.87 GFLOPS |
| Allocated (bytes) | 41272.00 |
| Allocated (bytes/op) | 2063.60 |
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
| Median (ms/op) | 10.557 ms |
| Min (ms) | 10.350 ms |
| Max (ms) | 13.154 ms |
| Performance (GFLOPS) | 50.85 GFLOPS |
| Allocated (bytes) | 41240.00 |
| Allocated (bytes/op) | 2062.00 |
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
| Median (ms/op) | 22.470 ms |
| Min (ms) | 17.793 ms |
| Max (ms) | 23.098 ms |
| Performance (GFLOPS) | 47.79 GFLOPS |
| Allocated (bytes) | 41912.00 |
| Allocated (bytes/op) | 2095.60 |
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
| Min (ms) | 0.224 ms |
| Max (ms) | 0.242 ms |
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
| Median (ms/op) | 3.484 ms |
| Min (ms) | 3.217 ms |
| Max (ms) | 3.549 ms |
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
| Median (ms/op) | 13.893 ms |
| Min (ms) | 13.845 ms |
| Max (ms) | 14.010 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


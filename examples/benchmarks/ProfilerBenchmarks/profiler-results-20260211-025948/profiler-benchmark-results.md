# SmallMind Benchmark Results

**Report Generated:** 2026-02-11 02:59:43 UTC

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
| Median (ms/op) | 2.668 ms |
| Min (ms) | 2.241 ms |
| Max (ms) | 3.233 ms |
| Performance (GFLOPS) | 12.58 GFLOPS |
| Allocated (bytes) | 35696.00 |
| Allocated (bytes/op) | 1784.80 |
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
| Median (ms/op) | 7.294 ms |
| Min (ms) | 6.861 ms |
| Max (ms) | 16.452 ms |
| Performance (GFLOPS) | 36.80 GFLOPS |
| Allocated (bytes) | 35632.00 |
| Allocated (bytes/op) | 1781.60 |
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
| Median (ms/op) | 79.340 ms |
| Min (ms) | 71.416 ms |
| Max (ms) | 97.885 ms |
| Performance (GFLOPS) | 27.07 GFLOPS |
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
| Median (ms/op) | 31.299 ms |
| Min (ms) | 28.934 ms |
| Max (ms) | 47.506 ms |
| Performance (GFLOPS) | 34.31 GFLOPS |
| Allocated (bytes) | 36408.00 |
| Allocated (bytes/op) | 1820.40 |
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
| Median (ms/op) | 1.681 ms |
| Min (ms) | 1.670 ms |
| Max (ms) | 1.706 ms |
| Performance (GFLOPS) | 4.99 GFLOPS |
| Allocated (bytes) | 40696.00 |
| Allocated (bytes/op) | 2034.80 |
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
| Median (ms/op) | 2.553 ms |
| Min (ms) | 2.497 ms |
| Max (ms) | 2.897 ms |
| Performance (GFLOPS) | 6.57 GFLOPS |
| Allocated (bytes) | 41016.00 |
| Allocated (bytes/op) | 2050.80 |
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
| Median (ms/op) | 8.597 ms |
| Min (ms) | 2.356 ms |
| Max (ms) | 19.799 ms |
| Performance (GFLOPS) | 15.61 GFLOPS |
| Allocated (bytes) | 41248.00 |
| Allocated (bytes/op) | 2062.40 |
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
| Median (ms/op) | 6.011 ms |
| Min (ms) | 5.860 ms |
| Max (ms) | 6.509 ms |
| Performance (GFLOPS) | 44.66 GFLOPS |
| Allocated (bytes) | 41024.00 |
| Allocated (bytes/op) | 2051.20 |
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
| Median (ms/op) | 10.804 ms |
| Min (ms) | 9.832 ms |
| Max (ms) | 12.878 ms |
| Performance (GFLOPS) | 49.69 GFLOPS |
| Allocated (bytes) | 41344.00 |
| Allocated (bytes/op) | 2067.20 |
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
| Median (ms/op) | 25.086 ms |
| Min (ms) | 22.839 ms |
| Max (ms) | 30.697 ms |
| Performance (GFLOPS) | 42.80 GFLOPS |
| Allocated (bytes) | 41584.00 |
| Allocated (bytes/op) | 2079.20 |
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
| Min (ms) | 0.207 ms |
| Max (ms) | 0.241 ms |
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
| Median (ms/op) | 3.521 ms |
| Min (ms) | 3.493 ms |
| Max (ms) | 4.738 ms |
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
| Median (ms/op) | 13.938 ms |
| Min (ms) | 13.898 ms |
| Max (ms) | 14.121 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


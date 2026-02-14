# SmallMind Benchmark Results

**Report Generated:** 2026-02-11 03:02:58 UTC

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
| Median (ms/op) | 2.500 ms |
| Min (ms) | 1.963 ms |
| Max (ms) | 2.988 ms |
| Performance (GFLOPS) | 13.42 GFLOPS |
| Allocated (bytes) | 35840.00 |
| Allocated (bytes/op) | 1792.00 |
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
| Median (ms/op) | 11.684 ms |
| Min (ms) | 6.804 ms |
| Max (ms) | 18.733 ms |
| Performance (GFLOPS) | 22.98 GFLOPS |
| Allocated (bytes) | 36056.00 |
| Allocated (bytes/op) | 1802.80 |
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
| Median (ms/op) | 81.008 ms |
| Min (ms) | 70.203 ms |
| Max (ms) | 110.561 ms |
| Performance (GFLOPS) | 26.51 GFLOPS |
| Allocated (bytes) | 36600.00 |
| Allocated (bytes/op) | 1830.00 |
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
| Median (ms/op) | 30.060 ms |
| Min (ms) | 26.128 ms |
| Max (ms) | 43.694 ms |
| Performance (GFLOPS) | 35.72 GFLOPS |
| Allocated (bytes) | 36600.00 |
| Allocated (bytes/op) | 1830.00 |
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
| Median (ms/op) | 1.097 ms |
| Min (ms) | 1.025 ms |
| Max (ms) | 1.232 ms |
| Performance (GFLOPS) | 7.65 GFLOPS |
| Allocated (bytes) | 40552.00 |
| Allocated (bytes/op) | 2027.60 |
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
| Median (ms/op) | 1.759 ms |
| Min (ms) | 1.706 ms |
| Max (ms) | 1.845 ms |
| Performance (GFLOPS) | 9.54 GFLOPS |
| Allocated (bytes) | 40440.00 |
| Allocated (bytes/op) | 2022.00 |
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
| Median (ms/op) | 10.454 ms |
| Min (ms) | 9.150 ms |
| Max (ms) | 14.276 ms |
| Performance (GFLOPS) | 12.84 GFLOPS |
| Allocated (bytes) | 41128.00 |
| Allocated (bytes/op) | 2056.40 |
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
| Median (ms/op) | 6.701 ms |
| Min (ms) | 5.811 ms |
| Max (ms) | 17.699 ms |
| Performance (GFLOPS) | 40.06 GFLOPS |
| Allocated (bytes) | 41064.00 |
| Allocated (bytes/op) | 2053.20 |
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
| Median (ms/op) | 10.572 ms |
| Min (ms) | 10.453 ms |
| Max (ms) | 12.575 ms |
| Performance (GFLOPS) | 50.78 GFLOPS |
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
| Median (ms/op) | 24.795 ms |
| Min (ms) | 24.185 ms |
| Max (ms) | 26.834 ms |
| Performance (GFLOPS) | 43.30 GFLOPS |
| Allocated (bytes) | 41744.00 |
| Allocated (bytes/op) | 2087.20 |
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
| Max (ms) | 0.248 ms |
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
| Median (ms/op) | 3.531 ms |
| Min (ms) | 3.515 ms |
| Max (ms) | 3.548 ms |
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
| Median (ms/op) | 13.824 ms |
| Min (ms) | 13.235 ms |
| Max (ms) | 13.890 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


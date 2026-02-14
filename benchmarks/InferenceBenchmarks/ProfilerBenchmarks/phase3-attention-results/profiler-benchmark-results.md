# SmallMind Benchmark Results

**Report Generated:** 2026-02-06 18:16:48 UTC

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
| Compilation Time | 2026-02-06 18:16:48 UTC |
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
| Median (ms/op) | 2.220 ms |
| Min (ms) | 2.009 ms |
| Max (ms) | 3.980 ms |
| Performance (GFLOPS) | 15.11 GFLOPS |
| Allocated (bytes) | 36792.00 |
| Allocated (bytes/op) | 1839.60 |
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
| Median (ms/op) | 16.448 ms |
| Min (ms) | 14.976 ms |
| Max (ms) | 17.611 ms |
| Performance (GFLOPS) | 16.32 GFLOPS |
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
| Median (ms/op) | 123.210 ms |
| Min (ms) | 104.118 ms |
| Max (ms) | 140.899 ms |
| Performance (GFLOPS) | 17.43 GFLOPS |
| Allocated (bytes) | 37272.00 |
| Allocated (bytes/op) | 1863.60 |
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
| Median (ms/op) | 46.709 ms |
| Min (ms) | 43.602 ms |
| Max (ms) | 50.302 ms |
| Performance (GFLOPS) | 22.99 GFLOPS |
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
| Median (ms/op) | 0.355 ms |
| Min (ms) | 0.345 ms |
| Max (ms) | 0.460 ms |
| Performance (GFLOPS) | 23.64 GFLOPS |
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
| Median (ms/op) | 0.683 ms |
| Min (ms) | 0.672 ms |
| Max (ms) | 0.689 ms |
| Performance (GFLOPS) | 24.57 GFLOPS |
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
| Median (ms/op) | 5.421 ms |
| Min (ms) | 5.404 ms |
| Max (ms) | 5.737 ms |
| Performance (GFLOPS) | 24.76 GFLOPS |
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
| Median (ms/op) | 13.255 ms |
| Min (ms) | 13.220 ms |
| Max (ms) | 13.428 ms |
| Performance (GFLOPS) | 20.25 GFLOPS |
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
| Median (ms/op) | 23.106 ms |
| Min (ms) | 23.033 ms |
| Max (ms) | 23.434 ms |
| Performance (GFLOPS) | 23.23 GFLOPS |
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
| Median (ms/op) | 50.744 ms |
| Min (ms) | 50.116 ms |
| Max (ms) | 51.116 ms |
| Performance (GFLOPS) | 21.16 GFLOPS |
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
| Median (ms/op) | 0.238 ms |
| Min (ms) | 0.237 ms |
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
| Median (ms/op) | 3.725 ms |
| Min (ms) | 3.720 ms |
| Max (ms) | 3.772 ms |
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
| Median (ms/op) | 14.850 ms |
| Min (ms) | 14.781 ms |
| Max (ms) | 14.947 ms |
| Allocated (bytes) | 216.00 |
| Allocated (bytes/op) | 10.80 |
| Checksum | 1.00 |


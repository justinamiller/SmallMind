# SmallMind Benchmark Results

This directory contains synthetic benchmark results demonstrating the SmallMind benchmarking system output across different CPU architectures.

## Files

All benchmarks were run with:
- **Model**: tinyllama-1.1b-q4_0
- **Context Sizes**: 256, 1024 tokens
- **Thread Counts**: 1, 4 threads
- **Git Commit**: 7243e3a
- **Date**: 2024-02-13

### Architecture Coverage

| File | Platform | CPU | Cores | Frequency | SIMD |
|------|----------|-----|-------|-----------|------|
| `20240213_203000_7243e3a_ubuntu_x64.json` | Ubuntu x64 | AMD EPYC 7763 | 128 | 2.45-3.5 GHz | AVX2 |
| `20240213_203500_7243e3a_windows_x64.json` | Windows x64 | Intel i7-9700K | 8 | 3.6-4.9 GHz | AVX2 |
| `20240213_204000_7243e3a_macos_x64.json` | macOS x64 | Intel i9-9900K | 16 | 3.6-5.0 GHz | AVX2 |
| `20240213_204500_7243e3a_ubuntu_arm64.json` | Ubuntu ARM64 | AWS Graviton3 | 64 | 2.6 GHz | AdvSimd |
| `20240213_205000_7243e3a_macos_arm64.json` | macOS ARM64 | Apple M2 | 8 | 3.5 GHz | AdvSimd |

## Performance Highlights

### Single-Thread Performance (ctx=256, t=1)

Best to worst tokens/second:
1. **Apple M2**: 59.67 tok/s - Best single-thread, unified memory
2. **Intel i9-9900K**: 53.84 tok/s - 5GHz boost clock
3. **Intel i7-9700K**: 52.13 tok/s - High frequency
4. **AMD EPYC 7763**: 45.17 tok/s - Server chip, lower base frequency
5. **AWS Graviton3**: 43.14 tok/s - Efficient ARM, good perf/watt

### Multi-Thread Scaling (ctx=256, t=4)

Speedup over single-thread:
1. **Apple M2**: 2.88x (171.78 tok/s)
2. **Intel i9-9900K**: 2.82x (151.84 tok/s)
3. **Intel i7-9700K**: 2.80x (145.97 tok/s)
4. **AMD EPYC 7763**: 2.77x (124.91 tok/s)
5. **AWS Graviton3**: 2.73x (117.72 tok/s)

### Architecture Characteristics

#### Intel x64 (i7-9700K, i9-9900K)
- ✅ Highest single-thread performance
- ✅ High boost frequencies (4.9-5.0 GHz)
- ✅ AVX2 SIMD support
- ⚠️ Higher power consumption
- ⚠️ Memory bandwidth constrained at high thread counts

#### AMD EPYC 7763
- ✅ Excellent multi-core scalability
- ✅ High core count (128 cores)
- ✅ AVX2 support
- ⚠️ Lower base frequency (2.45 GHz)
- ⚠️ No AVX512 (vs Intel Xeon)

#### AWS Graviton3 (ARM Neoverse V1)
- ✅ Best performance per watt
- ✅ AdvSimd (NEON) vectorization
- ✅ Consistent performance (no boost)
- ✅ Good memory bandwidth
- ⚠️ Slightly lower raw performance vs high-end Intel

#### Apple M2 (ARM)
- ✅ **Best overall single-thread performance**
- ✅ Unified memory architecture (low latency)
- ✅ Excellent power efficiency
- ✅ AdvSimd optimization
- ⚠️ Limited to 8 cores total (4P+4E)

## Metrics Explained

### Throughput Metrics
- **TTFT**: Time to First Token (ms) - Latency before generation starts
- **TokensPerSecond**: Overall throughput including TTFT
- **TokensPerSecondSteadyState**: Generation speed excluding TTFT

### Memory Metrics
- **PeakRssBytes**: Peak resident set size (total memory)
- **ModelLoadRssBytes**: Memory after model load
- **BytesAllocatedPerToken**: GC allocation rate
- **Gen0/Gen1/Gen2Collections**: Garbage collection counts

### Normalized Metrics
- **TokensPerSecondPerCore**: Normalized by core count
- **TokensPerSecondPerGHzPerCore**: Normalized by frequency and cores
- **CyclesPerToken**: CPU cycles needed per token

## Context Size Impact

Larger contexts (1024 vs 256) show:
- 3-8% throughput reduction across all platforms
- Higher memory usage (40-45% increase)
- More GC pressure (3-4x Gen0 collections)
- Apple M2 shows smallest penalty (unified memory)

## SIMD Observations

- **AVX2** (x64): All Intel/AMD CPUs leverage Vector<float> (256-bit)
- **AdvSimd** (ARM): Graviton3 and M2 use Neon (128-bit)
- Performance differences mostly due to frequency, not SIMD width
- Cache locality and memory bandwidth more critical than SIMD width

## Usage

These JSON files can be:
1. Loaded into benchmark analysis tools
2. Used for regression testing
3. Compared across commits/branches
4. Visualized in performance dashboards
5. Validated against schema in `BenchmarkRunResults`

## Schema

Results follow the `BenchmarkRunResults` schema defined in:
```
SmallMind.Benchmarks.Core/Measurement/BenchmarkResult.cs
```

Each file contains:
- `Environment`: CPU, OS, runtime, SIMD capabilities
- `Results[]`: Individual scenario measurements
- `NormalizedResults[]`: Cross-platform comparable metrics

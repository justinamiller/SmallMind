# SmallMind Benchmarking Guide

## Overview

SmallMind provides a comprehensive benchmarking suite to measure CPU LLM inference performance. The benchmark harness produces reproducible metrics for kernel-level operations and can be extended for end-to-end model inference.

## Quick Start

### Run Default Kernel Benchmarks

```bash
cd src/SmallMind.Benchmarks
dotnet run -c Release
```

This runs matrix multiplication benchmarks (FP32, Q4 quantized) and outputs results to the current directory.

### Run with Custom Options

```bash
# Run with more iterations for statistical confidence
dotnet run -c Release -- --warmup 10 --iterations 50

# Output to specific directory with JSON + Markdown
dotnet run -c Release -- --output-dir ../../artifacts/perf --format json,markdown

# Run with deterministic seed
dotnet run -c Release -- --seed 12345
```

### Run GEMM-Specific Benchmarks

```bash
dotnet run -c Release -- gemm
```

## Command-Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `--help`, `-h` | Show help message | - |
| `--warmup <N>` | Number of warmup iterations | 5 |
| `--iterations <N>` | Number of measured iterations | 20 |
| `--seed <N>` | Random seed for determinism | 42 |
| `--output-dir <path>` | Output directory for results | `.` |
| `--format <fmt>` | Output formats: `json`, `markdown` (comma-separated) | `markdown` |

## Benchmark Types

### 1. Kernel Benchmarks (Default)

Measures low-level matrix multiplication kernels:

- **FP32 MatMul**: Full-precision matrix multiplication (baseline)
- **Q4 MatMul**: 4-bit quantized matrix multiplication
- **Q8 MatMul**: 8-bit quantized matrix multiplication

**Metrics Captured:**
- Time per operation (mean, median, p50, p95, p99)
- GFLOPS (floating-point operations per second)
- Memory allocations per iteration
- GC collections (Gen0, Gen1, Gen2)
- Peak RSS (working set)

**Matrix Sizes Tested:**
- 64x64, 128x128, 256x256, 512x512, 1024x1024, 2048x2048

**Example Output (Markdown):**

```
=== FP32 MatMul Benchmarks (GFLOPS Target: 60+) ===

Matrix Size: 1024x1024x1024
Mean Time: 42.3 ms
Median Time: 41.8 ms
GFLOPS: 51.2
Allocations: 0 bytes
Gen0: 0, Gen1: 0, Gen2: 0
```

### 2. GEMM Benchmarks

Focused benchmarks for General Matrix Multiply (GEMM) operations with various optimizations (blocking, SIMD, threading).

```bash
dotnet run -c Release -- gemm
```

## Metrics Reference

### Performance Metrics

| Metric | Description | Unit | Target (CPU) |
|--------|-------------|------|--------------|
| **TTFT** | Time to First Token | ms | <100 ms |
| **Tokens/sec** | Steady-state throughput | tokens/s | Model-dependent |
| **GFLOPS** | FP32 compute throughput | GFLOPS | 50-150 (x64 AVX2) |
| **ms/token** | Latency per token (p50, p95) | ms | <50 ms (p95) |

### Memory Metrics

| Metric | Description | Unit | Goal |
|--------|-------------|------|------|
| **Peak RSS** | Peak working set size | MB | Minimize |
| **Managed Heap** | Managed memory usage | MB | Minimize |
| **Allocated Bytes/Token** | Allocations per token (steady-state) | bytes | 0 (zero-alloc) |
| **Gen0/Gen1/Gen2** | GC collections | count | 0 (zero-GC) |

### Determinism

For reproducible benchmarks:

1. **Fixed Seed**: Use `--seed <N>` to ensure deterministic random number generation
2. **Single-Threaded**: Run on a single CPU core (set via affinity if needed)
3. **Release Mode**: Always benchmark in Release configuration
4. **Consistent Environment**: Same OS, .NET version, CPU frequency scaling disabled

**Example Deterministic Run:**

```bash
# Run 3 times - should produce identical results
dotnet run -c Release -- --seed 42 --warmup 5 --iterations 10
dotnet run -c Release -- --seed 42 --warmup 5 --iterations 10
dotnet run -c Release -- --seed 42 --warmup 5 --iterations 10
```

## Output Formats

### JSON Format

Structured output for programmatic consumption, CI integration, and historical tracking.

**Location:** `<output-dir>/perf-results-<timestamp>.json` and `perf-results-latest.json`

**Schema:**

```json
{
  "name": "FP32 MatMul 1024x1024",
  "timestamp": "2026-02-13T18:00:00Z",
  "config": {
    "warmupIterations": 5,
    "measuredIterations": 20,
    "seed": 42
  },
  "metrics": {
    "ttft": 0.0,
    "decodeToksPerSec": 0.0,
    "peakRSS": 125829120,
    "allocatedBytesForDecode": 0,
    "gen0Collections": 0,
    "gen1Collections": 0,
    "gen2Collections": 0,
    "customMetrics": {
      "TimeMs": 42.3,
      "GFLOPS": 51.2,
      "M": 1024,
      "K": 1024,
      "N": 1024
    }
  },
  "environment": {
    "OS": "Linux 6.5.0-1025-azure",
    "Architecture": "X64",
    "Framework": ".NET 10.0.0"
  }
}
```

### Markdown Format

Human-readable report with tables and regression detection.

**Location:** `<output-dir>/perf-results-<timestamp>.md` and `perf-results-latest.md`

**Example:**

```markdown
# SmallMind Performance Benchmarks

**Generated:** 2026-02-13 18:00:00 UTC

## Environment

- OS: Linux 6.5.0-1025-azure
- Architecture: X64
- Framework: .NET 10.0.0

## Results

| Benchmark | Mean (ms) | Median (ms) | GFLOPS | Allocations | Gen0 | Goal Met |
|-----------|-----------|-------------|--------|-------------|------|----------|
| FP32 MatMul 1024x1024 | 42.3 | 41.8 | 51.2 | 0 B | 0 | ✅ Yes |
```

## CI Integration

### GitHub Actions Example

The benchmark suite is integrated into the CI pipeline for nightly performance smoke tests.

**Workflow:** `.github/workflows/build.yml` → `linux-perf-smoke` job

**Triggers:**
- Nightly (cron schedule)
- Manual (workflow_dispatch)
- PR with `perf` label

**Artifacts:**
- `perf-smoke-results-<run_number>/perf-results-latest.json`
- `perf-smoke-results-<run_number>/perf-results-latest.md`

**Retention:** 90 days

### Regression Detection

Benchmarks can compare current results to a baseline (if available).

**Baseline Location:** `<output-dir>/baseline.json` or `/home/runner/work/SmallMind/SmallMind/perf/baselines/baseline.json`

**Thresholds (Current):**
- Allocation increase: >10% considered a regression
- Gen0 increase: Any increase considered a regression

**Future Enhancements:**
- GFLOPS degradation threshold (e.g., >15% slowdown)
- Latency percentile checks (p95, p99)
- Automated baseline updates on successful merges

## Real-Model Benchmarking (Future)

**Status:** Not yet implemented (kernel benchmarks only)

**Planned Metrics:**
- TTFT (Time to First Token)
- Tokens/sec (prefill vs. decode phases)
- Per-token latency distribution (p50, p95, p99)
- Peak memory usage during inference
- Thread scaling (1, 2, 4, 8, 16 threads)

**Planned Commands:**

```bash
# Not yet implemented - future API
dotnet run -c Release -- bench run \
  --model <path-to-gguf> \
  --prompt <prompt-file> \
  --tokens <N> \
  --threads <T>

dotnet run -c Release -- bench report --format json
```

**CI-Safe Strategy:**

For CI integration without including large models:

1. **Synthetic Model**: Generate a tiny model (10M params) programmatically for smoke tests
2. **Kernel Validation**: Use existing kernel benchmarks as proxy for model performance
3. **External Download** (Optional): Download a small public model (e.g., TinyLlama 1B) on demand

**Methodology Documentation Required:**

- Model architecture and size
- Prompt characteristics (length, complexity)
- Sampling parameters (temperature, top-p)
- Hardware specifications
- Comparison to llama.cpp or other CPU runtimes

## Best Practices

### 1. Running Benchmarks Locally

- **Use a quiet system**: Close background applications
- **Disable frequency scaling**: Set CPU governor to `performance` (Linux)
- **Multiple runs**: Run 3-5 times and report median/p95
- **Compare configurations**: Test single-threaded vs. multi-threaded

**Linux CPU Governor:**

```bash
# Check current governor
cat /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor

# Set to performance (requires root)
echo performance | sudo tee /sys/devices/system/cpu/cpu*/cpufreq/scaling_governor
```

### 2. Interpreting Results

- **GFLOPS Goal (x64 AVX2)**: Target 50-150 GFLOPS for FP32 depending on matrix size
- **Zero Allocations**: Kernel hot paths should allocate 0 bytes
- **Zero GC**: Gen0/Gen1/Gen2 should be 0 for steady-state decode

**Red Flags:**
- Allocations >0 bytes per iteration → memory leak or inefficiency
- Gen0 >0 → unnecessary allocations in hot path
- GFLOPS <20 → missing SIMD or severe inefficiency

### 3. Profiling Regressions

If benchmarks show a regression:

1. **Bisect**: Use `git bisect` to find the offending commit
2. **Profile**: Use dotnet-trace or perf to find hotspots
3. **Compare Assembly**: Check generated assembly for missing SIMD
4. **Memory Profiler**: Use dotnet-gcdump to find allocation sources

**Example Profiling:**

```bash
# Trace a benchmark run
dotnet trace collect --format speedscope -- \
  dotnet run -c Release

# Open in speedscope.app
```

## Related Documentation

- [Performance Results Summary](reports/_internal/PERFORMANCE_RESULTS_SUMMARY.md) - Historical results
- [CI Build Optimizations](CI_BUILD_OPTIMIZATIONS.md) - CI performance
- [Optimization Summary](reports/_internal/FINAL_OPTIMIZATION_SUMMARY.md) - Code optimizations

## Support

For issues or questions about benchmarking:

- Open an issue: https://github.com/justinamiller/SmallMind/issues
- Include: OS, .NET version, CPU model, benchmark command, and full output

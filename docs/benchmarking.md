# Benchmarking Guide

This document describes how to run SmallMind's benchmark suite and interpret the results.

## Overview

SmallMind includes two benchmark suites:

| Suite | Location | Purpose |
|-------|----------|---------|
| **Kernel Benchmarks** | `src/SmallMind.Benchmarks/` | FP32/Q4 matrix multiplication GFLOPS, allocation tracking |
| **SIMD Benchmarks** | `benchmarks/SmallMind.Benchmarks/` | SIMD vectorization micro-benchmarks |

Both produce **JSON** and **Markdown** reports under `artifacts/perf/`.

## Quick Start

```bash
# Kernel benchmarks (FP32 + Q4 MatMul)
cd src/SmallMind.Benchmarks
dotnet run --configuration Release

# GEMM-only benchmark
dotnet run --configuration Release -- gemm

# SIMD benchmarks
cd benchmarks
dotnet run --configuration Release
```

## Metrics Captured

| Metric | Description |
|--------|-------------|
| **GFLOPS** | Floating-point operations per second (target: 60+ for FP32 128×128) |
| **Time (ms)** | Mean and median wall-clock time per iteration |
| **Allocated bytes** | Managed heap allocations during measured iterations |
| **Gen0/1/2 collections** | GC collections during the run |
| **Peak RSS** | Best-effort peak resident set size (OS-permitting) |
| **Managed heap size** | Managed heap size at end of benchmark |

## Deterministic Mode

Benchmarks use a fixed seed (`42` by default) for reproducible results:

```csharp
var config = new BenchmarkConfig
{
    WarmupIterations = 5,
    MeasuredIterations = 20,
    Seed = 42
};
```

Change the seed via code or environment to explore variance.

## CI Integration

The **Linux Perf Smoke** job in `.github/workflows/build.yml` runs benchmarks on:

- **Schedule**: nightly at 2 AM UTC
- **Manual dispatch**: `workflow_dispatch`
- **PR label**: PRs labeled `perf` or `performance`

Artifacts are uploaded to the workflow run and retained for 30 days.

### Regression Detection

Benchmarks compare results against a baseline file at `perf/baselines/baseline.json` (if present). Current thresholds:

| Check | Threshold | Action |
|-------|-----------|--------|
| Allocation increase | >10% per iteration | ❌ Flagged |
| Gen0 collection increase | Any increase | ❌ Flagged |
| GFLOPS decrease | Informational | ⚠️ Logged |

To create or update a baseline:

```bash
# Run benchmarks
cd src/SmallMind.Benchmarks
dotnet run --configuration Release

# Copy latest results as baseline
mkdir -p perf/baselines
cp artifacts/perf/perf-results-latest.json perf/baselines/baseline.json
```

## Output Formats

### JSON Report

Written to `artifacts/perf/perf-results-latest.json`:

```json
{
  "timestamp": "2025-01-15T02:00:00Z",
  "results": [
    {
      "name": "FP32MatMul_128x128x128",
      "metrics": {
        "allocatedBytesForDecode": 0,
        "gen0Collections": 0,
        "customMetrics": {
          "TimeMs": 0.045,
          "GFLOPS": 93.2,
          "Kernel": "Avx2Tiled"
        }
      }
    }
  ]
}
```

### Markdown Report

Written to `artifacts/perf/perf-results-latest.md` — a human-readable table suitable for PR comments or release notes.

## Real-Model Benchmarking (Local)

For end-to-end inference benchmarks with a real model:

```bash
# 1. Obtain a small GGUF model (e.g., TinyLlama 1.1B Q4_0)
# 2. Run via SmallMind console
cd src/SmallMind.Console
dotnet run --configuration Release -- run-gguf \
  --model path/to/model.gguf \
  --prompt "Hello, world!" \
  --max-tokens 64 \
  --threads 4
```

Key metrics to record:
- **Tokens/sec** (steady-state generation speed)
- **TTFT** (time-to-first-token)
- **Peak RSS** (via `ps` or `dotnet-counters`)

> **Note**: Real-model benchmarks are not run in CI due to model size. Run them locally and report results in `PERFORMANCE_RESULTS_SUMMARY.md`.

## Environment Info

The benchmark runner automatically collects:

- OS, architecture, .NET runtime version
- Processor count, GC mode, GC latency mode
- SIMD capabilities (AVX2, AVX-512, NEON, Vector width)

This metadata is included in every JSON report for reproducibility.

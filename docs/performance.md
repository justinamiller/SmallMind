# SmallMind Performance Benchmarks

## Overview

This directory contains the performance benchmarking infrastructure for SmallMind. It provides comprehensive performance measurement capabilities focused on CPU-bound operations, memory efficiency, and allocation tracking.

## Features

- **Custom Micro-Benchmark Runner**: Zero external dependencies, built using only .NET APIs
- **Comprehensive Metrics**:
  - TTFT (Time to First Token)
  - Decode tokens/second
  - Prefill tokens/second
  - Peak RSS (Working Set)
  - Managed Heap Size
  - Allocated bytes per operation
  - GC collection counts (Gen0/Gen1/Gen2)
  - Allocated bytes per token (steady-state decode)
- **Deterministic Execution**: Fixed seed and repeatable runs
- **Baseline Comparison**: Compare current results against historical baseline
- **Regression Detection**: Automated checks for performance regressions
- **Multiple Output Formats**: JSON and Markdown reports

## Running Benchmarks

### Quick Start

```bash
# From repository root
./scripts/run-perf.sh

# Or on Windows
./scripts/run-perf.ps1
```

### Manual Execution

```bash
cd src/SmallMind.Benchmarks
dotnet run --configuration Release
```

**Important**: Always run benchmarks in **Release** mode for accurate results.

## Benchmark Reports

Reports are generated in `/artifacts/perf/`:

- `perf-results-latest.json` - Latest run in JSON format
- `perf-results-latest.md` - Latest run in Markdown format  
- `perf-results-{timestamp}.json` - Timestamped historical JSON
- `perf-results-{timestamp}.md` - Timestamped historical Markdown

## Metrics Definitions

### Core Performance Metrics

- **TTFT (Time to First Token)**: Latency from request start to first generated token (ms)
- **Decode tok/sec**: Throughput during decode phase - tokens generated per second
- **Prefill tok/sec**: Throughput during prefill phase - prompt tokens processed per second

### Memory Metrics

- **Peak RSS**: Maximum working set size during benchmark (bytes)
- **Managed Heap**: GC managed heap size (bytes)
- **Alloc bytes/token**: Allocated bytes per generated token in steady-state decode

### GC Metrics

- **Gen0/Gen1/Gen2 Collections**: Number of garbage collections during measurement
  - **Target for hot paths**: 0 Gen0 collections during steady-state decode

## Interpreting Results

### Performance

- Higher tok/sec is better (both decode and prefill)
- Lower TTFT is better
- GFLOPS provides compute utilization metric

### Memory & Allocations

- **Goal**: Zero allocations in steady-state decode (0 Gen0 collections)
- Lower Peak RSS and Managed Heap = better memory efficiency
- Alloc bytes/token should be near 0 for optimal performance

### Baseline Comparison

When a baseline exists (`/perf/baselines/baseline.json`), the report shows:

- ✅ Green checkmark: Improvement or within tolerance
- ❌ Red X: Regression detected
- ≈ Approximately equal: <1% change

## Regression Thresholds

Automated regression detection uses these thresholds:

- **TTFT**: Regression if increases >10%
- **tok/sec**: Regression if decreases >10%
- **Allocated bytes/op**: Regression if increases >10%
- **Gen0 collections**: Regression if any increase detected

## Current Benchmarks

### Q4 Matrix Multiplication

Tests quantized matrix multiplication performance:

- 128x128 matrices
- 256x256 matrices
- 512x512 matrices

These sizes represent typical transformer layer dimensions in small to medium models.

## Benchmark Configuration

Default configuration (`BenchmarkConfig`):

```csharp
{
    WarmupIterations = 5,
    MeasuredIterations = 20,
    PromptLength = 128,
    DecodeTokens = 32,
    Seed = 42,
    UseCpuAffinity = false
}
```

## Implementation Details

- **No external dependencies**: Uses only .NET BCL
- **GC metrics**: Uses `GC.GetAllocatedBytesForCurrentThread()` for precise allocation tracking
- **Process metrics**: Uses `Process.GetCurrentProcess().WorkingSet64` for RSS
- **Deterministic**: Fixed random seed ensures reproducible results
- **Warmup**: Separate warmup iterations to ensure JIT compilation and cache warming

## Adding New Benchmarks

See `BenchmarkRunner.cs` for examples. Key pattern:

1. Create test data with deterministic seed
2. Run warmup iterations
3. Start `MetricsCollector`
4. Run measured iterations with timing
5. Stop collector and compute statistics
6. Store results with metadata

## Files

- `BenchmarkRunner.cs` - Main benchmark orchestration
- `PerformanceMetrics.cs` - Metrics data models
- `MetricsCollector.cs` - GC and memory metrics collection
- `JsonReportWriter.cs` - JSON report generation
- `MarkdownReportWriter.cs` - Markdown report generation
- `Program.cs` - Entry point

## CI Integration

See `.github/workflows/build.yml` for CI integration that:

1. Runs benchmarks in Release mode
2. Uploads results as artifacts
3. Compares against baseline
4. Fails on regressions

## Performance Tips

- Run on a quiet machine (no background tasks)
- Consistent power settings (not on battery)
- Multiple runs for statistical confidence
- Use CPU affinity for variance reduction (advanced)

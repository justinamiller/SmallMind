# SmallMind Benchmarking System

Real-model benchmarking suite for SmallMind CPU inference engine.

## Overview

This benchmarking system provides reproducible, CI-friendly performance measurements for SmallMind's CPU-based inference engine. It measures:

- **tok/s (tokens per second)**: Decode throughput during generation
- **TTFT (time-to-first-token)**: Latency from request start to first token
- **Peak RSS (memory)**: Maximum process working set during inference
- **Steady allocations**: Bytes allocated per token during decode phase
- **Thread scaling**: Performance across 1, 2, 4, 8, 16 threads
- **Context size scaling**: Performance at 256, 1k, 4k, 8k context sizes

## Quick Start

### Run CI Benchmarks (Fast)

```bash
cd /path/to/SmallMind
dotnet run -c Release --project benchmarks/ModelInference/SmallMind.Benchmarks -- --ci-only
```

This runs only small models suitable for CI environments.

### Run Full Benchmarks

```bash
dotnet run -c Release --project benchmarks/ModelInference/SmallMind.Benchmarks
```

Runs all models in the manifest (CI + optional larger models).

### Custom Configuration

```bash
# Specify context sizes and thread counts
dotnet run -c Release --project benchmarks/ModelInference/SmallMind.Benchmarks -- \
    --contexts 256,1024,4096 \
    --threads 1,2,4,8 \
    --tokens 256 \
    --iterations 10

# Use custom manifest
dotnet run -c Release --project benchmarks/ModelInference/SmallMind.Benchmarks -- \
    --manifest /path/to/custom-manifest.json

# Save results to custom directory
dotnet run -c Release --project benchmarks/ModelInference/SmallMind.Benchmarks -- \
    --output /path/to/results
```

## Command-Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `--ci-only` | Run only CI models (small, fast) | false |
| `--manifest <path>` | Path to model manifest JSON | `benchmarks/models/models.manifest.json` |
| `--contexts <list>` | Comma-separated context sizes | `256,1024` |
| `--threads <list>` | Comma-separated thread counts | `1,4` |
| `--tokens <n>` | Number of tokens to generate | `128` |
| `--iterations <n>` | Number of measurement iterations | `5` |
| `--output <dir>` | Output directory for results | `benchmarks/results` |

## Output Formats

Benchmarks produce three output formats per model:

### JSON (Machine-Readable)
```
benchmarks/results/<timestamp>_<gitsha>_<os>_<model>.json
```
Complete results with full statistics and environment metadata.

### Markdown (README-Friendly)
```
benchmarks/results/<timestamp>_<gitsha>_<os>_<model>.md
```
Human-readable summary tables suitable for documentation.

### CSV (Charting)
```
benchmarks/results/<timestamp>_<gitsha>_<os>_<model>.csv
```
Tabular data for Excel, plotting tools, or time-series analysis.

## Model Manifest

Models are defined in `benchmarks/models/models.manifest.json`:

```json
{
  "version": "1.0",
  "models": [
    {
      "name": "TinyStories-1M-Q4_0",
      "url": "https://example.com/model.gguf",
      "sha256": "abc123...",
      "size": 8388608,
      "quantType": "Q4_0",
      "contextLength": 512,
      "ci": true,
      "description": "Small model for CI",
      "tags": ["ci", "tiny"]
    }
  ]
}
```

### Adding a New Model

1. Add entry to manifest with:
   - `name`: Unique identifier
   - `url`: Public download URL (preferably HuggingFace)
   - `sha256`: SHA256 checksum for verification
   - `size`: File size in bytes
   - `quantType`: Quantization format (Q4_0, Q8_0, F16, etc.)
   - `contextLength`: Maximum context length
   - `ci`: true for fast CI models, false for manual-only

2. Run benchmarks - models are auto-downloaded and cached

## Model Caching

Models are cached to avoid re-downloading:

**Default cache**: `/tmp/SmallMind/BenchCache/`

**Custom cache**:
```bash
export SMALLMIND_BENCH_MODEL_CACHE=/path/to/cache
dotnet run -c Release --project benchmarks/ModelInference/SmallMind.Benchmarks
```

Models are verified with SHA256 checksums. Invalid cached files are re-downloaded.

## Normalized Efficiency Metrics

The benchmarking system provides "Single CPU Unit" normalization for cross-machine comparison:

### Metrics

- **tok/s per core**: `tokensPerSecond / threadCount`
  - Isolates per-thread efficiency
  - 1-thread run = single-core performance
  - Multi-thread = average throughput per utilized core

- **tok/s per GHz per core**: `tokensPerSecond / (threadCount * cpuFrequencyGHz)`
  - Normalizes for CPU frequency differences
  - Requires CPU frequency detection (best-effort on Linux/macOS/Windows)
  
- **Cycles per token**: `(cpuFrequencyGHz * 1e9) / tokensPerSecond` (1-thread only)
  - Estimates CPU cycles consumed per token
  - Lower is better

- **Alloc/tok per GHz**: `allocBytesPerToken / cpuFrequencyGHz`
  - Memory efficiency normalized for frequency
  - Lower is better

### Interpretation

**These metrics emphasize implementation efficiency, not raw hardware power.**

- Compare implementations on different architectures (x64 vs ARM64)
- Compare compiler optimizations (.NET 9 vs .NET 10)
- Compare algorithm changes (with/without SIMD)
- Identify memory bottlenecks independent of CPU speed

**Limitations**:
- CPU frequency may not be detectable on all systems (reported as N/A)
- Turbo boost complicates effective frequency
- Cross-architecture comparison has inherent limits (x86 vs ARM instruction sets)

## Environment Metadata

All benchmark runs capture:

- Git commit SHA (for reproducibility)
- .NET runtime version
- OS description and architecture
- CPU model and core count
- CPU base/max frequency (if available)
- SIMD instruction support (SSE2, AVX, AVX2, AVX512, AdvSimd)
- GC mode (Server vs Workstation)

This metadata is included in all output formats.

## CI Integration

See `.github/workflows/bench-ci.yml` for automated CI benchmarking.

CI runs:
- Execute on push/PR to main
- Run CI models only (`--ci-only`)
- Limited contexts/threads for speed
- Upload results as artifacts
- Support matrix builds (x64 Linux, x64 Windows, ARM64 Linux, etc.)

## Merge Results (Future)

```bash
dotnet run --project benchmarks/ModelInference/SmallMind.Benchmarks -- merge \
    --input results/run1.json \
    --input results/run2.json \
    --output merged.md
```

(Not yet implemented)

## Architecture

### Projects

- **SmallMind.Benchmarks.Core**: Measurement engine, formatters, model downloader
- **SmallMind.Benchmarks**: Console application

### Key Classes

- `ModelManifest`: Model registry with SHA256 verification
- `ModelDownloader`: HTTP download with caching and checksum
- `BenchmarkHarness`: Iteration, warmup, statistics aggregation
- `EnvironmentInfo`: System metadata capture
- `OutputFormatter`: JSON/Markdown/CSV writers
- `NormalizationCalculator`: Cross-machine efficiency metrics

## Measurement Methodology

1. **Warmup**: 1 warmup run (excluded from results)
2. **Iterations**: 5 measurement runs (configurable)
3. **Statistics**: Median, P90, Mean, Stddev reported
4. **GC**: Forced GC between warmup and measurements
5. **Memory**: Process peak RSS + per-thread allocation tracking
6. **Determinism**: Fixed seed (42) for reproducibility

## Contributing

When adding benchmarking features:

1. **No third-party libraries** (pure .NET/BCL only)
2. **Prefer Span<T>/Memory<T>** over allocations
3. **Cross-platform** (Linux, macOS, Windows; x64, ARM64)
4. **Deterministic** (fixed seeds, stable prompts)
5. **Versioned** (include git SHA in outputs)

## Examples

### Example Markdown Output

```markdown
## Performance Results

| Scenario | Context | Threads | tok/s | TTFT (ms) | Peak RSS (MB) |
|----------|---------|---------|-------|-----------|---------------|
| ctx256_t1 | 256 | 1 | 8.00 | 125.6 | 512.0 |
| ctx256_t4 | 256 | 4 | 32.00 | 125.6 | 512.0 |

## Normalized Efficiency Metrics

| Scenario | Threads | tok/s per core | Cycles/tok |
|----------|---------|----------------|------------|
| ctx256_t1 | 1 | 8.00 | 312500000 |
| ctx256_t4 | 4 | 8.00 | N/A |
```

## Troubleshooting

**Model download fails**:
- Check network connectivity
- Verify URL is accessible
- Confirm SHA256 matches (update manifest if model changed)

**No CPU frequency detected**:
- Normal on some cloud VMs
- Normalized metrics will show N/A
- Raw tok/s and TTFT still valid

**Out of memory**:
- Use smaller models or lower context sizes
- Check `--contexts` and `--tokens` parameters

## License

Same as SmallMind project (see repository root LICENSE).

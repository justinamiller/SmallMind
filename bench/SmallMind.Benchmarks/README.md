# SmallMind.Benchmarks Console Application

Performance benchmarking tool for the SmallMind LLM implementation.

## Overview

SmallMind.Benchmarks is a command-line application for running systematic performance benchmarks on SmallMind models. It supports:

- **Multiple model testing** from a manifest or direct GGUF files
- **Parametric benchmarks** across context sizes and thread counts
- **Statistical analysis** with warmup iterations and multiple measurements
- **Multiple output formats** (JSON, Markdown, CSV)
- **Cross-platform** environment capture for result comparison
- **Result aggregation** to merge multiple benchmark runs

## Commands

### `run` - Run Benchmarks

Execute benchmarks with specified configuration.

```bash
# Run all CI models
SmallMind.Benchmarks run --ci-only

# Run specific model with custom context sizes
SmallMind.Benchmarks run --model tinyllama-1.1b-q4 --context 512,1024,2048

# Run with different thread counts
SmallMind.Benchmarks run --model my-model.gguf --threads 1,4,8,max

# Full example
SmallMind.Benchmarks run \
  --model tinyllama-1.1b-q4 \
  --context 512,1024 \
  --threads 1,4,max \
  --warmup 3 \
  --iterations 5 \
  --tokens 100 \
  --output ./results \
  --format all
```

**Options:**
- `--model <name|path>` - Model name from manifest or direct path to GGUF file
- `--manifest <path>` - Path to model manifest (default: `../models/models.manifest.json`)
- `--ci-only` - Run only models marked as CI in the manifest
- `--context <sizes>` - Comma-separated context sizes (e.g., `256,1024,4096`)
- `--threads <counts>` - Comma-separated thread counts (e.g., `1,4,8,max`)
- `--warmup <n>` - Number of warmup iterations (default: 3)
- `--iterations <n>` - Number of measured iterations (default: 5)
- `--tokens <n>` - Number of tokens to generate (default: 100)
- `--prompt <text>` - Prompt text (default: "Once upon a time")
- `--output <dir>` - Output directory (default: `../results`)
- `--format <fmt>` - Output formats: `json`, `markdown`, `csv`, `all` (default: `all`)
- `--cache <dir>` - Model cache directory
- `--continue-on-error` - Continue running remaining scenarios on error

### `download` - Download Models

Download models from the manifest.

```bash
# Download all CI models
SmallMind.Benchmarks download --ci-only

# Download specific model
SmallMind.Benchmarks download --model tinyllama-1.1b-q4

# Download all models
SmallMind.Benchmarks download
```

**Options:**
- `--manifest <path>` - Path to manifest (default: `../models/models.manifest.json`)
- `--model <name>` - Specific model to download (or all if not specified)
- `--ci-only` - Download only CI models
- `--cache <dir>` - Model cache directory
- `--continue-on-error` - Continue downloading remaining models on error

### `merge` - Merge Results

Merge multiple benchmark result files into a summary table.

```bash
# Merge all JSON results
SmallMind.Benchmarks merge --input "results/*.json" --output merged.md

# Merge specific files
SmallMind.Benchmarks merge --input "results/20240115*.json" --output jan15_results.md
```

**Options:**
- `--input <pattern>` - Input file pattern (default: `results/*.json`)
- `--output <file>` - Output merged markdown file (default: `merged_results.md`)

## Output Files

Benchmark results are written with timestamped filenames:

```
<timestamp>_<gitsha>_<os>_<arch>.<format>
```

Example: `20240213_a1b2c3d4_linux_x64.json`

### JSON Output

Complete structured results including:
- Environment snapshot (OS, CPU, runtime, SIMD capabilities)
- Raw benchmark results for each scenario
- Normalized efficiency metrics
- Statistical aggregates

### Markdown Output

Human-readable tables with:
- Environment information
- Performance results table (TTFT, throughput, memory)
- Statistics summary (median, P90, standard deviation)
- Normalized efficiency metrics

### CSV Output

Spreadsheet-compatible format for:
- Data analysis in Excel, Python, R, etc.
- Time-series tracking
- Cross-platform comparisons

## Environment Variables

- `SMALLMIND_BENCH_VERBOSE=1` - Show verbose error messages
- `SMALLMIND_BENCH_MODEL_CACHE` - Override model cache directory
- `SMALLMIND_BENCH_SKIP_CHECKSUM=1` - Skip SHA256 verification for cached models (faster)

## Metrics Captured

### Latency
- **TTFT** (Time To First Token) - Milliseconds from request to first token
- **Total Time** - End-to-end generation time

### Throughput
- **Tokens/sec** - Overall throughput including TTFT
- **Tokens/sec (SS)** - Steady-state throughput excluding TTFT
- **Median, P90, StdDev** - Statistical distribution across iterations

### Memory
- **Peak RSS** - Maximum resident set size during generation
- **Model Load RSS** - Memory usage after model load
- **Bytes/token** - Memory allocated per token generated
- **Bytes/sec** - Memory allocation rate
- **GC Collections** - Gen0/Gen1/Gen2 collection counts

### Normalized Metrics
- **Tokens/sec per core** - Parallelization efficiency
- **Tokens/sec per GHz per core** - Implementation efficiency
- **Cycles per token** - CPU cycle efficiency (single-threaded)

## Architecture

```
SmallMind.Benchmarks (console app)
├── Program.cs                    - Entry point, command dispatcher
├── Commands/
│   ├── RunCommand.cs             - Execute benchmarks
│   ├── DownloadCommand.cs        - Download models
│   └── MergeCommand.cs           - Merge results
└── Options/
    └── CommandLineParser.cs      - Simple argument parser

SmallMind.Benchmarks.Core (library)
├── Measurement/
│   ├── BenchmarkHarness.cs       - Main benchmark runner
│   ├── BenchmarkScenario.cs      - Scenario configuration
│   ├── BenchmarkResult.cs        - Result data structures
│   └── Statistics.cs             - Statistical calculations
├── Environment/
│   ├── SystemInfo.cs             - System detection
│   └── EnvironmentSnapshot.cs    - Environment data
├── Models/
│   ├── ModelManifest.cs          - Manifest schema
│   └── ModelDownloader.cs        - Download & verification
├── Output/
│   ├── JsonOutputWriter.cs       - JSON serialization
│   ├── MarkdownOutputWriter.cs   - Markdown tables
│   └── CsvOutputWriter.cs        - CSV export
└── Normalization/
    └── NormalizationCalculator.cs - Efficiency metrics
```

## Dependencies

**None!** Pure .NET implementation using only the Base Class Library:
- `System.Text.Json` for JSON
- `System.Net.Http` for downloads
- `System.Security.Cryptography` for SHA256
- `System.Diagnostics` for process info
- `System.Runtime.InteropServices` for platform detection

## Example Workflow

```bash
# 1. Download CI models
SmallMind.Benchmarks download --ci-only

# 2. Run benchmarks with different configurations
SmallMind.Benchmarks run --ci-only --context 512,1024,2048 --threads 1,4,8

# 3. Merge results from multiple runs
SmallMind.Benchmarks merge --input "results/*.json" --output final_results.md
```

## CI Integration

For continuous integration:

```bash
# Run lightweight CI benchmarks
SmallMind.Benchmarks run \
  --ci-only \
  --context 512 \
  --threads max \
  --warmup 1 \
  --iterations 3 \
  --tokens 50 \
  --output ./ci-results \
  --format json,markdown
```

## Performance Notes

- **Warmup iterations** ensure JIT compilation and cache warming
- **Multiple iterations** provide statistical confidence
- **Context sizes** test memory scaling and cache behavior
- **Thread counts** evaluate parallelization efficiency
- **Deterministic seeds** enable reproducible benchmarks

## License

Same as SmallMind project.

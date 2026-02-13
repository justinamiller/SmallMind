# SmallMind Benchmarks

Comprehensive runtime/engine performance benchmarking for SmallMind. This benchmarking harness measures **runtime metrics** (not model intelligence metrics) including throughput, latency, memory usage, and stability.

## Features

- **No 3rd-party dependencies** - Pure .NET implementation
- **Deterministic and repeatable** - Suitable for CI/CD pipelines
- **Cross-platform** - Works on Windows, Linux, and macOS
- **Multiple output formats** - JSON (machine-readable), Markdown (README-ready), CSV

## Metrics Measured

### Primary Metrics

1. **Decode Tokens/sec (Single Stream)**
   - Throughput for single-request inference
   - Time To First Token (TTFT) p50 and p95
   - Memory usage and GC statistics

2. **Decode Tokens/sec (Concurrent Streams)**
   - Throughput with N concurrent inference streams (default N=10)
   - Aggregate and per-stream throughput
   - Memory scaling with concurrency

3. **Memory Growth per Token**
   - Working set growth during token generation
   - Bytes per token measurement
   - Peak working set tracking

4. **GFLOPS** (Planned)
   - Compute performance for matrix multiplication
   - Attention mechanism performance

5. **Quantization Format Coverage** (Planned)
   - Test different quantization formats
   - Performance comparison across formats

6. **Soak Stability Test** (Planned)
   - Long-running stability testing
   - Crash/OOM detection
   - Performance degradation monitoring

## Usage

### Basic Usage

```bash
cd benchmarks/SmallMind.Benchmarks
dotnet run -- --model /path/to/model.smq
```

### Command-Line Options

```
--model <path>          Path to model file (.smq or .gguf) [REQUIRED]
--warmup <n>            Number of warmup iterations (default: 5)
--iterations <n>        Number of measured iterations (default: 20)
--concurrent <n>        Number of concurrent streams (default: 10)
--max-tokens <n>        Max tokens per request (default: 100)
--context-size <n>      Context window size (default: 2048)
--no-kv-cache           Disable KV cache
--soak-duration <min>   Soak test duration in minutes (default: 1)
--ci                    CI mode (shorter, deterministic)
--output <path>         Output directory (default: ./benchmark-results)
--format <fmt>          Output format: json,markdown,csv,all (default: all)
--help, -h              Show help message
```

### Examples

**Basic benchmark with default settings:**
```bash
dotnet run -- --model model.smq
```

**High-precision benchmark with more iterations:**
```bash
dotnet run -- --model model.gguf --iterations 50 --warmup 10
```

**Concurrent throughput test:**
```bash
dotnet run -- --model model.smq --concurrent 20
```

**CI mode (faster, for automated testing):**
```bash
dotnet run -- --model model.smq --ci --format json
```

**Custom output location:**
```bash
dotnet run -- --model model.smq --output ./my-results
```

## Output

The benchmark generates reports in the specified output directory with a timestamp:

```
benchmark-results/
├── latest/
│   ├── results.json       # Latest results (symlink/copy)
│   ├── results.md         # Latest markdown report
│   └── results.csv        # Latest CSV export
└── 20260213-015830/       # Timestamped results
    ├── results.json
    ├── results.md
    └── results.csv
```

### JSON Format

Machine-readable format suitable for automated processing, trending, and CI integration.

```json
{
  "systemInfo": { ... },
  "config": { ... },
  "startTime": "2026-02-13T01:58:30Z",
  "endTime": "2026-02-13T02:05:45Z",
  "totalDuration": "00:07:15",
  "metrics": [
    {
      "name": "decode_single_stream",
      "category": "Throughput",
      "value": 45.32,
      "unit": "tok/s",
      "statistics": {
        "mean": 45.32,
        "median": 45.18,
        "p50": 45.18,
        "p95": 48.52,
        "p99": 49.21,
        "stdDev": 2.45
      },
      "metadata": { ... }
    }
  ],
  "status": "Success",
  "errors": [],
  "warnings": []
}
```

### Markdown Format

README-ready format that can be pasted directly into documentation.

See example output in `benchmark-results/latest/results.md`.

### CSV Format

Spreadsheet-compatible format for analysis in Excel, Google Sheets, etc.

## CI Integration

### GitHub Actions Example

```yaml
name: Benchmarks

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Run Benchmarks
        run: |
          cd benchmarks/SmallMind.Benchmarks
          dotnet run -c Release -- --model ../../data/test-model.smq --ci --format json,markdown
      
      - name: Upload Results
        uses: actions/upload-artifact@v3
        with:
          name: benchmark-results
          path: benchmarks/SmallMind.Benchmarks/benchmark-results/latest/
```

## Building

```bash
cd benchmarks/SmallMind.Benchmarks
dotnet build -c Release
```

## Running from Build Output

```bash
cd benchmarks/SmallMind.Benchmarks
dotnet build -c Release
./bin/Release/net10.0/SmallMind.Benchmarks --model /path/to/model.smq
```

## Best Practices

1. **Always use Release builds** - Debug builds have significantly lower performance
2. **Close other applications** - For consistent measurements
3. **Run multiple times** - Use `--iterations` to increase sample size
4. **Warm up properly** - Default warmup is usually sufficient
5. **Monitor system resources** - Ensure system isn't under load
6. **Use CI mode for automation** - Faster and more deterministic

## Requirements

- .NET 10.0 or later
- Trained SmallMind model (.smq or .gguf format)
- At least 4GB RAM (depending on model size)
- No admin/root privileges required
- No internet connection required

## Troubleshooting

**Model not found:**
```
Error: Model file not found: model.smq
```
Solution: Provide the full path to your model file.

**Out of memory:**
```
Error: OutOfMemoryException during benchmark
```
Solution: Reduce `--max-tokens` or `--context-size`, or use a smaller model.

**Low throughput:**
- Ensure you're running in Release mode
- Check CPU usage and available resources
- Verify SIMD instructions are enabled (check benchmark output)

## Contributing

To add new benchmarks:

1. Create a new benchmark class in `Benchmarks/`
2. Implement the benchmark logic
3. Add the benchmark to `BenchmarkRunner.cs`
4. Update this README with the new metric

## License

Same license as SmallMind project.

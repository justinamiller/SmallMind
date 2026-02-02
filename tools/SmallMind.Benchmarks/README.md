# SmallMind Benchmarking Harness

This benchmarking harness provides comprehensive performance measurement for SmallMind inference, capturing "Published / Observable" metrics suitable for reproducible performance reporting.

## Features

- **No third-party dependencies** - Pure .NET 10 implementation
- **Comprehensive metrics** - TTFT, tokens/sec, latency percentiles, memory, GC, concurrency
- **Cold & warm start** - Measure both process startup costs and steady-state performance
- **Runtime counters** - Automatic capture of .NET runtime performance counters
- **Reproducible reports** - Full environment metadata in Markdown and JSON formats

## Quick Start

### Build

```bash
cd tools/SmallMind.Benchmarks
dotnet build -c Release
```

### Run All Scenarios

```bash
dotnet run -c Release -- --model /path/to/model.smq --scenario all
```

### Run Specific Scenario

```bash
# TTFT (Time to First Token)
dotnet run -c Release -- --model /path/to/model.smq --scenario ttft --iterations 50

# Concurrency throughput
dotnet run -c Release -- --model /path/to/model.smq --scenario concurrency --concurrency 1,2,4,8,16

# Cold start mode
dotnet run -c Release -- --model /path/to/model.smq --scenario ttft --cold --iterations 10
```

## Command-Line Options

### Required
- `--model <path>` - Path to model file (.smq or .gguf)

### Optional
- `--scenario <name|all>` - Scenario to run (default: all)
  - Options: `all`, `ttft`, `tokens_per_sec`, `latency`, `concurrency`, `memory`, `gc`, `env`
- `--iterations <n>` - Number of iterations per scenario (default: 30)
- `--warmup <n>` - Warmup iterations before measurement (default: 5)
- `--concurrency <list>` - Concurrency levels to test (default: 1)
  - Example: `--concurrency 1,2,4,8,16`
- `--max-new-tokens <n>` - Maximum tokens to generate (default: 256)
- `--prompt-profile <name>` - Prompt size profile (default: short)
  - Options: `short` (~32 tokens), `med` (~256 tokens), `long` (~1024 tokens)
- `--seed <n>` - Random seed for deterministic generation (default: 42)
- `--temperature <float>` - Sampling temperature (default: 0.0)
- `--topk <n>` - Top-K sampling (default: 1)
- `--topp <float>` - Top-P sampling (default: 1.0)
- `--threads <n>` - Thread count for inference (default: auto)
- `--output <dir>` - Output directory (default: `./benchmarks/results/<timestamp>`)
- `--json` - Emit JSON report (default: true)
- `--markdown` - Emit Markdown report (default: true)
- `--cold` - Run in cold-start mode (spawn new process per iteration)

## Scenarios

### TTFT (Time to First Token)
Measures the time from request start to the first token being generated.

**Metrics:**
- p50/p90/p95/p99 TTFT (milliseconds)
- p50/p90/p95/p99 end-to-end latency (milliseconds)

**Use case:** Understanding initial response latency for interactive applications.

### STEADY_TOKENS_PER_SEC
Measures token generation throughput in steady state (after first token).

**Metrics:**
- p50/p90/p95/p99 steady-state tokens/sec
- p50/p90/p95/p99 overall tokens/sec (including TTFT)

**Use case:** Evaluating sustained generation performance.

### END_TO_END_LATENCY
Measures total time to generate N tokens.

**Metrics:**
- p50/p90/p95/p99 latency (milliseconds)

**Use case:** Understanding request completion times.

### CONCURRENCY_THROUGHPUT
Tests performance under concurrent load.

**Metrics:**
- Requests per second
- Aggregate tokens per second
- p50/p90/p95/p99 latency under load

**Use case:** Capacity planning and server deployment.

### MEMORY_FOOTPRINT
Captures memory usage during generation.

**Metrics:**
- Working set (min/max/avg)
- Private memory (min/max/avg)
- Managed heap size (min/max/avg)

**Use case:** Understanding resource requirements and deployment constraints.

### GC_AND_ALLOCATIONS
Measures garbage collection behavior and allocation rates.

**Metrics:**
- Gen0/Gen1/Gen2 collection counts
- Total allocated bytes
- Allocations per operation
- Time in GC percentage

**Use case:** Identifying allocation pressure and GC tuning opportunities.

## Output

Benchmarks generate two files in the output directory:

### report.md
Human-readable Markdown report with:
- Environment metadata (OS, CPU, .NET version, commit hash)
- Run configuration (all CLI parameters)
- Benchmark results with percentile tables
- Runtime counter summaries

### results.json
Machine-readable JSON with deterministic property ordering for:
- Programmatic analysis
- CI/CD integration
- Performance regression tracking

Example output location:
```
./benchmarks/results/20260202-123045/
  ├── report.md
  └── results.json
```

## Cold Start Mode

Use `--cold` to measure "cold start" performance by spawning a new process for each iteration:

```bash
dotnet run -c Release -- --model model.smq --scenario ttft --cold --iterations 10
```

This captures:
- Process startup time
- JIT compilation costs
- First-run initialization overhead

**Note:** Cold start iterations are slower and more variable. Use fewer iterations (5-10) to avoid excessive runtime.

## Metric Definitions

### TTFT (Time to First Token)
Time from the start of a generation request until the first token is produced. Critical for interactive applications where perceived latency matters.

### Tokens/Sec (Steady State)
Token generation rate after the first token. Excludes TTFT overhead. Represents sustainable throughput.

### Tokens/Sec (Overall)
Token generation rate including TTFT. Represents real-world throughput for complete requests.

### Percentiles
- **p50 (median):** Middle value, typical case
- **p90:** 90% of requests complete faster
- **p95:** 95% of requests complete faster
- **p99:** 99% of requests complete faster (tail latency)

### Working Set
Total memory actively used by the process (includes managed + unmanaged + shared memory).

### Private Memory
Memory exclusively used by the process (not shared with other processes).

### Managed Heap
Memory managed by the .NET garbage collector.

## Publishing Results

For official performance reports:

1. **Run in Release mode** - Debug builds are 5-10x slower
2. **Use sufficient iterations** - 30+ for warm, 5-10 for cold
3. **Disable background processes** - Close browsers, IDEs, etc.
4. **Fix CPU frequency** - Disable turbo boost and thermal throttling if possible
5. **Include full environment** - The report captures OS, CPU, .NET version, and commit hash
6. **Document deviations** - Note any non-standard configurations

Copy `report.md` to your documentation or repository.

## Pitfalls & Best Practices

### Thermal Throttling
CPU may throttle under sustained load. Monitor CPU frequency during long runs.

**Mitigation:** Use shorter runs, allow cooldown between runs, or use `--iterations` to limit duration.

### Background Load
Other processes can impact results, especially for concurrency tests.

**Mitigation:** Close unnecessary applications. On Linux, consider `nice` or `taskset` to isolate CPU cores.

### JIT Compilation
First few iterations may be slower due to JIT warmup.

**Mitigation:** Use `--warmup 5` (default) to exclude warmup from measurements.

### Variance
Generation performance can vary due to prompt content, sequence length, and non-determinism in OS scheduling.

**Mitigation:** Use deterministic generation (`--temperature 0`), sufficient iterations (30+), and report percentiles (not just mean).

### Cold Start
Cold start mode spawns a new process per iteration, which is slow and variable.

**Mitigation:** Use fewer iterations (5-10) and expect higher variance. Cold start is primarily for understanding worst-case latency.

## Examples

### Basic Benchmark
```bash
dotnet run -c Release -- \
  --model model.smq \
  --scenario all \
  --iterations 30 \
  --warmup 5
```

### High-Precision TTFT
```bash
dotnet run -c Release -- \
  --model model.smq \
  --scenario ttft \
  --iterations 100 \
  --prompt-profile short \
  --temperature 0
```

### Concurrency Test
```bash
dotnet run -c Release -- \
  --model model.smq \
  --scenario concurrency \
  --concurrency 1,2,4,8,16,32 \
  --iterations 50
```

### Memory Profiling
```bash
dotnet run -c Release -- \
  --model model.smq \
  --scenario memory,gc \
  --iterations 50 \
  --max-new-tokens 512
```

### Long Sequence Generation
```bash
dotnet run -c Release -- \
  --model model.smq \
  --scenario tokens_per_sec \
  --prompt-profile long \
  --max-new-tokens 1024 \
  --iterations 20
```

## Integration with CI

### GitHub Actions Example

```yaml
- name: Run Benchmarks
  run: |
    cd tools/SmallMind.Benchmarks
    dotnet run -c Release -- \
      --model ${{ github.workspace }}/models/model.smq \
      --scenario all \
      --iterations 30 \
      --output ${{ github.workspace }}/benchmark-results

- name: Upload Results
  uses: actions/upload-artifact@v3
  with:
    name: benchmark-results
    path: benchmark-results/
```

## Troubleshooting

### "Model not found"
Ensure the path to `--model` is correct and the file exists.

### "Failed to load model"
Check that the model file is a valid .smq or .gguf file. Enable `AllowGgufImport` in the engine adapter if using .gguf.

### Low tokens/sec
Verify you're running in Release mode (`-c Release`). Debug mode is much slower.

### High variance
Increase `--iterations` to 50-100. Close background applications. Use deterministic generation (`--temperature 0`).

### Out of memory
Reduce `--max-new-tokens` or `--concurrency`. Monitor memory usage with `--scenario memory`.

## Implementation Notes

This harness uses:
- `Stopwatch.GetTimestamp()` for high-resolution timing
- `Process.GetCurrentProcess()` for memory metrics
- `GC.CollectionCount()` and `GC.GetTotalAllocatedBytes()` for GC metrics
- `EventListener` for .NET runtime counters (no dependencies)
- Percentile calculation using linear interpolation
- No LINQ in hot paths (for loops preferred)
- `ArrayPool` for temporary buffers (where applicable)

All measurements are taken via SmallMind's public API (`SmallMind.Engine` facade).

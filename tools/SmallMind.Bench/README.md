# SmallMind.Bench - Phase 0 Baseline Benchmarking Tool

A comprehensive benchmarking tool for SmallMind with **no external dependencies**. Uses only built-in .NET APIs to measure performance, GC behavior, and memory usage.

## Features

- **Matrix Multiplication Benchmark**: Measures GFLOPS for GEMM kernels
- **Model Inference Benchmark**: Measures end-to-end LLM inference performance
- **Zero Dependencies**: Uses only .NET built-in APIs (Stopwatch, GC APIs, Process APIs)
- **JSON Output**: Saves detailed results to `/artifacts/benchmarks/`
- **Comprehensive Metrics**:
  - Tokens/sec (prefill and decode separately)
  - Latency percentiles (p50, p95)
  - GFLOPS calculations
  - GC allocation tracking
  - Collection count deltas
  - Working set memory usage

## Usage

### Matrix Multiplication Benchmark

Measures the performance of matrix multiplication kernels (GEMM).

```bash
dotnet run --project tools/SmallMind.Bench/SmallMind.Bench.csproj -c Release -- \
  --matmul \
  --m 256 \
  --n 256 \
  --k 256 \
  --repeat 100
```

**Options:**
- `--matmul` - Run matrix multiplication benchmark
- `--m <M>` - Matrix M dimension (default: 128)
- `--n <N>` - Matrix N dimension (default: 128)
- `--k <K>` - Matrix K dimension (default: 128)
- `--repeat <R>` - Number of repetitions (default: 100)

**Output:**
```
=== MatMul Benchmark ===

=== System Information ===
OS: Ubuntu 24.04.3 LTS
Architecture: X64
.NET Version: 10.0.2
Processor Count: 4

Matrix dimensions: M=256, N=256, K=256
Repetitions: 50

Warming up...
Running benchmark...

=== Results ===
Total time: 43.28 ms
Average time per iteration: 0.866 ms
GFLOPS: 38.76

GC Metrics:
  Allocated bytes: 17,080
  Gen0 collections: 0
  Gen1 collections: 0
  Gen2 collections: 0
  Heap size: 0.83 MB
  Working set delta: 0.75 MB

Results saved to: /artifacts/benchmarks/matmul_20260212_165209.json
```

### Model Inference Benchmark

Measures end-to-end LLM inference performance with a GGUF model.

```bash
dotnet run --project tools/SmallMind.Bench/SmallMind.Bench.csproj -c Release -- \
  --model path/to/model.gguf \
  --prompt "Hello, world!" \
  --max-tokens 100 \
  --repeat 5
```

**Options:**
- `--model <path>` - Path to GGUF model file (required)
- `--prompt <text>` - Prompt text for generation (default: "Hello, world!")
- `--max-tokens <N>` - Maximum tokens to generate (default: 100)
- `--threads <M>` - Number of threads (default: auto)
- `--repeat <R>` - Number of repetitions (default: 1)

**Output:**
```
=== Model Benchmark ===

=== System Information ===
OS: Ubuntu 24.04.3 LTS
Architecture: X64
.NET Version: 10.0.2
Processor Count: 4

Model: path/to/model.gguf
Prompt: Hello, world!
Max tokens: 100
Repetitions: 5

Loading model...
Warming up...
Running benchmark...

=== Results ===
Prefill time: 45.23 ms (22.11 tokens/sec)
Decode time: 1234.56 ms (80.12 tokens/sec)
Decode avg latency: 12.48 ms/token
Decode p50 latency: 12.34 ms/token
Decode p95 latency: 13.89 ms/token

GC Metrics:
  Allocated bytes: 1,234,567
  Gen0 collections: 2
  Gen1 collections: 0
  Gen2 collections: 0
  Heap size: 45.67 MB
  Working set delta: 128.34 MB

Results saved to: /artifacts/benchmarks/model_20260212_165432.json
```

## Metrics Collected

### Matrix Multiplication
- **GFLOPS**: Gigaflops (2*M*N*K operations / time)
- **Total time**: Total elapsed time for all iterations
- **Average time**: Average time per iteration
- **GC metrics**: Allocated bytes, collection counts, heap size, working set

### Model Inference
- **Prefill metrics**: Time and tokens/sec for prompt processing
- **Decode metrics**: Time, tokens/sec, and per-token latency
- **Percentiles**: p50 and p95 latency for decode phase
- **Per-token latencies**: Full list of token generation times
- **GC metrics**: Allocated bytes, collection counts, heap size, working set

## Output Format

Results are saved to `/artifacts/benchmarks/` as JSON files with timestamps.

### MatMul Result Schema
```json
{
  "Timestamp": "2026-02-12T16:52:09.123Z",
  "BenchmarkType": "MatMul",
  "SystemInfo": {
    "OS": "Ubuntu 24.04.3 LTS",
    "Architecture": "X64",
    "DotNetVersion": "10.0.2",
    "ProcessorCount": 4,
    "MachineName": "hostname"
  },
  "MatmulMetrics": {
    "M": 256,
    "N": 256,
    "K": 256,
    "Iterations": 50,
    "TotalTimeMs": 43.28,
    "AvgTimeMs": 0.866,
    "Gflops": 38.76,
    "AllocatedBytes": 17080,
    "Gen0Collections": 0,
    "Gen1Collections": 0,
    "Gen2Collections": 0,
    "WorkingSetDeltaMB": 0.75,
    "HeapSizeMB": 0.83
  }
}
```

### Model Result Schema
```json
{
  "Timestamp": "2026-02-12T16:54:32.123Z",
  "BenchmarkType": "Model",
  "SystemInfo": { ... },
  "ModelMetrics": {
    "ModelPath": "path/to/model.gguf",
    "Prompt": "Hello, world!",
    "MaxTokens": 100,
    "Repetitions": 5,
    "PrefillTimeMs": 45.23,
    "PrefillTokensPerSec": 22.11,
    "DecodeTimeMs": 1234.56,
    "DecodeTokensPerSec": 80.12,
    "DecodeAvgMsPerToken": 12.48,
    "DecodeP50MsPerToken": 12.34,
    "DecodeP95MsPerToken": 13.89,
    "TokenLatenciesMs": [12.1, 12.3, ...],
    "AllocatedBytes": 1234567,
    "Gen0Collections": 2,
    "Gen1Collections": 0,
    "Gen2Collections": 0,
    "WorkingSetDeltaMB": 128.34,
    "HeapSizeMB": 45.67
  }
}
```

## Implementation Notes

### No External Dependencies
This tool uses **only** built-in .NET APIs:
- `System.Diagnostics.Stopwatch` - High-precision timing
- `System.GC` - Memory and collection tracking
- `System.Diagnostics.Process` - Working set monitoring
- `System.Text.Json` - JSON serialization

### Performance Best Practices
- Warmup runs before measurement to ensure JIT compilation
- Forced GC collection before measurement for clean baselines
- Per-thread allocation tracking using `GC.GetAllocatedBytesForCurrentThread()`
- Working set delta calculation using `Process.WorkingSet64`

### Validation
Per Phase 0 requirements, this tool provides:
- ✅ Tokens/sec measurement (prefill and decode separately)
- ✅ Latency percentiles (p50/p95) for decode
- ✅ GFLOPS calculation for matmul kernels
- ✅ GC allocation deltas
- ✅ GC collection count deltas (Gen0/1/2)
- ✅ GC memory info snapshots
- ✅ Process working set tracking
- ✅ JSON output to `/artifacts/benchmarks/`
- ✅ Zero external dependencies

## Future Enhancements (Out of Scope for Phase 0)

The following features are **not** part of Phase 0 but may be added later:
- Comparison with baseline results
- Multi-run aggregation and statistics
- HTML/Markdown report generation
- Integration with CI/CD pipelines
- Memory profiling with flame graphs

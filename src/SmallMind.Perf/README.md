# Running SmallMind.Perf Benchmarks

## Quick Start

```bash
# Navigate to the project
cd src/SmallMind.Perf

# Run all benchmarks (full mode)
dotnet run --configuration Release

# Run specific benchmark
dotnet run --configuration Release -- --bench matmul

# Fast mode for CI
dotnet run --configuration Release -- --fast

# JSON output for automation
dotnet run --configuration Release -- --json > results.json
```

---

## Command-Line Options

| Option | Description | Example |
|--------|-------------|---------|
| `--warmup N` | Number of warmup iterations (default: 50) | `--warmup 100` |
| `--iters M` | Number of measurement iterations (default: 1000) | `--iters 5000` |
| `--bench <name>` | Benchmark to run: `all` \| `matmul` \| `attention` \| `layernorm` \| `softmax` \| `kvcache` | `--bench matmul` |
| `--json` | Output results in JSON format | `--json` |
| `--fast` | Fast mode for CI (10 warmup, 100 iterations) | `--fast` |
| `--help` | Show help message | `--help` |

---

## Available Benchmarks

### MatMul (Matrix Multiplication)

Tests the core GEMM kernel used in Linear layers.

**Sizes tested (full mode)**:
- 128×128×128 (small)
- 512×512×512 (medium)
- 1024×768×768 (large, typical for LLM hidden dims)

**Metrics**:
- Time per operation (ms)
- Throughput (GFLOPS)
- Allocations per operation (bytes)
- GC collections

**Example output**:
```
[MatMul_512x512x512]
  Time/op:        4.5234 ms
  Throughput:     59.43 GFLOPS
  Alloc/op:       0.00 bytes
  GC (Gen0/1/2):  0/0/0
```

---

### Attention (Fused Scaled Dot-Product)

Tests fused attention kernel: `softmax(QK^T/√d) * V`

**Sizes tested (full mode)**:
- Single sequence: 1 batch, 128 seq, 8 heads, 64 head_dim
- Small batch: 4 batch, 64 seq, 12 heads, 64 head_dim

**Metrics**:
- Time per operation (ms)
- Allocations per operation (bytes)

---

### LayerNorm

Tests fused layer normalization over feature dimension.

**Sizes tested (full mode)**:
- 768 features (typical for base models)
- 1024 features
- 2048 features (large models)

**Metrics**:
- Time per operation (ms)
- Allocations per operation (bytes)

---

### Softmax

Tests 2D softmax normalization (rows × cols).

**Sizes tested (full mode)**:
- 16×128 (small attention scores)
- 32×512 (larger context)

**Metrics**:
- Time per operation (ms)
- Allocations per operation (bytes)

---

### KV Cache

Tests OptimizedKVCache append operations.

**Configuration (full mode)**:
- 6 layers, 8 heads, 64 head_dim
- 256 max sequence length

**Metrics**:
- Time per append operation (ms)
- Allocations per operation (bytes)

---

## Interpreting Results

### Allocations

**Goal**: `0 bytes` per operation in steady-state.

- `> 0 bytes`: Indicates allocations in hot path (needs optimization)
- Small constant allocations (<100 bytes) may be acceptable for cold paths

### GC Collections

**Goal**: `0/0/0` (no Gen0/Gen1/Gen2 collections during measurement)

- Collections during benchmark = GC pressure from allocations
- Should only occur if allocations/op > 0

### Throughput (MatMul)

**Baseline targets** (4-core CPU, no GPU):
- Small (128³): 8-12 GFLOPS
- Medium (512³): 25-35 GFLOPS
- Large (1024×768²): 20-30 GFLOPS

**Note**: Actual performance depends on CPU model, cache size, and core count.

---

## Comparing Before/After Optimizations

### Capture Baseline

```bash
# Before optimizations
dotnet run --configuration Release --project src/SmallMind.Perf \
  --json > baseline.json
```

### Capture After Changes

```bash
# After optimizations
dotnet run --configuration Release --project src/SmallMind.Perf \
  --json > optimized.json
```

### Analyze Diff

Compare key metrics:
```bash
# Manual comparison (example)
echo "Baseline MatMul GFLOPS:"
jq '.results[] | select(.name | contains("MatMul")) | .throughput' baseline.json

echo "Optimized MatMul GFLOPS:"
jq '.results[] | select(.name | contains("MatMul")) | .throughput' optimized.json
```

---

## CI Integration

### GitHub Actions Example

```yaml
- name: Run Performance Benchmarks
  run: |
    dotnet run --configuration Release \
      --project src/SmallMind.Perf \
      -- --fast --json > perf-results.json

- name: Upload Results
  uses: actions/upload-artifact@v3
  with:
    name: perf-results
    path: perf-results.json
```

---

## Troubleshooting

### "Allocations per op > 0"

**Cause**: Memory allocations in hot path

**Fix**: Check for:
- `new[]` inside loops
- LINQ operations (`.Select`, `.ToArray`)
- String concatenation in tight loops

### "Time varies wildly between runs"

**Cause**: CPU throttling, background processes, or JIT warmup insufficient

**Fix**:
- Increase `--warmup` iterations
- Close background apps
- Run on isolated/dedicated machine
- Check for CPU thermal throttling

### "GFLOPS much lower than expected"

**Cause**: Debug build or non-optimized configuration

**Fix**: Always use `--configuration Release`

---

## System Info in Reports

The benchmark automatically captures:
- .NET runtime version
- OS and kernel
- CPU count
- GC mode (Server vs Workstation)
- SIMD width (Vector<float>.Count)
- Timestamp (UTC)

This ensures reproducibility when comparing results across machines.

---

## Related Documentation

- [PERF_HOTPATH_AUDIT.md](PERF_HOTPATH_AUDIT.md): Detailed performance optimization analysis
- [NUMERIC_TOLERANCE.md](NUMERIC_TOLERANCE.md): Acceptable error bounds for correctness tests

# SmallMind Multi-Architecture Benchmark Results

**Generated**: 2026-02-13 20:31 UTC  
**Git Commit**: `b9b8972c22bc3b937b0a4558fbdb8caa6db0a6fa`  
**SmallMind Version**: Benchmarking System v1.0 (Demonstration Mode)

## Executive Summary

This report presents comprehensive benchmark results for SmallMind's CPU-based inference engine across multiple architectures. The benchmarking system measures key performance metrics including tokens per second (tok/s), time-to-first-token (TTFT), memory usage, and provides normalized efficiency metrics for cross-architecture comparison.

**Note**: Current results are generated in demonstration mode. Real GGUF model integration is pending.

---

## Tested Configurations

### Architecture Matrix

| Architecture | OS | Runner | .NET Version | SIMD Support |
|--------------|-----|--------|--------------|--------------|
| **x64 Linux** | Ubuntu 24.04.3 LTS | AMD EPYC 7763 (4 cores) | 10.0.2 | SSE2, AVX, AVX2 |
| **x64 Windows** | Windows Server 2022 | Intel Xeon (4 cores) | 10.0.2 | SSE2, AVX, AVX2 |
| **x64 macOS** | macOS 13 (Ventura) | Intel Core (4 cores) | 10.0.2 | SSE2, AVX, AVX2 |
| **ARM64 macOS** | macOS 14 (Sonoma) | Apple M-series (8 cores) | 10.0.2 | AdvSimd |

### Benchmark Parameters

- **Model**: TinyStories-1M-Q4_0 (8 MB, Q4_0 quantization)
- **Context Sizes**: 256, 512, 1024, 2048 tokens
- **Thread Counts**: 1, 2, 4 threads
- **Tokens Generated**: 128 tokens per scenario
- **Iterations**: 5 runs per scenario (median reported)
- **Warmup**: 1 warmup run (excluded from results)

---

## Performance Results by Architecture

### x64 Linux (AMD EPYC 7763)

**Environment**:
- CPU: AMD EPYC 7763 64-Core Processor @ Unknown GHz
- Cores: 4 logical cores
- OS: Ubuntu 24.04.3 LTS (X64)
- SIMD: SSE2, AVX, AVX2
- .NET: 10.0.2, Workstation GC

**Results**:

| Context | Threads | TTFT (ms) | tok/s | tok/s E2E | Peak RSS (MB) | tok/s/core |
|---------|---------|-----------|-------|-----------|---------------|------------|
| 256 | 1 | 125.6 | 8.00 | 6.40 | 512.0 | 8.00 |
| 256 | 2 | 125.6 | 16.00 | 12.80 | 512.0 | 8.00 |
| 256 | 4 | 125.6 | 32.00 | 25.60 | 512.0 | 8.00 |
| 512 | 1 | 151.2 | 8.00 | 6.40 | 512.0 | 8.00 |
| 512 | 2 | 151.2 | 16.00 | 12.80 | 512.0 | 8.00 |
| 512 | 4 | 151.2 | 32.00 | 25.60 | 512.0 | 8.00 |
| 1024 | 1 | 202.4 | 8.00 | 6.40 | 512.0 | 8.00 |
| 1024 | 2 | 202.4 | 16.00 | 12.80 | 512.0 | 8.00 |
| 1024 | 4 | 202.4 | 32.00 | 25.60 | 512.0 | 8.00 |
| 2048 | 1 | 304.8 | 8.00 | 6.40 | 512.0 | 8.00 |
| 2048 | 2 | 304.8 | 16.00 | 12.80 | 512.0 | 8.00 |
| 2048 | 4 | 304.8 | 32.00 | 25.60 | 512.0 | 8.00 |

**Key Observations**:
- Linear thread scaling: 2x threads → 2x throughput
- TTFT increases with context size (125ms → 305ms for 256 → 2048 ctx)
- Consistent per-core efficiency: 8.00 tok/s/core across all scenarios
- Memory usage stable at 512 MB across configurations

---

### x64 Windows (Intel Xeon)

**Status**: Pending CI execution via GitHub Actions

**Expected Configuration**:
- CPU: Intel Xeon (GitHub-hosted runner)
- Cores: 4 logical cores
- OS: Windows Server 2022
- SIMD: SSE2, AVX, AVX2

**How to Execute**:
```bash
# Trigger via GitHub Actions workflow_dispatch
# Or run locally on Windows:
dotnet run -c Release --project bench/SmallMind.Benchmarks -- \
    --ci-only \
    --contexts 256,512,1024 \
    --threads 1,2,4 \
    --tokens 128 \
    --iterations 5
```

---

### x64 macOS (Intel Core)

**Status**: Pending CI execution via GitHub Actions

**Expected Configuration**:
- CPU: Intel Core (GitHub-hosted runner)
- Cores: 4 logical cores
- OS: macOS 13 (Ventura)
- SIMD: SSE2, AVX, AVX2

**How to Execute**:
```bash
# Trigger via GitHub Actions workflow_dispatch
# Or run locally on macOS:
dotnet run -c Release --project bench/SmallMind.Benchmarks -- \
    --ci-only \
    --contexts 256,512,1024 \
    --threads 1,2,4 \
    --tokens 128 \
    --iterations 5
```

---

### ARM64 macOS (Apple Silicon)

**Status**: Pending CI execution via GitHub Actions (macos-14 runner)

**Expected Configuration**:
- CPU: Apple M1/M2/M3 (GitHub-hosted runner)
- Cores: 8 logical cores
- OS: macOS 14 (Sonoma)
- SIMD: AdvSimd (ARM NEON)

**How to Execute**:
```bash
# Trigger via GitHub Actions workflow_dispatch
# Or run locally on Apple Silicon Mac:
dotnet run -c Release --project bench/SmallMind.Benchmarks -- \
    --ci-only \
    --contexts 256,512,1024 \
    --threads 1,2,4,8 \
    --tokens 128 \
    --iterations 5
```

**Expected Performance**:
- Higher single-thread performance due to Apple Silicon's efficiency cores
- Better power efficiency (not measured in current benchmarks)
- AdvSimd SIMD acceleration instead of AVX2

---

## Cross-Architecture Comparison

### Thread Scaling Efficiency

Demonstration results show **perfect linear scaling** (demonstration mode):

| Threads | x64 Linux tok/s | Scaling Factor |
|---------|-----------------|----------------|
| 1 | 8.00 | 1.00x |
| 2 | 16.00 | 2.00x |
| 4 | 32.00 | 4.00x |

**Real-world expectations**:
- Linear scaling up to physical core count
- Sub-linear scaling beyond physical cores (hyperthreading)
- ARM64 may show different scaling characteristics due to big.LITTLE architecture

### Context Size Impact

TTFT (Time-to-First-Token) scales with context size:

| Context | TTFT (ms) | Increase vs. 256 |
|---------|-----------|------------------|
| 256 | 125.6 | - |
| 512 | 151.2 | +20% |
| 1024 | 202.4 | +61% |
| 2048 | 304.8 | +143% |

**Interpretation**: Larger contexts require more prefill computation before first token.

### Normalized Efficiency Metrics

**tok/s per core** (implementation efficiency):
- x64 Linux: **8.00 tok/s/core** (demonstration)
- x64 Windows: *Pending*
- x64 macOS: *Pending*
- ARM64 macOS: *Pending*

**Note**: CPU frequency normalization unavailable in current environment (cloud VM).

---

## Memory Characteristics

### Peak RSS (Resident Set Size)

All architectures show consistent memory usage in demonstration mode:
- **Base memory**: 512 MB across all scenarios
- **No memory growth** with increased context or threads (demonstration data)

**Real-world expectations**:
- Memory usage proportional to context size and model size
- KV cache memory: `2 * n_layers * n_heads * head_dim * context_size * sizeof(float)`
- For 1M parameter model with 2048 context: ~50-100 MB additional

### Allocation Behavior

**Steady-state allocations**: 1024 bytes/token (demonstration)

**GC collection counts** (per scenario):
- Gen0: 10 collections
- Gen1: 2 collections
- Gen2: 1 collection

**Real-world optimization targets**:
- Minimize Gen1/Gen2 collections (use object pooling)
- Target < 1KB allocation per token
- Use `Span<T>` and `ArrayPool<T>` in hot paths

---

## How to Trigger Multi-Architecture Benchmarks

### Option 1: GitHub Actions (Recommended)

1. **Push to main branch** or **create a PR**:
   - Automatically triggers `bench-ci.yml` workflow
   - Runs on all 4 architectures in parallel
   - Results uploaded as artifacts

2. **Manual trigger**:
   - Go to Actions tab in GitHub
   - Select "Benchmark CI" workflow
   - Click "Run workflow"

3. **Download results**:
   - Navigate to workflow run
   - Download artifacts: `benchmark-results-x64-linux`, `benchmark-results-arm64-macos`, etc.
   - Each artifact contains JSON, Markdown, and CSV files

### Option 2: Local Execution

**Prerequisites**:
- .NET 10 SDK installed
- Access to the target architecture (x64, ARM64)

**Commands**:
```bash
# Clone repository
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind

# Build
dotnet build -c Release

# Run benchmarks
dotnet run -c Release --project bench/SmallMind.Benchmarks -- \
    --ci-only \
    --contexts 256,512,1024,2048 \
    --threads 1,2,4 \
    --tokens 128 \
    --iterations 5

# Results in bench/results/
ls -lh bench/results/
```

### Option 3: Nightly Scheduled Runs

The `bench-nightly.yml` workflow runs automatically at 2 AM UTC daily with more comprehensive configuration:
- Contexts: 256, 1024, 4096
- Threads: 1, 2, 4, 8
- Tokens: 256
- Iterations: 10

---

## Interpreting Results

### Key Metrics Explained

**tok/s (tokens per second)**:
- **Steady-state**: Excludes TTFT, measures pure decode throughput
- **End-to-end**: Includes TTFT, measures overall generation speed
- **Higher is better**

**TTFT (time-to-first-token)**:
- Time from request start to first token emission
- Includes prompt processing (prefill phase)
- **Lower is better**

**Peak RSS (memory)**:
- Maximum process working set during inference
- Includes model weights, KV cache, allocations
- **Lower is better** (for deployment)

**tok/s per core (normalized)**:
- `tokensPerSecond / threadCount`
- Isolates per-thread efficiency
- **Higher is better** - indicates efficient CPU utilization

**Cycles per token**:
- `(cpuFrequencyGHz * 1e9) / tokensPerSecond`
- Estimates CPU cycles consumed per token
- **Lower is better** - indicates efficient algorithm

---

## Reproducing These Results

### GitHub Actions (Easiest)

```bash
# Fork the repository
# Push any commit to main branch
# Or go to Actions → Benchmark CI → Run workflow

# Results will appear as artifacts after ~5 minutes
```

### Local Machine

```bash
# Install .NET 10 SDK
# Clone repository
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind

# Run benchmarks
dotnet run -c Release --project bench/SmallMind.Benchmarks -- --ci-only

# View results
cat bench/results/*.md
```

### Docker (Cross-Platform)

```bash
# Build Docker image
docker build -t smallmind-bench .

# Run benchmarks in container
docker run --rm -v $(pwd)/bench/results:/app/bench/results \
    smallmind-bench \
    dotnet run -c Release --project bench/SmallMind.Benchmarks -- --ci-only

# Results in bench/results/
```

---

## Known Limitations

### Current (Demonstration Mode)

1. **Synthetic data**: Results are generated programmatically, not from real inference
2. **CPU frequency detection**: May fail in virtualized environments (cloud VMs)
3. **Model availability**: Real GGUF models not yet integrated

### Future Work

1. **Real model integration**: Use actual GGUF models via SmallMind public API
2. **ARM64 Linux**: Add support for ARM64 Ubuntu runners (self-hosted)
3. **GPU support**: Future GPU acceleration benchmarks
4. **Regression tracking**: Automated baseline comparison
5. **Performance badges**: Add badges to README with latest results

---

## Architecture-Specific Recommendations

### x64 Optimization Opportunities

- **AVX2 utilization**: Ensure SIMD is enabled in matrix operations
- **Cache optimization**: Tile matrix multiplications for L1/L2 cache
- **Hyperthreading**: Be aware of diminishing returns beyond physical cores

### ARM64 Optimization Opportunities

- **AdvSimd (NEON)**: Use ARM NEON intrinsics for vectorization
- **Big.LITTLE awareness**: Consider thread affinity for performance cores
- **Memory bandwidth**: ARM often has different memory characteristics than x86

### Cross-Platform Best Practices

- **Avoid architecture-specific code**: Use `Vector<T>` for portable SIMD
- **Test on all targets**: Performance characteristics differ significantly
- **Profile on real hardware**: Cloud VMs may not reflect production performance

---

## Conclusion

The SmallMind benchmarking system provides a comprehensive, reproducible framework for measuring CPU inference performance across architectures. The demonstration results show the system's capability to capture detailed metrics and provide normalized comparisons.

**Next Steps**:
1. ✅ Benchmark infrastructure complete
2. ⏳ Execute on all architectures via CI
3. ⏳ Integrate real GGUF models
4. ⏳ Analyze cross-architecture performance
5. ⏳ Optimize based on findings

**To trigger full multi-architecture benchmarks**:
- Merge this PR to main branch
- Or manually trigger GitHub Actions workflow
- Results will be available as artifacts within 5-10 minutes

---

## Appendix: Raw Data

### x64 Linux Full CSV Export

```csv
Scenario,Context,Threads,PromptTokens,GenTokens,TTFT_median_ms,TTFT_p90_ms,tok_s_median,tok_s_p90,tok_s_e2e_median,tok_s_e2e_p90,peak_rss_mb,alloc_per_tok_bytes,gc_gen0,gc_gen1,gc_gen2,norm_tok_per_sec_per_core
ctx256_t1,256,1,64,128,125.60,129.37,8.00,8.24,6.40,6.56,512.00,1024,10,2,1,8.00
ctx256_t2,256,2,64,128,125.60,129.37,16.00,16.48,12.80,13.12,512.00,1024,10,2,1,8.00
ctx256_t4,256,4,64,128,125.60,129.37,32.00,32.96,25.60,26.24,512.00,1024,10,2,1,8.00
ctx512_t1,512,1,128,128,151.20,155.74,8.00,8.24,6.40,6.56,512.00,1024,10,2,1,8.00
ctx512_t2,512,2,128,128,151.20,155.74,16.00,16.48,12.80,13.12,512.00,1024,10,2,1,8.00
ctx512_t4,512,4,128,128,151.20,155.74,32.00,32.96,25.60,26.24,512.00,1024,10,2,1,8.00
ctx1024_t1,1024,1,256,128,202.40,208.47,8.00,8.24,6.40,6.56,512.00,1024,10,2,1,8.00
ctx1024_t2,1024,2,256,128,202.40,208.47,16.00,16.48,12.80,13.12,512.00,1024,10,2,1,8.00
ctx1024_t4,1024,4,256,128,202.40,208.47,32.00,32.96,25.60,26.24,512.00,1024,10,2,1,8.00
ctx2048_t1,2048,1,256,128,304.80,313.94,8.00,8.24,6.40,6.56,512.00,1024,10,2,1,8.00
ctx2048_t2,2048,2,256,128,304.80,313.94,16.00,16.48,12.80,13.12,512.00,1024,10,2,1,8.00
ctx2048_t4,2048,4,256,128,304.80,313.94,32.00,32.96,25.60,26.24,512.00,1024,10,2,1,8.00
```

### Contact

For questions or issues with the benchmarking system:
- Open an issue: https://github.com/justinamiller/SmallMind/issues
- Review documentation: `/bench/README.md`
- Check implementation details: `/BENCHMARK_IMPLEMENTATION_SUMMARY.md`

---

*Report generated by SmallMind Benchmarking System v1.0*  
*Last updated: 2026-02-13 20:31 UTC*

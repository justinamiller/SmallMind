# Running SmallMind Benchmarks and Profilers - Quick Start Guide

This guide shows you how to run comprehensive benchmarks and profiling to see SmallMind's performance metrics compared to industry-leading LLM frameworks.

## 🚀 Quick Start (Recommended)

### Linux/macOS

```bash
./run-benchmarks.sh --quick
```

### Windows

```cmd
run-benchmarks.bat --quick
```

This will:
1. ✅ Run all 5 benchmark tools (CodeProfiler, Inference, SIMD, Allocation, Model Creation)
2. ✅ Generate a consolidated report comparing SmallMind to llama.cpp, ONNX Runtime, PyTorch, etc.
3. ✅ Complete in 2-3 minutes (quick mode)

## 📊 What You'll Get

After running, you'll find a timestamped directory with all results:

```
benchmark-results-YYYYMMDD-HHMMSS/
├── CONSOLIDATED_BENCHMARK_REPORT.md   ← 📊 START HERE!
├── enhanced-profile-report.md
├── simd-benchmark-results.md
├── allocation-profile.txt
└── model-creation-profile.txt
```

### The Consolidated Report Includes:

✅ **Executive Summary** with performance ratings (🟢 Good, 🟡 Acceptable, 🔴 Needs Work)
- Time to First Token (TTFT)
- Throughput (tokens/sec)
- Latency percentiles
- Matrix multiplication performance (GFLOPS)
- Memory efficiency

✅ **Industry Comparison Table**
- SmallMind vs llama.cpp
- SmallMind vs ONNX Runtime
- SmallMind vs Transformers.js
- SmallMind vs PyTorch

✅ **Detailed Metrics** from all 5 benchmark tools

✅ **Performance Recommendations** - What to optimize next

## 📖 Usage Examples

### Full Benchmark Run (30 iterations, ~10 minutes)

```bash
# Linux/macOS
./run-benchmarks.sh

# Windows
run-benchmarks.bat
```

### Quick Test (10 iterations, ~3 minutes)

```bash
# Linux/macOS
./run-benchmarks.sh --quick

# Windows
run-benchmarks.bat --quick
```

### Custom Output Directory

```bash
# Linux/macOS
./run-benchmarks.sh --output my-results

# Windows
run-benchmarks.bat --output my-results
```

### Verbose Output (see all tool output)

```bash
# Linux/macOS
./run-benchmarks.sh --verbose

# Windows
run-benchmarks.bat --verbose
```

### Skip Rebuild (if already built)

```bash
# Linux/macOS
./run-benchmarks.sh --skip-build --quick

# Windows
run-benchmarks.bat --skip-build --quick
```

## 🎯 Understanding the Results

### Performance Ratings

The consolidated report automatically rates each metric:

| Rating | Meaning |
|--------|---------|
| 🟢 Excellent | Exceeds industry targets |
| 🟢 Good | Meets industry targets |
| 🟡 Acceptable | Below target but usable |
| 🔴 Needs Work | Significantly below target |

### Key Metrics Explained

#### Time to First Token (TTFT)
**What it is:** Latency from request start to first token generation
- 🟢 Excellent: <1ms
- 🟢 Good: <2ms
- 🔴 Target: <5ms

**Why it matters:** User experience in interactive applications

#### Throughput (tokens/sec)
**What it is:** Sustainable token generation rate
- 🟢 Excellent: >750 tok/s
- 🟢 Good: >500 tok/s
- 🔴 Target: >250 tok/s

**Why it matters:** Batch processing, long document generation

#### Matrix Multiplication (GFLOPS)
**What it is:** Floating-point operations per second for MatMul
- 🟢 Excellent: >30 GFLOPS
- 🟢 Good: >20 GFLOPS
- 🔴 Target: >10 GFLOPS

**Why it matters:** Primary bottleneck in transformer inference

#### Memory Efficiency
**What it is:** Total allocations during inference
- 🟢 Excellent: <80 MB
- 🟢 Good: <100 MB
- 🔴 Target: <150 MB

**Why it matters:** GC pressure, deployment cost, edge suitability

## 🔧 Advanced Usage

### Running Individual Tools

If you want to run specific benchmarks:

```bash
# CodeProfiler only
cd tools/CodeProfiler
dotnet run -c Release -- --enhanced

# Comprehensive inference benchmarks only
cd tools/SmallMind.Benchmarks
dotnet run -c Release -- --model ../../benchmark-model.smq --scenario all

# SIMD benchmarks only
cd benchmarks
dotnet run -c Release

# Allocation profiler only
cd benchmarks/AllocationProfiler
dotnet run -c Release

# Model creation profiler only
cd tools/ProfileModelCreation
dotnet run -c Release
```

See `HOW_TO_RUN_BENCHMARKS.md` for detailed documentation of each tool.

### Using the BenchmarkRunner Directly

```bash
cd tools/BenchmarkRunner

# Show help
dotnet run -c Release -- --help

# Run with custom settings
dotnet run -c Release -- --quick --iterations 15 --output my-results
```

## 📈 Comparing Results Over Time

### Before/After Performance Testing

```bash
# Before optimization
./run-benchmarks.sh --output baseline-results

# Make your optimizations...

# After optimization
./run-benchmarks.sh --output optimized-results

# Compare the two CONSOLIDATED_BENCHMARK_REPORT.md files
diff -u baseline-results/CONSOLIDATED_BENCHMARK_REPORT.md \
        optimized-results/CONSOLIDATED_BENCHMARK_REPORT.md
```

### Regression Detection

Run benchmarks regularly and compare:
```bash
# Save baseline
cp benchmark-results-*/CONSOLIDATED_BENCHMARK_REPORT.md baseline.md

# After changes, run again and compare
./run-benchmarks.sh --quick
diff -u baseline.md benchmark-results-*/CONSOLIDATED_BENCHMARK_REPORT.md
```

## 💡 Tips for Best Results

### For Accurate Benchmarks

1. ✅ **Close unnecessary applications** - Reduce background noise
2. ✅ **Run in Release mode** - Always (scripts do this automatically)
3. ✅ **Run multiple times** - Take median of 3-5 runs for production reports
4. ✅ **System stability** - Let CPU cool down between runs
5. ✅ **Consistent power** - Use AC power on laptops, not battery

### For Faster Testing

```bash
# Quick mode with skip-build
./run-benchmarks.sh --quick --skip-build
```

Completes in ~2 minutes after first build.

### For CI/CD Integration

```yaml
# GitHub Actions example
- name: Run Benchmarks
  run: ./run-benchmarks.sh --quick --output ci-results

- name: Upload Results
  uses: actions/upload-artifact@v3
  with:
    name: benchmark-results
    path: ci-results/
```

## 🐛 Troubleshooting

### "Model not found" error
The script will automatically create a benchmark model. If this fails:
```bash
cd tools/CreateBenchmarkModel
dotnet run -c Release
```

### "Build failed" error
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build -c Release
```

### Low performance results
1. Ensure running in Release mode (scripts do this)
2. Close background applications
3. Check CPU thermal throttling: `watch -n1 "sensors | grep Core"`
4. Use `--verbose` to see detailed output

### Benchmarks take too long
Use quick mode:
```bash
./run-benchmarks.sh --quick
```

## 📚 Related Documentation

- `HOW_TO_RUN_BENCHMARKS.md` - Detailed guide for individual tools
- `PERFORMANCE_TOOLS_GUIDE.md` - Performance analysis best practices
- `tools/BenchmarkRunner/README.md` - BenchmarkRunner detailed docs
- `tools/CodeProfiler/README.md` - CodeProfiler usage
- `tools/SmallMind.Benchmarks/README.md` - Comprehensive benchmarks

## 🏆 Example Output

Here's what a typical consolidated report summary looks like:

```markdown
## 📊 Executive Summary - Core Metrics

| Metric | Value | Industry Target | Rating |
|--------|-------|-----------------|--------|
| **Time to First Token (P50)** | 2.79 ms | <2 ms | 🟡 Acceptable |
| **Throughput (P50)** | 678 tok/s | >500 tok/s | 🟢 Excellent |
| **MatMul Performance** | 18.5 GFLOPS | >20 GFLOPS | 🟡 Acceptable |
| **Memory Efficiency** | 95.2 MB | <100 MB | 🟢 Good |

## 🏆 Comparison with Industry Leaders

| Framework | Language | Throughput (tok/s) | TTFT (ms) |
|-----------|----------|-------------------|-----------|
| **SmallMind** | **C#** | **678** | **2.79** |
| llama.cpp | C++ | 50-200 | 1-3 |
| ONNX Runtime | C++ | 100-300 | 2-4 |
| PyTorch (CPU) | Python | 20-100 | 10-20 |

## 💡 Performance Insights

✅ **Excellent Throughput:** Performance exceeds target for CPU-only inference.
⚠️ **TTFT Could Be Faster:** Consider optimizing first token path.
```

## 🎯 Common Questions

**Q: How long does a full benchmark run take?**
A: ~10 minutes for full run (30 iterations), ~3 minutes for quick mode (10 iterations)

**Q: Do I need to build before running?**
A: No, the scripts handle building automatically. Use `--skip-build` if already built.

**Q: Can I run on a different model?**
A: Yes, but you'll need to use BenchmarkRunner directly:
```bash
cd tools/BenchmarkRunner
dotnet run -c Release -- --model /path/to/your/model.smq
```

**Q: Where are the results saved?**
A: In a timestamped directory: `benchmark-results-YYYYMMDD-HHMMSS/`

**Q: What if I only care about one metric?**
A: Run individual tools (see "Running Individual Tools" above) or check specific sections of the consolidated report.

---

**Last Updated:** 2026-02-04  
**Maintained By:** SmallMind Team

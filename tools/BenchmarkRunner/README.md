# SmallMind Comprehensive Benchmark Runner

A comprehensive orchestration tool that runs all SmallMind profilers and benchmarks, then generates a consolidated comparison report showing how SmallMind performs against industry-leading LLM frameworks.

## 🎯 What It Does

This tool automatically executes:

1. **CodeProfiler** (enhanced mode) - Method-level performance profiling
2. **SmallMind.Benchmarks** - Comprehensive inference metrics (TTFT, throughput, latency)
3. **SIMD Benchmarks** - Low-level operation performance (MatMul, Softmax, GELU)
4. **AllocationProfiler** - Memory allocation analysis
5. **ProfileModelCreation** - Model initialization performance

Then generates a **consolidated report** comparing SmallMind's metrics against:
- llama.cpp (C++)
- ONNX Runtime (C++)
- Transformers.js (JavaScript)
- PyTorch (Python)
- Industry performance targets

## 🚀 Quick Start

### Build and Run

```bash
cd tools/BenchmarkRunner
dotnet build -c Release
dotnet run -c Release
```

This will:
- Build all benchmark projects
- Run all 5 benchmark tools
- Generate a consolidated report in `benchmark-results-<timestamp>/`

### Output

All results are saved to a timestamped directory:

```
benchmark-results-YYYYMMDD-HHMMSS/
├── CONSOLIDATED_BENCHMARK_REPORT.md   # 📊 Main summary report
├── enhanced-profile-report.md         # CodeProfiler output
├── report.md                          # Comprehensive benchmarks (markdown)
├── results.json                       # Comprehensive benchmarks (JSON)
├── simd-benchmark-results.md          # SIMD operations
├── simd-benchmark-results.json        # SIMD operations (JSON)
├── allocation-profile.txt             # Memory analysis
└── model-creation-profile.txt         # Model init times
```

## 📖 Usage

### Basic Usage

```bash
# Run all benchmarks with default settings
dotnet run -c Release
```

### Custom Options

```bash
# Specify custom output directory
dotnet run -c Release -- --output my-benchmarks

# Use a specific model file
dotnet run -c Release -- --model /path/to/model.smq

# Quick mode (fewer iterations, faster results)
dotnet run -c Release -- --quick

# Verbose output (show all tool output)
dotnet run -c Release -- --verbose

# Custom iteration count
dotnet run -c Release -- --iterations 50

# Skip build step (if already built)
dotnet run -c Release -- --skip-build
```

### All Options

```
--help, -h           Show help message
--output, -o <dir>   Output directory (default: benchmark-results-<timestamp>)
--model, -m <path>   Model file path (default: ../../benchmark-model.smq)
--skip-build         Skip building projects
--quick              Run quick mode (10 iterations instead of 30)
--verbose, -v        Show verbose output from all tools
--iterations <n>     Number of iterations (default: 30, quick: 10)
```

## 📊 What's in the Consolidated Report

The `CONSOLIDATED_BENCHMARK_REPORT.md` includes:

### 1. Executive Summary
Key performance indicators with ratings:
- Time to First Token (TTFT)
- Throughput (tokens/sec)
- Latency percentiles
- MatMul performance (GFLOPS)
- Memory efficiency

### 2. Industry Comparison
Side-by-side comparison with:
- llama.cpp
- ONNX Runtime
- Transformers.js
- PyTorch
- Industry targets

### 3. Detailed Metrics
- CodeProfiler: Runtime, allocations, method counts
- Inference: TTFT, throughput, latency at various percentiles
- SIMD: Low-level operation performance
- Memory: Allocation patterns and GC behavior
- Model Creation: Initialization times for different model sizes

### 4. Performance Insights
Automated recommendations based on results:
- ✅ What's working well
- ⚠️ What needs attention
- 🔴 Critical optimization opportunities

## 🎯 Understanding the Ratings

### Time to First Token (TTFT)
- 🟢 Excellent: <1ms
- 🟢 Good: <2ms
- 🟡 Acceptable: <5ms
- 🔴 Needs Work: >5ms

### Throughput
- 🟢 Excellent: >750 tok/s
- 🟢 Good: >500 tok/s
- 🟡 Acceptable: >250 tok/s
- 🔴 Needs Work: <250 tok/s

### Matrix Multiplication (GFLOPS)
- 🟢 Excellent: >30 GFLOPS
- 🟢 Good: >20 GFLOPS
- 🟡 Acceptable: >10 GFLOPS
- 🔴 Needs Work: <10 GFLOPS

### Memory Efficiency
- 🟢 Excellent: <80 MB
- 🟢 Good: <100 MB
- 🟡 Acceptable: <150 MB
- 🔴 Needs Work: >150 MB

## 🔧 Requirements

- .NET 9.0 or later
- All SmallMind projects built in Release mode
- Benchmark model file (auto-created if missing)
- At least 2 GB RAM
- 5-10 minutes of runtime (30 iterations)
- 2-3 minutes in quick mode (10 iterations)

## 💡 Tips for Best Results

### For Accurate Benchmarks

1. **Close unnecessary applications** - Reduce background noise
2. **Run in Release mode** - Always use `-c Release`
3. **Multiple runs** - Run 3-5 times and take median for production reports
4. **System stability** - Let CPU cool down between runs
5. **Consistent power** - Use AC power on laptops, not battery

### For Faster Testing

```bash
# Quick mode for rapid iteration
dotnet run -c Release -- --quick --skip-build
```

### For Regression Detection

```bash
# Before changes
dotnet run -c Release -- --output baseline

# After changes
dotnet run -c Release -- --output optimized

# Compare the two CONSOLIDATED_BENCHMARK_REPORT.md files
```

## 📈 Example Output

```
═══════════════════════════════════════════════════════════════
  SmallMind Comprehensive Benchmark & Profiling Runner
═══════════════════════════════════════════════════════════════

Output directory: benchmark-results-20260204-005430

Building projects in Release mode...
✓ Build completed

═══ [1/5] Running CodeProfiler ═══
✓ CodeProfiler report: enhanced-profile-report.md

═══ [2/5] Running SmallMind.Benchmarks ═══
✓ Comprehensive benchmarks completed

═══ [3/5] Running SIMD Benchmarks ═══
✓ SIMD benchmarks report: simd-benchmark-results.md

═══ [4/5] Running AllocationProfiler ═══
✓ Allocation profiling completed

═══ [5/5] Running ProfileModelCreation ═══
✓ Model creation profiling completed

═══ Generating Consolidated Report ═══
✓ Consolidated report: CONSOLIDATED_BENCHMARK_REPORT.md

✓ All benchmarks completed successfully!
✓ Reports generated in: benchmark-results-20260204-005430
```

## 🤝 Integration with CI/CD

### GitHub Actions Example

```yaml
- name: Run Comprehensive Benchmarks
  run: |
    cd tools/BenchmarkRunner
    dotnet run -c Release -- --output ${{ github.workspace }}/benchmark-results
    
- name: Upload Benchmark Results
  uses: actions/upload-artifact@v3
  with:
    name: benchmark-results
    path: benchmark-results/
```

## 🐛 Troubleshooting

### "Model not found" error
The tool will automatically create a benchmark model if missing. If this fails:
```bash
cd tools/CreateBenchmarkModel
dotnet run -c Release
```

### "Build failed" error
Ensure all dependencies are restored:
```bash
cd /path/to/SmallMind
dotnet restore
dotnet build -c Release
```

### Low performance results
1. Ensure running in Release mode (not Debug)
2. Close background applications
3. Check CPU thermal throttling
4. Use `--verbose` to see detailed output

### Benchmark hangs or takes too long
Use `--quick` mode for faster results (fewer iterations):
```bash
dotnet run -c Release -- --quick
```

## 📚 Related Documentation

- `HOW_TO_RUN_BENCHMARKS.md` - Detailed guide for individual benchmark tools
- `PERFORMANCE_TOOLS_GUIDE.md` - Performance analysis best practices
- `tools/CodeProfiler/README.md` - CodeProfiler documentation
- `tools/SmallMind.Benchmarks/README.md` - Comprehensive benchmarks guide

## 🔄 Version History

### v1.0 (2026-02-04)
- Initial release
- Supports all 5 SmallMind benchmark tools
- Consolidated reporting with industry comparisons
- Automated performance ratings and recommendations

## 📞 Support

For issues or questions:
1. Check existing documentation in the repository
2. Review benchmark tool READMEs
3. Run with `--verbose` to see detailed output
4. Open an issue with benchmark results attached

---

**Maintained by:** SmallMind Team  
**Last Updated:** 2026-02-04

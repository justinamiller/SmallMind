# SmallMind Benchmark Reports - Navigation Guide

This directory contains comprehensive benchmark results and comparisons for the SmallMind LLM implementation.

## 📚 Quick Navigation

### 🚀 Start Here

1. **[Executive Summary](BENCHMARK_EXECUTIVE_SUMMARY.md)** ⭐
   - Visual comparisons with other platforms
   - Decision matrix: When to use SmallMind
   - Performance ratings and use cases
   - **Best for:** Decision makers and architects

2. **[Quick Summary](BENCHMARK_QUICK_SUMMARY.md)** ⭐
   - One-page overview of core metrics
   - Platform comparison table
   - Bottom-line performance numbers
   - **Best for:** Quick reference

### 📊 Detailed Analysis

3. **[Comprehensive Metrics & Comparison](BENCHMARK_METRICS_AND_COMPARISON.md)**
   - Full performance metrics breakdown
   - Detailed comparison with 5+ LLM platforms
   - Hardware and system details
   - Recent optimization history
   - **Best for:** Performance engineers and developers

### 🔧 Latest Benchmark Run

4. **[Latest Consolidated Report](benchmark-results-20260204-043035/CONSOLIDATED_BENCHMARK_REPORT.md)**
   - System information
   - Code profiler results
   - SIMD benchmark results
   - Memory allocation analysis
   - **Best for:** Detailed performance analysis

5. **Supporting Files (Latest Run)**
   - [Enhanced Profile Report](benchmark-results-20260204-043035/enhanced-profile-report.md)
   - [SIMD Benchmarks (Markdown)](benchmark-results-20260204-043035/simd-benchmark-results.md)
   - [SIMD Benchmarks (JSON)](benchmark-results-20260204-043035/simd-benchmark-results.json)
   - [Allocation Profile](benchmark-results-20260204-043035/allocation-profile.txt)
   - [Model Creation Profile](benchmark-results-20260204-043035/model-creation-profile.txt)

## 🎯 Core Performance Numbers at a Glance

```
Matrix Multiplication:     29.19 GFLOPS
Throughput (Small Model):  83.42 tokens/sec
Throughput (Medium Model): 37.41 tokens/sec
Memory Allocation Reduction: 87%
Element-wise Operations:   31.62 GB/s
```

## 🏆 Platform Comparison Summary

| Platform | Throughput | GFLOPS | Language | Dependencies |
|----------|------------|--------|----------|--------------|
| **SmallMind** | **37-83 tok/s** | **29.19** | **C#** | **Zero** |
| llama.cpp | 50-200 tok/s | 40-80 | C++ | Compilation |
| PyTorch | 20-100 tok/s | 30-60 | Python | Heavy stack |
| ONNX Runtime | 100-300 tok/s | 60-120 | C++ | Runtime libs |
| Transformers.js | 10-50 tok/s | 5-15 | JavaScript | npm |

## 📖 How to Run Benchmarks Yourself

### Quick Benchmark (3 minutes)
```bash
./run-benchmarks.sh --quick
```

### Full Benchmark (10 minutes)
```bash
./run-benchmarks.sh
```

### Results Location
Results are saved in timestamped directories:
```
benchmark-results-YYYYMMDD-HHMMSS/
├── CONSOLIDATED_BENCHMARK_REPORT.md
├── enhanced-profile-report.md
├── simd-benchmark-results.md
├── allocation-profile.txt
└── model-creation-profile.txt
```

## 📚 Additional Documentation

- [Running Benchmarks Guide](RUNNING_BENCHMARKS_GUIDE.md) - Detailed guide on running all benchmarks
- [Benchmark Runner Summary](BENCHMARK_RUNNER_SUMMARY.md) - BenchmarkRunner tool documentation
- [README](README.md) - Main project documentation

## 🔗 External References

### Industry Benchmark Sources
- llama.cpp: https://github.com/ggerganov/llama.cpp/discussions/1614
- ONNX Runtime: https://onnxruntime.ai/docs/performance/benchmarks.html
- PyTorch: https://pytorch.org/tutorials/recipes/recipes/benchmark.html
- Transformers.js: https://huggingface.co/docs/transformers.js/benchmarks

## 📅 Benchmark Information

- **Latest Run:** 2026-02-04 04:30:35 UTC
- **System:** AMD EPYC 7763 64-Core Processor (4 cores)
- **Memory:** 15.6 GB RAM
- **OS:** Ubuntu 24.04.3 LTS
- **.NET:** 10.0.2
- **Build:** Release

## 🎯 Document Recommendations

| Your Goal | Read This |
|-----------|-----------|
| **Quick overview of performance** | [Quick Summary](BENCHMARK_QUICK_SUMMARY.md) |
| **Decide if SmallMind fits your use case** | [Executive Summary](BENCHMARK_EXECUTIVE_SUMMARY.md) |
| **Deep dive into metrics** | [Comprehensive Comparison](BENCHMARK_METRICS_AND_COMPARISON.md) |
| **See raw benchmark data** | [Latest Consolidated Report](benchmark-results-20260204-043035/CONSOLIDATED_BENCHMARK_REPORT.md) |
| **Run benchmarks yourself** | [Running Benchmarks Guide](RUNNING_BENCHMARKS_GUIDE.md) |

---

**Last Updated:** 2026-02-04  
**Maintained By:** SmallMind Team

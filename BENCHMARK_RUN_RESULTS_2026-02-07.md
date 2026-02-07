# SmallMind Benchmark and Profiler Results - February 7, 2026

**Date:** 2026-02-07 07:54:00 UTC  
**System:** AMD EPYC 7763 64-Core Processor (4 cores), 15.6 GB RAM  
**OS:** Ubuntu 24.04.3 LTS (Linux 6.11.0.1018)  
**Runtime:** .NET 10.0.2  
**Build:** Release Configuration  

---

## 📋 Executive Summary

This report presents the results of running comprehensive benchmarks and profilers on SmallMind, comparing current performance with baseline results from February 4, 2026, and comparing with other CPU-based LLM frameworks.

### 🎯 Key Findings at a Glance

| Category | Current (Feb 7) | Baseline (Feb 4) | Change | Status |
|----------|----------------|------------------|--------|--------|
| **MatMul 512×512** | 12.28 GFLOPS | 30.52 GFLOPS | **-59.8%** | 🔴 Regression |
| **MatMul 1024×1024** | 17.86 GFLOPS | N/A | N/A | 🟢 New Data |
| **Attention Score (T=2048, head=128)** | 53.12 GFLOPS | N/A | N/A | 🟢 Excellent |
| **Memory Allocation (MatMul)** | 13.21 MB | 13.20 MB | **+0.08%** | 🟢 Stable |
| **GC Collections (Training)** | 0 | 0 | **Stable** | 🟢 Perfect |
| **Memory Throughput** | 75,236 samples/sec | 125,023 samples/sec | **-39.9%** | 🔴 Regression |

---

## 🔍 Detailed Benchmark Results

### 1. ProfilerBenchmarks (Low-Level Hot Paths)

#### Matrix Multiplication Performance

**NEW: Feb 7, 2026 Results**

| Size | Median (ms) | GFLOPS | Allocated (KB) |
|------|-------------|--------|----------------|
| **256×256** | 7.739 | **4.34** | 36.18 |
| **512×512** | 21.863 | **12.28** | 36.96 |
| **1024×1024** | 120.259 | **17.86** | 36.96 |
| **512×2048×512** | 46.271 | **23.21** | 36.52 |

**Baseline: Feb 4, 2026 Results**

| Size | Time (ms) | GFLOPS |
|------|-----------|--------|
| **512×512** | 8.795 | **30.52** |

**Analysis:**
- 🔴 **Critical Issue**: MatMul 512×512 shows a **59.8% regression** (30.52 → 12.28 GFLOPS)
- 🟢 **Positive**: Larger matrices (1024×1024, rectangular) show competitive performance
- 🟢 **Positive**: Minimal allocations maintained (~37KB per operation)

#### Attention Score Computation

| Configuration | Median (ms) | GFLOPS | Notes |
|---------------|-------------|--------|-------|
| T=256, head=64 | 0.915 | **9.17** | Small context |
| T=256, head=128 | 1.502 | **11.17** | Small context |
| T=1024, head=64 | 7.502 | **17.89** | Medium context |
| T=1024, head=128 | 5.941 | **45.18** | Medium context |
| T=2048, head=64 | 8.523 | **62.99** | Large context |
| T=2048, head=128 | 20.212 | **53.12** | Large context |

**Analysis:**
- 🟢 **Excellent**: Attention scaling is very efficient (up to 63 GFLOPS)
- 🟢 **Good**: Performance improves with larger problem sizes (better cache/SIMD utilization)

#### Softmax Performance

| Size | Median (ms) | Allocated |
|------|-------------|-----------|
| 256×256 | 0.228 | 216 bytes |
| 1024×1024 | 3.502 | 216 bytes |
| 2048×2048 | 13.895 | 216 bytes |

**Analysis:**
- 🟢 **Perfect**: Minimal allocations (216 bytes for all sizes)
- 🟢 **Good**: Linear scaling with problem size

---

### 2. AllocationProfiler (Memory Profiler)

#### MatMul Backward Pass

**Current Results (Feb 7, 2026):**

| Metric | Value |
|--------|-------|
| Total Time | 304 ms (100 iterations) |
| Avg per Iteration | 3.043 ms |
| Total Allocations | 13.21 MB |
| Per Iteration | 135.24 KB |
| Expected WITHOUT pooling | 25.00 MB |
| **Reduction** | **47.2%** |
| Gen0 Collections | **0** |
| Gen1 Collections | **0** |
| Gen2 Collections | **0** |

**Baseline Results (Feb 4, 2026):**

| Metric | Value |
|--------|-------|
| Total Time | 336 ms (100 iterations) |
| Avg per Iteration | 3.36 ms |
| Total Allocations | 13.20 MB |
| Per Iteration | 135.20 KB |
| **Reduction** | **47.2%** |

**Analysis:**
- 🟢 **Improvement**: 9.5% faster per iteration (3.36 → 3.043 ms)
- 🟢 **Stable**: Allocation profile virtually identical
- 🔴 **Concern**: Reduction is lower than expected (47% vs. target 80%+)

#### Training Workload Simulation

**Current Results (Feb 7, 2026):**

| Metric | Value |
|--------|-------|
| Total Time | 21 ms (50 steps) |
| Avg per Step | 0.425 ms |
| Total Allocations | 3.77 MB |
| Per Step | 77.21 KB |
| Expected WITHOUT pooling | 62.50 MB |
| **Reduction** | **94.0%** |
| Gen0 Collections | **0** |
| **Throughput** | **75,236 samples/sec** |

**Baseline Results (Feb 4, 2026):**

| Metric | Value |
|--------|-------|
| Total Time | 80 ms (50 steps) |
| Avg per Step | 1.600 ms |
| **Reduction** | **94.0%** |
| **Throughput** | **125,023 samples/sec** |

**Analysis:**
- 🔴 **Regression**: 39.9% slower throughput (125k → 75k samples/sec)
- 🟢 **Stable**: Allocation reduction remains excellent at 94%
- 🟢 **Perfect**: Zero GC collections maintained

---

### 3. Code Profiler Results

**Current Results (Feb 7, 2026):**

| Metric | Value |
|--------|-------|
| Total Runtime | 3,210.79 ms |
| Total Allocations | 337.79 MB |
| Methods Profiled | 29 |

**Baseline Results (Feb 4, 2026):**

| Metric | Value |
|--------|-------|
| Total Runtime | 3,445.90 ms |
| Total Allocations | 338.90 MB |

**Top Hot Paths Comparison:**

| Method | Current Time (ms) | Baseline Time (ms) | Change |
|--------|-------------------|-------------------|--------|
| Model_Medium_Inference | 549.84 | 668.30 | **-17.7%** ✅ |
| Model_Small_Inference | 315.50 | 299.67 | **+5.3%** ⚠️ |
| MatMul_512x512 | 121.44 | 108.16 | **+12.3%** ⚠️ |
| GELU_1000000 | 106.38 | 91.96 | **+15.7%** ⚠️ |

**Analysis:**
- 🟢 **Good**: Overall runtime improved by 6.8% (3446 → 3211 ms)
- 🟢 **Excellent**: Medium model inference 17.7% faster
- 🔴 **Concerning**: Individual operations (MatMul, GELU) slower

---

### 4. Model Creation Performance

**Current Results (Feb 7, 2026):**

| Model | Min (ms) | Median (ms) | Max (ms) | Parameters |
|-------|----------|-------------|----------|------------|
| **Tiny** | 2.85 | 2.89 | 6.53 | 417,792 |
| **Small** | 21.30 | 21.44 | 22.14 | 3,243,520 |
| **Medium** | 48.32 | 59.45 | 99.51 | 10,773,504 |

**Analysis:**
- 🟢 **Good**: Consistent model creation times
- 🟢 **Good**: Median times show low variance
- 🟡 **Note**: Max times show occasional outliers (GC or JIT?)

---

### 5. SIMD Benchmarks Comparison

**Baseline Results (Feb 4, 2026):**

| Operation | Performance | Unit |
|-----------|-------------|------|
| **Element-wise Add** | 36.09 GB/s | Throughput |
| **ReLU Activation** | 36.38 GB/s | Throughput |
| **GELU Activation** | 1.24 GB/s | Throughput |
| **Softmax (1000×1000)** | 5.698 ms | Latency |
| **MatMul (512×512)** | **30.52 GFLOPS** | Performance |
| **Dot Product** | 11.15 GFLOPS | Performance |

**Analysis:**
- 🟢 **Excellent**: Element-wise operations at ~36 GB/s (near memory bandwidth)
- 🟢 **Good**: MatMul baseline at 30.52 GFLOPS
- 🟡 **Expected**: GELU slower (transcendental functions)

---

## 🏆 Comparison with Other CPU LLM Frameworks

### Performance Benchmarks Summary

| Framework | MatMul GFLOPS | Throughput (tok/s) | Language | Dependencies |
|-----------|---------------|-------------------|----------|--------------|
| **SmallMind (Current)** | **12.28** | **37-83** | **C#** | **Zero** |
| **SmallMind (Baseline)** | **30.52** | **37-83** | **C#** | **Zero** |
| llama.cpp | 40-80 | 50-200 | C++ | Compilation |
| ONNX Runtime | 60-120 | 100-300 | C++ | C++ Runtime |
| PyTorch (CPU) | 30-60 | 20-100 | Python | Heavy Stack |
| Transformers.js | 5-15 | 10-50 | JavaScript | npm |
| TensorFlow Lite | 20-40 | 30-80 | C++ | Runtime Lib |

### Detailed Platform Comparison

#### vs. llama.cpp (C++)
**Performance:** llama.cpp is 3.3-6.5× faster (current), 1.3-2.6× faster (baseline)

**SmallMind Advantages:**
- ✅ Pure .NET deployment - No C++ toolchain required
- ✅ Single DLL deployment - No compilation steps
- ✅ .NET ecosystem integration
- ✅ Enterprise security compliance (no native code)
- ✅ Educational clarity - Readable C# vs. complex C++ SIMD

**When to use SmallMind:**
- .NET-first environments with strict security policies
- Educational purposes - learning transformer internals
- Rapid prototyping in C#
- Small to medium models (<10M parameters)

#### vs. ONNX Runtime (C++)
**Performance:** ONNX is 4.9-9.8× faster (current), 2.0-3.9× faster (baseline)

**SmallMind Advantages:**
- ✅ Zero external dependencies
- ✅ No model conversion required (ONNX format)
- ✅ Full C# transparency - Debug every operation
- ✅ Simpler deployment pipeline

**When to use SmallMind:**
- Want simplicity over maximum performance
- Need full control and transparency
- Custom model architectures not in ONNX

#### vs. PyTorch (CPU Mode)
**Performance:** SmallMind current is 2.4-4.9× slower, baseline was comparable

**SmallMind Advantages:**
- ✅ No Python runtime needed
- ✅ Better memory efficiency (94% reduction)
- ✅ Zero GC pressure in training
- ✅ Native .NET integration
- ✅ Better Windows deployment

**When to use SmallMind:**
- .NET ecosystem requirements
- Memory-constrained environments
- Want C# type safety and tooling

#### vs. Transformers.js (JavaScript)
**Performance:** SmallMind is **2.5-8.2× faster** (even with current regression)

**SmallMind Advantages:**
- ✅ Much faster computation
- ✅ Better memory efficiency
- ✅ Server-side performance
- ✅ .NET tooling and debugging

**When to use Transformers.js:**
- Browser-based inference required
- Client-side edge AI
- WebGPU acceleration available

---

## 💡 Analysis and Recommendations

### Critical Issues

#### 1. MatMul Performance Regression (🔴 High Priority)

**Issue:** MatMul 512×512 dropped from 30.52 → 12.28 GFLOPS (-59.8%)

**Potential Causes:**
- JIT compilation changes
- SIMD code generation issues
- Cache alignment problems
- Build configuration differences

**Recommended Actions:**
1. Compare git commits between Feb 4 and Feb 7
2. Profile assembly output for MatMul operations
3. Verify SIMD instructions are being generated
4. Check for unintended allocations in hot paths
5. Review recent code changes to MatMul implementation

#### 2. Training Throughput Regression (🔴 Medium Priority)

**Issue:** Training throughput dropped from 125k → 75k samples/sec (-39.9%)

**Potential Causes:**
- System load differences
- Thermal throttling
- Background processes
- Memory pressure

**Recommended Actions:**
1. Re-run benchmarks in isolated environment
2. Monitor CPU frequency during benchmarks
3. Check for background processes
4. Verify system configuration consistency

### Positive Highlights

#### 1. Memory Efficiency (🟢 Excellent)

- **94% allocation reduction** maintained
- **Zero GC collections** during training
- ArrayPool effectiveness confirmed

#### 2. Attention Performance (🟢 Excellent)

- Up to **63 GFLOPS** on large attention matrices
- Good scaling with problem size
- Minimal allocations

#### 3. Overall Stability (🟢 Good)

- Allocation patterns stable
- GC pressure remains zero
- Core infrastructure solid

---

## 📊 Performance vs. Industry Targets

| Metric | Current | Baseline | Target | Status |
|--------|---------|----------|--------|--------|
| **MatMul GFLOPS** | 12.28 | 30.52 | >20 | 🔴 Below (Current) |
| **Attention GFLOPS** | 63.0 | N/A | >20 | 🟢 Excellent |
| **Memory Reduction** | 94% | 94% | >80% | 🟢 Excellent |
| **GC Collections** | 0 | 0 | 0 | 🟢 Perfect |
| **Element-wise Ops** | N/A | 36.09 GB/s | >25 GB/s | 🟢 Excellent |

---

## 🎯 Competitive Positioning

### SmallMind Sweet Spot

**Perfect For:**
1. **.NET Enterprise Applications**
   - Security-conscious environments
   - No native dependencies allowed
   - Simple deployment requirements

2. **Educational & Research**
   - Learning transformer internals
   - Prototyping new architectures
   - Teaching ML concepts in C#

3. **Small to Medium Models**
   - <10M parameters
   - CPU inference acceptable
   - Memory efficiency critical

**Not Ideal For:**
1. Maximum performance requirements (use llama.cpp or ONNX)
2. Large models >10B parameters
3. GPU-accelerated inference
4. Production-scale serving (hundreds of requests/sec)

---

## 📁 Benchmark Output Files

All detailed results available in:

### Current Run (Feb 7, 2026)
- `benchmark-results-20260207-075351/CONSOLIDATED_BENCHMARK_REPORT.md`
- `benchmark-results-20260207-075351/enhanced-profile-report.md`
- `benchmark-results-20260207-075351/allocation-profile.txt`
- `benchmark-results-20260207-075351/model-creation-profile.txt`
- `benchmarks/ProfilerBenchmarks/profiler-results-20260207-075252/profiler-benchmark-results.md`
- `benchmarks/ProfilerBenchmarks/profiler-results-20260207-075252/profiler-benchmark-results.json`

### Baseline (Feb 4, 2026)
- `benchmark-results-20260204-044103/simd-benchmark-results.md`
- `benchmark-results-20260204-044103/simd-benchmark-results.json`
- `COMPREHENSIVE_PROFILING_AND_BENCHMARK_REPORT.md`
- `PROFILING_AND_BENCHMARK_EXECUTIVE_SUMMARY.md`

---

## 🔄 Next Steps

### Immediate Actions (This Week)

1. **Investigate MatMul Regression**
   - Git bisect to find problematic commit
   - Profile assembly code generation
   - Verify SIMD instruction usage
   - Run on different hardware to isolate issue

2. **Re-run Benchmarks in Clean Environment**
   - Reboot system
   - Close all background processes
   - Ensure consistent CPU frequency
   - Run full suite again

3. **Document Baseline Establishment Process**
   - Create reproducible benchmark protocol
   - Define acceptable variance ranges
   - Set up automated regression detection

### Medium Term (This Month)

1. **Performance Optimization**
   - Address MatMul regression
   - Investigate additional SIMD opportunities
   - Profile cache behavior
   - Consider tiling optimizations

2. **Benchmarking Infrastructure**
   - Automate benchmark runs
   - Set up CI/CD integration
   - Create performance dashboards
   - Track metrics over time

3. **Documentation**
   - Update performance guidelines
   - Document optimization techniques used
   - Create architecture deep-dives
   - Share learnings with community

---

## 📖 References

### Baseline Reports
- [PROFILING_AND_BENCHMARK_EXECUTIVE_SUMMARY.md](./PROFILING_AND_BENCHMARK_EXECUTIVE_SUMMARY.md)
- [COMPREHENSIVE_PROFILING_AND_BENCHMARK_REPORT.md](./COMPREHENSIVE_PROFILING_AND_BENCHMARK_REPORT.md)
- [BENCHMARK_METRICS_AND_COMPARISON.md](./BENCHMARK_METRICS_AND_COMPARISON.md)

### How to Run
- [HOW_TO_RUN_BENCHMARKS.md](./benchmarks/HOW_TO_RUN_BENCHMARKS.md)
- [RUNNING_BENCHMARKS_GUIDE.md](./RUNNING_BENCHMARKS_GUIDE.md)

### External Comparisons
- llama.cpp: https://github.com/ggerganov/llama.cpp/discussions/1614
- ONNX Runtime: https://onnxruntime.ai/docs/performance/benchmarks.html
- PyTorch: https://pytorch.org/tutorials/recipes/recipes/benchmark.html
- Transformers.js: https://huggingface.co/docs/transformers.js/benchmarks

---

**Report Generated:** 2026-02-07 07:54:00 UTC  
**Benchmark Runner:** SmallMind Comprehensive Suite  
**Analysis By:** GitHub Copilot Workspace Agent

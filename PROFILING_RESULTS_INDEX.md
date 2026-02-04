# Profiling Results Summary - February 4, 2026

## 📋 Quick Access

### Main Reports (Start Here)

1. **[PROFILING_METRICS_SUMMARY.md](./PROFILING_METRICS_SUMMARY.md)** ⭐ **RECOMMENDED**
   - Comprehensive executive summary
   - Detailed analysis of performance and memory metrics
   - Root cause analysis and recommendations
   - Compares current run to baseline (Feb 3, 2026)

2. **[profiling-results-20260204-023819/](./profiling-results-20260204-023819/)**
   - Complete profiling results directory
   - All raw outputs and detailed reports
   - Visual performance comparison charts

### Individual Reports

- **[current-vs-baseline-comparison.md](./current-vs-baseline-comparison.md)** - Side-by-side comparison with previous run
- **[enhanced-profile-report.md](./enhanced-profile-report.md)** - Current CodeProfiler results
- **[profiling-results-20260204-023819/PERFORMANCE_COMPARISON_CHART.md](./profiling-results-20260204-023819/PERFORMANCE_COMPARISON_CHART.md)** - Visual charts and graphs

## 🎯 Key Findings at a Glance

### Executive Summary

| Metric | Baseline (Feb 3) | Current (Feb 4) | Change | Verdict |
|--------|------------------|-----------------|--------|---------|
| **Total Runtime** | 5,927.60 ms | 9,277.78 ms | **+56.5%** | 🔴 Performance regression |
| **Total Allocations** | 2,550.03 MB | 338.71 MB | **-86.7%** | 🟢 Memory optimization success |
| **Training Throughput** | 43,092 samp/s | 10,861 samp/s | **-74.8%** | 🔴 Critical regression |
| **GC Gen0 Collections** | 0 | 0 | Stable | 🟢 Perfect memory pressure |

### What Happened?

#### 🔴 Critical Issues Found

1. **MatMul Performance Crisis** (161-465% slower)
   - Core matrix multiplication operations severely degraded
   - Affects all model sizes but especially smaller matrices
   - Root cause: Likely SIMD optimization regression or cache behavior change

2. **Training Throughput Collapse** (-74.8%)
   - Training speed dropped from 43K samples/sec to 11K samples/sec
   - Directly impacts development and iteration speed
   - Tied to MatMul performance issues

3. **Medium Model Inference** (+82% slower)
   - Inference time nearly doubled for larger models
   - Makes production deployment impractical

#### 🟢 Major Wins Achieved

1. **Memory Allocations** (-86.7%)
   - Massive reduction from 2.5GB to 339MB
   - ArrayPool optimizations highly effective
   - Zero garbage collection pressure

2. **Softmax Operations** (-81% to -96%)
   - Outstanding optimization across all sizes
   - Softmax_2048: 6.2ms → 0.23ms (96% improvement)
   - Demonstrates successful SIMD implementation

3. **Small Model Performance** (-19.5%)
   - Faster inference for smaller models
   - 24.5% better throughput
   - Different code path or better cache locality

## 🔍 Detailed Analysis Available

### CodeProfiler Results

The CodeProfiler ran in **enhanced mode**, profiling:
- ✅ **29 unique methods** across SIMD ops, tensor ops, and transformer inference
- ✅ **Small model** (128 dim, 2 layers, 470K params)
- ✅ **Medium model** (256 dim, 4 layers, 3.45M params)
- ✅ **SIMD operations** (MatMul, GELU, Softmax)
- ✅ **Tensor operations** (Add, Multiply, Broadcast)

**Output files:**
- `enhanced-profile-report.md` - Current results
- `current-vs-baseline-comparison.md` - Baseline comparison
- Full logs in `profiling-results-20260204-023819/`

### AllocationProfiler (Memory Profiler) Results

The memory profiler tested:
- ✅ **MatMul backward pass** allocations (128×256 @ 256×128, 100 iterations)
- ✅ **Training workload** simulation (50 steps, batch=32, hidden=256)
- ✅ **ArrayPool effectiveness** measurements
- ✅ **GC pressure** analysis

**Key metrics:**
- MatMul allocations: 13.23 MB (47% reduction from expected 25 MB)
- Training allocations: 3.78 MB (94% reduction from expected 62.5 MB)
- GC collections: **0** across all generations ✅

## 🔧 What To Do Next

### Immediate Actions (Critical Priority)

1. **Investigate MatMul Regression** 🔴
   ```bash
   # Use git bisect to find the offending commit
   git bisect start
   git bisect bad HEAD
   git bisect good <known-good-commit>
   ```

2. **Profile SIMD Code Generation**
   - Check if Vector<float> operations are being used
   - Verify JIT is generating SIMD instructions
   - Look for unintended allocations or boxing

3. **Compare Assembly Output**
   - Generate assembly for MatMul in baseline vs current
   - Look for differences in loop unrolling, vectorization

### How to Re-run Profilers

If you make changes and want to re-profile:

```bash
cd /home/runner/work/SmallMind/SmallMind

# Run CodeProfiler enhanced mode
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- --enhanced

# Run Memory Profiler
dotnet run --project benchmarks/AllocationProfiler/AllocationProfiler.csproj

# Generate comparison with baseline
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- --compare \
  benchmark-results-20260204-005926/enhanced-profile-report.md \
  enhanced-profile-report.md \
  new-comparison.md
```

## 📊 Visual Performance Breakdown

### Runtime Distribution

```
Total Runtime: 9277.78 ms

Model_Medium_Inference:     2186.63 ms  (23.6%) ████████████████████
MatMul_512x512:              449.13 ms  ( 4.8%) ████
Model_Small_Inference:       427.71 ms  ( 4.6%) ████
MatMul_Iteration:            390.54 ms  ( 4.2%) ███
GELU_1000000:                163.03 ms  ( 1.8%) █
Model_Medium_Creation:       139.84 ms  ( 1.5%) █
Other operations:           5521.00 ms  (59.5%) ████████████████████████████████
```

### Memory Allocations Distribution

```
Total Allocations: 338.71 MB

Model_Medium_Inference:       83.12 MB  (24.5%) ████████████████████
Model_Medium_Creation:        26.42 MB  ( 7.8%) ██████
Model_Small_Inference:        18.97 MB  ( 5.6%) ████
Model_Small_Creation:          3.61 MB  ( 1.1%) █
Tensor Operations:             1.55 MB  ( 0.5%) 
MatMul Operations:             0.10 MB  ( 0.0%)
Other operations:            205.00 MB  (60.5%) ████████████████████████████████
```

## 📚 Additional Resources

- **CodeProfiler Documentation:** [tools/CodeProfiler/README.md](./tools/CodeProfiler/README.md)
- **Profiling Guides:** [docs/profiling/](./docs/profiling/)
- **Previous Baseline:** [benchmark-results-20260204-005926/](./benchmark-results-20260204-005926/)
- **Performance Optimization Guide:** Custom instructions in `.github/copilot-instructions.md`

## 🔗 Related Documents

- [PROFILING_METRICS_SUMMARY.md](./PROFILING_METRICS_SUMMARY.md) - Full analysis
- [current-vs-baseline-comparison.md](./current-vs-baseline-comparison.md) - Comparison report
- [enhanced-profile-report.md](./enhanced-profile-report.md) - Current results
- [profiling-results-20260204-023819/README.md](./profiling-results-20260204-023819/README.md) - Results directory index

---

**Generated:** 2026-02-04 02:38:19 UTC  
**Environment:** .NET 10.0.2, Unix 6.11.0.1018, 4 CPU cores  
**Duration:** CodeProfiler ~90s, AllocationProfiler ~3s, Comparison <1s

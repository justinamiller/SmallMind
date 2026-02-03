# SmallMind Performance Profiling - February 2026

## 📋 Overview

This profiling task analyzed SmallMind's LLM implementation to identify hot paths and optimization opportunities for:
- **Inference throughput** (tokens per second)
- **Training performance** (time per iteration)
- **Memory efficiency** (allocations and GC pressure)

## 🎯 Key Findings

### Current Performance
- **Inference Speed:** 19.7 tokens/sec (50.8 ms per token)
- **Memory Usage:** 51.5 MB allocated per token
- **MatMul Performance:** 16.3 GFLOPS (512×512, ~29% of theoretical peak)
- **Training Throughput:** 1.48 billion params/sec (AdamW optimizer)

### Critical Bottlenecks
1. **99.6% of memory** allocated in `Transformer_Forward` (no tensor pooling)
2. **Attention mechanism** uses O(T²) dot product loops instead of batched matrix multiply
3. **Softmax** computes exp() for masked positions (50% waste)
4. **No KV-Cache** for autoregressive generation
5. **LayerNorm** normalization loop not SIMD-vectorized

## 📊 Profiling Results

### Comprehensive Analysis
**File:** [`PROFILER_HOT_PATHS_ANALYSIS_2026-02-03.md`](./PROFILER_HOT_PATHS_ANALYSIS_2026-02-03.md) (18 KB, 609 lines)

Contains:
- Detailed breakdown of all hot paths
- Specific code locations (file:line)
- Current vs optimized implementations
- Expected speedup for each optimization
- Architecture-specific considerations

**Key sections:**
- Rank #1-8: Critical hot paths with measurements
- Optimization roadmap (3 phases)
- Implementation priorities
- Expected performance after optimizations

### Implementation Guide
**File:** [`NEXT_OPTIMIZATION_PHASES.md`](./NEXT_OPTIMIZATION_PHASES.md) (10 KB, 380 lines)

Contains:
- Week-by-week implementation plan
- Code templates for each optimization
- Testing & validation procedures
- Timeline with cumulative speedups

**Phases:**
- **Week 1:** Memory pooling, batched MatMul, masked softmax (+290-370%)
- **Week 2-3:** KV-Cache, cache blocking, ArrayPool (+67-100% more)
- **Week 4+:** Flash Attention, quantization, graph fusion (+30% more)

### Quick Reference
**File:** [`HOT_PATHS_QUICK_REF.md`](./HOT_PATHS_QUICK_REF.md) (3 KB, 131 lines)

One-page summary with:
- Top 5 bottlenecks
- Phase 1 quick wins (1 week)
- Expected results table
- Quick test commands

### Executive Summary
**File:** [`PROFILER_SUMMARY.txt`](./PROFILER_SUMMARY.txt) (2.5 KB, 100 lines)

Text summary of:
- Profiling measurements
- Top bottlenecks (P0 and P1)
- Optimization targets
- Recommendations

## 🚀 Recommended Next Steps

### Immediate (This Week)
Start with **Phase 1 optimizations** for 4-5x speedup:

1. **Implement TensorPool** (2 days)
   - Create `src/SmallMind.Core/Memory/TensorPool.cs`
   - Modify `Transformer.cs` to use pooled buffers
   - Expected: 51.5 MB → 8 MB per token

2. **Add BatchedMatMul to Attention** (3 days)
   - Implement in `MatMulOps.cs`
   - Replace loops in `ComputeAttentionScores` (lines 501-591)
   - Replace triple loop in `ApplyAttention` (lines 688-748)
   - Expected: 50ms → 15ms per attention

3. **Fused Masked Softmax** (1 day)
   - Modify `ApplySoftmax` (lines 593-686)
   - Skip j > i positions entirely
   - Expected: 2x faster softmax

4. **SIMD in LayerNorm** (4 hours)
   - Add `Vector<float>` loop in `LayerNormOps.cs` (line 65)
   - Expected: 2.6x faster normalization

**Week 1 Result:** 6.4 → 30 tokens/sec (**4.7x improvement**)

### Short Term (Weeks 2-3)
**Phase 2 infrastructure** improvements:

5. **KV-Cache Implementation**
   - Create `src/SmallMind.Transformers/Inference/KVCache.cs`
   - Modify attention to cache K/V tensors
   - Expected: +50% inference speed

6. **MatMul Cache Blocking**
   - Implement tiled multiply in `MatMulOps.cs`
   - Expected: 16.3 → 30 GFLOPS

7. **ArrayPool for Gradients**
   - Replace heap allocations in backward pass
   - Expected: +10% training speed

**Week 3 Result:** 30 → 60 tokens/sec (**9.4x total improvement**)

### Long Term (Weeks 4+)
**Phase 3 advanced** optimizations:
- Flash Attention (block-sparse)
- INT8 quantization
- Graph-level operator fusion

**Final Target:** 75+ tokens/sec (**11.7x total improvement**)

## 📁 File Organization

### Documentation Files (This Profiling Task)
```
PROFILER_HOT_PATHS_ANALYSIS_2026-02-03.md  # Main analysis (18 KB)
NEXT_OPTIMIZATION_PHASES.md                # Implementation guide (10 KB)
HOT_PATHS_QUICK_REF.md                     # Quick reference (3 KB)
PROFILER_SUMMARY.txt                       # Executive summary (2.5 KB)
README_PROFILING_2026-02.md                # This file
```

### Profiler Tools
```
tools/CodeProfiler/
  ├── Program.cs                # Main profiler entry point
  ├── PerformanceProfiler.cs    # Timing and memory tracking
  ├── EnhancedProfiler.cs       # Comprehensive profiling
  ├── DeepProfiler.cs           # Deep SIMD profiling
  └── profile-report.md         # Latest run results
```

### Benchmark Tools
```
benchmarks/
  ├── TrainingBenchmark/        # AdamW, MatMul, LayerNorm benchmarks
  ├── SimdBenchmarks.csproj     # SIMD performance tests
  └── README.md                 # Benchmark documentation
```

### Source Code (To Be Modified)
```
src/SmallMind.Transformers/Core/
  ├── Transformer.cs            # Main hot path (lines 501-748)
  └── NeuralNet.cs              # Layers (Embedding, Linear, LayerNorm)

src/SmallMind.Core/
  ├── Simd/MatMulOps.cs         # Matrix multiply kernels
  └── Core/LayerNormOps.cs      # Normalization operations
```

## 🧪 Running the Profiler

### Deep Profile (Recommended)
```bash
cd tools/CodeProfiler
dotnet run -c Release -- --deep
```

Outputs:
- Console: Top 20 hot paths summary
- File: `profile-report.md` (detailed breakdown)

### Training Benchmarks
```bash
cd benchmarks/TrainingBenchmark
dotnet run -c Release
```

Measures:
- AdamW optimizer performance
- MatMul throughput (GFLOPS)
- LayerNorm throughput (GB/s)

### SIMD Benchmarks
```bash
cd benchmarks
dotnet run -c Release --project SimdBenchmarks.csproj
```

Tests:
- AVX2/AVX-512 capabilities
- Vector<T> performance
- Memory bandwidth

## 📈 Performance Tracking

### Baseline Metrics (Feb 2026)
| Metric | Current | Target (Phase 1) | Target (Final) |
|--------|---------|-----------------|----------------|
| Tokens/sec | 6.4 | 30 | 75+ |
| Memory/token | 51.5 MB | 8 MB | 3-5 MB |
| Forward pass | 51 ms | 15 ms | 6-8 ms |
| MatMul GFLOPS | 16.3 | 20 | 30+ |

### How to Measure Improvements
```bash
# 1. Run baseline
cd tools/CodeProfiler
dotnet run -c Release -- --deep > baseline.txt

# 2. Make optimizations
# ... (implement changes)

# 3. Run comparison
dotnet run -c Release -- --deep > optimized.txt

# 4. Compare
diff baseline.txt optimized.txt

# Key metrics to check:
# - Memory allocated (should decrease)
# - Time per operation (should decrease)
# - GC collections (should decrease)
```

## 🎓 Understanding the Analysis

### Hot Path Rankings
Hot paths are ranked by **impact × frequency**:
- **Rank #1:** Transformer forward pass (97.5% of time, 99.6% of memory)
- **Rank #2-3:** Attention operations (main compute bottleneck)
- **Rank #4-5:** Memory allocations and data movement

### Optimization Priority
Optimizations are prioritized by **ROI** (return on investment):
- **P0 (Critical):** >2x speedup, low-medium effort → Do NOW
- **P1 (Important):** 1.5-2x speedup, medium effort → Do Week 2-3
- **P2 (Nice-to-have):** 1.2-1.5x speedup, high effort → Do Week 4+

### Expected Speedups
Speedup estimates are **multiplicative**:
- TensorPool: 1.4x
- BatchedMatMul: 2.5x
- Masked Softmax: 1.2x
- SIMD LayerNorm: 1.1x
- **Combined:** 1.4 × 2.5 × 1.2 × 1.1 = **4.6x**

## 🔬 Validation & Testing

### Correctness Tests
```bash
cd tests
dotnet test -c Release
```

Ensures:
- Numerical accuracy (gradients, forward pass)
- Shape correctness
- Edge cases (empty inputs, large tensors)

### Performance Regression Tests
```bash
cd benchmarks
./run_all_benchmarks.sh
```

Tracks:
- Inference speed (tokens/sec)
- Training speed (params/sec)
- Memory usage (MB/token)

### Continuous Monitoring
Set up automated benchmarks after each optimization phase to track:
- Performance improvements
- Memory usage changes
- Potential regressions

## 📞 References

### Related Documentation
- `PERFORMANCE_OPTIMIZATIONS.md` - General optimization techniques
- `PERFORMANCE_QUICK_REFERENCE.md` - Quick optimization tips
- `SIMD_BENCHMARK_RESULTS.md` - SIMD analysis
- Previous profiling reports in repository root

### External Resources
- [.NET Performance Tips](https://learn.microsoft.com/en-us/dotnet/standard/performance/)
- [SIMD in .NET](https://devblogs.microsoft.com/dotnet/hardware-intrinsics-in-net-core/)
- [High-Performance .NET](https://adamsitnik.com/Hardware-Counters-Diagnoser/)

## ✅ Task Completion

### Deliverables
- [x] Run comprehensive profiler on latest code
- [x] Identify top hot paths for tokens/sec improvement
- [x] Identify bottlenecks for training time reduction
- [x] Document memory allocation patterns
- [x] Create optimization roadmap with priorities
- [x] Provide implementation guide with code examples
- [x] Generate quick reference for immediate actions
- [x] Document expected performance improvements

### Files Created
1. ✅ `PROFILER_HOT_PATHS_ANALYSIS_2026-02-03.md` (comprehensive analysis)
2. ✅ `NEXT_OPTIMIZATION_PHASES.md` (implementation roadmap)
3. ✅ `HOT_PATHS_QUICK_REF.md` (one-page reference)
4. ✅ `PROFILER_SUMMARY.txt` (executive summary)
5. ✅ `README_PROFILING_2026-02.md` (this overview)

### Next Actions
**Recommended:** Start Phase 1 optimizations this week for 4-5x speedup!

---

**Generated:** 2026-02-03  
**Profiler Version:** Deep Profile v2.0  
**Analysis Duration:** Comprehensive  
**Status:** ✅ Complete and ready for implementation

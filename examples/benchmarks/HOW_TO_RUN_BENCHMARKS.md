# How to Run SmallMind Benchmarks and Profilers

This guide explains how to run the complete SmallMind benchmarking and profiling suite.

---

## 🎯 Quick Start - Run All Benchmarks

```bash
# 1. Build in Release mode
dotnet build -c Release

# 2. Run comprehensive benchmarks
cd tools/SmallMind.Benchmarks
dotnet run -c Release -- --model ../../benchmark-model.smq --scenario all --iterations 30

# 3. Run SIMD benchmarks
cd ../../benchmarks
dotnet run -c Release

# 4. Run allocation profiler
cd AllocationProfiler
dotnet run -c Release

# 5. Run model creation profiler
cd ../../tools/ProfileModelCreation
dotnet run -c Release
```

---

## 📊 Individual Benchmark Tools

### 1. SmallMind.Benchmarks (Comprehensive Inference)

**Purpose:** Measure end-to-end inference performance, TTFT, throughput, latency, memory, and GC behavior.

**Location:** `tools/SmallMind.Benchmarks/`

**Run:**
```bash
cd tools/SmallMind.Benchmarks
dotnet run -c Release -- --model /path/to/model.smq --scenario all --iterations 30
```

**Options:**
- `--scenario all|ttft|tokens_per_sec|latency|concurrency|memory|gc|env`
- `--iterations <n>` - Number of iterations (default: 30)
- `--warmup <n>` - Warmup iterations (default: 5)
- `--max-new-tokens <n>` - Tokens to generate (default: 256)
- `--output <dir>` - Output directory (default: auto-generated)

**Output:**
- `report.md` - Human-readable Markdown report
- `results.json` - Machine-readable JSON data

**Typical Runtime:** 2-5 minutes

---

### 2. SIMD Benchmarks (Low-Level Operations)

**Purpose:** Measure performance of SIMD-optimized low-level operations (matmul, activations, etc.)

**Location:** `benchmarks/`

**Run:**
```bash
cd benchmarks
dotnet run -c Release
```

**Output:**
- `benchmark-results.md` - Markdown report with system info
- `benchmark-results.json` - JSON data

**Operations Tested:**
- Element-wise addition (10M elements)
- ReLU activation (10M elements)
- GELU activation (1M elements)
- Softmax (1000×1000 matrix)
- Matrix multiplication (512×512)
- Dot product (10M elements)

**Typical Runtime:** 30-60 seconds

---

### 3. AllocationProfiler (Memory Analysis)

**Purpose:** Measure memory allocation patterns and GC pressure, specifically ArrayPool effectiveness.

**Location:** `benchmarks/AllocationProfiler/`

**Run:**
```bash
cd benchmarks/AllocationProfiler
dotnet run -c Release
```

**Output:** Console output with allocation metrics

**Tests:**
- MatMul backward pass allocation profile
- Training workload simulation

**Typical Runtime:** 10-20 seconds

---

### 4. ProfileModelCreation (Initialization)

**Purpose:** Measure model creation time for different sizes.

**Location:** `tools/ProfileModelCreation/`

**Run:**
```bash
cd tools/ProfileModelCreation
dotnet run -c Release
```

**Output:** Console output with creation times

**Model Sizes:**
- Tiny: 417K parameters
- Small: 3.2M parameters
- Medium: 10.7M parameters

**Typical Runtime:** 5-10 seconds

---

## 📈 Generating Comparison Reports

To compare new results with baseline:

1. **Run all benchmarks** (as shown above)

2. **Create comparison report:**
```bash
# Compare with previous BENCHMARK_RESULTS_2026-02-03.md
# Update BENCHMARK_COMPARISON_2026-02-04.md with new data
# Update PROFILING_AND_BENCHMARKING_SUMMARY_2026-02-04.md
```

3. **Review key metrics:**
   - TTFT (Time to First Token)
   - Throughput (tokens/sec)
   - Memory footprint
   - GC collections
   - SIMD operation performance

---

## 🔧 Troubleshooting

### "Build failed" errors

**Solution:** Ensure you're in Release mode:
```bash
dotnet build -c Release
```

### "Model not found" errors

**Solution:** Verify model path or create benchmark model:
```bash
cd tools/CreateBenchmarkModel
dotnet run -c Release
```

### Low performance results

**Checklist:**
1. ✅ Running in Release mode (not Debug)
2. ✅ No other heavy processes running
3. ✅ Sufficient CPU cooling (no thermal throttling)
4. ✅ Consistent power settings (not on battery for laptops)

### High variance in results

**Solutions:**
- Increase iterations: `--iterations 50`
- Close background applications
- Run benchmarks multiple times and average results
- Use deterministic generation: `--temperature 0`

---

## 📊 Understanding Results

### Time to First Token (TTFT)

**What it measures:** Latency from request start to first token generation.

**Good value:** <2ms for CPU inference  
**Excellent value:** <1ms

**Impacts:** User experience in interactive applications

---

### Throughput (tokens/sec)

**What it measures:** Sustainable token generation rate.

**Good value:** >500 tok/s for CPU  
**Excellent value:** >750 tok/s for CPU

**Impacts:** Batch processing, long document generation

---

### Memory Footprint

**What it measures:** Working set size during generation.

**Good value:** <100 MB  
**Excellent value:** <80 MB

**Impacts:** Deployment cost, edge device suitability

---

### GC Metrics

**What to watch:**
- **Gen2 collections:** Should be 0 or very low
- **Allocation rate:** <1.5 GB/s
- **Time in GC:** <10%

**High GC pressure indicates:**
- Too many allocations
- Need for more ArrayPool usage
- Potential for object pooling

---

### SIMD Performance

**Matrix Multiplication:**
- Good: >15 GFLOPS (CPU)
- Excellent: >20 GFLOPS (CPU)

**Element-wise Operations:**
- Good: >25 GB/s
- Excellent: >30 GB/s

---

## 🎯 Best Practices

### Before Running Benchmarks

1. **Close unnecessary applications**
2. **Disable turbo boost** (for consistent results)
3. **Set performance power plan** (Windows/Linux)
4. **Let CPU cool down** between runs
5. **Run multiple times** and take median

### Interpreting Results

1. **Focus on percentiles** (P50, P95, P99) not just mean
2. **Watch for variance** - low StdDev is good
3. **Compare trends** - absolute numbers vary by hardware
4. **Consider context** - CPU vs GPU, model size, etc.

### Reporting Results

Always include:
- ✅ Hardware specs (CPU model, cores, RAM)
- ✅ OS and version
- ✅ .NET version
- ✅ Build configuration (Release)
- ✅ Git commit hash
- ✅ Timestamp

---

## 📁 Output Locations

After running benchmarks, results are in:

```
/benchmark-results-YYYYMMDD-HHMMSS/
  ├── report.md                    # SmallMind.Benchmarks
  └── results.json

/benchmarks/
  ├── benchmark-results.md         # SIMD Benchmarks
  └── benchmark-results.json

Console output:
  - AllocationProfiler
  - ProfileModelCreation
```

---

## 🔄 Automation

### CI/CD Integration

```yaml
# Example GitHub Actions workflow
- name: Run Benchmarks
  run: |
    dotnet build -c Release
    cd tools/SmallMind.Benchmarks
    dotnet run -c Release -- \
      --model ../../benchmark-model.smq \
      --scenario all \
      --iterations 30 \
      --output ${{ github.workspace }}/benchmark-results
```

### Regression Detection

Compare results with previous baseline:
```bash
# Store baseline
cp benchmark-results-*/results.json baseline.json

# Compare new results
# Use jq or custom script to compare metrics
# Alert on >5% regressions
```

---

## 📞 Support

For questions or issues:
1. Check PERFORMANCE_TOOLS_GUIDE.md
2. Review benchmark README files
3. See repository documentation
4. Open an issue with benchmark results attached

---

**Last Updated:** 2026-02-04  
**Maintained By:** SmallMind Team

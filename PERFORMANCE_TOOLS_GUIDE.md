# How to Use SmallMind Performance Tools

This guide explains how to use the profiling and benchmarking tools to analyze SmallMind's performance.

---

## 🎯 Quick Start

### Run Standard Profiler

```bash
cd tools/CodeProfiler
dotnet run -c Release
```

**Output:** `profile-report.md` with hot path analysis

### Run Enhanced Profiler

```bash
cd tools/CodeProfiler
dotnet run -c Release -- --enhanced
```

**Output:** `enhanced-profile-report.md` with detailed multi-level analysis

### Run SIMD Benchmarks

```bash
cd benchmarks
dotnet run -c Release
```

**Output:** 
- `benchmark-results.md` (human-readable)
- `benchmark-results.json` (machine-readable)

---

## 📊 Available Profiling Modes

### 1. Standard Mode (Default)

**Use Case:** Quick performance check  
**Duration:** ~2 seconds  
**Output:** Basic hot path report

```bash
dotnet run -c Release
```

**Profiles:**
- Transformer forward pass
- Token generation
- Sampling operations
- Memory allocations

### 2. Deep Mode

**Use Case:** Include SIMD operation profiling  
**Duration:** ~5 seconds  
**Output:** Hot paths + SIMD benchmark data

```bash
dotnet run -c Release -- --deep
```

**Profiles:**
- All standard mode operations
- Matrix multiplication (128×128, 256×256, 512×512)
- Low-level SIMD kernels

### 3. Enhanced Mode

**Use Case:** Comprehensive multi-level analysis  
**Duration:** ~10 seconds  
**Output:** Detailed profiling with multiple model sizes

```bash
dotnet run -c Release -- --enhanced
```

**Profiles:**
- Low-level SIMD operations (multiple sizes)
- Tensor operations (add, multiply, broadcast)
- Small model inference (128 dim, 2 layers)
- Medium model inference (256 dim, 4 layers)
- Top 30 hot paths
- Top 15 memory allocators

---

## 🔧 Command-Line Options

### CodeProfiler

```bash
# Standard mode with custom output
dotnet run -c Release -- custom-report.md

# Deep mode with custom parameters
dotnet run -c Release -- report.md 5 100 --deep
# Args: [output] [num_inferences] [max_tokens] [--deep]

# Enhanced mode (comprehensive)
dotnet run -c Release -- --enhanced
```

### Benchmarks

```bash
# Run all SIMD benchmarks
cd benchmarks
dotnet run -c Release

# View results
cat benchmark-results.md
```

---

## 📈 Understanding the Reports

### Profile Report Structure

```markdown
# Performance Profile Report

## System Information
- OS, architecture, CPU cores
- .NET version, GC mode
- SIMD capabilities

## 🔥 Hot Paths (by Time)
- Methods ranked by total runtime
- Call count, average time
- Memory allocations

## 💾 Top Allocators (by Memory)
- Methods ranked by total allocations
- Call count, average allocation per call

## 📞 Call Hierarchy
- Parent-child relationships
- Understanding the call stack
```

### Key Metrics to Watch

1. **Total Time (ms)** - How long the method ran
2. **% of Total** - Percentage of overall runtime
3. **Calls** - How many times the method was called
4. **Avg Time (ms)** - Average time per call
5. **Alloc (MB)** - Total memory allocated

### What to Look For

**🔥 Hot Paths:**
- Methods with >10% of total runtime
- High call counts with low average time (tight loops)
- High average time with low call counts (expensive operations)

**💾 Memory Issues:**
- Methods allocating >10 MB total
- High allocation per call (>1 MB)
- Unexpected allocations in tight loops

---

## 🎯 Interpreting SIMD Benchmarks

### Benchmark Output Example

```
--- Matrix Multiplication Benchmark ---
  Size: 512 x 512 × 512 x 512
  Time: 8.989 ms/op
  Performance: 29.86 GFLOPS
```

### Performance Ratings

| Metric | Excellent | Good | Moderate | Needs Work |
|--------|-----------|------|----------|------------|
| MatMul GFLOPS | >25 | 15-25 | 10-15 | <10 |
| Throughput GB/s | >20 | 10-20 | 5-10 | <5 |

### What "Good" Looks Like

- **Matrix Multiplication:** 25-30 GFLOPS (on AVX2)
- **Element-wise Ops:** 20-25 GB/s (near memory bandwidth)
- **ReLU/Add:** 20+ GB/s
- **GELU:** 2-4 GB/s (complex operation)
- **Softmax:** <10 ms for 1000×1000

---

## 🔬 Advanced Usage

### Profile Custom Workloads

Edit `Program.cs` to add custom profiling:

```csharp
using (profiler.BeginScope("MyOperation"))
{
    // Your code here
}
```

### Add Custom Benchmarks

Edit `SimdBenchmarks.cs`:

```csharp
static void BenchmarkMyOperation()
{
    var sw = Stopwatch.StartNew();
    
    // Warmup
    MyOperation();
    
    // Benchmark
    for (int i = 0; i < iterations; i++)
    {
        MyOperation();
    }
    
    sw.Stop();
    // Report results
}
```

### Track Specific Metrics

```csharp
var startMemory = GC.GetTotalMemory(false);
var startTime = Stopwatch.GetTimestamp();

// Your operation

var endTime = Stopwatch.GetTimestamp();
var endMemory = GC.GetTotalMemory(false);

var elapsedMs = (endTime - startTime) * 1000.0 / Stopwatch.Frequency;
var allocatedMB = (endMemory - startMemory) / (1024.0 * 1024.0);

Console.WriteLine($"Time: {elapsedMs:F2} ms, Allocated: {allocatedMB:F2} MB");
```

---

## 📊 Performance Baselines

### Expected Performance (Current)

**Small Model (128 dim, 2 layers):**
- Forward Pass: ~13 ms per token
- Memory: ~7 MB per token
- Throughput: ~7.7 tokens/second

**SIMD Operations:**
- MatMul 512×512: ~9 ms (29 GFLOPS)
- Element-wise Add 10M: ~5 ms (24 GB/s)
- GELU 1M: ~6 ms (1.25 GB/s)

### Target Performance (After Optimization)

**Small Model:**
- Forward Pass: ~4-6 ms per token
- Memory: <1 MB per token
- Throughput: ~20-25 tokens/second

**SIMD Operations:**
- MatMul: Maintain 25-30 GFLOPS
- Element-wise: Maintain 20-25 GB/s
- GELU: Improve to 3-4 GB/s

---

## 🐛 Troubleshooting

### Profiler Not Running?

**Issue:** Build errors or missing dependencies

```bash
# Rebuild from scratch
cd tools/CodeProfiler
dotnet clean
dotnet build -c Release
```

### Benchmarks Fail to Build?

**Issue:** Multiple Main() entry points

**Fix:** Edit `SimdBenchmarks.csproj` to exclude subdirectories:

```xml
<ItemGroup>
  <Compile Remove="TokenizerPerf/**/*.cs" />
  <Compile Remove="TrainingBenchmark/**/*.cs" />
</ItemGroup>
```

### Performance Results Inconsistent?

**Issue:** Debug mode or background processes

**Solutions:**
1. Always use Release mode: `-c Release`
2. Close other applications
3. Run multiple iterations and average
4. Ensure CPU isn't throttled

### "Not enough memory" Error?

**Issue:** Large model or long sequence

**Solutions:**
1. Reduce model size
2. Reduce max tokens
3. Reduce number of inferences
4. Increase system memory

---

## 📚 Further Reading

### Performance Analysis Documents

1. **PERFORMANCE_QUICK_REFERENCE.md** - Quick overview
2. **PERFORMANCE_BENCHMARKING_EXECUTIVE_SUMMARY.md** - Executive summary
3. **COMPREHENSIVE_HOT_PATHS_ANALYSIS.md** - Detailed 21KB analysis

### Optimization History

- **PERFORMANCE_IMPROVEMENTS_2026-02.md** - Recent SIMD optimizations
- **PROFILER_HOT_PATHS_REPORT.md** - Previous profiling results
- **SIMD_OPTIMIZATION_RESULTS.md** - SIMD benchmark history

### External Resources

- [.NET Performance](https://learn.microsoft.com/en-us/dotnet/standard/performance/)
- [BenchmarkDotNet](https://benchmarkdotnet.org/) - Advanced benchmarking
- [PerfView](https://github.com/microsoft/perfview) - Production profiling

---

## 🎯 Common Workflows

### Before Starting Optimization

```bash
# 1. Run baseline profiler
cd tools/CodeProfiler
dotnet run -c Release

# 2. Run benchmarks
cd ../../benchmarks
dotnet run -c Release

# 3. Save results
cp ../tools/CodeProfiler/profile-report.md baseline-profile.md
cp benchmark-results.md baseline-benchmarks.md
```

### After Making Changes

```bash
# 1. Run profiler again
cd tools/CodeProfiler
dotnet run -c Release

# 2. Compare results
diff baseline-profile.md profile-report.md

# 3. Run benchmarks
cd ../../benchmarks
dotnet run -c Release

# 4. Validate improvement
# Look for:
# - Reduced total time
# - Reduced memory allocations
# - Maintained or improved SIMD performance
```

### Regression Testing

```bash
# Add to your CI/CD pipeline
dotnet test tests/SmallMind.PerfTests/ -c Release

# Or run manually
cd tests/SmallMind.PerfTests
dotnet test -c Release --logger "console;verbosity=detailed"
```

---

## 💡 Tips & Best Practices

### Profiling Tips

1. **Always use Release mode** - Debug mode is 10-100× slower
2. **Run multiple iterations** - First run includes JIT overhead
3. **Profile realistic workloads** - Use representative model sizes
4. **Isolate operations** - Profile one change at a time
5. **Track both time and memory** - Both matter for performance

### Benchmarking Tips

1. **Warm up first** - Run operation once before timing
2. **Average multiple runs** - Reduce variance
3. **Check SIMD detection** - Ensure AVX2/FMA are used
4. **Compare to baseline** - Track improvements over time
5. **Document system config** - CPU, memory, OS matter

### Optimization Workflow

1. **Profile first** - Don't optimize blindly
2. **Measure impact** - Quantify improvements
3. **One change at a time** - Know what helped
4. **Test correctness** - Don't break functionality
5. **Document findings** - Help future developers

---

## 📞 Support

**Questions?**
- Check the analysis documents first
- Review existing profiling reports
- Look at code examples in COMPREHENSIVE_HOT_PATHS_ANALYSIS.md

**Need Help?**
- Open an issue with profiling output attached
- Include system information (OS, CPU, .NET version)
- Share the generated reports

**Contributing?**
- Profile your changes before submitting PR
- Include benchmark results in PR description
- Update baselines if making performance improvements

---

**Happy Profiling!** 🚀

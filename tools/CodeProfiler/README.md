# SmallMind Code Profiler

A high-precision performance profiling tool for analyzing hot paths, memory allocations, and call hierarchies in SmallMind's transformer inference pipeline.

## Features

- **Method-Level Timing**: Microsecond-precision timing using Stopwatch
- **Memory Tracking**: Tracks allocations per method using GC.GetTotalAllocatedBytes()
- **Call Hierarchy**: Records parent-child relationships between method calls
- **Hot Path Analysis**: Identifies the most time-consuming code paths
- **Allocation Analysis**: Finds methods with highest memory pressure
- **Detailed Reports**: Generates markdown reports with comprehensive performance data
- **Profile Comparison**: Compare performance between different test runs
- **Model Comparison**: Analyze scaling characteristics across model sizes

## Usage

### Enhanced Profiling (Recommended)

```bash
cd /home/runner/work/SmallMind/SmallMind
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- --enhanced
```

**Enhanced profile includes:**
- Low-level SIMD operations (MatMul, GELU, Softmax)
- Mid-level tensor operations
- High-level transformer inference
- Small and Medium model comparison (automatically)
- Saves to `enhanced-profile-report.md`

### Profile Comparison

Compare two profiling runs to identify performance changes:

```bash
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- --compare \
  previous-profile-report.md \
  enhanced-profile-report.md \
  profile-comparison-report.md
```

**Comparison report includes:**
- Overall performance summary (runtime, allocations)
- Top 10 improvements and regressions
- Detailed method-by-method comparison
- Model scaling analysis
- SIMD operation performance (with GFLOPS)

### Model-Only Comparison

Deep dive into model size scaling characteristics:

```bash
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- --model-compare \
  enhanced-profile-report.md \
  model-comparison-report.md
```

**Model comparison includes:**
- Small vs Medium model specifications
- Performance metrics (throughput, latency, memory)
- Scaling efficiency analysis
- Forward pass breakdown

### Basic Profiling

```bash
cd /home/runner/work/SmallMind/SmallMind
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj
```

**Default behavior:**
- Runs 3 inferences
- Generates 50 tokens per inference  
- Saves report to `profile-report.md`

### Custom Parameters

```bash
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- <output-file> <num-inferences> <tokens-per-inference>
```

**Example:**
```bash
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- my-profile.md 5 100
```

### Deep Profiling (with SIMD Benchmarks)

```bash
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- --deep
```

**Deep profile includes:**
- SIMD operation benchmarks (MatMul, Softmax, GELU)
- Multiple matrix sizes (128×128, 256×256, 512×512)
- Detailed transformer layer profiling
- Enhanced memory allocation tracking

## Output

The profiler generates comprehensive markdown reports:

### Enhanced Profile Report (`enhanced-profile-report.md`)

Contains current test run detailed profile:
- **Summary**: Total runtime, allocations, methods profiled
- **Hot Paths Table**: Top 30 operations by execution time
- **Top Allocators Table**: Top 15 operations by memory allocation
- Includes both Small and Medium model benchmarks

### Profile Comparison Report (`profile-comparison-report.md`)

Side-by-side comparison of two test runs:
- **Overall Performance Summary**: Total runtime, allocations, method count changes
- **Performance Verdict**: Automatic assessment (Improved/Regressed/Stable)
- **Top 10 Improvements**: Operations that got faster
- **Top 10 Regressions**: Operations that got slower
- **Detailed Method Comparison**: Full breakdown of all methods
- **Model Size Comparison**: Small vs Medium scaling analysis
- **SIMD Operation Performance**: MatMul performance with GFLOPS metrics

### Model Comparison Report (`model-comparison-report.md`)

Deep dive into model scaling:
- **Model Specifications**: Parameters, dimensions, context windows
- **Performance Metrics**: Inference time, throughput, memory usage
- **Scaling Efficiency Analysis**: How well performance scales with model size
- **Forward Pass Analysis**: Detailed breakdown of forward pass timing

### Standard Profile Report (`profile-report.md`)

Basic profiling output:

### 1. System Information
- OS and architecture
- CPU core count
- .NET version
- GC mode (Server vs Workstation)

### 2. Hot Paths (by Time)
Top methods consuming CPU time with:
- Total time and percentage of runtime
- Call count and average time per call
- Min/max execution times

### 3. Top Allocators (by Memory)
Methods allocating the most memory with:
- Total allocations and percentage
- Average allocation per call

### 4. Most Called Methods
Frequently executed methods ranked by call count

### 5. Call Hierarchy
Parent-child relationships showing which methods call which

### 6. Performance Insights
Automated analysis identifying:
- Hot path concentration (e.g., "Top 5 methods account for X% of runtime")
- High-allocation methods
- High-frequency methods

## Example Output

### Enhanced Profile

```
═══ Top 30 Hot Paths (by Time) ════════════════════════════════

Rank  Method                                   Time (ms)       Calls      Avg (ms)     Alloc (MB)  
────────────────────────────────────────────────────────────────────────────────────────────────────
1     Model_Medium_Inference                   2553.97         1          2553.969     734.48      
2     Model_Medium_GenerateToken               2553.92         25         102.157      734.48      
3     Model_Medium_Forward                     2553.49         25         102.140      734.48      
4     Model_Small_Inference                    447.73          1          447.725      110.33      
5     Model_Small_GenerateToken                447.66          25         17.906       110.33
```

### Profile Comparison

```
## 📊 Overall Performance Summary

| Metric | Previous | Current | Delta | Change % |
|--------|----------|---------|-------|----------|
| **Total Runtime** | 5,591.91 ms | 10,312.68 ms | +4720.77 ms | +84.4% |
| **Total Allocations** | 2,566.93 MB | 2,566.90 MB | -0.03 MB | -0.0% |

### 🎯 Performance Verdict

⚠️ **REGRESSED**: Overall performance degraded by 84.4%

## 🚀 Top 10 Improvements

| Method | Previous (ms) | Current (ms) | Delta (ms) | Change % |
|--------|---------------|--------------|------------|----------|
| `Softmax_2048` | 2.01 | 0.27 | -1.74 | -86.6% |
| `Softmax_Iteration` | 2.21 | 0.40 | -1.81 | -81.9% |
```

### Model Comparison

```
## 📊 Model Specifications

| Model | Dimensions | Layers | Heads | Parameters | Context |
|-------|-----------|--------|-------|------------|---------|
| **Small** | 128 | 2 | 4 | 470,528 | 64 |
| **Medium** | 256 | 4 | 8 | 3,454,464 | 128 |

## ⏱️ Performance Metrics

| Metric | Small Model | Medium Model | Ratio (Med/Small) |
|--------|-------------|--------------|-------------------|
| **Total Inference Time** | 447.73 ms | 2,553.97 ms | 5.70x |
| **Tokens per Second** | 55.84 | 9.79 | 0.18x |

### Computational Efficiency: **1.29x**

✅ **Excellent** - Nearly linear scaling with parameter count
```

## Programmatic Usage

### Basic Profiling

```csharp
using CodeProfiler;

using var profiler = new PerformanceProfiler();

// Profile a code section
using (profiler.BeginScope("MyOperation"))
{
    // Your code here
}

// Get hot paths
var hotPaths = profiler.GetHotPaths(10);

// Generate report
string report = profiler.GenerateReport();
await File.WriteAllTextAsync("report.md", report);
```

### Nested Profiling

```csharp
using (profiler.BeginScope("OuterOperation"))
{
    // Outer operation code
    
    using (profiler.BeginScope("InnerOperation"))
    {
        // Inner operation code
    }
    
    // More outer operation code
}

// The profiler tracks that InnerOperation was called by OuterOperation
```

## Architecture

### Core Components

1. **PerformanceProfiler**: Main profiler class
   - Thread-safe metric collection
   - Stopwatch-based timing
   - GC allocation tracking

2. **ProfileScope**: RAII-style scoping for measurements
   - Auto-starts timer on creation
   - Auto-records metrics on disposal
   - Zero allocation when profiler is disabled

3. **MethodProfile**: Per-method statistics
   - Call count
   - Total/min/max execution time
   - Memory allocations
   - Parent call tracking

4. **DeepProfiler**: Extended profiling utilities
   - SIMD operation benchmarks
   - Transformer-specific instrumentation

### Performance Overhead

- **Enabled**: <1% overhead for typical workloads
- **Disabled**: Zero overhead (early return in BeginScope)

The profiler uses efficient data structures and avoids LINQ to minimize allocations during profiling.

## Interpreting Results

### Hot Path Concentration

```
Top 5 hot paths account for 97.5% of total runtime
```

This indicates a highly concentrated hot path - optimizing these 5 methods will yield maximum impact.

### Memory Allocation Patterns

```
Transformer_Forward: 51.5 MB per call
```

High per-call allocation suggests:
- Opportunity for buffer pooling
- Potential for memory pressure/GC
- Cache efficiency concerns

### Call Hierarchy Analysis

```
Transformer_Forward
  Called by: GenerateToken (150 times)
```

Helps identify:
- Entry points for optimization
- Calling frequency patterns
- Opportunity for caching/memoization

## Troubleshooting

### "No methods profiled"

Ensure you're calling `profiler.BeginScope()` and properly disposing scopes:

```csharp
using (profiler.BeginScope("MyMethod"))
{
    // Code
} // Scope must be disposed to record metrics
```

### High profiling overhead

- Reduce scope granularity (profile larger code blocks)
- Avoid profiling in tight loops
- Consider sampling-based profiling for hot loops

### Memory measurements seem incorrect

- GC.GetTotalAllocatedBytes() includes all allocations, including GC collections
- Results are cumulative - subtract background allocation noise
- Warmup runs recommended for accurate measurement

## Comparison with Other Tools

| Feature | CodeProfiler | dotnet-trace | BenchmarkDotNet |
|---------|-------------|--------------|-----------------|
| Zero dependencies | ✓ | ✗ | ✗ |
| Call hierarchy | ✓ | ✓ | ✗ |
| Memory tracking | ✓ | ✓ | ✓ |
| Markdown reports | ✓ | ✗ | ✓ |
| Method-level | ✓ | ✓ | ✓ |
| Instruction-level | ✗ | ✓ | ✗ |
| Statistical analysis | Basic | Advanced | Advanced |

**When to use CodeProfiler:**
- Quick hot path identification
- Memory allocation analysis
- Embedded in CI/CD pipeline
- No external tool dependencies

**When to use dotnet-trace:**
- CPU sampling profiling
- System-wide performance analysis
- Advanced diagnostics

**When to use BenchmarkDotNet:**
- Micro-benchmarking
- Statistical significance testing
- Regression detection

## Contributing

To add new profiling scenarios:

1. Add methods to `DeepProfiler` class
2. Call `profiler.BeginScope()` around code to measure
3. Update report generation if needed

Example:
```csharp
public static void ProfileNewOperation(PerformanceProfiler profiler)
{
    using (profiler.BeginScope("NewOperation"))
    {
        // Operation code
    }
}
```

## License

Part of the SmallMind project - MIT License

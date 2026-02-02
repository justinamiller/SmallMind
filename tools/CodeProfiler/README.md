# SmallMind Code Profiler

A high-precision performance profiling tool for analyzing hot paths, memory allocations, and call hierarchies in SmallMind's transformer inference pipeline.

## Features

- **Method-Level Timing**: Microsecond-precision timing using Stopwatch
- **Memory Tracking**: Tracks allocations per method using GC.GetTotalAllocatedBytes()
- **Call Hierarchy**: Records parent-child relationships between method calls
- **Hot Path Analysis**: Identifies the most time-consuming code paths
- **Allocation Analysis**: Finds methods with highest memory pressure
- **Detailed Reports**: Generates markdown reports with comprehensive performance data

## Usage

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
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj -- deep-profile.md 3 50 --deep
```

**Deep profile includes:**
- SIMD operation benchmarks (MatMul, Softmax, GELU)
- Multiple matrix sizes (128×128, 256×256, 512×512)
- Detailed transformer layer profiling
- Enhanced memory allocation tracking

## Output

The profiler generates a comprehensive markdown report containing:

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

```
=== Top 10 Hot Paths ===

 1. Transformer_Forward
    Time: 23559.01 ms total, 157.060 ms avg (150 calls)
    Alloc: 7549.63 MB

 2. GenerateToken
    Time: 23562.59 ms total, 157.084 ms avg (150 calls)
    Alloc: 7549.63 MB

 3. MatMul_512x512
    Time: 299.61 ms total, 299.608 ms avg (1 calls)
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

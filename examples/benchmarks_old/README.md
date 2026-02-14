# SmallMind Benchmarks

This directory contains performance benchmarking tools for SmallMind's SIMD-optimized operations.

## Overview

The benchmark suite measures performance of core SIMD kernels and generates comprehensive reports with full system metadata for reproducibility.

## Running Benchmarks

To run the benchmark suite:

```bash
cd benchmarks
dotnet run --configuration Release
```

**Important:** Always run benchmarks in Release mode for accurate results. Debug builds will show a warning.

## Benchmark Reports

After running benchmarks, two report files are generated:

- **`benchmark-results.md`** - Human-readable Markdown report with environment metadata and results
- **`benchmark-results.json`** - Machine-readable JSON report for programmatic analysis

### Report Contents

Each report includes:

#### Environment Metadata
- **Machine/Hardware**
  - CPU Architecture (x64, ARM64, etc.)
  - Logical core count
  - CPU model/brand string
  - SIMD capabilities (Vector size, AVX, AVX2, AVX-512, ARM NEON, etc.)
  - Memory (total and available)
  - Endianness
  - NUMA awareness
  
- **Operating System**
  - Platform (Windows, Linux, macOS)
  - OS name and version
  - Kernel version
  
- **.NET Runtime**
  - .NET version
  - Runtime identifier (RID)
  - Framework description
  - GC mode (Server vs Workstation)
  - GC latency mode
  - Tiered JIT compilation status
  - ReadyToRun status
  
- **Process**
  - Bitness (32-bit vs 64-bit)
  - Priority class
  
- **Build**
  - Configuration (Release/Debug)
  - Target framework
  - Compilation timestamp
  - Git commit hash (if available)

#### Benchmark Results

Each benchmark includes:
- Operation name
- Parameters (size, iterations, etc.)
- Performance metrics (time, throughput, GFLOPS)
- Timestamp

### Example Report

```markdown
# SmallMind Benchmark Results

## Environment

### Machine
| Property | Value |
|----------|-------|
| CPU Architecture | X64 |
| Logical Cores | 16 |
| CPU Model | AMD EPYC 7763 64-Core Processor |
| SIMD Width (Vector<float>) | 8 |
| AVX2 | Supported |
...

## Benchmark Results

### Matrix Multiplication
**Parameters:**
- M: 512
- K: 512
- N: 512

**Metrics:**
| Metric | Value |
|--------|-------|
| Time (ms/op) | 7.210 ms |
| Performance (GFLOPS) | 37.23 GFLOPS |
```

## Current Benchmarks

The suite currently includes:

1. **Element-wise Add** - Vector addition throughput
2. **ReLU Activation** - Neural network activation function
3. **Softmax** - Probability normalization (2D)
4. **Matrix Multiplication** - Core linear algebra operation
5. **Dot Product** - Vector inner product

## Reproducibility

All benchmarks capture complete system metadata to ensure results are:
- **Comparable** across different machines
- **Reproducible** with full environment context
- **Auditable** for performance regression tracking

## Adding New Benchmarks

To add a new benchmark:

1. Create a benchmark method following the pattern in `SimdBenchmarks.cs`
2. Record results using `BenchmarkResult` class
3. Add result to `_report.Results`

Example:
```csharp
static void BenchmarkMyOperation()
{
    // Setup and warmup
    // ...
    
    // Measure
    var sw = Stopwatch.StartNew();
    // ... run operation
    sw.Stop();
    
    // Record
    var result = new BenchmarkResult
    {
        Name = "My Operation",
        Timestamp = DateTime.UtcNow,
        Parameters = new Dictionary<string, object>
        {
            ["Size"] = size
        },
        Metrics = new Dictionary<string, double>
        {
            ["Time (ms/op)"] = msPerOp
        }
    };
    _report.Results.Add(result);
}
```

## Implementation Details

- **No external dependencies** - Pure C# implementation
- **Cross-platform** - Runs on Windows, Linux, and macOS
- **Safe metadata collection** - All probes wrapped in try/catch
- **SIMD detection** - Automatic detection of CPU capabilities
- **Structured output** - Both Markdown and JSON formats

## Files and Directories

- `SimdBenchmarks.cs` - Main benchmark harness
- `SystemInfoCollector.cs` - System metadata collection
- `SystemInfo.cs` - Data models for metadata
- `BenchmarkResult.cs` - Benchmark result data structures
- `MarkdownReportWriter.cs` - Markdown report generator
- `JsonReportWriter.cs` - JSON report generator
- `results/` - Historical benchmark results and test artifacts (excluded from git)

## Additional Documentation

See [HOW_TO_RUN_BENCHMARKS.md](HOW_TO_RUN_BENCHMARKS.md) for detailed instructions on running benchmarks and interpreting results.

Historical benchmark reports and analysis can be found in [/docs/benchmarks/](../docs/benchmarks/).


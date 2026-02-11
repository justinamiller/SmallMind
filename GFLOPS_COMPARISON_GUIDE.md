# GFLOPS Performance Comparison - How to Use

This document explains how to use the GFLOPS comparison benchmark suite to compare PR #192 and PR #193.

## Quick Start

### Option 1: Run on Current Branch Only (Quick Test)

```bash
./run-quick-gflops-test.sh
```

This will:
- Build the benchmark in Release mode
- Run all tests on the current branch
- Generate `GFLOPS_COMPARISON_RESULTS.json` and `GFLOPS_COMPARISON_RESULTS.md`

### Option 2: Compare All Three Branches (Full Comparison)

```bash
./run-gflops-comparison.sh
```

This will:
1. Run benchmarks on `main` branch (baseline)
2. Run benchmarks on `copilot/push-smallmind-matmuls-to-60-gflops` (PR #192)
3. Run benchmarks on `copilot/optimize-matrix-multiplication` (PR #193)
4. Generate a comparison report in `benchmark-results/gflops-comparison/`

**Note:** This script will automatically stash any uncommitted changes, switch branches, run tests, and restore your original state.

## Understanding the Results

### Key Metrics

The benchmark measures:

1. **GFLOPS** - Billion floating-point operations per second
   - Higher is better
   - Target: 60+ GFLOPS for optimized implementations

2. **Memory Allocations** - Bytes allocated per operation
   - Lower is better
   - Target: 0 bytes/op (zero-allocation)

3. **GC Collections** - Garbage collection events
   - Lower is better
   - Target: 0 collections

4. **Memory Bandwidth** - Estimated GB/s throughput
   - Higher is better
   - Limited by hardware (typically 20-40 GB/s for DDR4)

### Workload Categories

#### Square Matrices
- **64×64 to 256×256**: L1/L2 cache friendly
- **512×512 to 1024×1024**: L2/L3 cache friendly
- **2048×2048**: Memory bandwidth bound

#### LLM-Specific Workloads
- **M=1 (Single Token)**: Most important for inference speed
- **M=32 (Batch)**: Important for serving multiple users
- **M=256, M=512 (Prefill)**: Important for initial context processing

### Sustained Throughput
- 30-second continuous test
- Tests JIT stability and thermal behavior
- Important for real-world workloads

## Interpreting Comparison Results

After running the full comparison, examine the `COMPARISON_REPORT.md` file:

### Look For

1. **Peak GFLOPS**: Which implementation achieves the highest performance?
2. **Average GFLOPS**: Which is more consistent across workloads?
3. **Zero-Allocation Count**: Which has better memory efficiency?
4. **M=1 Performance**: Critical for inference - check detailed reports
5. **Prefill Performance**: Important for initial prompt processing

### Example Analysis

If you see:

```
PR #192: Peak 60.2 GFLOPS, Avg 38.5, Zero-Alloc 15/18
PR #193: Peak 66.1 GFLOPS, Avg 41.2, Zero-Alloc 12/18
```

**Interpretation:**
- PR #193 has better peak and average GFLOPS (faster)
- PR #192 has better memory efficiency (fewer allocations)
- Trade-off: Speed vs. Memory efficiency

**Recommendation:**
- For inference-heavy workloads: Consider PR #193 (faster)
- For memory-constrained scenarios: Consider PR #192 (fewer allocations)
- Check M=1 performance specifically in detailed reports

## Advanced Usage

### Modify Test Parameters

Edit `benchmarks/GFLOPSComparisonBenchmark/Program.cs`:

```csharp
// Change matrix sizes
private readonly int[] _matrixSizes = new[] { 128, 256, 512 };

// Change LLM workloads
private readonly (int M, int K, int N, string Name)[] _llmWorkloads = new[]
{
    (1, 512, 512, "Single Token Decode"),
    // Add more...
};

// Change sustained test duration
while (sw.Elapsed.TotalSeconds < 10)  // Reduced from 30s
```

### Run with Different JIT Settings

```bash
# Disable tiered PGO
DOTNET_TieredPGO=0 ./run-quick-gflops-test.sh

# Disable tiered compilation  
DOTNET_TieredCompilation=0 ./run-quick-gflops-test.sh

# Both disabled
DOTNET_TieredPGO=0 DOTNET_TieredCompilation=0 ./run-quick-gflops-test.sh
```

### Extract Specific Metrics

From JSON output:

```bash
# Get peak GFLOPS
cat GFLOPS_COMPARISON_RESULTS.json | grep -o '"MaxGFLOPS":[^,]*'

# Get average GFLOPS
cat GFLOPS_COMPARISON_RESULTS.json | grep -o '"AvgGFLOPS":[^,]*'

# Get zero-allocation count
cat GFLOPS_COMPARISON_RESULTS.json | grep -o '"ZeroAllocationCount":[^,]*'
```

## Troubleshooting

### Benchmark Takes Too Long

The benchmark can take 5-10 minutes depending on matrix sizes. To speed up:

1. Edit `Program.cs` and reduce matrix sizes:
   ```csharp
   private readonly int[] _matrixSizes = new[] { 128, 256, 512 };
   ```

2. Reduce sustained test duration:
   ```csharp
   while (sw.Elapsed.TotalSeconds < 10)  // Was 30
   ```

3. Reduce iterations in `DetermineIterationCount()`

### Low GFLOPS Results

Check:
- Running in Release mode (`-c Release`)
- AVX2/FMA support detected (check system info in output)
- Not running in debug mode
- No background processes consuming CPU
- Thermal throttling not occurring

### Branch Switching Fails

If the comparison script fails to switch branches:
- Commit or stash your changes first
- Check that the branch names are correct
- Ensure you have the branches locally

### Build Errors

If benchmark fails to build:
```bash
cd benchmarks/GFLOPSComparisonBenchmark
dotnet clean
dotnet restore
dotnet build -c Release
```

## Output Files

### Single Branch Test
- `benchmarks/GFLOPSComparisonBenchmark/GFLOPS_COMPARISON_RESULTS.json`
- `benchmarks/GFLOPSComparisonBenchmark/GFLOPS_COMPARISON_RESULTS.md`

### Multi-Branch Comparison
- `benchmark-results/gflops-comparison/Baseline_Main_results.json`
- `benchmark-results/gflops-comparison/Baseline_Main_results.md`
- `benchmark-results/gflops-comparison/PR_192_GemmMicrokernels_results.json`
- `benchmark-results/gflops-comparison/PR_192_GemmMicrokernels_results.md`
- `benchmark-results/gflops-comparison/PR_193_FixedIndexing_results.json`
- `benchmark-results/gflops-comparison/PR_193_FixedIndexing_results.md`
- `benchmark-results/gflops-comparison/COMPARISON_REPORT.md`

## Next Steps

After running the benchmarks:

1. Review the comparison report
2. Check detailed metrics for each workload
3. Consider trade-offs (speed vs. memory)
4. Make a decision on which PR to merge
5. Document the decision rationale

## See Also

- [GFLOPSComparisonBenchmark README](benchmarks/GFLOPSComparisonBenchmark/README.md)
- [PR #192](https://github.com/justinamiller/SmallMind/pull/192)
- [PR #193](https://github.com/justinamiller/SmallMind/pull/193)
- [StandardLLMBenchmarks](benchmarks/StandardLLMBenchmarks/README.md)

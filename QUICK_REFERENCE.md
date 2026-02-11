# GFLOPS Comparison - Quick Reference Card

## 🚀 Quick Start

```bash
# Compare all three branches (recommended)
./run-gflops-comparison.sh

# Or test current branch only
./run-quick-gflops-test.sh
```

## 📊 What Gets Measured

| Metric | Description | Target |
|--------|-------------|--------|
| **GFLOPS** | Computational throughput | 60+ GFLOPS |
| **Allocations** | Memory per operation | 0 bytes/op |
| **GC** | Garbage collections | 0 collections |
| **M=1 GFLOPS** | Single-token decode (inference) | Critical metric |

## 📁 Output Locations

### Single Branch Test
```
benchmarks/GFLOPSComparisonBenchmark/
├── GFLOPS_COMPARISON_RESULTS.json
└── GFLOPS_COMPARISON_RESULTS.md
```

### Full Comparison
```
benchmark-results/gflops-comparison/
├── Baseline_Main_results.{json,md}
├── PR_192_GemmMicrokernels_results.{json,md}
├── PR_193_FixedIndexing_results.{json,md}
└── COMPARISON_REPORT.md ← START HERE
```

## 🔍 Key Results to Check

### 1. COMPARISON_REPORT.md
- Peak GFLOPS for each PR
- Average GFLOPS for each PR
- Zero-allocation test counts

### 2. Individual Results (*.md files)
- M=1 performance (critical for inference)
- Prefill performance (M=256, M=512)
- Memory allocations per workload
- GC pressure indicators

## 🎯 Decision Framework

### Choose PR #192 if:
- ✅ Memory efficiency is critical
- ✅ Zero allocations required
- ✅ Prefill performance is priority
- ⚠️ Can accept lower M=1 performance

### Choose PR #193 if:
- ✅ Peak performance is critical
- ✅ Inference (M=1) is priority
- ✅ Small-to-medium matrices are common
- ⚠️ Can accept some allocations

## 📖 Documentation

| Document | Purpose |
|----------|---------|
| `PERFORMANCE_TEST_SUMMARY.md` | Executive summary |
| `GFLOPS_COMPARISON_GUIDE.md` | Complete usage guide |
| `benchmarks/.../README.md` | Technical reference |
| `COMPARISON_REPORT.md` | **Comparison results** |

## ⚡ Common Commands

```bash
# Run full comparison
./run-gflops-comparison.sh

# Quick test on current branch
./run-quick-gflops-test.sh

# View comparison report
cat benchmark-results/gflops-comparison/COMPARISON_REPORT.md

# View main branch results
cat benchmark-results/gflops-comparison/Baseline_Main_results.md

# View PR #192 results
cat benchmark-results/gflops-comparison/PR_192_GemmMicrokernels_results.md

# View PR #193 results
cat benchmark-results/gflops-comparison/PR_193_FixedIndexing_results.md

# Extract peak GFLOPS from JSON
grep -o '"MaxGFLOPS":[^,]*' benchmark-results/gflops-comparison/*.json
```

## 🔧 Troubleshooting

### Benchmark Too Slow?
Edit `benchmarks/GFLOPSComparisonBenchmark/Program.cs`:
```csharp
// Reduce matrix sizes
private readonly int[] _matrixSizes = new[] { 128, 256, 512 };

// Reduce sustained test duration  
while (sw.Elapsed.TotalSeconds < 10)  // Was 30
```

### Low GFLOPS?
Check:
- [ ] Running in Release mode
- [ ] AVX2/FMA detected (check system info)
- [ ] No background CPU load
- [ ] Not thermal throttling

### Build Errors?
```bash
cd benchmarks/GFLOPSComparisonBenchmark
dotnet clean
dotnet restore
dotnet build -c Release
```

## 📝 PR Comparison Context

### PR #192: GemmMicrokernels Routing
- Routes MatMulOps → GemmMicrokernels
- Zero allocations via Span-based design
- Multi-level cache blocking (L1/L2/L3)
- 60+ GFLOPS on 128×128

### PR #193: A-Indexing Bug Fix
- Fixes `A[0 * K + k]` → `A[0 * ldA + k]`
- Was reading garbage memory
- 60+ GFLOPS on 128×128, 256×256
- Improved correctness

## 🎓 Understanding Results

### GFLOPS Targets (CPU)
- **Excellent:** >60 GFLOPS
- **Good:** 40-60 GFLOPS
- **Acceptable:** 20-40 GFLOPS
- **Needs Work:** <20 GFLOPS

### Allocations
- **Optimal:** 0 bytes/op
- **Acceptable:** <100 bytes/op
- **Warning:** >1KB/op
- **Critical:** >10KB/op

### Critical Workload: M=1
- Most important for inference
- Typical autoregressive generation
- Should be fast (>5 GFLOPS minimum)

---

**Start here:** Run `./run-gflops-comparison.sh` then check `COMPARISON_REPORT.md`

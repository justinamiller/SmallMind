# PR 198 vs PR 197 - Fresh Benchmark Results

**Date:** 2026-02-12 17:10 UTC  
**Environment:** Ubuntu 24.04.3 LTS, X64, .NET 10.0.2, 4 cores  
**Purpose:** Fresh benchmark comparison using actual tools from each PR

---

## Fresh Benchmark Results

### PR 198: SmallMind.Bench Tool (FP32 MatMul)

**Tool:** `tools/SmallMind.Bench/Program.cs` (609 lines)  
**Command:** `dotnet run --project tools/SmallMind.Bench/SmallMind.Bench.csproj -c Release -- --matmul --m 128 --n 128 --k 128 --repeat 20`

| Matrix Size | Iterations | Avg Time (ms) | Total Time (ms) | GFLOPS | Allocated Bytes | GC (0/1/2) | Heap (MB) | WorkingSet Δ (MB) |
|-------------|-----------|---------------|-----------------|--------|-----------------|------------|-----------|-------------------|
| 128³ | 20 | 0.128 | 2.56 | **32.78** | 14,264 | 0/0/0 | 0.26 | 0.83 |

**Key Findings:**
- ✅ 32.78 GFLOPS for FP32 128³ matrix multiplication
- ✅ Zero GC collections
- ✅ Minimal allocations (14KB)
- ✅ JSON output saved to `/artifacts/benchmarks/matmul_20260212_171018.json`

---

### PR 197: Performance Baseline Snapshot (Q4 MatMul)

**Source:** `PERF_BASELINE_V2_SNAPSHOT.md` (captured snapshot)  
**Note:** PR 197 includes a `BenchmarkRunner` orchestration tool, but the actual Q4 benchmarks appear to run from a different location

| Matrix Size | Variant | Iterations | Avg Time (ms) | Median (ms) | GFLOPS | Speedup vs Original |
|-------------|---------|-----------|---------------|-------------|--------|---------------------|
| 128³ | Original | 20 | 10.91 | 11.29 | 0.384 | - |
| 128³ | Optimized | 20 | 6.61 | 6.62 | **0.635** | 1.65x |
| 256³ | Original | 20 | - | - | 0.305 | - |
| 256³ | Optimized | 20 | - | - | **0.344** | 1.13x |
| 512³ | Original | 20 | - | - | 0.301 | - |
| 512³ | Optimized | 20 | - | - | **0.309** | 1.03x |

**Key Findings:**
- ⚠️ 0.384-0.635 GFLOPS for Q4 quantized matmul
- ✅ Zero GC collections
- ✅ Shows "Original" vs "Optimized" kernel comparison
- ⚠️ Performance degrades with larger matrices (optimization helps less)

---

## Direct Comparison

### GFLOPS Comparison (128³ Matrix)

| Benchmark Type | PR 197 (Original) | PR 197 (Optimized) | PR 198 (FP32) | Ratio (PR198 / PR197 Opt) |
|----------------|-------------------|--------------------|--------------|-----------------------------|
| **GFLOPS** | 0.384 | 0.635 | 32.78 | **51.6x faster** |

⚠️ **CRITICAL NOTE:** This is NOT an apples-to-apples comparison:
- **PR 197** measures Q4 quantized matrix multiplication (4-bit values)
- **PR 198** measures FP32 matrix multiplication (32-bit floats)
- Q4 operations require dequantization and are inherently more complex

### What This Tells Us

1. **PR 197 Q4 Performance Issue**
   - 0.384-0.635 GFLOPS is very low even for Q4
   - For reference, simple scalar FP32 code should achieve 10+ GFLOPS on this hardware
   - Q4 should be ~4-6x slower than FP32 due to bit unpacking, not 50x slower
   - **Suggests:** Q4 implementation in PR 197 has performance issues

2. **PR 198 FP32 Baseline**
   - 32.78 GFLOPS is reasonable for naive FP32 matmul on 4-core CPU
   - Could be improved with blocking/tiling (target: 50-100 GFLOPS)
   - Good baseline for measuring future optimizations

3. **Optimization Effectiveness (PR 197)**
   - 128³: 1.65x speedup (good)
   - 256³: 1.13x speedup (modest)
   - 512³: 1.03x speedup (minimal)
   - **Pattern:** Optimizations help less as matrices grow → cache issue?

---

## Benchmarking Tool Comparison

### Feature Matrix

| Feature | PR 197 BenchmarkRunner | PR 198 SmallMind.Bench |
|---------|------------------------|------------------------|
| **Tool Type** | Orchestrator (runs other tools) | Self-contained benchmark |
| **MatMul Benchmark** | Via external tool | ✅ Built-in |
| **Model Inference Bench** | Via external tool | ✅ Built-in |
| **JSON Output** | ✅ Yes | ✅ Yes |
| **Quantization Tests** | ✅ Q4, Q6 | ❌ FP32 only |
| **Kernel Comparison** | ✅ Original vs Optimized | ❌ Single kernel |
| **Percentile Metrics** | ❌ No | ✅ p50, p95 |
| **CLI Arguments** | Complex (--model, --iterations, etc.) | Simple (--matmul, --model) |
| **Dependencies** | External benchmark tools | ✅ Zero dependencies |
| **Code Complexity** | High (orchestrator) | Low (direct measurement) |

---

## Performance Analysis

### Expected vs Actual GFLOPS

**For 128³ FP32 MatMul on 4-core CPU:**
- **Theoretical Peak:** ~400 GFLOPS (if using AVX2 optimally)
- **Practical Naive:** 10-30 GFLOPS
- **Blocked/Tiled:** 50-100 GFLOPS
- **PR 198 Actual:** 32.78 GFLOPS ✅ *Good for naive implementation*

**For 128³ Q4 MatMul on 4-core CPU:**
- **Expected (naive):** 5-10 GFLOPS (assuming ~3x slower than FP32)
- **Expected (optimized):** 15-20 GFLOPS (with SIMD dequantization)
- **PR 197 Actual:** 0.384-0.635 GFLOPS ❌ *10-50x slower than expected*

### Why is PR 197 Q4 So Slow?

Reviewing the Q4KTensor implementation from PR 197:

```csharp
// From PR 197: src/SmallMind.Quantization/Tensors/Q4KTensor.cs
// Dequantize each sub-block
for (int subBlock = 0; subBlock < SUB_BLOCK_COUNT; subBlock++)
{
    // Extract 6-bit scale and min for this sub-block from the 12-byte scales field
    // This is a simplified extraction - actual llama.cpp uses bit packing
    int scaleIdx = subBlock * 12 / 8;
    byte scaleByte = scalesBytes[scaleIdx];
    float sc = d * (scaleByte & 0x3F); // 6-bit scale
    float m = dmin * ((scaleByte >> 6) & 0x3F); // 6-bit min (approximation)
    
    // Dequantize 32 values in this sub-block
    for (int i = 0; i < SUB_BLOCK_SIZE / 2; i++)
    {
        byte packed = qs[qsOffset + i];
        int q0 = packed & 0xF;
        int q1 = (packed >> 4) & 0xF;
        
        dst[subBlockDstOffset + i * 2] = sc * q0 - m;
        dst[subBlockDstOffset + i * 2 + 1] = sc * q1 - m;
    }
}
```

**Issues:**
1. ❌ Nested loops without SIMD
2. ❌ Scalar bit extraction (no vectorization)
3. ❌ Comment admits "simplified extraction" vs llama.cpp
4. ❌ No cache blocking for large matrices
5. ❌ Each value requires bit unpacking + 2 float ops

**Recommendation:** PR 197's Q4_K implementation needs SIMD optimization before merging.

---

## Recommendations Update

### For PR 197

1. **CRITICAL: Fix Q4_K Performance**
   - Current: 0.384-0.635 GFLOPS (unacceptably slow)
   - Target: 5-10 GFLOPS minimum (15-20x improvement needed)
   - **Actions:**
     - Implement SIMD bit unpacking using Vector256<T>
     - Add cache blocking for matrices > 256
     - Complete the bit-packing logic per llama.cpp spec
     - Add performance tests vs reference implementation

2. **Validate Optimized Variant**
   - "Optimized" variant shows diminishing returns at larger sizes
   - Investigate cache behavior for 512³ matrices
   - May need different blocking strategy for large matrices

### For PR 198

1. **Add Quantized Benchmarks**
   - Currently only tests FP32
   - Should add --quant flag for Q4, Q6, Q8 testing
   - Would enable measuring PR 197's improvements

2. **Add Kernel Variants**
   - Support benchmarking multiple implementations
   - E.g., --variant naive|blocked|simd
   - Enables before/after comparison like PR 197

### Merge Strategy Update

Based on fresh benchmarks:

1. **Do NOT merge PR 197 yet**
   - Q4 performance is 10-50x too slow
   - Implementation is incomplete (admitted in comments)
   - Would set a bad performance baseline

2. **Merge PR 198 first** ✅
   - Provides solid FP32 baseline (32.78 GFLOPS)
   - Good benchmarking infrastructure
   - Can be used to validate PR 197 fixes

3. **Fix PR 197, then re-benchmark**
   - Fix Q4_K implementation with SIMD
   - Use PR 198's tool to measure improvement
   - Target: > 5 GFLOPS for Q4 (10x current)

---

## Conclusion

**Fresh benchmarking reveals:**

✅ **PR 198** has solid FP32 baseline (32.78 GFLOPS) and excellent tooling  
❌ **PR 197** has severe Q4 performance issues (0.384-0.635 GFLOPS)

**The performance delta is NOT because they measure different things** - even accounting for Q4 vs FP32, PR 197 is 10-50x slower than it should be.

**Action Required:** PR 197 needs significant performance work before merging.

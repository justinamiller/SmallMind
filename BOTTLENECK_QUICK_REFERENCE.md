# SmallMind - Bottleneck Quick Reference

**Source:** CLR Profiler Analysis (2026-02-03)  
**Full Report:** [CLR_PROFILER_BOTTLENECK_ANALYSIS.md](CLR_PROFILER_BOTTLENECK_ANALYSIS.md)

---

## 🎯 Top 3 Critical Bottlenecks

### 1. Attention Score Computation ⚡ **P0 - CRITICAL**
**Impact:** 40-50% of forward pass time  
**Speedup Potential:** **3-4x faster**

**Problem:** Using nested dot product loops instead of batched matrix multiplication
```csharp
// ❌ SLOW: O(T²) dot product calls
for (int i = 0; i < T; i++)
    for (int j = 0; j <= i; j++)
        scores[i,j] = DotProduct(q[i], k[j]) * scale;

// ✅ FAST: Single batched MatMul
MatMulOps.MatMulTransposeB(Q, K, scores, T, headSize, T);
```

**Files:** `Transformer.cs` lines 1003-1079, `MatMulOps.cs`

---

### 2. Attention Value Aggregation ⚡ **P0 - CRITICAL**
**Impact:** 30-40% of forward pass time  
**Speedup Potential:** **3-4x faster**

**Problem:** Triple nested loop instead of matrix multiplication
```csharp
// ❌ SLOW: Triple nested loop
for (int i = 0; i < T; i++)
    for (int d = 0; d < headSize; d++)
        for (int j = 0; j < T; j++)
            output[i,d] += attention[i,j] * value[j,d];

// ✅ FAST: Single MatMul
MatMulOps.MatMul(attention, value, output, T, T, headSize);
```

**Files:** `Transformer.cs` lines 1185-1240

---

### 3. Memory Allocations 💾 **P1 - HIGH**
**Impact:** 46.9 MB per token, 99.57% of allocations  
**Reduction Potential:** **74-82% less memory**

**Problem:** No tensor pooling for intermediate buffers
```csharp
// ❌ SLOW: Allocates every forward pass
var qkv = new Tensor(shape);  // 46.9 MB/token!

// ✅ FAST: Reuse from pool
var buffer = _pool.Rent(size);
```

**Solutions:**
- Implement `TensorPool` for buffer reuse
- Add `InferenceKVCache` to avoid recomputation
- Use `ArrayPool<float>` for gradients

**Files:** Create `TensorPool.cs`, `InferenceKVCache.cs`, modify `Transformer.cs`

---

## 📊 Performance Metrics

| Metric | Current | Target | Improvement |
|--------|---------|--------|-------------|
| **Tokens/second** | 6.2 | 18-25 | **+190-300%** |
| **Memory/token** | 46.9 MB | 8-12 MB | **-74-82%** |
| **Forward pass** | 48 ms | 12-15 ms | **+220-300%** |
| **MatMul GFLOPS** | 16.1 | 27-34 | **+68-111%** |

---

## 🗺️ Optimization Roadmap

### Phase 1: Critical Wins (Week 1) - **4-5x speedup**
1. ✅ **Attention Scores** - Batched MatMul
2. ✅ **Attention Values** - Batched MatMul  
3. ✅ **Tensor Pooling** - Reduce allocations

**Result:** 6.2 → 27-31 tokens/sec

### Phase 2: Infrastructure (Weeks 2-3) - **2-2.5x more**
1. ✅ **KV-Cache** - Avoid recomputation
2. ✅ **MatMul Tiling** - Cache blocking
3. ✅ **SIMD Softmax** - Vectorization

**Result:** 27-31 → 54-78 tokens/sec

### Phase 3: Advanced (Weeks 4-6) - **1.3-1.5x more**
1. ✅ **LayerNorm SIMD** - Vectorized normalization
2. ✅ **Embedding Grad** - Local buffering (training)
3. ✅ **Model Init** - Faster initialization

**Result:** 54-78 → 70-117 tokens/sec

---

## 🔧 Quick Implementation Guide

### 1. Add BatchedMatMul to MatMulOps.cs
```csharp
public static void MatMulTransposeB(
    ReadOnlySpan<float> A, ReadOnlySpan<float> B, Span<float> C,
    int M, int K, int N)
{
    C.Clear();
    for (int i = 0; i < M; i++)
        for (int k = 0; k < K; k++)
        {
            float aik = A[i * K + k];
            for (int j = 0; j < N; j++)
                C[i * N + j] += aik * B[j * K + k];
        }
}
```

### 2. Update Attention Score Computation
```csharp
private void ComputeAttentionScoresInPlace(Tensor q, Tensor k, Tensor scores, int B, int T)
{
    float scale = 1.0f / MathF.Sqrt(_headSize);
    
    Parallel.For(0, B * _nHead, bh =>
    {
        int offset = bh * T * _headSize;
        int scoreOffset = bh * T * T;
        
        var qBh = new ReadOnlySpan<float>(q.Data, offset, T * _headSize);
        var kBh = new ReadOnlySpan<float>(k.Data, offset, T * _headSize);
        var scoresBh = new Span<float>(scores.Data, scoreOffset, T * T);
        
        MatMulOps.MatMulTransposeB(qBh, kBh, scoresBh, T, _headSize, T);
        
        // Apply scale and causal mask
        for (int i = 0; i < T; i++)
            for (int j = 0; j < T; j++)
                scoresBh[i * T + j] = (j <= i) ? scoresBh[i * T + j] * scale : float.NegativeInfinity;
    });
    
    ApplySoftmaxInPlace(scores, B, T);
}
```

### 3. Update Attention Value Aggregation
```csharp
private void ApplyAttentionInPlace(Tensor att, Tensor v, Tensor output, int B, int T)
{
    Parallel.For(0, B * _nHead, bh =>
    {
        int attOffset = bh * T * T;
        int vOffset = bh * T * _headSize;
        int outOffset = bh * T * _headSize;
        
        var attBh = new ReadOnlySpan<float>(att.Data, attOffset, T * T);
        var vBh = new ReadOnlySpan<float>(v.Data, vOffset, T * _headSize);
        var outBh = new Span<float>(output.Data, outOffset, T * _headSize);
        
        MatMulOps.MatMul(attBh, vBh, outBh, T, T, _headSize);
    });
}
```

---

## 🧪 Testing & Validation

### Before Making Changes
```bash
cd tools/CodeProfiler
dotnet run -c Release -- --deep > baseline.txt
```

### After Each Optimization
```bash
dotnet run -c Release -- --deep > optimized.txt
# Compare results
diff baseline.txt optimized.txt
```

### Correctness Check
```csharp
// Verify output matches within tolerance
var baseline = model.Forward(input);
var optimized = optimizedModel.Forward(input);
Assert.AreEqual(baseline.Data, optimized.Data, delta: 1e-5);
```

---

## 📈 Expected Results by Phase

| Phase | Tokens/sec | Memory/token | Forward (ms) | vs Baseline |
|-------|-----------|--------------|--------------|-------------|
| **Baseline** | 6.2 | 46.9 MB | 48.0 | - |
| **Phase 1** | 27-31 | 8-12 MB | 12-15 | **+335-400%** |
| **Phase 2** | 54-78 | 5-8 MB | 6-8 | **+770-1,160%** |
| **Phase 3** | 70-117 | 3-5 MB | 4-6 | **+1,030-1,790%** |

---

## 🎓 Key Learnings

1. **Batched operations >> Individual operations**  
   Single MatMul is 3-4x faster than many dot products

2. **Memory pooling is critical**  
   46.9 MB/token → 3-5 MB/token saves 90%+ allocations

3. **SIMD requires proper data layout**  
   Contiguous memory access patterns enable vectorization

4. **Profile first, optimize second**  
   Focus on the 20% of code consuming 80% of time

---

## 📚 See Also

- **Full Analysis:** [CLR_PROFILER_BOTTLENECK_ANALYSIS.md](CLR_PROFILER_BOTTLENECK_ANALYSIS.md)
- **Existing Docs:**
  - `PROFILER_HOT_PATHS_ANALYSIS_2026-02-03.md` - Previous analysis
  - `PERFORMANCE_OPTIMIZATIONS.md` - General optimization guide
  - `PERFORMANCE_QUICK_REFERENCE.md` - Performance tips
  - Custom instruction file - Performance guidelines

- **Tools:**
  - `tools/CodeProfiler/` - SmallMind profiler
  - `benchmarks/SimdBenchmarks.csproj` - SIMD benchmarks
  - `benchmarks/TrainingBenchmark/` - Training performance

---

**Generated:** 2026-02-03  
**Next Review:** After Phase 1 implementation

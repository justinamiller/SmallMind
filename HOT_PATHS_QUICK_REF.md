# SmallMind Hot Paths - Quick Reference Card
**Date:** 2026-02-03  
**One-Page Summary**

---

## 📊 Current Performance

| Metric | Value |
|--------|-------|
| **Tokens/sec** | 6.4 |
| **Memory/token** | 51.5 MB |
| **MatMul (512×512)** | 16.3 GFLOPS |
| **Forward pass time** | ~51 ms/token |

---

## 🔥 Top 5 Bottlenecks

1. **No tensor pooling** → 99.6% memory allocated in forward pass
2. **Attention uses loops** → Should use batched MatMul (Q @ K^T)
3. **Softmax computes masked positions** → Wastes 50% of exp() calls
4. **No KV-Cache** → Recomputes all previous tokens
5. **LayerNorm not vectorized** → Scalar loop instead of SIMD

---

## ⚡ Phase 1 Quick Wins (1 Week)

### 1. TensorPool (2 days) → **+40% speed, -84% memory**
```csharp
// Create once:
var pool = new TensorPool();

// Use instead of new Tensor():
var buffer = pool.Get("qkv", B * T * 3 * nEmbd);
```
**Files:** Create `TensorPool.cs`, modify `Transformer.cs`

### 2. Batched MatMul (3 days) → **+150% attention speed**
```csharp
// Replace loops in ComputeAttentionScores:
MatMulOps.BatchedMatMul(Q, K_T, scores, B*nHead, T, headSize, T);
```
**Files:** Add to `MatMulOps.cs`, modify `Transformer.cs` lines 501-591

### 3. Masked Softmax (1 day) → **+100% softmax**
```csharp
// Skip j > i:
for (int j = 0; j <= i; j++)  // Not j < T
    scores[i,j] = exp(...);
```
**Files:** `Transformer.cs` lines 593-686

### 4. SIMD LayerNorm (4 hours) → **+160% LayerNorm**
```csharp
for (int f = 0; f <= features - vecSize; f += vecSize) {
    var vec = new Vector<float>(input, offset + f);
    // vectorized normalize
}
```
**Files:** `LayerNormOps.cs` line 65

**Week 1 Total:** 6.4 → ~30 tokens/sec (**4.7x faster**)

---

## 🚀 Phase 2 (Weeks 2-3)

5. **KV-Cache** → +50% inference
6. **MatMul blocking** → +20% MatMul  
7. **ArrayPool gradients** → +10% training

**Week 3 Total:** 30 → ~60 tokens/sec (**9.4x total**)

---

## 📁 Key Files

**Core:**
- `src/SmallMind.Transformers/Core/Transformer.cs` - Main forward pass
- `src/SmallMind.Core/Simd/MatMulOps.cs` - Matrix multiply
- `src/SmallMind.Core/Core/LayerNormOps.cs` - Normalization

**To Create:**
- `src/SmallMind.Core/Memory/TensorPool.cs`
- `src/SmallMind.Transformers/Inference/KVCache.cs`

---

## 🧪 Quick Test

```bash
# Before:
cd tools/CodeProfiler && dotnet run -c Release -- --deep

# After changes:
cd tools/CodeProfiler && dotnet run -c Release -- --deep

# Should see:
# - Memory: 51.5 MB → ~8 MB per token ✓
# - Time: 51 ms → ~15-20 ms per forward ✓
```

---

## 📈 Expected Results

| Phase | Tokens/sec | Memory/tok | Time |
|-------|-----------|-----------|------|
| Now | 6.4 | 51.5 MB | 51 ms |
| Week 1 | 30 | 8 MB | 15 ms |
| Week 3 | 60 | 5 MB | 8 ms |
| Week 6 | 75+ | 3 MB | 6 ms |

---

## 🎯 Priority

**Do THIS WEEK:**
1. ✅ TensorPool
2. ✅ BatchedMatMul  
3. ✅ Masked softmax
4. ✅ SIMD LayerNorm

**Result:** 4.7x faster in 5 days!

---

**Full Details:** See `PROFILER_HOT_PATHS_ANALYSIS_2026-02-03.md`  
**Implementation:** See `NEXT_OPTIMIZATION_PHASES.md`

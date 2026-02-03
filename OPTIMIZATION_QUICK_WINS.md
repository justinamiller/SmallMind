# SmallMind Optimization Quick Wins

**Goal:** Achieve **4-5x performance improvement** in **1 week** with low-risk changes.

---

## 🎯 The Problem

Current performance bottleneck analysis shows:

| Issue | Current State | Impact |
|-------|--------------|--------|
| **Excessive Allocations** | 353.52 MB/operation | 90% wasted memory |
| **Serial Dot Products** | 65,536+ individual calls | 3-4x slower than batched |
| **Wasted Softmax** | Computes 50% unused values | 2x slower than masked |
| **No KV-Cache** | Recomputes past K/V | 1.5-2x slower for long sequences |

**Total Waste:** Current inference is running at **~20% of potential speed**.

---

## ✅ The Solution (Priority 0)

### Quick Win #1: Integrate TensorPool (2 days)

**Impact:** 90% reduction in allocations (353 MB → 35 MB)

**Code Location:** `src/SmallMind.Transformers/Core/Transformer.cs`

**Current Code:**
```csharp
// BAD: Allocates new tensor every forward pass
var qkvTensor = new Tensor(new[] { batchSize, seqLen, 3 * embedDim });
var qTensor = new Tensor(new[] { batchSize, seqLen, embedDim });
var kTensor = new Tensor(new[] { batchSize, seqLen, embedDim });
// ... lots more allocations
```

**Optimized Code:**
```csharp
// GOOD: Rent from pool, return after use
var qkvTensor = TensorPool.Shared.Rent(new[] { batchSize, seqLen, 3 * embedDim });
var qTensor = TensorPool.Shared.Rent(new[] { batchSize, seqLen, embedDim });
var kTensor = TensorPool.Shared.Rent(new[] { batchSize, seqLen, embedDim });

try
{
    // Use tensors...
    // Forward pass logic here
}
finally
{
    // CRITICAL: Return to pool
    TensorPool.Shared.Return(qkvTensor);
    TensorPool.Shared.Return(qTensor);
    TensorPool.Shared.Return(kTensor);
    // ... return all rented tensors
}
```

**Testing:**
```bash
# Before
dotnet run -c Release -- --scenario memory,gc
# Check: Allocations/Op should drop from 353 MB to ~35 MB

# After
dotnet run -c Release -- --scenario memory,gc
# Verify: Gen0 collections should drop from 446 to ~50
```

---

### Quick Win #2: Batched MatMul for Attention (2-3 days)

**Impact:** 3-4x speedup in attention (45% of total time)

**Code Location:** `src/SmallMind.Transformers/Core/Transformer.cs` lines 501-591

**Current Code (SLOW):**
```csharp
// BAD: Nested loops with 65,536+ dot product calls
for (int i = 0; i < seqLen; i++)
{
    for (int j = 0; j <= i; j++)  // Causal mask: j <= i
    {
        float score = 0f;
        for (int k = 0; k < headDim; k++)
        {
            score += Q[i * headDim + k] * K[j * headDim + k];
        }
        scores[i * seqLen + j] = score / sqrtHeadDim;
    }
}
```

**Optimized Code (FAST):**
```csharp
// GOOD: Single batched matrix multiply
// scores = Q @ K^T / sqrt(headDim)
// Shape: (seqLen, headDim) @ (headDim, seqLen) = (seqLen, seqLen)

// Step 1: Transpose K for efficient MatMul
var kTransposed = TensorPool.Shared.Rent(new[] { headDim, seqLen });
try
{
    TransposeMatrix(K, kTransposed, seqLen, headDim);
    
    // Step 2: Batched MatMul (uses SIMD and cache blocking)
    MatMulOps.MatMul(
        Q, kTransposed, scores,
        M: seqLen, K: headDim, N: seqLen
    );
    
    // Step 3: Scale by sqrt(headDim)
    float scale = 1f / MathF.Sqrt(headDim);
    VectorOps.ScalarMultiply(scores, scale, scores, seqLen * seqLen);
}
finally
{
    TensorPool.Shared.Return(kTransposed);
}
```

**Implementation Steps:**
1. Add `BatchedMatMul()` method to `MatMulOps.cs` (if not exists)
2. Replace all attention score loops with single MatMul call
3. Apply causal mask AFTER MatMul (cheaper than during)

**Testing:**
```bash
# Profile before
dotnet run -c Release -- --scenario ttft,tokens_per_sec --iterations 50
# Check: Attention should take ~45% of forward pass time

# Profile after
# Verify: Attention should take ~15% of forward pass time (3x faster)
```

---

### Quick Win #3: Fused Masked Softmax (1 day)

**Impact:** 2x speedup in softmax (8-10ms → 4-5ms)

**Code Location:** `src/SmallMind.Transformers/Core/Transformer.cs` lines 593-686

**Current Code (WASTEFUL):**
```csharp
// BAD: Computes exp() for all positions, then zeros ~50%
for (int i = 0; i < seqLen; i++)
{
    // Compute exp for ALL positions
    for (int j = 0; j < seqLen; j++)
    {
        scores[i * seqLen + j] = MathF.Exp(scores[i * seqLen + j] - max);
    }
    
    // Then apply causal mask (wastes computation)
    for (int j = i + 1; j < seqLen; j++)
    {
        scores[i * seqLen + j] = 0f;  // Wasted exp() above!
    }
}
```

**Optimized Code (EFFICIENT):**
```csharp
// GOOD: Only compute exp() for valid (unmasked) positions
for (int i = 0; i < seqLen; i++)
{
    int rowOffset = i * seqLen;
    
    // Find max (only up to i for causal)
    float max = float.NegativeInfinity;
    for (int j = 0; j <= i; j++)  // Causal: j <= i
    {
        if (scores[rowOffset + j] > max)
            max = scores[rowOffset + j];
    }
    
    // Exp and sum (only valid positions)
    float sum = 0f;
    for (int j = 0; j <= i; j++)  // Causal: j <= i
    {
        float expVal = MathF.Exp(scores[rowOffset + j] - max);
        scores[rowOffset + j] = expVal;
        sum += expVal;
    }
    
    // Normalize (only valid positions)
    float invSum = 1f / sum;
    for (int j = 0; j <= i; j++)
    {
        scores[rowOffset + j] *= invSum;
    }
    
    // Zero invalid positions (j > i)
    for (int j = i + 1; j < seqLen; j++)
    {
        scores[rowOffset + j] = 0f;
    }
}
```

**Testing:**
```bash
# Profile softmax time
dotnet run -c Release -- --scenario latency --iterations 100
# Verify: Softmax should be ~2x faster
```

---

### Quick Win #4: Integrate KV-Cache (2-3 days)

**Impact:** 1.5-2x speedup for sequences > 32 tokens

**Code Location:** Needs integration into `Transformer.Forward()`

**Current Code (INEFFICIENT):**
```csharp
// BAD: Recomputes K and V for entire sequence every token
public Tensor Forward(Tensor input)
{
    // Every call computes K/V for ALL positions (including past)
    var qTensor = ComputeQ(input);  // Only new token needed
    var kTensor = ComputeK(input);  // Recomputes all past K!
    var vTensor = ComputeV(input);  // Recomputes all past V!
    
    // Attention with full sequence
    var attention = ComputeAttention(qTensor, kTensor, vTensor);
    // ...
}
```

**Optimized Code (EFFICIENT):**
```csharp
// GOOD: Cache K/V from past, only compute new token
private KVCache _kvCache = new KVCache(maxSeqLen: 2048, numHeads, headDim);

public Tensor Forward(Tensor input, int position)
{
    // Only compute Q/K/V for NEW token
    var qNew = ComputeQ(input);  // Just current token
    var kNew = ComputeK(input);  // Just current token
    var vNew = ComputeV(input);  // Just current token
    
    // Append to cache
    _kvCache.Append(kNew, vNew);
    
    // Get full K/V history from cache
    var kFull = _kvCache.GetKeys(position);    // Cached + new
    var vFull = _kvCache.GetValues(position);  // Cached + new
    
    // Attention with full sequence (but K/V from cache)
    var attention = ComputeAttention(qNew, kFull, vFull);
    // ...
}
```

**Implementation Steps:**
1. Add `KVCache` field to `Transformer` class
2. Modify `Forward()` to accept `position` parameter
3. Compute Q/K/V only for new token(s)
4. Append new K/V to cache
5. Retrieve full K/V from cache for attention

**Testing:**
```bash
# Test with long sequence generation
dotnet run -c Release -- --max-new-tokens 512 --iterations 10
# Verify: Should be ~1.5-2x faster for long sequences
```

---

## 📊 Expected Results

### Before Optimizations

| Metric | Value |
|--------|-------|
| TTFT | 1.52 ms |
| Throughput | 783 tok/s |
| Allocations/Op | 353.52 MB |
| Gen0 GC | 446 |
| Gen1 GC | 103 |

### After P0 Optimizations

| Metric | Target | Improvement |
|--------|--------|-------------|
| TTFT | **0.8-1.0 ms** | **1.5-2x faster** |
| Throughput | **3,000-4,000 tok/s** | **4-5x faster** |
| Allocations/Op | **35 MB** | **10x reduction** |
| Gen0 GC | **50** | **9x reduction** |
| Gen1 GC | **10** | **10x reduction** |

---

## 🚀 Implementation Timeline

### Day 1-2: TensorPool Integration
- [ ] Audit all `new Tensor()` calls in Transformer
- [ ] Replace with `TensorPool.Shared.Rent()`
- [ ] Add try/finally blocks for cleanup
- [ ] Test memory reduction

### Day 3-4: Batched MatMul
- [ ] Implement/verify `MatMulOps.BatchedMatMul()`
- [ ] Replace attention score loops
- [ ] Add matrix transpose helper
- [ ] Test throughput improvement

### Day 5: Fused Masked Softmax
- [ ] Modify softmax to skip invalid positions
- [ ] Test correctness (outputs should match)
- [ ] Verify 2x speedup

### Day 6-7: KV-Cache Integration
- [ ] Add KVCache field to Transformer
- [ ] Modify Forward() signature
- [ ] Implement cache append/retrieve
- [ ] Test long sequence performance

### Day 8: Validation & Benchmarking
- [ ] Run full benchmark suite
- [ ] Compare before/after metrics
- [ ] Verify 4-5x overall speedup
- [ ] Document results

---

## ⚠️ Risk Mitigation

1. **Correctness Testing**
   ```bash
   # Run integration tests before/after each change
   dotnet test tests/SmallMind.IntegrationTests
   ```

2. **Performance Regression**
   ```bash
   # Benchmark after each change
   dotnet run -c Release -- --scenario all --iterations 30
   ```

3. **Memory Leaks**
   ```bash
   # Monitor TensorPool statistics
   TensorPool.Shared.PrintStatistics();
   # Should show: totalRents ≈ totalReturns
   ```

4. **Gradual Rollout**
   - Implement one optimization at a time
   - Validate before moving to next
   - Keep git history clean for easy rollback

---

## 🎯 Success Criteria

After 1 week of P0 optimizations:

✅ Throughput: **783 tok/s → 3,000+ tok/s** (4x improvement)  
✅ Allocations: **353 MB → 35 MB** (10x reduction)  
✅ GC collections: **446 Gen0 → 50 Gen0** (9x reduction)  
✅ TTFT: **1.52 ms → ~1.0 ms** (1.5x improvement)  
✅ All tests passing  
✅ No regressions in accuracy

---

## 📚 References

- Profiler Analysis: `PROFILER_HOT_PATHS_ANALYSIS_2026-02-03.md`
- Benchmark Results: `BENCHMARK_RESULTS_2026-02-03.md`
- TensorPool Implementation: `src/SmallMind.Core/Core/MemoryPool.cs`
- MatMul Ops: `src/SmallMind.Core/Simd/MatMulOps.cs`
- KV-Cache: `src/SmallMind.Core/KVCache/KVCache.cs`
- Transformer: `src/SmallMind.Transformers/Core/Transformer.cs`

---

**Author:** GitHub Copilot Agent  
**Date:** February 3, 2026  
**Priority:** P0 (Critical - High Impact, Low Risk)

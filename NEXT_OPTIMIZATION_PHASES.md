# SmallMind - Next Optimization Phases for Token/Sec & Training Performance

**Date:** 2026-02-03  
**Priority:** HIGH  
**Goal:** Improve tokens/sec, reduce training time, optimize core LLM functionality

---

## 🎯 Current State vs Target

| Metric | **Current** | **Target (Phase 1)** | **Target (Final)** |
|--------|-------------|---------------------|-------------------|
| **Inference Throughput** | 6.4 tok/s | 29 tok/s | 75+ tok/s |
| **Memory per Token** | 51.5 MB | 8 MB | 3-5 MB |
| **MatMul Performance** | 16.3 GFLOPS | 20 GFLOPS | 30+ GFLOPS |
| **Training Iteration** | 100% baseline | 85% time | 65% time |

---

## 🔥 Top 5 Hot Paths to Fix NOW

### **1. Tensor Memory Pooling** ⭐⭐⭐⭐⭐
**Impact:** -84% memory allocation, +40% speed  
**Effort:** 2-3 days  
**Priority:** P0 - Do this FIRST

**Current Problem:**
```
Every forward pass allocates 51.5 MB:
- QKV tensors: 8-12 MB
- Attention scores: 4-8 MB  
- LayerNorm outputs: 2-4 MB
- Linear outputs: 20-30 MB
```

**Solution:**
Create `TensorPool.cs` to reuse buffers:

```csharp
public class TensorPool
{
    private Dictionary<string, float[]> _buffers = new();
    
    public Span<float> Get(string key, int size)
    {
        if (!_buffers.TryGetValue(key, out var buf) || buf.Length < size)
            _buffers[key] = new float[size];
        return _buffers[key].AsSpan(0, size);
    }
}

// Usage:
var qkvBuffer = pool.Get("qkv", B * T * 3 * nEmbd);
// Instead of: new Tensor(new[] { B, T, 3 * nEmbd })
```

**Files to modify:**
- Create: `src/SmallMind.Core/Memory/TensorPool.cs`
- Modify: `src/SmallMind.Transformers/Core/Transformer.cs` (all Forward methods)

---

### **2. Batched MatMul for Attention** ⭐⭐⭐⭐⭐
**Impact:** +150% attention speed (main bottleneck)  
**Effort:** 3-4 days  
**Priority:** P0

**Current Problem:**
```csharp
// Lines 501-591: ComputeAttentionScores uses nested loops
for (int i = 0; i < T; i++)
    for (int j = 0; j <= i; j++)
        scores[i,j] = DotProduct(Q[i], K[j]); // 65K+ calls!
```

For seq_len=128, heads=8: **65,536 dot product calls** instead of 1 matrix multiply!

**Solution:**
```csharp
// Replace with single batched GEMM
MatMulOps.BatchedMatMul(
    Q, K_transposed, scores,
    batch: B * nHead,
    M: T, K: headSize, N: T
);
```

**Implementation Steps:**
1. Add `BatchedMatMul` to `MatMulOps.cs`
2. Transpose K once per head  
3. Replace loop in `ComputeAttentionScores` (line 521-576)
4. Replace triple loop in `ApplyAttention` (line 709-717)

**Expected speedup:** 
- Attention: 50ms → 15ms per forward pass
- Overall: 6.4 → 15 tokens/sec

**Files to modify:**
- `src/SmallMind.Core/Simd/MatMulOps.cs` (add BatchedMatMul)
- `src/SmallMind.Transformers/Core/Transformer.cs` (lines 501-591, 688-748)

---

### **3. Fused Masked Softmax** ⭐⭐⭐⭐
**Impact:** +100% softmax speed  
**Effort:** 1 day  
**Priority:** P0

**Current Problem:**
```csharp
// Computes exp() for ALL positions, then zeros half
for (int j = 0; j < T; j++)
    exp_val = MathF.Exp(scores[i,j] - max);
    
// Then wastes the work:
for (int j = i + 1; j < T; j++)
    scores[i,j] = 0; // 50% of exp() calls wasted!
```

**Solution:**
```csharp
// Only compute exp() for valid positions
for (int i = 0; i < T; i++)
{
    float max = FindMax(scores[i], count: i+1); // Only valid
    float sum = 0;
    for (int j = 0; j <= i; j++) {  // Skip j > i
        scores[i,j] = MathF.Exp(scores[i,j] - max);
        sum += scores[i,j];
    }
    for (int j = 0; j <= i; j++)
        scores[i,j] /= sum;
}
```

**Files to modify:**
- `src/SmallMind.Transformers/Core/Transformer.cs` (lines 593-686)

---

### **4. KV-Cache for Inference** ⭐⭐⭐⭐
**Impact:** +50% inference speed for autoregressive generation  
**Effort:** 3-4 days  
**Priority:** P1 (after Phase 1)

**Current Problem:**
When generating token N, we recompute K and V for tokens 1 through N-1 every time!

**Solution:**
```csharp
public class KVCache
{
    private float[][] _keys;    // [layer][seqLen * heads * headDim]
    private float[][] _values;
    private int _length;
    
    public void Append(int layer, Span<float> newKey, Span<float> newValue);
    public ReadOnlySpan<float> GetAllKeys(int layer);
    public ReadOnlySpan<float> GetAllValues(int layer);
}

// In attention forward:
if (inference && cache != null)
{
    cache.Append(layerIdx, K_new, V_new);
    K = cache.GetAllKeys(layerIdx);  // Includes previous + new
    V = cache.GetAllValues(layerIdx);
}
```

**Files to create:**
- `src/SmallMind.Transformers/Inference/KVCache.cs`

**Files to modify:**
- `src/SmallMind.Transformers/Core/Transformer.cs` (Forward method)

---

### **5. SIMD in LayerNorm** ⭐⭐⭐
**Impact:** +160% LayerNorm speed  
**Effort:** 4-6 hours  
**Priority:** P1

**Current Problem:**
```csharp
// Lines 65-69 in LayerNormOps.cs - scalar loop
for (int f = 0; f < features; f++)
{
    float normalized = (input[offset + f] - mean) * invStd;
    output[offset + f] = gamma[f] * normalized + beta[f];
}
```

**Solution:**
```csharp
int vecSize = Vector<float>.Count;
var meanVec = new Vector<float>(mean);
var invStdVec = new Vector<float>(invStd);

int f = 0;
for (; f <= features - vecSize; f += vecSize)
{
    var inputVec = new Vector<float>(input, offset + f);
    var gammaVec = new Vector<float>(gamma, f);
    var betaVec = new Vector<float>(beta, f);
    
    var norm = (inputVec - meanVec) * invStdVec;
    (gammaVec * norm + betaVec).CopyTo(output.Slice(offset + f));
}
// Remainder scalar loop
for (; f < features; f++) { ... }
```

**Files to modify:**
- `src/SmallMind.Core/Core/LayerNormOps.cs` (lines 65-69)

---

## 📋 Implementation Checklist

### **Week 1: Critical Path Optimizations (P0)**

- [ ] **Day 1-2:** Implement TensorPool
  - [ ] Create `TensorPool.cs` with Get/Clear methods
  - [ ] Add pool field to TransformerModel
  - [ ] Replace all `new Tensor()` in forward pass with pooled buffers
  - [ ] Test memory usage drops to ~8 MB/token
  
- [ ] **Day 3-4:** Add BatchedMatMul to attention
  - [ ] Implement `BatchedMatMul` in MatMulOps.cs
  - [ ] Add unit tests for batched multiply
  - [ ] Replace `ComputeAttentionScores` loops with BatchedMatMul
  - [ ] Replace `ApplyAttention` triple loop with MatMul
  - [ ] Benchmark: expect 50ms → 15ms per attention
  
- [ ] **Day 5:** Fused masked softmax
  - [ ] Modify `ApplySoftmax` to skip masked positions
  - [ ] Add SIMD to max/sum reduction
  - [ ] Test correctness vs original implementation
  
- [ ] **Day 5:** SIMD in LayerNorm
  - [ ] Add Vector<float> loop to normalization
  - [ ] Benchmark improvement

**Expected Results after Week 1:**
- Inference: 6.4 → ~25-30 tokens/sec (**+290-370%**)
- Memory: 51.5 → ~8 MB/token (**-84%**)

---

### **Week 2-3: Infrastructure Improvements (P1)**

- [ ] **KV-Cache Implementation**
  - [ ] Create KVCache class with append/get
  - [ ] Add cache parameter to Forward method
  - [ ] Modify attention to use cached K/V
  - [ ] Add cache warmup for prompt processing
  
- [ ] **MatMul Cache Blocking**
  - [ ] Implement tiled multiply in MatMulOps
  - [ ] Tune TILE_SIZE for CPU cache
  - [ ] Benchmark: 16.3 → 25-30 GFLOPS
  
- [ ] **ArrayPool for Gradients**
  - [ ] Replace temp gradient allocations with ArrayPool
  - [ ] Measure training speedup

**Expected Results after Week 3:**
- Inference: 30 → ~50-60 tokens/sec (**+67-100%**)
- Memory: 8 → ~5 MB/token (**-38%**)
- Training: -15-20% time per iteration

---

### **Week 4+: Advanced Optimizations (P2)**

- [ ] Flash Attention implementation
- [ ] INT8 quantization for inference
- [ ] Graph-level operator fusion
- [ ] Multi-GPU support (if applicable)

---

## 🔬 Testing & Validation

### Before Each Change

```bash
# Baseline profiling
cd tools/CodeProfiler
dotnet run -c Release -- --deep > baseline.txt

# Training benchmark
cd ../../benchmarks/TrainingBenchmark  
dotnet run -c Release > train_baseline.txt
```

### After Each Change

```bash
# Re-run profiling
cd tools/CodeProfiler
dotnet run -c Release -- --deep > optimized.txt

# Compare results
diff baseline.txt optimized.txt

# Verify correctness with tests
cd ../../tests
dotnet test -c Release
```

### Key Metrics to Track

1. **Tokens per second** - Main inference metric
2. **Memory per token** - Allocation pressure
3. **MatMul GFLOPS** - Compute efficiency
4. **GC collections** - Gen0/Gen1/Gen2 counts
5. **Training iteration time** - End-to-end training

---

## 🚀 Quick Start

To begin optimizations immediately:

```bash
# 1. Get baseline
cd /home/runner/work/SmallMind/SmallMind
cd tools/CodeProfiler
dotnet run -c Release -- --deep

# 2. Create TensorPool (Day 1)
mkdir -p src/SmallMind.Core/Memory
# Create TensorPool.cs (see template above)

# 3. Modify Transformer to use pool
# Edit src/SmallMind.Transformers/Core/Transformer.cs

# 4. Test & measure
dotnet test -c Release
cd tools/CodeProfiler
dotnet run -c Release -- --deep
# Should see 51.5 MB → ~8 MB per token
```

---

## 📊 Expected Timeline

| Week | Focus | Est. Speedup | Cumulative |
|------|-------|--------------|------------|
| **1** | Memory pooling + Batched MatMul + Softmax | 4-5x | **4-5x** |
| **2** | KV-Cache | 1.5x | **6-7x** |
| **3** | MatMul blocking + ArrayPool | 1.2x | **7-9x** |
| **4+** | Flash Attention, Quantization | 1.3x | **10-12x** |

**Final Target:** 6.4 → 75+ tokens/sec (**11.7x improvement**)

---

## 📖 References

- **Main Analysis:** `PROFILER_HOT_PATHS_ANALYSIS_2026-02-03.md`
- **Previous Reports:** `PROFILER_EXECUTIVE_SUMMARY.md`, `PROFILER_HOT_PATHS_REPORT.md`
- **Benchmarks:** `benchmarks/TrainingBenchmark/`, `SIMD_BENCHMARK_RESULTS.md`
- **Code Locations:** See main analysis document for line numbers

---

**Priority Order:**
1. 🔴 P0: TensorPool (Days 1-2)
2. 🔴 P0: BatchedMatMul (Days 3-4)  
3. 🔴 P0: Masked Softmax (Day 5)
4. 🟡 P1: SIMD LayerNorm (Day 5)
5. 🟡 P1: KV-Cache (Week 2)
6. 🟡 P1: MatMul blocking (Week 2-3)
7. 🟢 P2: Advanced optimizations (Week 4+)

**Start with P0 items - they give 4-5x speedup in 5 days!**

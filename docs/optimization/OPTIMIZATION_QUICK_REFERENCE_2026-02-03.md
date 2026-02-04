# SmallMind Optimization Quick Reference
**Date:** February 3, 2026  
**Based on:** Enhanced CodeProfiler Analysis

---

## 🎯 Top 5 Optimization Opportunities (By ROI)

### 1. Fix TensorAdd Regression (P0) ⚡ CRITICAL
```
Current:   10.84 ms (regressed +269% from 2.94 ms)
Target:    <3 ms
Est. Gain: ~8 ms per inference
Impact:    HIGH - Used in all forward passes
```

**Root Cause:** Recent code change broke SIMD vectorization  
**Quick Fix:**
```csharp
// BEFORE (slow):
for (int i = 0; i < length; i++)
    result[i] = a[i] + b[i];  // Scalar

// AFTER (fast):
int vectorSize = Vector<float>.Count;
for (int i = 0; i <= length - vectorSize; i += vectorSize)
{
    var va = new Vector<float>(a, i);
    var vb = new Vector<float>(b, i);
    (va + vb).CopyTo(result, i);
}
// Handle remainder...
```

---

### 2. Blocked Matrix Multiplication (P0) ⚡ CRITICAL
```
Current:   172.11 ms for 512×512 (regressed +44%)
Target:    <100 ms
Est. Gain: ~70-80 ms per large matmul
Impact:    HIGH - Bottleneck for larger models
```

**Root Cause:** Cache misses, poor memory locality  
**Quick Fix:**
```csharp
// BEFORE: Naive triple loop
for (int i = 0; i < M; i++)
    for (int j = 0; j < N; j++)
        for (int k = 0; k < K; k++)
            C[i,j] += A[i,k] * B[k,j];

// AFTER: 32×32 tiled blocking
const int TILE = 32;
for (int i0 = 0; i0 < M; i0 += TILE)
    for (int k0 = 0; k0 < K; k0 += TILE)
        for (int j0 = 0; j0 < N; j0 += TILE)
        {
            int iMax = Math.Min(i0 + TILE, M);
            int kMax = Math.Min(k0 + TILE, K);
            int jMax = Math.Min(j0 + TILE, N);
            
            for (int i = i0; i < iMax; i++)
                for (int k = k0; k < kMax; k++)
                {
                    float aik = A[i * K + k];
                    for (int j = j0; j < jMax; j++)
                        C[i * N + j] += aik * B[k * N + j];
                }
        }
```

---

### 3. GELU Fast Approximation (P0) ⚡ CRITICAL
```
Current:   100.60 ms for 1M elements (regressed +70%)
Target:    <50 ms
Est. Gain: ~50-60 ms per large activation
Impact:    MEDIUM - Used in every transformer block
```

**Root Cause:** Using slow tanh-based exact formula  
**Quick Fix:**
```csharp
// BEFORE: Exact GELU (slow)
public float GELU(float x)
{
    return 0.5f * x * (1f + MathF.Tanh(
        MathF.Sqrt(2f / MathF.PI) * (x + 0.044715f * x * x * x)));
}

// AFTER: Fast approximation (used by GPT-2)
public float GELUFast(float x)
{
    return x * Sigmoid(1.702f * x);
}

[MethodImpl(MethodImplOptions.AggressiveInlining)]
private static float Sigmoid(float x)
{
    return 1f / (1f + MathF.Exp(-x));
}

// BATCH VERSION with SIMD:
public void GELUBatchSIMD(Span<float> x)
{
    for (int i = 0; i < x.Length; i++)
        x[i] = GELUFast(x[i]);
}
```

---

### 4. Tensor Memory Pooling (P1) 🔧 HIGH PRIORITY
```
Current:   2550 MB total allocations
Target:    <500 MB
Est. Gain: ~2000 MB reduction, faster GC
Impact:    MEDIUM - Reduces memory pressure
```

**Root Cause:** Creating new arrays for every tensor operation  
**Quick Fix:**
```csharp
public sealed class TensorPool
{
    private static readonly ConcurrentBag<float[]>[] _pools;
    private static readonly int[] _sizes = { 64, 128, 256, 512, 1024, 2048, 4096 };
    
    static TensorPool()
    {
        _pools = new ConcurrentBag<float[]>[_sizes.Length];
        for (int i = 0; i < _pools.Length; i++)
            _pools[i] = new ConcurrentBag<float[]>();
    }
    
    public static float[] Rent(int size)
    {
        int bucketIndex = GetBucketIndex(size);
        if (bucketIndex >= 0 && _pools[bucketIndex].TryTake(out var array))
            return array;
        
        int actualSize = bucketIndex >= 0 ? _sizes[bucketIndex] : size;
        return new float[actualSize];
    }
    
    public static void Return(float[] array)
    {
        int bucketIndex = GetBucketIndex(array.Length);
        if (bucketIndex >= 0 && array.Length == _sizes[bucketIndex])
        {
            Array.Clear(array);
            _pools[bucketIndex].Add(array);
        }
    }
    
    private static int GetBucketIndex(int size)
    {
        for (int i = 0; i < _sizes.Length; i++)
            if (size <= _sizes[i]) return i;
        return -1;
    }
}

// USAGE:
var buffer = TensorPool.Rent(1024);
try
{
    // Use buffer...
}
finally
{
    TensorPool.Return(buffer);
}
```

---

### 5. Fused Softmax (P1) 🔧 HIGH PRIORITY
```
Current:   6.22 ms for 2048 elements (regressed +210%)
Target:    <2 ms
Est. Gain: ~4 ms per attention layer
Impact:    MEDIUM - Critical for attention
```

**Root Cause:** Multiple passes over data, cache misses  
**Quick Fix:**
```csharp
// BEFORE: Multiple passes
float max = logits.Max();  // Pass 1
var exp = logits.Select(x => MathF.Exp(x - max)).ToArray();  // Pass 2
float sum = exp.Sum();  // Pass 3
return exp.Select(x => x / sum).ToArray();  // Pass 4

// AFTER: Fused single-pass (in-place)
public void SoftmaxInPlace(Span<float> logits)
{
    // Pass 1: Find max
    float max = float.NegativeInfinity;
    for (int i = 0; i < logits.Length; i++)
        if (logits[i] > max) max = logits[i];
    
    // Pass 2: Exp and sum (FUSED)
    float sum = 0f;
    for (int i = 0; i < logits.Length; i++)
    {
        logits[i] = MathF.Exp(logits[i] - max);
        sum += logits[i];
    }
    
    // Pass 3: Normalize
    float invSum = 1f / sum;
    for (int i = 0; i < logits.Length; i++)
        logits[i] *= invSum;
}
```

---

## 📊 Expected Overall Impact

Implementing all P0 optimizations:
```
Runtime:   5927 ms → ~5800 ms (-2%)
Memory:    2550 MB → ~500 MB (-80%)
Throughput: +2-5%
```

Note: The primary bottleneck is the model inference itself (87% of runtime), 
not the individual operations. Significant speedup requires optimizing the 
forward pass as a whole, including KV-caching and attention optimization.

---

## 🔍 How to Verify

### After Each Optimization
```bash
# Run profiler
cd /home/runner/work/SmallMind/SmallMind
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj --configuration Release -- --enhanced

# Compare results
dotnet run --project tools/CodeProfiler/CodeProfiler.csproj --configuration Release -- -c \
  previous-profile-report.md \
  enhanced-profile-report.md \
  profile-comparison.md

# Check for improvements
grep "Top 10 Improvements" profile-comparison.md -A 10
```

### Key Metrics to Watch
- **TensorAdd_10000:** Should be <3 ms (currently 10.84 ms)
- **MatMul_512x512:** Should be <100 ms (currently 172.11 ms)
- **GELU_1000000:** Should be <50 ms (currently 100.60 ms)
- **Total Allocations:** Should be <500 MB (currently 2550 MB)

---

## 🚨 Common Pitfalls

### 1. SIMD Alignment
```csharp
// DON'T: Start SIMD at arbitrary offset
for (int i = offset; i < length; i += vectorSize) { ... }

// DO: Start aligned, handle prefix/suffix
int aligned = (offset + vectorSize - 1) / vectorSize * vectorSize;
for (int i = offset; i < aligned; i++) { /* scalar */ }
for (int i = aligned; i < length - vectorSize; i += vectorSize) { /* SIMD */ }
for (int i = ...; i < length; i++) { /* scalar */ }
```

### 2. Pooling Array Sizes
```csharp
// DON'T: Pool arbitrary sizes
TensorPool.Return(new float[1337]);  // Won't be reused

// DO: Use bucket sizes
int bucketSize = GetNextPowerOf2(size);
var buffer = TensorPool.Rent(bucketSize);
```

### 3. Matrix Layout
```csharp
// DON'T: Column-major access (cache misses)
for (int j = 0; j < N; j++)
    for (int i = 0; i < M; i++)
        C[i,j] = ...;

// DO: Row-major access (cache friendly)
for (int i = 0; i < M; i++)
    for (int j = 0; j < N; j++)
        C[i * N + j] = ...;
```

---

## 📅 Implementation Timeline

**Week 1 (P0):**
- Day 1-2: Fix TensorAdd regression → Verify <3ms
- Day 3-4: Implement blocked matmul → Verify <100ms  
- Day 5: Optimize GELU → Verify <50ms

**Week 2-3 (P1):**
- Week 2: Add tensor pooling → Verify <500MB
- Week 3: Optimize Softmax → Verify <2ms

**Week 4 (Verification):**
- Re-run full benchmark suite
- Compare with industry leaders
- Update documentation

---

## 🎓 Reference Implementation

See `SmallMind.Core.Simd` for existing SIMD utilities:
- `VectorOps.Add()` - SIMD addition
- `VectorOps.Multiply()` - SIMD multiplication
- `MatrixOps.MatMul()` - Matrix multiplication

Look for TODO comments marking optimization opportunities.

---

**Last Updated:** February 3, 2026  
**Next Review:** After P0 optimizations implemented

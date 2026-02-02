# Code Optimization Examples - Ready to Apply
**Quick copy-paste optimizations for SmallMind**

---

## 🚀 Optimization #1: Array.Copy in Embeddings (15 min)

**File:** `src/SmallMind.Transformers/Core/NeuralNet.cs`  
**Lines:** 162-175  
**Expected Gain:** 20-30% faster embeddings

### Before (Current Code):
```csharp
for (int b = 0; b < batch; b++)
{
    for (int s = 0; s < seq; s++)
    {
        int idx = (int)input.Data[b * seq + s];
        if (idx >= 0 && idx < _numEmbeddings)
        {
            for (int j = 0; j < _embeddingDim; j++)
            {
                output.Data[(b * seq + s) * _embeddingDim + j] = Weight.Data[idx * _embeddingDim + j];
            }
        }
    }
}
```

### After (Optimized):
```csharp
for (int b = 0; b < batch; b++)
{
    for (int s = 0; s < seq; s++)
    {
        int idx = (int)input.Data[b * seq + s];
        if (idx >= 0 && idx < _numEmbeddings)
        {
            int srcOffset = idx * _embeddingDim;
            int dstOffset = (b * seq + s) * _embeddingDim;
            
            // Use Array.Copy for bulk memory transfer (much faster)
            Array.Copy(
                Weight.Data, srcOffset,
                output.Data, dstOffset,
                _embeddingDim
            );
        }
    }
}
```

---

## 🚀 Optimization #2: SIMD in Residual Connections (15 min)

**File:** `src/SmallMind.Transformers/Core/Transformer.cs`  
**Lines:** 276-302  
**Expected Gain:** 5-10% overall speedup

### Before (Current Code):
```csharp
private Tensor AddTensors(Tensor a, Tensor b)
{
    var result = new Tensor(a.Shape, requiresGrad: true);
    for (int i = 0; i < a.Size; i++)
    {
        result.Data[i] = a.Data[i] + b.Data[i];
    }
    
    if (a.RequiresGrad || b.RequiresGrad)
    {
        result.SetBackward(() =>
        {
            if (a.RequiresGrad)
            {
                for (int i = 0; i < a.Size; i++)
                    a.Grad[i] += result.Grad[i];
            }
            if (b.RequiresGrad)
            {
                for (int i = 0; i < b.Size; i++)
                    b.Grad[i] += result.Grad[i];
            }
        });
    }
    
    return result;
}
```

### After (Optimized):
```csharp
private Tensor AddTensors(Tensor a, Tensor b)
{
    var result = new Tensor(a.Shape, requiresGrad: true);
    
    // SIMD-accelerated forward pass
    int vectorSize = Vector<float>.Count;
    int i = 0;
    
    // SIMD loop
    for (; i <= a.Size - vectorSize; i += vectorSize)
    {
        var va = new Vector<float>(a.Data.AsSpan(i));
        var vb = new Vector<float>(b.Data.AsSpan(i));
        (va + vb).CopyTo(result.Data.AsSpan(i));
    }
    
    // Scalar remainder
    for (; i < a.Size; i++)
    {
        result.Data[i] = a.Data[i] + b.Data[i];
    }
    
    if (a.RequiresGrad || b.RequiresGrad)
    {
        result.SetBackward(() =>
        {
            if (a.RequiresGrad)
            {
                // SIMD-accelerated gradient accumulation
                int j = 0;
                for (; j <= a.Size - vectorSize; j += vectorSize)
                {
                    var vGrad = new Vector<float>(result.Grad.AsSpan(j));
                    var vAGrad = new Vector<float>(a.Grad.AsSpan(j));
                    (vAGrad + vGrad).CopyTo(a.Grad.AsSpan(j));
                }
                for (; j < a.Size; j++)
                    a.Grad[j] += result.Grad[j];
            }
            if (b.RequiresGrad)
            {
                int j = 0;
                for (; j <= b.Size - vectorSize; j += vectorSize)
                {
                    var vGrad = new Vector<float>(result.Grad.AsSpan(j));
                    var vBGrad = new Vector<float>(b.Grad.AsSpan(j));
                    (vBGrad + vGrad).CopyTo(b.Grad.AsSpan(j));
                }
                for (; j < b.Size; j++)
                    b.Grad[j] += result.Grad[j];
            }
        });
    }
    
    return result;
}
```

**Don't forget to add using:** `using System.Numerics;` at the top of the file.

---

## 🚀 Optimization #3: SIMD in Position Embeddings (20 min)

**File:** `src/SmallMind.Transformers/Core/Transformer.cs`  
**Lines:** 164-216  
**Expected Gain:** 3-5% speedup

### Before (Current Code):
```csharp
private Tensor AddPositionEmbeddings(Tensor tokEmb, Tensor posEmb, int B, int T, int nEmbd)
{
    var result = new Tensor(new int[] { B, T, nEmbd }, requiresGrad: true);
    
    for (int b = 0; b < B; b++)
    {
        for (int t = 0; t < T; t++)
        {
            int resultOffset = (b * T + t) * nEmbd;
            int tokEmbOffset = (b * T + t) * nEmbd;
            int posEmbOffset = t * nEmbd;
            
            // Vectorized addition
            for (int e = 0; e < nEmbd; e++)
            {
                result.Data[resultOffset + e] = 
                    tokEmb.Data[tokEmbOffset + e] + posEmb.Data[posEmbOffset + e];
            }
        }
    }
    
    // ... backward pass ...
    
    return result;
}
```

### After (Optimized):
```csharp
private Tensor AddPositionEmbeddings(Tensor tokEmb, Tensor posEmb, int B, int T, int nEmbd)
{
    var result = new Tensor(new int[] { B, T, nEmbd }, requiresGrad: true);
    int vectorSize = Vector<float>.Count;
    
    for (int b = 0; b < B; b++)
    {
        for (int t = 0; t < T; t++)
        {
            int resultOffset = (b * T + t) * nEmbd;
            int tokEmbOffset = (b * T + t) * nEmbd;
            int posEmbOffset = t * nEmbd;
            
            // SIMD-accelerated addition
            int e = 0;
            for (; e <= nEmbd - vectorSize; e += vectorSize)
            {
                var vTok = new Vector<float>(tokEmb.Data.AsSpan(tokEmbOffset + e));
                var vPos = new Vector<float>(posEmb.Data.AsSpan(posEmbOffset + e));
                (vTok + vPos).CopyTo(result.Data.AsSpan(resultOffset + e));
            }
            
            // Scalar remainder
            for (; e < nEmbd; e++)
            {
                result.Data[resultOffset + e] = 
                    tokEmb.Data[tokEmbOffset + e] + posEmb.Data[posEmbOffset + e];
            }
        }
    }
    
    // Backward pass (keep as-is or also optimize with SIMD)
    if (tokEmb.RequiresGrad || posEmb.RequiresGrad)
    {
        result.SetBackward(() =>
        {
            if (tokEmb.RequiresGrad)
            {
                // Can also optimize this with SIMD
                for (int i = 0; i < result.Size; i++)
                {
                    tokEmb.Grad[i] += result.Grad[i];
                }
            }
            if (posEmb.RequiresGrad)
            {
                for (int b = 0; b < B; b++)
                {
                    for (int t = 0; t < T; t++)
                    {
                        for (int e = 0; e < nEmbd; e++)
                        {
                            posEmb.Grad[t * nEmbd + e] += result.Grad[(b * T + t) * nEmbd + e];
                        }
                    }
                }
            }
        });
    }
    
    return result;
}
```

---

## 🚀 Optimization #4: Parallel Embedding Lookups (10 min)

**File:** `src/SmallMind.Transformers/Core/NeuralNet.cs`  
**Lines:** 156-202  
**Expected Gain:** 15-25% faster for batch > 4

### Before (Current Code):
```csharp
for (int b = 0; b < batch; b++)
{
    for (int s = 0; s < seq; s++)
    {
        int idx = (int)input.Data[b * seq + s];
        if (idx >= 0 && idx < _numEmbeddings)
        {
            // ... copy embedding ...
        }
    }
}
```

### After (Optimized):
```csharp
// Parallelize over batch when beneficial
if (batch >= 4)
{
    Parallel.For(0, batch, b =>
    {
        for (int s = 0; s < seq; s++)
        {
            int idx = (int)input.Data[b * seq + s];
            if (idx >= 0 && idx < _numEmbeddings)
            {
                int srcOffset = idx * _embeddingDim;
                int dstOffset = (b * seq + s) * _embeddingDim;
                
                Array.Copy(
                    Weight.Data, srcOffset,
                    output.Data, dstOffset,
                    _embeddingDim
                );
            }
        }
    });
}
else
{
    for (int b = 0; b < batch; b++)
    {
        for (int s = 0; s < seq; s++)
        {
            int idx = (int)input.Data[b * seq + s];
            if (idx >= 0 && idx < _numEmbeddings)
            {
                int srcOffset = idx * _embeddingDim;
                int dstOffset = (b * seq + s) * _embeddingDim;
                
                Array.Copy(
                    Weight.Data, srcOffset,
                    output.Data, dstOffset,
                    _embeddingDim
                );
            }
        }
    }
}
```

**Don't forget to add using:** `using System.Threading.Tasks;` at the top of the file.

---

## 🚀 Optimization #5: ExtractAndReshapeQKV Array.Copy (10 min)

**File:** `src/SmallMind.Transformers/Core/Transformer.cs`  
**Lines:** 397-431  
**Expected Gain:** 5-8% faster attention

### Before (Current Code):
```csharp
for (int t = 0; t < T; t++)
{
    int srcIdx = batchInOffset + t * 3 * _nEmbd + headInOffset;
    int dstIdx = headOutOffset + t * _headSize;
    
    // Copy entire head dimension at once using Array.Copy (faster than element-by-element)
    Array.Copy(qkv.Data, srcIdx, extracted.Data, dstIdx, _headSize);
}
```

This is already using `Array.Copy` - good! But we can add parallelization:

### After (Optimized):
```csharp
private Tensor ExtractAndReshapeQKV(Tensor qkv, int index, int B, int T)
{
    var extracted = new Tensor(new int[] { B, _nHead, T, _headSize }, requiresGrad: true);
    int embdOffset = index * _nEmbd;
    
    // Parallelize over batch when B >= 4
    if (B >= 4)
    {
        Parallel.For(0, B, b =>
        {
            int batchInOffset = b * T * 3 * _nEmbd;
            int batchOutOffset = b * _nHead * T * _headSize;
            
            for (int h = 0; h < _nHead; h++)
            {
                int headInOffset = embdOffset + h * _headSize;
                int headOutOffset = batchOutOffset + h * T * _headSize;
                
                for (int t = 0; t < T; t++)
                {
                    int srcIdx = batchInOffset + t * 3 * _nEmbd + headInOffset;
                    int dstIdx = headOutOffset + t * _headSize;
                    
                    Array.Copy(qkv.Data, srcIdx, extracted.Data, dstIdx, _headSize);
                }
            }
        });
    }
    else
    {
        // Original sequential code for small batches
        for (int b = 0; b < B; b++)
        {
            int batchInOffset = b * T * 3 * _nEmbd;
            int batchOutOffset = b * _nHead * T * _headSize;
            
            for (int h = 0; h < _nHead; h++)
            {
                int headInOffset = embdOffset + h * _headSize;
                int headOutOffset = batchOutOffset + h * T * _headSize;
                
                for (int t = 0; t < T; t++)
                {
                    int srcIdx = batchInOffset + t * 3 * _nEmbd + headInOffset;
                    int dstIdx = headOutOffset + t * _headSize;
                    
                    Array.Copy(qkv.Data, srcIdx, extracted.Data, dstIdx, _headSize);
                }
            }
        }
    }
    
    return extracted;
}
```

---

## 📊 Quick Impact Summary

Apply these 5 optimizations in order:

| # | Optimization | Time | Expected Gain | Difficulty |
|---|--------------|------|---------------|------------|
| 1 | Array.Copy in Embeddings | 15 min | 20-30% faster embeddings | Easy |
| 2 | SIMD Residual Connections | 15 min | 5-10% overall | Easy |
| 3 | SIMD Position Embeddings | 20 min | 3-5% overall | Easy |
| 4 | Parallel Embeddings | 10 min | 15-25% (batch>4) | Easy |
| 5 | Parallel QKV Extract | 10 min | 5-8% attention | Easy |

**Total time:** ~70 minutes  
**Total expected gain:** ~15-25% speedup overall  
**No breaking changes:** These are all internal optimizations

---

## 🧪 How to Test

After applying each optimization:

1. **Build the project:**
   ```bash
   dotnet build
   ```

2. **Run the profiler:**
   ```bash
   cd tools/CodeProfiler
   dotnet run -- /tmp/profile.md 3 50 --deep
   ```

3. **Check for improvement:**
   - Look at "Tokens/Second" - should increase
   - Look at "Total Time" for each method - should decrease
   - Verify no increase in memory allocation

4. **Run tests (if available):**
   ```bash
   dotnet test
   ```

---

## ⚠️ Important Notes

1. **Add using statements** at the top of files:
   ```csharp
   using System.Numerics;           // For Vector<T>
   using System.Threading.Tasks;    // For Parallel.For
   ```

2. **These are backward compatible** - no API changes

3. **No memory pooling yet** - these are quick wins before the major refactor

4. **Profile before and after** to verify improvements

5. **Don't skip testing** - SIMD can have subtle bugs

---

## 🎯 Next Steps After Quick Wins

Once you've applied these 5 optimizations:

1. Implement TensorPool (see full analysis document)
2. Optimize LayerNorm with Welford's algorithm
3. Add batched MatMul to attention
4. Implement KV-Cache

**These 5 quick wins will give you ~20% improvement in 1 hour.**  
**The full optimization plan will give you 2.5-3× improvement in 12 weeks.**

---

**Questions?** See `PROFILER_ANALYSIS_2026-02-02.md` for detailed explanations and `PROFILER_EXECUTIVE_SUMMARY.md` for quick reference.

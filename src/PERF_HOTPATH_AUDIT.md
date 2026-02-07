# SmallMind Hot Path Performance Audit

**Date**: 2026-02-07  
**Scope**: src/SmallMind.Core, src/SmallMind.Quantization  
**Goal**: Identify and eliminate hidden JIT costs in hot paths  

---

## 🔴 CRITICAL ISSUES (High Impact)

### 1. Unsealed Tensor Class Prevents Devirtualization

**File**: `src/SmallMind.Core/Core/Tensor.cs`  
**Line**: 14  
**Pattern**: `public class Tensor : IDisposable` (not sealed)

**Why it's hot**: Tensor is used in every operation - matrix multiply, attention, normalization. Called per-token, per-layer.

**Hidden cost**: 
- Virtual method dispatch cannot be inlined by JIT
- Property getters (`Data`, `Shape`, `Grad`, `RequiresGrad`) add vtable lookups
- Subclass `PooledTensor` forces polymorphic calls

**Fix**:
```csharp
// Before:
public class Tensor : IDisposable

// After:
public sealed class Tensor : IDisposable
```

**Risk**: LOW - No observed subclasses except PooledTensor (which can inherit from Tensor directly)

**Estimated Impact**: 5-15% reduction in tensor operation overhead

---

### 2. ITensorStorage Interface Dispatch in Tensor Operations

**File**: `src/SmallMind.Core/Core/ITensorStorage.cs`, `Tensor.cs`  
**Pattern**: Interface method calls in Tensor class

**Why it's hot**: Every tensor operation may call:
- `ITensorStorage.CopyTo()` - line 25
- `ITensorStorage.CopyFrom()` - line 26
- `ITensorStorage.Get()` / `Set()` - lines 19-20

**Hidden cost**:
- Interface dispatch prevents inlining
- Cannot devirtualize even with sealed implementations

**Fix Options**:
1. **Immediate**: Make hot path methods use generic constraints instead of interface
   ```csharp
   // Before:
   private readonly ITensorStorage _storage;
   
   // After (with generics):
   private readonly DenseStorage _storage;  // or ChunkedStorage
   ```

2. **Better**: Create fast-path specializations for DenseStorage (most common case)

**Risk**: MEDIUM - Requires refactoring of Tensor class internals

**Estimated Impact**: 10-15% in tensor-heavy operations

---

### 3. Span.Slice() in SIMD Inner Loops

**Files**: 
- `src/SmallMind.Core/Simd/MatMulOps.cs` (lines 336-350)
- `src/SmallMind.Core/Simd/LayerNormOps.cs` (lines 70, 91)
- `src/SmallMind.Core/Simd/ActivationOps.cs` (lines 69-70)
- `src/SmallMind.Quantization/Kernels/MatMulF32Q4Optimized.cs` (lines 49-50)

**Example** (MatMulOps.cs:336):
```csharp
for (int j = j0; j <= jMax - vectorSize; j += vectorSize)
{
    var vB = new Vector<float>(BSpanLocal.Slice(bRowStart + j));  // ❌ Slice + bounds check
    var vC = new Vector<float>(CSpanLocal.Slice(cRowStart + j));  // ❌ Slice + bounds check
    (vC + vA * vB).CopyTo(CSpanLocal.Slice(cRowStart + j));       // ❌ Slice + bounds check
}
```

**Why it's hot**: Called millions of times per inference in MatMul inner loop

**Hidden cost**:
- Each `.Slice()` creates new Span with bounds check
- JIT cannot always prove bounds are safe
- 3 bounds checks per iteration × millions of iterations

**Fix** (MatMulOps.cs example):
```csharp
// Before:
var vB = new Vector<float>(BSpanLocal.Slice(bRowStart + j));

// After (unsafe fixed pointer):
#if UNSAFE_KERNELS
unsafe
{
    fixed (float* pB = BSpanLocal, pC = CSpanLocal)
    {
        for (int j = j0; j <= jMax - vectorSize; j += vectorSize)
        {
            var vB = new Vector<float>(pB + bRowStart + j);
            var vC = new Vector<float>(pC + cRowStart + j);
            (vC + vA * vB).CopyTo(pC + cRowStart + j, Vector<float>.Count);
        }
    }
}
#else
// Original safe code...
#endif
```

**Risk**: MEDIUM - Requires unsafe code, need validation of bounds BEFORE loop

**Estimated Impact**: 5-10% reduction in MatMul time

---

## 🟠 HIGH PRIORITY ISSUES

### 4. Math.Clamp() Is Branchy in FastExp Hot Path

**File**: `src/SmallMind.Core/Optimized/OptimizedOps.cs`  
**Line**: 64  
**Context**: Called from `FusedScaleMaskSoftmax` (line 159) in attention

**Code**:
```csharp
public static float FastExp(float x)
{
    x = Math.Clamp(x, -87.3f, 88.7f);  // ❌ Branchy
    // ... fast approximation ...
}
```

**Why it's hot**: Called per-element in softmax (attention scores)

**Hidden cost**: 
- `Math.Clamp` compiles to branches (cmp, jge, jle)
- Branch mispredictions on random data

**Fix**:
```csharp
// Before:
x = Math.Clamp(x, -87.3f, 88.7f);

// After (branchless):
x = MathF.Min(88.7f, MathF.Max(-87.3f, x));  // Uses MINSS/MAXSS instructions
```

**Risk**: NEGLIGIBLE - Equivalent behavior

**Estimated Impact**: 2-3% in softmax operations

---

## 🟡 MEDIUM PRIORITY ISSUES

### 5. Non-Sealed Classes in Hot Paths

**Files & Classes**:
- `src/SmallMind.Core/Core/KVCache.cs` - `public class KVCache` (line 18)
- `src/SmallMind.Core/Core/Optimizer.cs` - `public class AdamOptimizer` (line 15)

**Why it matters**: JIT cannot devirtualize calls to methods of non-sealed classes

**Fix**: Add `sealed` keyword if no inheritance is needed

**Risk**: LOW - Check for subclasses first

**Estimated Impact**: 1-3% in methods that use these classes

---

### 6. Foreach Over Dictionary in Model Loading

**File**: `src/SmallMind.Quantization/IO/Smq/SmqWriter.cs`  
**Pattern**: `foreach (var kvp in tensors)` where tensors is Dictionary

**Why it matters**: Dictionary enumerator allocation

**Hidden cost**: 
- Allocates enumerator object
- Not a hot path (only model loading)

**Fix**:
```csharp
// Before:
foreach (var kvp in tensors) { ... }

// After:
foreach (var kvp in tensors.AsEnumerable()) { ... }  // Or use for with Count
```

**Risk**: NEGLIGIBLE - Model loading only

**Estimated Impact**: LOW - Not in inference hot path

---

## ✅ GOOD PRACTICES OBSERVED

The codebase shows excellent optimization awareness:

1. ✅ **[SkipLocalsInit]** on all SIMD classes (MatMulOps, ActivationOps, etc.)
2. ✅ **[MethodImpl(AggressiveInlining)]** on tiny hot methods
3. ✅ **[MethodImpl(AggressiveOptimization)]** on kernel entrypoints
4. ✅ Static classes are properly used (no accidental instantiation)
5. ✅ Custom `FastExp()` approximation avoids expensive `MathF.Exp()`
6. ✅ Block-wise attention to reduce O(n²) memory
7. ✅ SIMD dispatch (AVX-512 → AVX2 → fallback)
8. ✅ Cache blocking in MatMul (32×32 tiles)
9. ✅ Unsafe pointers already used in AVX-512 kernels

---

## Priority Ranking

| Rank | Issue | File(s) | Impact | Effort | ROI |
|------|-------|---------|--------|--------|-----|
| 1 | Span.Slice() in loops | MatMulOps, LayerNormOps | HIGH | MED | ⭐⭐⭐ |
| 2 | Unsealed Tensor class | Tensor.cs | HIGH | LOW | ⭐⭐⭐ |
| 3 | ITensorStorage dispatch | Tensor.cs, ITensorStorage.cs | HIGH | MED | ⭐⭐ |
| 4 | Math.Clamp branchy | OptimizedOps.cs | MED | LOW | ⭐⭐ |
| 5 | Seal other classes | KVCache.cs, etc. | LOW | LOW | ⭐ |
| 6 | Foreach over Dict | SmqWriter.cs | LOW | LOW | ⭐ |

**Legend**: ⭐⭐⭐ = High ROI, ⭐⭐ = Medium ROI, ⭐ = Low ROI

---

## Next Steps

1. **Phase 2**: Eliminate interface/virtual dispatch (Issues #1, #2, #3)
2. **Phase 3**: Reduce bounds checks (Issue #3 - Span.Slice)
3. **Phase 4**: Apply surgical fixes (Issue #4 - Math.Clamp)
4. **Phase 5**: Seal remaining classes (Issues #5)
5. **Phase 6**: Run PerfRunner before/after to validate improvements

**Target**: 10-20% overall improvement in tokens/sec with 0 allocations per token maintained.

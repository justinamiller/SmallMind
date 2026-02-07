# Numeric Tolerance Policy for SmallMind

## Purpose
This document defines acceptable numeric tolerance for floating-point operations in SmallMind to ensure correctness while accounting for the inherent imprecision of floating-point arithmetic and optimized implementations.

## General Principles

### 1. Floating-Point Precision
- **Default Tolerance**: `1e-4f` (0.0001) for most operations
- **Rationale**: Balances numerical stability with practical precision needs for neural network inference

### 2. Optimization vs. Correctness Trade-offs
SmallMind prioritizes:
1. **Correctness** - Results must be mathematically sound within tolerance
2. **Performance** - SIMD/vectorized operations may introduce minor rounding differences
3. **Determinism** - Same inputs → same outputs (within tolerance)

---

## Operation-Specific Tolerances

### Matrix Multiplication (MatMul)
- **Tolerance**: `1e-4f` (relative) or `1e-6f` (absolute for small values)
- **Rationale**: 
  - Accumulated rounding errors from thousands of multiply-add operations
  - SIMD operations may process in different order than scalar
- **Validation**:
  ```csharp
  float diff = Math.Abs(optimized[i] - reference[i]);
  float absErr = diff;
  float relErr = diff / Math.Max(Math.Abs(reference[i]), 1e-6f);
  Assert.True(absErr < 1e-6f || relErr < 1e-4f);
  ```

### Softmax
- **Tolerance**: `1e-5f` for individual probabilities
- **Sum Tolerance**: `1e-4f` (sum must be within 1e-4 of 1.0)
- **Rationale**:
  - Exponentials are sensitive to input precision
  - Fast approximations (Pade, polynomial) trade precision for speed
- **Validation**:
  ```csharp
  // Individual probabilities
  Assert.InRange(output[i], 0f, 1f);
  
  // Sum to 1.0
  float sum = output.Sum();
  Assert.Equal(1.0f, sum, 1e-4f);
  ```

### Layer Normalization
- **Tolerance**: `1e-5f`
- **Rationale**:
  - Mean and variance calculations are sensitive
  - Welford's algorithm vs two-pass may differ slightly
- **Invariant**: Normalized output mean ≈ 0, std ≈ 1 (within 1e-4)

### Attention (Fused)
- **Tolerance**: `1e-4f`
- **Rationale**:
  - Combines MatMul + Softmax + MatMul (errors compound)
  - Block-wise flash attention may reorder operations
- **Validation**: Compare against unfused reference implementation

### Fast Approximations (FastExp, FastTanh, etc.)
- **Tolerance**: `5e-3f` (0.5% relative error)
- **Rationale**:
  - Pade approximations intentionally trade accuracy for speed
  - Neural networks are robust to this level of approximation error
- **When Acceptable**: Softmax, activations (GELU, swish)
- **NOT Acceptable**: Loss computation, gradient calculations (use exact)

---

## Determinism Requirements

### Same Seed → Same Output
All operations must be fully deterministic given the same inputs and random seed:

```csharp
[Fact]
public void Operation_IsDeterministic()
{
    var input = GenerateInput(seed: 42);
    var result1 = PerformOperation(input);
    var result2 = PerformOperation(input);
    Assert.Equal(result1, result2); // Exact equality
}
```

**Exceptions**:
- Parallel operations with non-commutative reductions (use stable ordering)
- Memory-mapped storage with varying OS page sizes (use fixed page size)

---

## Validation Strategy

### 1. Reference Implementations
For each optimized kernel, maintain a naive reference implementation:
- **MatMul**: O(n³) triple loop
- **Softmax**: Two-pass (find max, then exp & sum)
- **LayerNorm**: Two-pass (compute mean/var, then normalize)

### 2. Correctness Tests
- **Unit Tests**: Compare optimized vs reference on small inputs (8×8, 16×16)
- **Integration Tests**: Compare on realistic sizes (256×256, 512×512)
- **Property Tests**: Verify mathematical properties (softmax sums to 1, norm mean ≈ 0)

### 3. Regression Tests
Before accepting performance optimizations:
```csharp
[Fact]
public void Optimization_DoesNotChangeResults()
{
    var input = GenerateRealisticInput();
    var beforeOpt = ReferenceImplementation(input);
    var afterOpt = OptimizedImplementation(input);
    
    for (int i = 0; i < beforeOpt.Length; i++)
    {
        Assert.Equal(beforeOpt[i], afterOpt[i], tolerance: 1e-4f);
    }
}
```

---

## Unsafe Code Validation

When using `unsafe` code for performance:
1. **Pre-validate bounds** before entering unsafe block
2. **Test with address sanitizer** (if available)
3. **Fuzz test** with random sizes and offsets
4. **Document invariants** (e.g., "offset + length <= array.Length")

Example:
```csharp
public unsafe void UnsafeMatMul(float[] A, float[] B, float[] C, int M, int K, int N)
{
    // Validate BEFORE unsafe
    if (A.Length < M * K) throw new ArgumentException(nameof(A));
    if (B.Length < K * N) throw new ArgumentException(nameof(B));
    if (C.Length < M * N) throw new ArgumentException(nameof(C));
    
    fixed (float* pA = A, pB = B, pC = C)
    {
        // Now safe to index with confidence
        // ...
    }
}
```

---

## Acceptable Deviations

### SIMD vs Scalar
SIMD operations may produce slightly different results due to:
- **Different rounding order**: `(a+b)+(c+d)` vs `((a+b)+c)+d`
- **FMA units**: Fused multiply-add (`a*b+c`) has different rounding than `(a*b)+c`

**Accept** if: Difference < tolerance (1e-4f for most ops)
**Reject** if: Systematic bias or catastrophic cancellation

### Parallel vs Sequential
Parallel reductions with `Parallel.For` may sum in different order:

**Mitigation**:
- Use stable reduction algorithms (Kahan summation if needed)
- Document that parallel order is non-deterministic
- Provide `sequential: true` flag for exact reproducibility

---

## Performance vs. Correctness Trade-offs

| Optimization | Tolerance Impact | Acceptable? |
|--------------|------------------|-------------|
| SIMD vectorization | ±1e-5 (rounding order) | ✅ Yes |
| FMA instructions | ±1e-6 (different rounding) | ✅ Yes |
| Fast approximations (FastExp) | ±5e-3 (Pade error) | ✅ Yes (softmax only) |
| Block-wise attention | ±1e-4 (incremental softmax) | ✅ Yes |
| Reduced precision (FP16) | ±1e-2 to 1e-3 | ⚠️ Not implemented yet |
| Removing operations | Undefined | ❌ Never |

---

## Testing Checklist

Before merging performance optimizations:

- [ ] Unit tests pass (optimized matches reference within tolerance)
- [ ] Determinism tests pass (same input → same output)
- [ ] Integration tests pass on realistic workloads
- [ ] No NaN or Infinity in outputs (unless expected)
- [ ] Benchmark shows measurable improvement (>5%)
- [ ] No regressions in other benchmarks
- [ ] Documentation updated (if public API changed)

---

## References

- IEEE 754 Floating-Point Standard
- "What Every Computer Scientist Should Know About Floating-Point Arithmetic" - David Goldberg
- Kahan Summation Algorithm
- Welford's Online Variance Algorithm

---

**Last Updated**: 2026-02-07  
**Maintained By**: SmallMind Performance Team

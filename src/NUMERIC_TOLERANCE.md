# Numerical Tolerance Policy

**Project**: SmallMind
**Date**: 2026-02-07
**Version**: 1.0

## Purpose

This document defines acceptable numerical error tolerances for SmallMind's floating-point operations, ensuring correctness while allowing for expected precision variations across different CPU architectures and optimization levels.

---

## Tolerance Levels

### 1. Exact Operations (Zero Tolerance)

Operations that must produce **bit-exact** results:

- **Token IDs and vocabulary lookups**
- **Tensor shapes and dimension calculations**
- **Cache indices and position tracking**
- **Random number generation with fixed seed** (determinism tests)

**Tolerance**: `0.0` (exact match required)

**Validation**: Use `Assert.Equal()` or bitwise comparison

---

### 2. Basic Arithmetic (1e-6 relative error)

Simple floating-point operations with minimal accumulated error:

- **Element-wise addition/subtraction**
- **Element-wise multiplication/division**
- **Scalar-vector operations**

**Tolerance**: `1e-6f` (relative) or `1e-7f` (absolute for values near zero)

**Validation**:
```csharp
Assert.InRange(Math.Abs(actual - expected), 0, 1e-6f * Math.Abs(expected));
```

---

### 3. Aggregation Operations (1e-5 relative error)

Operations with sum reduction that accumulate rounding errors:

- **Vector dot products**
- **Matrix-vector products**
- **Sum, mean, variance calculations**

**Tolerance**: `1e-5f` (relative)

**Rationale**: SIMD horizontal reductions use different associativity than scalar loops, leading to slightly different rounding behavior.

**Validation**:
```csharp
AssertClose(expected, actual, relTol: 1e-5f);
```

---

### 4. Matrix Multiplication (1e-4 relative error)

Operations involving nested loops and many accumulations:

- **Matrix-matrix multiplication (MatMul)**
- **Batched MatMul**
- **Linear layer forward pass**

**Tolerance**: `1e-4f` (relative)

**Rationale**:
- Tiled algorithms change accumulation order
- SIMD FMA instructions vs scalar multiply-add differ slightly
- Parallel partitioning affects reduction order

**Validation**:
```csharp
AssertClose(expected, actual, relTol: 1e-4f);
```

---

### 5. Transcendental Functions (1e-4 to 5e-4 error)

Activation functions using approximations:

- **GELU** (Padé tanh approximation): `5e-4f` max absolute error
- **Softmax**: `1e-4f` relative error
- **LayerNorm**: `1e-4f` relative error
- **Exponential/Log approximations**: Platform-dependent

**Tolerance**:
- GELU: `5e-4f` (absolute, documented in ActivationOps.cs)
- Softmax: `1e-4f` (relative)
- LayerNorm: `1e-4f` (relative)

**Validation**:
```csharp
// GELU
Assert.InRange(Math.Abs(actual - expected), 0, 5e-4f);

// Softmax/LayerNorm
AssertClose(expected, actual, relTol: 1e-4f);
```

---

### 6. Attention Mechanisms (1e-3 relative error)

Complex multi-step operations:

- **Fused attention (QK^T → softmax → PV)**
- **Multi-head attention**
- **Causal attention with masking**

**Tolerance**: `1e-3f` (relative)

**Rationale**: Combines MatMul, softmax, and more MatMul with different tiling strategies.

**Validation**:
```csharp
AssertClose(expected, actual, relTol: 1e-3f);
```

---

## Platform-Specific Considerations

### SIMD Path Variations

Different SIMD paths may produce slightly different results:

1. **AVX-512** vs **AVX2** vs **ARM NEON** vs **Vector<T> scalar fallback**
2. **Fused Multiply-Add (FMA)** vs separate multiply + add

**Policy**: All paths must be within the tolerance for their operation category when compared to a **reference scalar implementation**.

**Example**:
```csharp
[Fact]
public void MatMul_SimdEquivalence()
{
    var scalarResult = MatMulNaive(A, B);  // Reference implementation
    var simdResult = MatMulOps.MatMul(A, B);  // Optimized SIMD
    
    AssertClose(scalarResult, simdResult, relTol: 1e-4f);
}
```

---

### Denormalized Numbers

**Flush-to-Zero (FTZ)** mode may be enabled on some CPUs for performance.

**Policy**:
- Accept denormal → 0 flushing in intermediate calculations
- Final outputs must respect tolerance even with FTZ

---

## Determinism Requirements

### Same Input → Same Output

When **same seed** is provided:

1. **Same CPU architecture**: Must be **bit-exact**
2. **Different SIMD paths** (e.g., AVX-512 vs ARM NEON): Must be within operation tolerance
3. **Different thread counts**: Must be within operation tolerance (due to parallel reduction order)

**Test Pattern**:
```csharp
[Fact]
public void Inference_Deterministic_SameSeed()
{
    var model1 = new Model(seed: 42);
    var model2 = new Model(seed: 42);
    
    var output1 = model1.Generate(prompt);
    var output2 = model2.Generate(prompt);
    
    // Bit-exact tokens (same architecture)
    Assert.Equal(output1.Tokens, output2.Tokens);
    
    // Close logits (cross-architecture)
    AssertClose(output1.Logits, output2.Logits, relTol: 1e-3f);
}
```

---

## Cached vs Non-Cached Inference

**KV-cache** enabled vs disabled should produce **identical results** within tolerance.

**Tolerance**: `1e-4f` (relative)

**Rationale**: Cache layout and memory access patterns differ, but numerical operations are the same.

**Validation**:
```csharp
[Theory]
[InlineData(true)]  // With cache
[InlineData(false)] // Without cache
public void Inference_CacheEquivalence(bool useCache)
{
    var outputs = model.Forward(input, useCache: useCache);
    
    // Compare cached vs non-cached runs
    AssertClose(outputsWithCache, outputsWithoutCache, relTol: 1e-4f);
}
```

---

## Quantization Tolerances

### Q8 (8-bit) Quantization

**Tolerance**: `0.02` (2% relative error) or `±4` quantization units

**Rationale**: 8-bit quantization inherently introduces discretization error.

### Q4 (4-bit) Quantization

**Tolerance**: `0.05` (5% relative error) or `±16` quantization units

**Rationale**: 4-bit has coarser granularity and higher quantization noise.

---

## Test Assertion Helpers

### AssertClose (Relative + Absolute Tolerance)

```csharp
public static void AssertClose(float expected, float actual, 
                                float relTol = 1e-5f, float absTol = 1e-8f)
{
    if (float.IsNaN(expected) || float.IsNaN(actual))
        throw new Exception($"NaN detected: expected={expected}, actual={actual}");
    
    float diff = Math.Abs(expected - actual);
    float relError = diff / (Math.Abs(expected) + 1e-10f);  // Avoid division by zero
    
    if (diff > absTol && relError > relTol)
    {
        throw new Exception(
            $"Values not close: expected={expected}, actual={actual}, " +
            $"relError={relError:E3}, relTol={relTol:E3}");
    }
}

public static void AssertClose(float[] expected, float[] actual, float relTol = 1e-5f)
{
    Assert.Equal(expected.Length, actual.Length);
    
    for (int i = 0; i < expected.Length; i++)
    {
        AssertClose(expected[i], actual[i], relTol);
    }
}
```

---

## Summary Table

| Operation | Tolerance | Type | Reason |
|-----------|-----------|------|--------|
| Token IDs | 0 (exact) | Absolute | Determinism requirement |
| Element-wise ops | 1e-6 | Relative | Single FP operation |
| Vector dot product | 1e-5 | Relative | Sum reduction variance |
| Matrix multiplication | 1e-4 | Relative | Nested loops, tiling |
| GELU activation | 5e-4 | Absolute | Padé approximation error |
| Softmax | 1e-4 | Relative | Exp + reduction |
| LayerNorm | 1e-4 | Relative | Variance + normalization |
| Attention | 1e-3 | Relative | Multi-stage operation |
| Q8 quantization | 0.02 (2%) | Relative | Discretization error |
| Q4 quantization | 0.05 (5%) | Relative | Coarse granularity |

---

## Updating Tolerances

**When to tighten tolerance:**
- New algorithm that demonstrably reduces error
- Hardware upgrade with better FP precision

**When to relax tolerance:**
- New optimization that changes accumulation order (document why)
- Cross-platform support for lower-precision hardware

**Process:**
1. Propose change in this document
2. Run full test suite on all supported platforms
3. Verify no degradation in model accuracy (perplexity, benchmark tasks)
4. Document rationale in git commit

---

## References

1. **IEEE 754 Floating Point**: Understanding rounding modes and precision limits
2. **SIMD Intrinsics**: Platform-specific FMA behavior (Intel vs ARM)
3. **Catastrophic Cancellation**: Numerical stability in LayerNorm variance computation
4. **Fast Approximations**: Trade-offs in GELU Padé vs exact erf() implementation

---

**Maintained by**: SmallMind Performance Team
**Last Updated**: 2026-02-07

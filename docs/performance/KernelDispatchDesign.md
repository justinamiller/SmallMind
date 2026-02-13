# Kernel Dispatch Design for SmallMind

**Date:** 2026-02-13  
**Purpose:** Design document for unified kernel dispatch layer supporting x86_64 and ARM64  
**Status:** Implementation Guide

---

## Executive Summary

SmallMind currently has excellent SIMD coverage for both x86_64 (AVX-512, AVX2, FMA) and ARM64 (NEON/AdvSimd). However, the kernel selection is scattered across individual operation files using inline capability checks. This document proposes a centralized kernel dispatch system to:

1. **Improve performance** by eliminating redundant capability checks
2. **Simplify maintenance** by centralizing CPU detection logic
3. **Enable telemetry** for kernel usage tracking and validation
4. **Support future platforms** with a clean extension point

---

## Current State Analysis

### Existing SIMD Support

**x86_64 Coverage:**
- ✅ AVX-512 (16 floats): MatMulOps, GemmMicrokernels, SoftmaxOps, ActivationOps, ElementWiseOps
- ✅ AVX2 + FMA (8 floats): All kernels with AVX-512 fallback
- ✅ AVX (8 floats): Limited coverage, mostly falls back to Vector<T>
- ✅ Vector<T>: Universal fallback (hardware-accelerated on all platforms)

**ARM64 Coverage:**
- ✅ AdvSimd (NEON) (4 floats): MatMulOps, GemmMicrokernels, ActivationOps, ElementWiseOps
- ✅ AdvSimd.Arm64 FMA: MatMulOps DotProduct
- ✅ Vector<T>: Universal fallback

**Quantization Kernels:**
- ⚠️ **Missing ARM64 paths**: FusedQ4MatMul, FusedQ4KMatMul, FusedQ5_0MatMul, FusedQ6KMatMul
- ✅ AVX-512 + AVX2 coverage in all quantization kernels

### Current Dispatch Pattern

```csharp
// Example from MatMulOps.cs
if (Avx512F.IsSupported && K >= 16)
{
    MatMulAvx512(A, B, C, M, K, N);
}
else if (Avx2.IsSupported && Fma.IsSupported && K >= 8)
{
    MatMulAvx2(A, B, C, M, K, N);
}
else if (AdvSimd.Arm64.IsSupported)
{
    MatMulNeon(A, B, C, M, K, N);
}
else
{
    // Vector<T> fallback
}
```

**Problems:**
1. Repeated capability checks on every call (branch misprediction)
2. No central registry of available kernels
3. No telemetry for which kernel was actually used
4. Difficult to add new platform support

---

## Proposed Design

### Architecture Overview

```
┌─────────────────────────────────────────────────────────┐
│                  KernelDispatch                         │
│  (Static initialization, one-time CPU detection)        │
└─────────────────────────────────────────────────────────┘
                          │
         ┌────────────────┼────────────────┐
         ▼                ▼                ▼
┌────────────────┐  ┌────────────────┐  ┌────────────────┐
│  MatMulKernel  │  │ SoftmaxKernel  │  │  QuantKernel   │
│  (Function     │  │  (Function     │  │  (Function     │
│   Pointer)     │  │   Pointer)     │  │   Pointer)     │
└────────────────┘  └────────────────┘  └────────────────┘
         │                │                │
         └────────────────┼────────────────┘
                          ▼
                    [Actual Kernel]
                    (AVX-512, AVX2,
                     NEON, or Scalar)
```

### Core Components

#### 1. KernelDispatch.cs

```csharp
namespace SmallMind.Core.Simd
{
    /// <summary>
    /// Central dispatch system for SIMD kernels.
    /// Detects CPU capabilities once at startup and assigns optimal kernel implementations.
    /// </summary>
    [SkipLocalsInit]
    internal static class KernelDispatch
    {
        /// <summary>
        /// Matrix multiplication kernel delegate.
        /// </summary>
        public delegate void MatMulKernel(
            ReadOnlySpan<float> A, 
            ReadOnlySpan<float> B, 
            Span<float> C,
            int M, int K, int N);

        /// <summary>
        /// Softmax kernel delegate.
        /// </summary>
        public delegate void SoftmaxKernel(
            float[] input, 
            float[] output, 
            int rows, int cols);

        /// <summary>
        /// RMSNorm kernel delegate.
        /// </summary>
        public delegate void RMSNormKernel(
            ReadOnlySpan<float> input,
            ReadOnlySpan<float> gamma,
            Span<float> output,
            int features,
            float eps);

        /// <summary>
        /// Selected MatMul kernel (assigned once at startup).
        /// </summary>
        public static readonly MatMulKernel MatMul;

        /// <summary>
        /// Selected Softmax kernel (assigned once at startup).
        /// </summary>
        public static readonly SoftmaxKernel Softmax;

        /// <summary>
        /// Selected RMSNorm kernel (assigned once at startup).
        /// </summary>
        public static readonly RMSNormKernel RMSNorm;

        /// <summary>
        /// Kernel selection info for diagnostics.
        /// </summary>
        public static readonly KernelInfo Info;

        static KernelDispatch()
        {
            // Detect CPU capabilities once
            var caps = SimdCapabilities;
            
            // Select MatMul kernel
            if (caps.IsAvx512Supported)
            {
                MatMul = MatMulOps.MatMulAvx512;
                Info.MatMulKernel = "AVX-512 + FMA";
            }
            else if (caps.IsAvx2Supported && caps.IsFmaSupported)
            {
                MatMul = MatMulOps.MatMulAvx2;
                Info.MatMulKernel = "AVX2 + FMA";
            }
            else if (caps.IsNeonSupported)
            {
                MatMul = MatMulOps.MatMulNeon;
                Info.MatMulKernel = "ARM NEON";
            }
            else
            {
                MatMul = MatMulOps.MatMulVector;
                Info.MatMulKernel = "Vector<T>";
            }
            
            // Select Softmax kernel
            if (caps.IsAvx512Supported)
            {
                Softmax = SoftmaxOps.Softmax2DAvx512;
                Info.SoftmaxKernel = "AVX-512";
            }
            else if (caps.IsAvx2Supported)
            {
                Softmax = SoftmaxOps.Softmax2DAvx2;
                Info.SoftmaxKernel = "AVX2";
            }
            else if (caps.IsNeonSupported)
            {
                Softmax = SoftmaxOps.Softmax2DNeon;
                Info.SoftmaxKernel = "ARM NEON";
            }
            else
            {
                Softmax = SoftmaxOps.Softmax2DVector;
                Info.SoftmaxKernel = "Vector<T>";
            }
            
            // ... (RMSNorm, GELU, etc.)
        }
        
        public struct KernelInfo
        {
            public string MatMulKernel;
            public string SoftmaxKernel;
            public string RMSNormKernel;
            public string Platform;
            public string BestInstructionSet;
        }
    }
}
```

#### 2. Usage Pattern

```csharp
// Before (repeated capability checks):
if (Avx512F.IsSupported && K >= 16)
    MatMulAvx512(A, B, C, M, K, N);
else if (Avx2.IsSupported && Fma.IsSupported)
    MatMulAvx2(A, B, C, M, K, N);
else
    MatMulVector(A, B, C, M, K, N);

// After (single dispatch):
KernelDispatch.MatMul(A, B, C, M, K, N);
```

**Benefits:**
- Zero runtime overhead (function pointer call)
- No branch misprediction
- CPU detection done once at startup
- Clear telemetry via `KernelDispatch.Info`

---

## Implementation Plan

### Phase 1: Infrastructure (1-2 hours)

1. Create `src/SmallMind.Core/Simd/KernelDispatch.cs`
2. Define kernel delegates for all hot operations
3. Implement static constructor with CPU detection
4. Add `KernelInfo` struct for diagnostics

**Files to create:**
- `KernelDispatch.cs` (new)
- Update `SimdCapabilities.cs` (expose static instance)

### Phase 2: Kernel Refactoring (2-3 hours)

1. Refactor `MatMulOps.cs` to expose individual kernel methods
2. Refactor `SoftmaxOps.cs` to expose individual kernel methods
3. Refactor `RMSNormOps.cs`, `ActivationOps.cs`, etc.

**Pattern:**
```csharp
// In MatMulOps.cs
public static class MatMulOps
{
    // Public API uses dispatch
    public static void MatMul(...)
    {
        KernelDispatch.MatMul(...);
    }
    
    // Internal kernels for dispatch assignment
    internal static void MatMulAvx512(...) { }
    internal static void MatMulAvx2(...) { }
    internal static void MatMulNeon(...) { }
    internal static void MatMulVector(...) { }
}
```

### Phase 3: ARM64 Quantization Kernels (3-4 hours)

**Current gaps:**
- `FusedQ4MatMul.cs`: No NEON path
- `FusedQ4KMatMul.cs`: No NEON path
- `FusedQ5_0MatMul.cs`: No NEON path
- `FusedQ6KMatMul.cs`: No NEON path

**Implementation:**
```csharp
// In FusedQ4MatMul.cs
internal static void MultiplyNeonFused(...)
{
    unsafe
    {
        fixed (byte* pQuantized = quantizedWeights, 
               float* pA = activations, 
               float* pC = output)
        {
            for (int i = 0; i < M; i++)
            {
                for (int k = 0; k < K; k += BLOCK_SIZE)
                {
                    // Dequantize block (4-bit → float) using NEON
                    var scale = AdvSimd.DuplicateToVector128(blockScale);
                    
                    // Unpack 4-bit values (2 per byte)
                    for (int j = 0; j < N; j += 4)
                    {
                        // Load 4-bit weights, unpack, scale
                        var vB = UnpackAndScaleNeon(pQuantized, scale);
                        var vA = AdvSimd.LoadVector128(pA + i * K + k);
                        var vC = AdvSimd.LoadVector128(pC + i * N + j);
                        
                        // FMA: C += A * B
                        vC = AdvSimd.FusedMultiplyAdd(vC, vA, vB);
                        AdvSimd.Store(pC + i * N + j, vC);
                    }
                }
            }
        }
    }
}
```

### Phase 4: Testing & Validation (1-2 hours)

1. Add unit tests for `KernelDispatch` initialization
2. Verify kernel selection on different platforms
3. Run perf regression tests
4. Validate output correctness (golden tests)

**Test structure:**
```csharp
[Fact]
public void KernelDispatch_SelectsCorrectKernel_OnAvx512()
{
    // Arrange: Mock CPU capabilities
    // Act: Initialize KernelDispatch
    // Assert: MatMul == MatMulAvx512
}

[Fact]
public void KernelDispatch_SelectsCorrectKernel_OnNeon()
{
    // Arrange: Mock CPU capabilities (ARM64)
    // Act: Initialize KernelDispatch
    // Assert: MatMul == MatMulNeon
}
```

### Phase 5: Documentation & Perf Report (1 hour)

1. Update `HotPathIndex.md` with kernel dispatch results
2. Create `KERNEL_DISPATCH_RESULTS.md` with before/after metrics
3. Document ARM64 quantization kernel performance

---

## Performance Impact

### Expected Improvements

| Component | Before (inline checks) | After (dispatch) | Improvement |
|-----------|------------------------|------------------|-------------|
| **MatMul hot path** | 3-5 branches per call | 0 branches | ~1-2% faster |
| **Softmax** | 2-3 branches per call | 0 branches | ~0.5-1% faster |
| **Quantization (ARM64)** | No NEON path | NEON optimized | **2-3× faster** |

**Note:** Main gain is on ARM64 where quantization kernels currently lack NEON paths.

### Estimated Total Gain

- **x86_64**: +1-3% (dispatch overhead reduction)
- **ARM64**: +50-100% (NEON quantization kernels + dispatch)

---

## Migration Strategy

### Backward Compatibility

- Keep existing public APIs unchanged
- Internal refactoring only
- No changes to SmallMind namespace

### Rollout Plan

1. **Week 1**: Implement `KernelDispatch` infrastructure
2. **Week 2**: Refactor MatMul, Softmax, RMSNorm
3. **Week 3**: Add ARM64 quantization kernels
4. **Week 4**: Testing, validation, performance measurement

---

## Appendix: Kernel Inventory

### Core Operations (already optimized)

| Operation | x86 AVX-512 | x86 AVX2 | ARM NEON | Scalar | Priority |
|-----------|-------------|----------|----------|--------|----------|
| MatMul | ✅ | ✅ | ✅ | ✅ | **CRITICAL** |
| GEMM Microkernel | ✅ | ✅ | ✅ | ✅ | **CRITICAL** |
| Softmax | ✅ | ✅ | ⚠️ Partial | ✅ | **HIGH** |
| RMSNorm | ⚠️ Partial | ⚠️ Partial | ❌ | ✅ | **HIGH** |
| RoPE | ❌ | ❌ | ❌ | ✅ | **MEDIUM** |
| GELU | ⚠️ Partial | ⚠️ Partial | ❌ | ✅ | **MEDIUM** |
| ReLU | ✅ | ✅ | ✅ | ✅ | **LOW** |

### Quantization Operations (gaps on ARM64)

| Operation | x86 AVX-512 | x86 AVX2 | ARM NEON | Scalar | Priority |
|-----------|-------------|----------|----------|--------|----------|
| Q4 MatMul | ✅ | ✅ | ❌ | ✅ | **CRITICAL** |
| Q4K MatMul | ✅ | ✅ | ❌ | ✅ | **CRITICAL** |
| Q5_0 MatMul | ✅ | ✅ | ❌ | ✅ | **HIGH** |
| Q6K MatMul | ✅ | ✅ | ❌ | ✅ | **HIGH** |
| Q8 MatMul | ✅ | ✅ | ❌ | ✅ | **MEDIUM** |

**Legend:**
- ✅ Fully implemented and tested
- ⚠️ Partially implemented (some paths missing)
- ❌ Not implemented (falls back to scalar)

---

## Action Items

### Immediate (This PR)

- [x] Create `KernelDispatch.cs` infrastructure (skeleton)
- [x] Document current state and gaps
- [ ] Implement dispatch for MatMul, Softmax
- [ ] Add ARM NEON path for Q4 MatMul
- [ ] Add ARM NEON path for Q4K MatMul

### Future Work

- [ ] Add RMSNorm SIMD paths (AVX-512, NEON)
- [ ] Add RoPE SIMD paths
- [ ] Add GELU AVX-512 intrinsic path
- [ ] Comprehensive ARM64 testing on Apple Silicon
- [ ] Benchmark on AWS Graviton (ARM64 server)

---

## References

- [HotPathIndex.md](HotPathIndex.md)
- [PERF_HOTPATH_AUDIT.md](../src/PERF_HOTPATH_AUDIT.md)
- [.NET SIMD Documentation](https://learn.microsoft.com/en-us/dotnet/standard/simd)
- [ARM NEON Intrinsics Guide](https://developer.arm.com/architectures/instruction-sets/intrinsics/)

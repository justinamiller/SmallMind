# GGUF K-Quant Support Status

## Overview

This document tracks the implementation status of K-quant format support in SmallMind, specifically focusing on Q4_K_M compatibility.

## Current GGUF Support

### ✅ Fully Supported

**Quantization Formats:**
- `Q4_0` - 4-bit symmetric quantization (block size 32)
- `Q8_0` - 8-bit symmetric quantization (block size 32)

**Features:**
- GGUF file format parsing (v2 and v3)
- Metadata extraction
- Tensor information reading
- Automatic conversion to SMQ format
- Block size conversion (GGUF 32 → SMQ 64)

### ⚠️ Partially Implemented

**K-Quant Infrastructure:**
- `GgufTensorType` enum includes K-quant types
- Type definitions exist for:
  - `Q2_K`, `Q3_K`, `Q4_K`, `Q5_K`, `Q6_K`, `Q8_K`
- Import validation detects unsupported types
- Clear error messages for unsupported formats

### ❌ Not Yet Implemented

**Missing K-Quant Support:**
- Q4_K_M tensor representation
- Q4_K_M kernel implementation
- Q4_K_M quantization/dequantization
- Other K-quant variants (Q2_K, Q3_K, Q5_K, Q6_K)

## Q4_K_M Implementation Plan

### Phase 1: Tensor Representation (Not Started)

**Tasks:**
1. Define `Q4_K_M_Tensor` class
2. Implement GGUF Q4_K_M block structure:
   - Super-blocks of 256 values
   - 8 sub-blocks of 32 values each
   - Scales and min values per sub-block
   - Quantized values (4-bit packed)

**File:**
- `src/SmallMind.Quantization/Tensors/Q4_K_M_Tensor.cs`

### Phase 2: GGUF Import Support (Not Started)

**Tasks:**
1. Add Q4_K_M case to `GgufImporter.ConvertTensor()`
2. Parse GGUF Q4_K_M block format
3. Convert to internal representation
4. Handle super-block alignment

**Files:**
- `src/SmallMind.Quantization/IO/Gguf/GgufImporter.cs`

### Phase 3: Kernel Implementation (Not Started)

**Tasks:**
1. Implement scalar blocked Q4_K_M kernel
2. Add AVX2 fast path (optional)
3. Ensure zero allocations in hot path
4. Validate against reference implementation

**File:**
- `src/SmallMind.Quantization/Kernels/MatMulF32Q4_K_M.cs`

### Phase 4: Testing & Validation (Not Started)

**Tasks:**
1. Unit tests for Q4_K_M quantization/dequantization
2. MatMul correctness tests
3. GGUF import round-trip tests
4. Performance benchmarks

**Files:**
- `tests/SmallMind.Quantization.Tests/Q4_K_M_Tests.cs`

## Current Behavior

### Unsupported Format Handling

When attempting to import a GGUF file with Q4_K_M tensors:

```
The following tensors have unsupported types:
  - model.layers.0.attention.wq: Q4_K
  - model.layers.0.attention.wk: Q4_K
  ...

Supported types: Q8_0, Q4_0
```

This provides:
- ✅ Clear error message
- ✅ List of problematic tensors
- ✅ Guidance on supported formats
- ✅ No silent failures or corruption

### Workaround

Until Q4_K_M support is implemented, users can:

1. Convert GGUF models to supported formats:
   ```bash
   # Use llama.cpp's quantize tool
   ./quantize model.gguf model-q4_0.gguf Q4_0
   ```

2. Use native SMQ format for quantization:
   ```csharp
   // Quantize directly in SmallMind
   var q4Tensor = Q4Tensor.Quantize(weights, rows, cols, blockSize: 64);
   ```

## Technical Details

### Q4_K_M Format Specification

**Block Structure:**
```
Super-block (256 values):
├─ scales_and_mins: fp16[8] - per sub-block
├─ d: fp16 - super-block delta (scale)
├─ dmin: fp16 - super-block minimum
└─ qs: uint8[128] - quantized values (4-bit packed)
```

**Memory Layout:**
- 256 values per super-block
- 8 sub-blocks of 32 values
- 2 scales per sub-block (scale + min)
- More accurate than Q4_0 due to min tracking

### Implementation Challenges

1. **Super-block Alignment:**
   - Tensor dimensions must be divisible by 256
   - Handle partial super-blocks at edges

2. **Scale Hierarchies:**
   - Super-block scale (`d`)
   - Sub-block scales (8 per super-block)
   - Efficient application during matmul

3. **Memory Layout:**
   - Different from Q4_0 (no direct mapping)
   - Requires custom unpacking logic

4. **Performance:**
   - More complex than Q4_0
   - Must maintain allocation-free guarantees
   - AVX2 optimization more challenging

## Timeline & Priorities

### High Priority
- [ ] Q4_K_M import (diagnostic only) - **Immediate**
- [ ] Clear compatibility matrix documentation - **Immediate**

### Medium Priority  
- [ ] Q4_K_M tensor representation - **Next Sprint**
- [ ] Scalar blocked kernel - **Next Sprint**

### Low Priority
- [ ] AVX2 optimized kernel - **Future**
- [ ] Other K-quant variants - **Future**

## References

- GGUF Specification: https://github.com/ggerganov/ggml/blob/master/docs/gguf.md
- llama.cpp K-quants: https://github.com/ggerganov/llama.cpp/pull/1684
- K-quant Paper: https://arxiv.org/abs/2306.00978

## Progress Tracking

| Component | Status | File |
|-----------|--------|------|
| Type definitions | ✅ Complete | `GgufTypes.cs` |
| Import validation | ✅ Complete | `GgufImporter.cs` |
| Error diagnostics | ✅ Complete | `GgufImporter.cs` |
| Tensor representation | ❌ Not started | - |
| Import conversion | ❌ Not started | - |
| Kernel implementation | ❌ Not started | - |
| Unit tests | ❌ Not started | - |
| Performance benchmarks | ❌ Not started | - |

**Legend:**
- ✅ Complete and tested
- ⚠️ Partial implementation
- ❌ Not yet started
- 🚧 In progress

Last Updated: 2026-02-06

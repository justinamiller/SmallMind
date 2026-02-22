# GGUF K-Quant Support Status

## Overview

This document tracks the implementation status of K-quant format support in SmallMind.

## Current GGUF Support

### ✅ Fully Supported

**Quantization Formats:**
- `F32` - 32-bit float (pass-through)
- `F16` - 16-bit float (converted to F32 on load)
- `Q4_0` - 4-bit symmetric quantization (block size 32)
- `Q4_1` - 4-bit asymmetric quantization (block size 32)
- `Q5_0` - 5-bit symmetric quantization (block size 32)
- `Q8_0` - 8-bit symmetric quantization (block size 32)
- `Q4_K` - K-quant 4-bit (super-block size 256, 144 bytes/block)
- `Q6_K` - K-quant 6-bit (super-block size 256, 210 bytes/block)

**Features:**
- GGUF file format parsing (v2 and v3)
- Metadata extraction
- Tensor information reading with correct byte-size calculation for all supported types
- Automatic conversion to SMQ format
- Q6_K dequantization and fused FP32×Q6_K matrix multiplication kernel
- Q4_K dequantization and fused FP32×Q4_K matrix multiplication kernel

### ⚠️ Size-Calculation Supported (Import Not Implemented)

These types are recognized for size calculation (so GGUF parsing succeeds) but the
import path will report them as unsupported if they appear in a model file:

- `Q2_K`, `Q3_K`, `Q5_K`, `Q8_K`

### ❌ Not Yet Supported

**IQ (Importance-weighted) Quantization Formats:**
- `IQ1_S`, `IQ2_XXS`, `IQ2_XS`, `IQ2_S`, `IQ3_XXS`, `IQ3_S`, `IQ4_NL`, `IQ4_XS`

## Q6_K Format Details

**Block Structure (256 values per super-block):**
```
ql[128]:    low 4 bits of each 6-bit value (2 values packed per byte)
qh[64]:     high 2 bits of each 6-bit value (4 values packed per byte)
scales[16]: int8 scales for each 16-value sub-block
d (fp16):   super-block scale
Total:      128 + 64 + 16 + 2 = 210 bytes
```

**Value reconstruction (per llama.cpp / ggml-quants.h spec):**
```
q = (ql[i/2] >> ((i%2)*4)) & 0xF          // low 4 bits
  | ((qh[i/4] >> ((i%4)*2)) & 0x3) << 4   // high 2 bits
value = d * scales[subblock] * (q - 32)   // center around 0
```

## Known Caveats

- `Q6_K` tensors require `totalElements % 256 == 0` for the import path; the size
  calculator handles partial blocks via ceiling division.
- Mixed-quant GGUF files (e.g. most Q6_K layers + F32 norms) are fully supported.
- There is no AVX2 vectorized exp in the GELU approximation path; see code comments
  in `FusedQ6KMatMul.cs` for details.

## Progress Tracking

| Component | Status | File |
|-----------|--------|------|
| Type definitions | ✅ Complete | `GgufTensorType.cs` |
| Size calculation | ✅ Complete | `GgufReader.cs` |
| Import validation | ✅ Complete | `GgufImporter.cs` |
| Q6_K tensor class | ✅ Complete | `Q6KTensor.cs` |
| Q6_K dequantization | ✅ Complete | `Q6KTensor.cs` |
| Q6_K fused MatMul | ✅ Complete | `FusedQ6KMatMul.cs` |
| Q6_K weight tensor | ✅ Complete | `Q6KWeightTensor.cs` |
| Q6_K GGUF import | ✅ Complete | `GgufImporter.cs` |
| Q6_K size unit tests | ✅ Complete | `GgufReaderTests.cs` |
| Q4_K tensor class | ✅ Complete | `Q4KTensor.cs` |
| Q4_K fused MatMul | ✅ Complete | `FusedQ4KMatMul.cs` |
| Q4_K GGUF import | ✅ Complete | `GgufImporter.cs` |

**Legend:**
- ✅ Complete and tested
- ⚠️ Partial implementation
- ❌ Not yet started
- 🚧 In progress

Last Updated: 2026-02-21

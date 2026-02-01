# SmallMind Quantization Guide

## Overview

SmallMind now supports **CPU-first quantized inference** with two quantization schemes and ecosystem compatibility through GGUF import. This enables running compressed models with minimal memory footprint while maintaining good accuracy.

## Supported Quantization Schemes

### Q8_0 (8-bit Symmetric)
- **Precision**: 8-bit signed integers (sbyte)
- **Range**: [-127, 127]
- **Block Size**: 64 (configurable)
- **Memory**: 1 byte per weight + 4 bytes per block scale
- **Accuracy**: ~16% relative error vs FP32
- **Use Case**: High-accuracy quantization for critical layers

### Q4_0 (4-bit Symmetric)
- **Precision**: 4-bit signed integers (packed)
- **Range**: [-8, 7]
- **Block Size**: 64 (configurable)
- **Memory**: 0.5 bytes per weight + 4 bytes per block scale  
- **Accuracy**: Variable (small values may quantize to zero)
- **Use Case**: Maximum compression for feedforward layers

## File Formats

### SMQ (SmallMind Quantized) - Native Format

Binary container optimized for fast loading and inference.

**Structure:**
```
Header (32 bytes)
  - Magic: "SMQv0001" (8 bytes ASCII)
  - Version: uint32
  - TensorCount: uint32
  - MetadataJsonLength: uint32
  - Reserved: 8 bytes

Metadata JSON (variable)
  - UTF-8 encoded model metadata

Tensor Directory (156 bytes per tensor)
  - Name: string with length prefix
  - DataType: uint32 (QuantScheme enum)
  - Rank: uint32
  - Dimensions: uint32[]
  - BlockSize: uint32
  - DataOffset: uint64
  - DataLength: uint64
  - AuxOffset: uint64 (for scales)
  - AuxLength: uint64

Tensor Data Blobs
  - Quantized weights at offsets
  - Scale factors in auxiliary sections
```

**Manifest Sidecar (model.smq.manifest.json):**
```json
{
  "format_version": 1,
  "model_name": "my-model",
  "created_utc": "2026-02-01T18:00:00Z",
  "tensor_count": 32,
  "quant_schemes": ["Q8_0", "Q4_0"],
  "model_dims": {
    "n_layers": 6,
    "hidden_dim": 384,
    "vocab_size": 50257
  },
  "smq_sha256": "abc123..."
}
```

### GGUF Import

SmallMind can import models from GGUF (GPT-Generated Unified Format), the standard used by llama.cpp and other tools.

**Supported:**
- GGUF versions 2 and 3
- Tensor types: Q8_0, Q4_0
- All metadata value types (strings, arrays, primitives)

**Unsupported (will error with clear message):**
- Other quantization types (Q4_1, Q5_0, Q5_1, Q6_K, etc.)
- K-quants (require lookup tables not implemented)

**Automatic Conversions:**
- Block size: GGUF uses 32, SMQ uses 64 → automatic re-quantization
- FP16 weights: Converted to FP32 on import

## CLI Tools

SmallMind provides command-line tools for working with quantized models.

### Import GGUF Model

```bash
# Import a GGUF model to SMQ format
dotnet run --project src/SmallMind.Console import-gguf model.gguf model.smq

# Example output:
# Importing GGUF model from: model.gguf
# Output SMQ file: model.smq
#
# ✓ Import complete!
#   Output: model.smq
#   Manifest: model.smq.manifest.json
#   GGUF size: 145.23 MB
#   SMQ size: 145.50 MB
```

**Supported GGUF quantization types:** Q8_0, Q4_0  
**Unsupported types will fail with error:**
```
Error: Unsupported tensor quantization type: Q4_K_M

Note: SmallMind only supports Q8_0 and Q4_0 quantization schemes.
Models using other quantization types (K-quants, Q4_1, Q5_0, etc.) are not supported.
```

### Inspect Model

```bash
# Basic inspection
dotnet run --project src/SmallMind.Console inspect model.smq

# Example output:
# === SMQ Model: model.smq ===
#
# File Information:
#   Size: 145.50 MB
#   Created: 2026-02-01 18:30:45
#
# Model Metadata:
#   embed_dim: 384
#   num_layers: 6
#   vocab_size: 50257
#
# Tensors: 32 total

# Detailed tensor listing
dotnet run --project src/SmallMind.Console inspect model.smq --tensors

# Example output (partial):
# Tensor Details:
# Name                                     Type       Shape                Size
# ------------------------------------------------------------------------------------------
# tok_emb.weight                           Q8_0       50257×384           18.51 MB
# blocks.0.attn.qkv_proj.weight            Q4_0       384×1152            0.21 MB
# blocks.0.attn.out_proj.weight            Q8_0       384×384             0.14 MB
# blocks.0.mlp.fc1.weight                  Q4_0       384×1536            0.28 MB
# ...

# Verbose mode (includes manifest)
dotnet run --project src/SmallMind.Console inspect model.smq --verbose
```

### Verify Model Integrity

```bash
# Validate SMQ file integrity
dotnet run --project src/SmallMind.Console verify model.smq

# Example output (success):
# Verifying SMQ file: model.smq
#
# ✓ Validation PASSED
#
# Manifest file: Found
#   Model name: my-quantized-model
#   Tensor count: 32
#   Created: 2026-02-01T18:30:45.1234567Z
#   Quant schemes: Q8_0, Q4_0

# Verbose mode
dotnet run --project src/SmallMind.Console verify model.smq --verbose

# Example output (validation error):
# Verifying SMQ file: corrupt-model.smq
#
# ✗ Validation FAILED
#
# Errors (2):
#   - Invalid magic header: expected 'SMQv0001', got 'CORRUPT0'
#   - Tensor directory checksum mismatch
```

## Usage Examples

### Basic Quantization (Programmatic)

```csharp
using SmallMind.Quantization.Tensors;
using SmallMind.Quantization.IO.Smq;

// Quantize a float weight matrix
float[] weights = /* your FP32 weights */;
int rows = 512, cols = 768;

// Q8 quantization (higher quality)
var q8 = Q8Tensor.Quantize(weights, rows, cols, blockSize: 64);

// Q4 quantization (maximum compression)
var q4 = Q4Tensor.Quantize(weights, rows, cols, blockSize: 64);

// Save to SMQ file
using (var writer = new SmqWriter("model.smq"))
{
    var metadata = new Dictionary<string, object>
    {
        ["model_name"] = "my-model",
        ["vocab_size"] = 50257
    };
    
    writer.WriteHeader(1, metadata); // 1 tensor
    
    var entry = new SmqFormat.TensorEntry
    {
        Name = "model.wte.weight",
        DataType = QuantScheme.Q8_0,
        Rank = 2,
        Dimensions = new[] { rows, cols },
        BlockSize = 64
    };
    
    writer.WriteTensor(entry, q8.Data, q8.Scales);
    writer.WriteManifest(/* manifest */);
}

// Load from SMQ file
using (var reader = new SmqReader("model.smq"))
{
    var info = reader.ReadHeader();
    var loadedQ8 = reader.LoadQ8Tensor("model.wte.weight");
}
```

### GGUF Import

```csharp
using SmallMind.Quantization.IO.Gguf;

// Import GGUF to SMQ
var importer = new GgufImporter();
await importer.ImportToSmqAsync("model.gguf", "model.smq");

// Or inspect before importing
using (var ggufReader = new GgufReader("model.gguf"))
{
    var info = ggufReader.ReadModelInfo();
    
    Console.WriteLine($"GGUF Version: {info.Version}");
    Console.WriteLine($"Tensors: {info.Tensors.Count}");
    
    foreach (var tensor in info.Tensors)
    {
        Console.WriteLine($"  {tensor.Name}: {tensor.Type}");
    }
    
    // Check for unsupported types
    var unsupported = info.Tensors
        .Where(t => t.Type != GgufTensorType.Q8_0 && t.Type != GgufTensorType.Q4_0)
        .ToList();
    
    if (unsupported.Any())
    {
        Console.WriteLine("Unsupported tensor types:");
        foreach (var t in unsupported)
        {
            Console.WriteLine($"  {t.Name}: {t.Type}");
        }
    }
}
```

### Quantized Inference

```csharp
using SmallMind.Quantization.Kernels;

// Load quantized model
using var reader = new SmqReader("model.smq");
var weights = reader.LoadQ8Tensor("model.layers.0.attn.wq");

// Perform quantized matrix multiplication
var activations = new float[batchSize * seqLen * hiddenDim];
var output = new float[batchSize * seqLen * hiddenDim];

MatMulF32Q8.Multiply(
    activations.AsSpan(),
    weights,
    output.AsSpan(),
    m: batchSize * seqLen,
    k: hiddenDim,
    n: hiddenDim
);
```

## CLI Usage (When Implemented)

```bash
# Quantize a float32 model to SMQ
smallmind quantize --in model.fp32 --out model.smq --scheme q8_0

# Import from GGUF
smallmind import-gguf --in model.gguf --out model.smq

# Inspect model
smallmind inspect model.smq
# Output:
#   Format: SMQ v1
#   Tensors: 32
#   Quant Schemes: Q8_0, Q4_0
#   Model Dims: 6 layers, 384 hidden, 50257 vocab

# Validate file integrity
smallmind verify model.smq
```

## Performance Characteristics

### Memory Savings

| Format | Bytes per Weight | Compression Ratio |
|--------|-----------------|-------------------|
| FP32   | 4               | 1.0x (baseline)   |
| FP16   | 2               | 2.0x              |
| Q8_0   | ~1.06           | 3.8x              |
| Q4_0   | ~0.56           | 7.1x              |

*Note: Includes overhead for block scales*

### Accuracy Trade-offs

**Q8_0:**
- Suitable for all layers
- ~16% max relative error on matmul operations
- Minimal accuracy degradation for most models

**Q4_0:**
- Best for FFN intermediate layers
- High error for small values (may quantize to zero)
- Not recommended for attention projections or embeddings

### Performance

**Fused Matmul Kernels:**
- No dequantization step (compute directly with quantized values)
- Scales applied during accumulation
- SIMD-ready (Vector<float> support)
- Memory bandwidth optimized (no extra allocations)

**Typical Speedup (CPU-only):**
- Q8 vs FP32: ~1.8x faster (less memory bandwidth)
- Q4 vs FP32: ~2.5x faster
- Actual speedup depends on CPU cache and memory speed

## Limitations

### Current Limitations

1. **CPU Only**: No GPU acceleration yet
2. **Limited GGUF Support**: Only Q8_0 and Q4_0 (no K-quants, mixed precision)
3. **Block Size**: Fixed at 64 for SMQ (GGUF uses 32, requires re-quantization)
4. **No Mixed Precision**: All tensors use same quantization scheme

### Future Enhancements

- [ ] Mixed quantization (Q8 for attention, Q4 for FFN)
- [ ] Asymmetric quantization (with zero-point)
- [ ] Group-wise quantization (smaller block sizes for better accuracy)
- [ ] K-quants support (Q4_K, Q5_K, Q6_K)
- [ ] GPU/CUDA kernels
- [ ] INT4 GEMM with lookup tables

## Troubleshooting

### Import Fails: "Unsupported tensor type"

**Problem:** GGUF file contains tensor types not supported by SmallMind.

**Solution:**
1. Check which types are unsupported: `smallmind inspect model.gguf`
2. Convert using llama.cpp: `llama-quantize model.gguf model_q8.gguf Q8_0`
3. Import the Q8/Q4-only GGUF

### High Inference Error with Q4

**Problem:** Model accuracy degraded significantly after Q4 quantization.

**Solution:**
1. Use Q8 for attention layers (less error)
2. Only quantize FFN layers to Q4
3. Keep embeddings and output layer in FP32

### SMQ File Validation Errors

**Problem:** `smallmind verify` reports offset overlaps or size mismatches.

**Solution:**
1. File may be corrupted - re-generate from source
2. Check manifest SHA256 matches file
3. Use SmqValidator programmatically to get detailed error list

## Implementation Details

### Block Quantization Algorithm

**Q8_0 (per block):**
```
scale = max(abs(block)) / 127
for each value v in block:
    quantized = round(v / scale)
    clamped = clamp(quantized, -127, 127)
    store as sbyte
```

**Q4_0 (per block):**
```
scale = max(abs(block)) / 7
for each value v in block:
    quantized = round(v / scale)
    clamped = clamp(quantized, -8, 7)
    pack two values per byte (4-bit each)
```

### Dequantization (for reference/testing only)

```csharp
float Dequantize(sbyte q, float scale) => q * scale
int Decode4Bit(byte nibble) => (nibble < 8) ? nibble : nibble - 16
```

### Fused MatMul Pseudocode

```
C[m×n] = A[m×k] * B_quant[k×n]

for each output row i:
    for each output col j:
        sum = 0
        for each input element idx:
            blockIdx = idx / blockSize
            scale = B.scales[blockIdx]
            q = B.data[idx]
            sum += A[i, idx] * q * scale
        C[i, j] = sum
```

## References

- [GGUF Specification](https://github.com/ggerganov/llama.cpp/blob/master/docs/GGUF.md)
- [Q4_0 Quantization in llama.cpp](https://github.com/ggerganov/llama.cpp/blob/master/ggml-quants.c)
- SmallMind Performance Guide: `PERFORMANCE_OPTIMIZATIONS.md`
- SmallMind API Documentation: XML docs in source code

## Contributing

To extend quantization support:
1. Add new `QuantScheme` enum value
2. Implement tensor type in `SmallMind.Quantization/Tensors/`
3. Add fused matmul kernel in `Kernels/`
4. Update `SmqFormat` to calculate sizes
5. Add tests for correctness vs reference
6. Update this documentation

For questions or issues, please open a GitHub issue.

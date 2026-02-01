# GGUF Reader Implementation

This directory contains the GGUF (GPT-Generated Unified Format) reader for SmallMind, enabling import of models in GGUF format.

## Files

- **GgufTypes.cs**: Core type definitions (enums, metadata, tensor info structures)
- **GgufModelInfo.cs**: Model metadata container
- **GgufReader.cs**: Low-level GGUF file parser
- **GgufImporter.cs**: High-level converter from GGUF to SMQ format

## Supported Features

### GGUF Versions
- Version 2 (legacy)
- Version 3 (current, with alignment metadata)

### Value Types
All GGUF metadata types are supported:
- Primitives: `UInt8`, `Int8`, `UInt16`, `Int16`, `UInt32`, `Int32`, `UInt64`, `Int64`
- Floating point: `Float32`, `Float64`
- Other: `Bool`, `String`, `Array`

### Tensor Types (Import Support)
Currently supported for import to SMQ:
- **Q8_0**: 8-bit symmetric quantization (block size 32)
- **Q4_0**: 4-bit symmetric quantization (block size 32)

Recognized but unsupported (will report clear error):
- F32, F16
- Q4_1, Q5_0, Q5_1, Q8_1
- Q2_K, Q3_K, Q4_K, Q5_K, Q6_K, Q8_K
- IQ2_XXS, IQ2_XS, IQ3_XXS, IQ1_S, IQ4_NL, IQ3_S, IQ2_S, IQ4_XS

## Usage

### Basic Import

```csharp
using SmallMind.Quantization.IO.Gguf;

var importer = new GgufImporter();
importer.ImportToSmq("model.gguf", "model.smq");
```

### Low-Level Reading

```csharp
using var stream = File.OpenRead("model.gguf");
using var reader = new GgufReader(stream);

// Read model metadata and tensor manifests
var modelInfo = reader.ReadModelInfo();

Console.WriteLine($"GGUF Version: {modelInfo.Version}");
Console.WriteLine($"Tensors: {modelInfo.Tensors.Count}");
Console.WriteLine($"Metadata keys: {modelInfo.Metadata.Count}");

// Access specific metadata
if (modelInfo.Metadata.TryGetValue("llama.context_length", out var ctxLen))
{
    Console.WriteLine($"Context length: {ctxLen}");
}

// Iterate tensors
foreach (var tensor in modelInfo.Tensors)
{
    Console.WriteLine($"{tensor.Name}: {tensor.Type}, dims={string.Join("x", tensor.Dimensions)}");
    
    // Read tensor data if needed
    byte[] data = reader.ReadTensorData(tensor.Offset, tensor.Size);
}
```

## Implementation Details

### GGUF Format Specifics

1. **Magic Header**: 4 bytes ASCII "GGUF"
2. **Version**: uint32 little-endian (2 or 3)
3. **Counts**: uint64 tensor count, uint64 metadata KV count
4. **Metadata**: Array of key-value pairs
5. **Tensor Infos**: Array of tensor metadata (name, dims, type, offset)
6. **Alignment**: Data section starts at aligned boundary (typically 32 bytes)
7. **Tensor Data**: Binary tensor data blobs

### String Encoding
GGUF uses a specific string format:
- uint64 length prefix (little-endian)
- UTF-8 encoded bytes (NOT null-terminated)

### Block Size Conversion

GGUF uses fixed block size 32 for quantized types, while SmallMind's SMQ format defaults to block size 64.

The importer automatically re-quantizes tensors:
1. **Dequantize** from GGUF blocks (block size 32, fp16 scales)
2. **Re-quantize** to SMQ blocks (block size 64, fp32 scales)

This ensures compatibility while preserving model quality.

### Half-Precision (fp16) Conversion

GGUF stores quantization scales as IEEE 754 half-precision floats. The importer converts these to single-precision (fp32) used by SMQ:

```csharp
// Bit-level conversion from fp16 to fp32
float scale = HalfToFloat(scaleHalf);
```

## Error Handling

### Unsupported Tensor Types

If a GGUF file contains unsupported tensor types, the importer will:
1. Scan **all** tensors first
2. Collect **all** unsupported types
3. Throw a single exception listing them all

Example error message:
```
The following tensors have unsupported types:
  - model.layers.0.attn.wq: Q4_K
  - model.layers.1.attn.wq: Q4_K
  - model.output: F32

Supported types: Q8_0, Q4_0
```

This avoids multiple failed import attempts.

### Invalid Files

The reader validates:
- Magic header must be "GGUF"
- Version must be 2 or 3
- File must be seekable and readable

## Testing

Comprehensive unit tests cover:
- Header parsing
- String reading (with length prefix)
- Metadata KV parsing (all value types)
- Tensor info parsing
- Version 2 and 3 support
- Alignment handling
- Error conditions (invalid magic, unsupported version)

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~GgufReaderTests"
```

## Future Extensions

To support additional quantization schemes:
1. Add tensor type to `GgufTensorType` enum
2. Implement size calculation in `GgufReader.CalculateTensorSize()`
3. Add conversion logic in `GgufImporter.ConvertTensor()`
4. Update `GgufImporter.IsSupportedType()` to include new type

## References

- [GGUF Specification](https://github.com/ggerganov/ggml/blob/master/docs/gguf.md)
- [llama.cpp GGUF Implementation](https://github.com/ggerganov/llama.cpp)

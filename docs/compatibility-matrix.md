# Compatibility Matrix

## Overview

This document defines the model formats, quantization schemes, tokenizers, and runtime limits supported by SmallMind's stable inference runtime.

**Legend:**
- ✅ **Stable Runtime Path**: Fully supported, tested, and part of stable API guarantees
- ⚠️ **Experimental**: Best-effort support, may change or be removed
- ❌ **Not Supported**: Not implemented or explicitly unsupported

---

## Model Formats

| Format | Extension | Status | Notes |
|--------|-----------|--------|-------|
| SmallMind Quantized | `.smq` | ✅ Stable | Native format, recommended for production |
| GGUF | `.gguf` | ⚠️ Experimental | Import via `AllowGgufImport=true`, converted to SMQ |
| JSON Checkpoint | `.json` | ⚠️ Experimental | FP32 checkpoint format, primarily for development |
| ONNX | `.onnx` | ❌ Not Supported | Not implemented |
| PyTorch | `.pt`, `.pth` | ❌ Not Supported | Not implemented |
| SafeTensors | `.safetensors` | ❌ Not Supported | Not implemented |

### Model Format Details

#### `.smq` (SmallMind Quantized)
- **Primary supported format**
- Optimized for inference
- Supports Q4 and Q8 quantization
- Includes model metadata and configuration
- Fast loading with validation

**Example:**
```csharp
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq"
});
```

#### `.gguf` (GGUF Import)
- **Experimental**: Requires `AllowGgufImport = true`
- Imports GGUF files and converts to SMQ format
- Not all GGUF tensor types are supported (see below)

**Example:**
```csharp
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.gguf",
    AllowGgufImport = true  // Required
});
```

**Supported GGUF Tensor Types:**
- F32, F16
- Q4_0, Q4_1
- Q5_0, Q5_1
- Q8_0
- Q4_K, Q5_K, Q6_K, Q8_K (K-quants)

**Unsupported GGUF Tensor Types:**
- Q2_K, Q3_K (rarely used)
- Q8_1 (rarely used)
- IQ variants (experimental)
- Throws `UnsupportedGgufTensorException` if encountered

---

## Quantization Schemes

| Scheme | Bits per Weight | Status | Typical Size Reduction | Notes |
|--------|-----------------|--------|------------------------|-------|
| FP32 | 32 | ✅ Stable | Baseline (1x) | Full precision, largest file size |
| Q8 | 8 | ✅ Stable | 4x smaller | ~1% accuracy loss, recommended |
| Q4 | 4 | ✅ Stable | 8x smaller | ~2-3% accuracy loss, good for large models |
| Q5, Q6 | 5-6 | ❌ Not Supported | - | Not implemented |
| Mixed Precision | Variable | ⚠️ Experimental | Varies | Research feature |

### Quantization Details

**Q8 (8-bit quantization):**
- Best balance of size vs accuracy
- Minimal quality degradation
- Recommended for production use

**Q4 (4-bit quantization):**
- Maximum compression
- Suitable for larger models where size is critical
- May require calibration for sensitive use cases

**Example:**
```csharp
// Loading a quantized model (Q8 or Q4)
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model-q8.smq"  // or model-q4.smq
});
```

---

## Tokenizers

| Tokenizer | Status | Notes |
|-----------|--------|-------|
| Byte-Pair Encoding (BPE) | ✅ Stable | Standard GPT-2 style tokenization |
| Character-Level | ✅ Stable | Simple character tokenization |
| WordPiece | ⚠️ Experimental | BERT-style tokenization |
| Byte-Level BPE | ⚠️ Experimental | GPT-3 style tokenization |
| SentencePiece | ❌ Not Supported | Not implemented |
| Tiktoken | ❌ Not Supported | Not implemented |

### Tokenizer Details

**BPE (Byte-Pair Encoding):**
- Default tokenizer for most models
- Vocabulary size: typically 50,000 - 100,000 tokens
- Handles unknown characters gracefully

**Character-Level:**
- Simple, no vocabulary required
- Suitable for small models and educational purposes
- Limited context window efficiency

**Example:**
```csharp
// Tokenizer is detected automatically from model metadata
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq"
});

// Tokenizer type available via ModelInfo
Console.WriteLine($"Tokenizer: {model.ModelInfo.TokenizerType}");
```

---

## Runtime Limits

| Limit | Default | Configurable | Notes |
|-------|---------|--------------|-------|
| Max Context Tokens | Model-dependent | ✅ Yes | Set via `MaxContextTokens` in `GenerationOptions` |
| Max New Tokens | 100 | ✅ Yes | Set via `MaxNewTokens` in `GenerationOptions` |
| Max Memory | Unlimited | ✅ Yes | Best-effort limit via `MaxMemoryBytes` in `ModelLoadRequest` |
| Max Timeout | None | ✅ Yes | Set via `TimeoutMs` in `GenerationOptions` |
| Max KV Cache Tokens | 4096 | ✅ Yes | Set via `MaxKvCacheTokens` in `SessionOptions` |
| Max Concurrent Requests | Unlimited | ⚠️ Best-effort | Limited by system resources |

### Limit Details

#### Context Length
- **Model-dependent**: Defined by model architecture (e.g., 2048, 4096, 8192 tokens)
- **Runtime enforcement**: Throws `ContextLimitExceededException` if exceeded
- **Recommendation**: Stay within 80% of max context to allow headroom

**Example:**
```csharp
var options = new GenerationOptions
{
    MaxContextTokens = 2048,  // Enforce limit
    MaxNewTokens = 256
};
```

#### Token Budget
- **Default**: 100 new tokens
- **Runtime enforcement**: Generation stops when budget reached
- **Throws**: `BudgetExceededException` if exceeded

#### Memory Budget
- **Best-effort**: Approximate limit on model memory usage
- **Not exact**: Includes model weights, KV cache, and runtime allocations
- **Recommendation**: Set conservatively, allow 20% headroom

**Example:**
```csharp
var request = new ModelLoadRequest
{
    Path = "model.smq",
    MaxMemoryBytes = 4L * 1024 * 1024 * 1024  // 4 GB
};
```

#### Timeout
- **Optional**: Maximum generation time in milliseconds
- **Runtime enforcement**: Throws `BudgetExceededException` if exceeded
- **Use case**: Prevent runaway generation in production

**Example:**
```csharp
var options = new GenerationOptions
{
    TimeoutMs = 30000  // 30 seconds max
};
```

---

## Thread Safety and Concurrency

| Component | Thread Safety | Concurrent Usage | Notes |
|-----------|---------------|------------------|-------|
| `ISmallMindEngine` | ✅ Thread-safe | Multiple operations allowed | No shared state between operations |
| `IModelHandle` | ✅ Read-only weights | Concurrent generation allowed | Isolated state per generation |
| `IChatSession` | ❌ Single-threaded | One operation at a time | Create separate sessions for concurrency |
| `IRagEngine` | ✅ Thread-safe | Concurrent queries allowed | Index operations may block |
| `IRagIndex` | ⚠️ Read-mostly | Concurrent reads, exclusive writes | Build index once, query many times |

### Concurrency Recommendations

**Single-threaded application:**
```csharp
using var engine = SmallMind.Create();
using var model = await engine.LoadModelAsync(request);

// Sequential generations
var result1 = await engine.GenerateAsync(model, request1);
var result2 = await engine.GenerateAsync(model, request2);
```

**Multi-threaded server:**
```csharp
using var engine = SmallMind.Create();
using var model = await engine.LoadModelAsync(request);

// Concurrent generations (thread-safe)
var task1 = engine.GenerateAsync(model, request1);
var task2 = engine.GenerateAsync(model, request2);
await Task.WhenAll(task1, task2);
```

**Chat sessions (one per conversation):**
```csharp
// Create separate sessions for concurrent conversations
var session1 = engine.CreateChatSession(model, options);
var session2 = engine.CreateChatSession(model, options);

// Use each session from single thread only
await session1.SendAsync("Hello");  // Thread 1
await session2.SendAsync("Hi");     // Thread 2
```

---

## Hardware Acceleration

| Feature | x64 | ARM64 | Notes |
|---------|-----|-------|-------|
| SSE2 | ✅ Required | N/A | Baseline SIMD support |
| AVX2 | ✅ Recommended | N/A | 2x performance improvement |
| NEON | N/A | ✅ Used when available | ARM SIMD support |
| GPU/CUDA | ❌ Not Supported | ❌ Not Supported | CPU-only runtime |

### Performance Notes

- **AVX2** provides significant speedup on x64 systems (Intel Haswell+, AMD Zen+)
- **NEON** is automatically used on ARM64 (Apple M1/M2, AWS Graviton, etc.)
- **No GPU support**: SmallMind is CPU-focused for portability and simplicity

---

## Platform-Specific Behaviors

### Floating-Point Arithmetic
- **x64 vs ARM64**: Minor variations in floating-point results due to CPU architecture
- **Determinism impact**: Use `Threads = 1` for strict cross-platform determinism
- **Recommendation**: Test on target platform for production deployments

### Memory Layout
- **Little-endian**: All model formats assume little-endian byte order
- **Big-endian**: Not supported or tested

### File System
- **Case-sensitive**: Model paths are case-sensitive on Linux/macOS
- **Case-insensitive**: Windows file system is case-insensitive but preserves case
- **Recommendation**: Use consistent casing in paths for portability

---

## Versioning and Compatibility

### Model Format Versioning
SmallMind models include a format version in their metadata:

```
Current version: 1.0
```

**Compatibility guarantee:**
- Models created with SmallMind v1.x can be loaded by any SmallMind v1.y (y >= x)
- Models created with SmallMind v2.x may not load in v1.y (major version bump indicates breaking format change)

### API Versioning
See [stability-and-compatibility.md](stability-and-compatibility.md) for API versioning policy.

---

## Testing and Validation

### Recommended Testing Matrix

For production deployments, test on:
- **Primary platform** (e.g., Linux x64)
- **Primary .NET version** (e.g., .NET 10.0)
- **Representative models** (your production models)

**Optional testing:**
- Secondary platforms (Windows, macOS, ARM64)
- Multiple .NET versions (9.0, 10.0)
- Edge cases (max context, large batches, long sessions)

### Validation Checklist
- [ ] Model loads successfully
- [ ] Generation produces expected outputs (deterministic mode)
- [ ] Streaming works with cancellation
- [ ] Budget limits are enforced
- [ ] Memory usage is within expectations
- [ ] Performance meets requirements

---

## Known Limitations

### Current Limitations
- **CPU-only**: No GPU acceleration
- **Single-node**: No distributed serving
- **Limited model formats**: .smq and .gguf only
- **No fine-tuning runtime**: Training is experimental

### Future Enhancements (Not Committed)
These may be added in future versions but are NOT guaranteed:
- Additional model formats (ONNX, SafeTensors)
- GPU acceleration (CUDA, Metal, Vulkan)
- Distributed serving capabilities
- Additional quantization schemes (Q5, Q6)

---

## Support and Updates

**Documentation Updates:**
This compatibility matrix is updated with each release. Check the version tag for the applicable SmallMind version.

**Reporting Issues:**
If you encounter compatibility issues, please report them at:
https://github.com/justinamiller/SmallMind/issues

Include:
- SmallMind version
- .NET runtime version
- Operating system and architecture
- Model format and size
- Reproducible example

---

## Summary Table

| Category | Stable Path | Experimental | Not Supported |
|----------|-------------|--------------|---------------|
| **Formats** | .smq | .gguf, .json | .onnx, .pt, .safetensors |
| **Quantization** | Q8, Q4, FP32 | Mixed precision | Q5, Q6, IQ |
| **Tokenizers** | BPE, Character | WordPiece, Byte-Level BPE | SentencePiece, Tiktoken |
| **Hardware** | CPU (x64, ARM64) | - | GPU |
| **Use Cases** | Inference, Streaming, RAG | Training | Distributed serving |

**Recommendation:** Stick to the "Stable Path" column for production deployments.

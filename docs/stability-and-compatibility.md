# Stability and Compatibility

## Primary Use Case

**SmallMind is a production-ready, local, CPU-first LLM inference runtime for .NET**

SmallMind is designed and optimized for:
- Loading models for local inference
- Tokenization and text generation
- Streaming generation with cancellation support
- Multi-turn conversations with KV cache optimization
- Resource governance (token/time/context/memory budgets)
- Deterministic generation for testing and reproducibility

This is the **stable, supported** path. All other use cases are considered experimental or best-effort.

---

## What is Stable

The following components are part of the **stable public API** and follow semantic versioning:

### Stable Namespaces
- `SmallMind.Abstractions` - All public types (interfaces, DTOs, exceptions, enums)
- `SmallMind.Engine.SmallMind` - Static factory class for creating engines

### Stable Interfaces
- `ISmallMindEngine` - Main engine with model loading, generation, streaming
- `IModelHandle` - Handle to loaded model with metadata
- `IChatSession` - Multi-turn conversation with state management
- `IRagEngine` - Retrieval-Augmented Generation pipeline
- `IRagIndex` - Document index for RAG
- `EngineCapabilities` - Runtime capability discovery

### Stable DTOs and Options
- Request types: `ModelLoadRequest`, `GenerationRequest`, `RagBuildRequest`, etc.
- Result types: `GenerationResult`, `RagAnswer`, `TokenEvent`, etc.
- Options types: `SmallMindOptions`, `GenerationOptions`, `SessionOptions`, etc.
- Enums: `GenerationMode`, `ChatRole`, `TokenEventKind`, etc.

### Stable Exceptions
All exception types in `SmallMind.Abstractions`:
- `SmallMindException` (base)
- `UnsupportedModelException`
- `UnsupportedGgufTensorException`
- `ContextLimitExceededException`
- `BudgetExceededException`
- `RagInsufficientEvidenceException`
- `SecurityViolationException`

**Exception Guarantees:**
- All exceptions include actionable error messages
- Exceptions include key values (requested vs max, extension, etc.)
- Exceptions include remediation guidance where applicable

---

## What is Internal/Unstable

Everything else is **internal implementation** and may change without notice:

### Internal Namespaces
- `SmallMind.Core.*` - Tensor operations, SIMD, RNG, validation
- `SmallMind.Runtime.*` - Model loading, batching, KV cache, quantization
- `SmallMind.Transformers.*` - Transformer architecture and layers
- `SmallMind.Tokenizers.*` - Tokenization implementations
- `SmallMind.Quantization.*` - Quantization formats and utilities
- `SmallMind.Experimental.*` - Training, research, and best-effort features

### Internal Implementation Details
- Tensor representations and layer implementations
- Quantization algorithms and file formats (internal structure)
- Caching strategies and batching details
- Internal optimizations (SIMD, memory pooling, etc.)
- Training utilities and experimental features

**Do not depend on internal types.** They are subject to change, refactoring, and removal.

---

## Experimental / Best-Effort Areas

The following are **NOT part of the stable runtime guarantee**:

### Training and Fine-Tuning (Experimental)
- Training large models from scratch
- Fine-tuning and transfer learning
- Custom training loops and optimizers
- Gradient checkpointing and mixed precision

**Status:** Experimental. Available via `SmallMind.Experimental.*` namespace. API may change.

### Research Utilities (Experimental)
- Advanced sampling strategies
- Custom tokenization schemes
- Experimental model architectures

**Status:** Best-effort. May be removed or redesigned.

### GPU Acceleration (Experimental)
- GPU/CUDA support (if present)
- Hardware-specific optimizations

**Status:** Experimental. CPU-first is the primary path.

### Distributed Serving (Not Implemented)
- Multi-node serving
- Distributed model sharding

**Status:** Not implemented. Not part of current scope.

---

## Versioning Policy

SmallMind follows **Semantic Versioning 2.0.0** for the stable public API:

### Version Format: MAJOR.MINOR.PATCH

- **MAJOR**: Breaking changes to stable public API
  - Removing public methods/properties
  - Changing method signatures
  - Removing or renaming public types
  - Changing exception behavior in breaking ways

- **MINOR**: Backwards-compatible additions
  - New optional parameters (with defaults)
  - New methods on interfaces (with default implementations)
  - New public types in stable namespaces
  - New enum values (with backwards-compatible defaults)

- **PATCH**: Backwards-compatible bug fixes
  - Fixing incorrect behavior
  - Performance improvements
  - Documentation updates
  - Internal refactoring (no API changes)

### Examples

- **v1.0.0 → v1.1.0**: Added `MaxMemoryBytes` property to `ModelLoadRequest` (optional, defaults to unlimited)
- **v1.1.0 → v1.1.1**: Fixed KV cache corruption bug (no API change)
- **v1.1.1 → v2.0.0**: Removed deprecated `ILegacyEngine` interface

### Internal Changes
Changes to internal implementation (e.g., optimizing matrix multiplication) do **NOT** trigger version bumps unless they affect public behavior or introduce breaking changes.

---

## Platform Support

### .NET Target Framework
- **Primary Target**: .NET 8.0+
- **Tested Frameworks**: .NET 8.0, .NET 9.0, .NET 10.0

### Operating Systems (Best-Effort)
- **Windows**: x64, ARM64
- **Linux**: x64, ARM64 (tested on Ubuntu, Alpine)
- **macOS**: x64, ARM64 (Apple Silicon)

**Note:** OS/architecture support is best-effort. We test on common platforms but cannot guarantee compatibility with all configurations.

### CPU Requirements
- **SSE2**: Required (x64)
- **AVX2**: Recommended for optimal performance (x64)
- **NEON**: Used when available (ARM64)

---

## Determinism

### When is Determinism Guaranteed?

If `EngineCapabilities.SupportsDeterministicMode == true`:

**Same inputs produce identical outputs** when:
- Same model (file hash)
- Same prompt (exact string)
- Same `GenerationOptions.Seed`
- `GenerationOptions.Mode = GenerationMode.Deterministic`
- Single-threaded execution

**Example:**
```csharp
var options = new GenerationOptions
{
    Mode = GenerationMode.Deterministic,
    Seed = 42,
    MaxNewTokens = 100
};

var result1 = await engine.GenerateAsync(model, new GenerationRequest { Prompt = "Hello", Options = options });
var result2 = await engine.GenerateAsync(model, new GenerationRequest { Prompt = "Hello", Options = options });

// result1.Text == result2.Text (guaranteed)
```

### What Can Affect Determinism?

Even in deterministic mode, the following may cause variations:
- **Platform differences**: Floating-point arithmetic varies across CPUs (x64 vs ARM64)
- **Multi-threading**: Parallel execution order can affect results (use `Threads = 1` for strict determinism)
- **Model updates**: Different model versions may produce different outputs
- **Quantization**: Quantized models may have slight variations vs FP32

### Use Cases for Deterministic Mode
- Automated testing and regression detection
- Debugging generation issues
- Reproducible benchmarks
- Compliance and auditability

---

## Concurrency and Thread Safety

### Engine (`ISmallMindEngine`)
- **Thread-safe**: Multiple threads can call `LoadModelAsync`, `GenerateAsync`, etc. concurrently
- **No shared state**: Each operation is independent
- **Dispose**: Call `Dispose()` on main thread after all operations complete

### Model Handle (`IModelHandle`)
- **Thread-safe for reads**: Model weights are read-only and shared safely
- **Concurrent generation**: Multiple threads/tasks can generate from the same model simultaneously
- **Each generation has isolated state**: No interference between concurrent requests
- **Dispose**: Safe to dispose after all generations complete

### Chat Session (`IChatSession`)
- **NOT thread-safe**: Each session is designed for single-threaded use
- **Recommendation**: Create separate sessions for concurrent conversations
- **State isolation**: Each session has its own conversation history and KV cache

### Thread Configuration
Control parallelism via `ModelLoadRequest.Threads`:

```csharp
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq",
    Threads = Environment.ProcessorCount  // Use all cores
});
```

**Recommendations:**
- **CPU-only systems**: Set `Threads = Environment.ProcessorCount` for best throughput
- **Shared servers**: Limit threads to prevent resource contention (e.g., `Threads = 4`)
- **Strict determinism**: Use `Threads = 1` to eliminate parallelism effects

---

## Memory Management

### Model Memory
Models are loaded into memory on `LoadModelAsync` and held until disposed:

```csharp
using var model = await engine.LoadModelAsync(request);
// Model weights in memory
// Dispose() releases memory
```

**Memory Budgets:**
```csharp
var request = new ModelLoadRequest
{
    Path = "model.smq",
    MaxMemoryBytes = 4L * 1024 * 1024 * 1024  // 4 GB limit (best-effort)
};
```

**Note:** `MaxMemoryBytes` is a **best-effort** limit. Exact memory usage depends on model architecture, quantization, and runtime allocations.

### KV Cache Memory
Chat sessions accumulate KV cache over time:

```csharp
var options = new SessionOptions
{
    EnableKvCache = true,
    MaxKvCacheTokens = 4096  // Limit cache growth
};

using var chat = engine.CreateChatSession(model, options);
```

**Recommendations:**
- Call `chat.Reset()` periodically to clear cache for long conversations
- Monitor memory usage in production deployments
- Set `MaxKvCacheTokens` based on available memory

---

## Deprecation Policy

When features or APIs are deprecated:

1. **Mark with `[Obsolete]` attribute**: Compiler warnings guide migration
2. **Document in CHANGELOG**: Clear migration path provided
3. **Maintain for at least one MINOR version**: Grace period for adoption
4. **Remove in next MAJOR version**: Clean break with advance notice

**Example:**
```csharp
// v1.5.0 - Deprecation warning
[Obsolete("Use GenerateAsync with GenerationOptions instead. Will be removed in v2.0.0")]
public Task<string> Generate(string prompt);

// v2.0.0 - Removed
```

---

## Best Practices

### 1. Always Use Stable APIs
✅ **Recommended:**
```csharp
using var engine = SmallMind.Create();
using var model = await engine.LoadModelAsync(new ModelLoadRequest { Path = "model.smq" });
```

❌ **Avoid:**
```csharp
using SmallMind.Runtime.Core;  // Internal namespace!
var model = new TransformerModel(...);  // Internal API!
```

### 2. Check Capabilities Before Using Features
```csharp
if (engine.Capabilities.SupportsRag && engine.Rag != null)
{
    // Safe to use RAG
}

if (engine.Capabilities.SupportsDeterministicMode)
{
    // Deterministic mode available
}
```

### 3. Handle Exceptions with Remediation
```csharp
try
{
    var result = await engine.GenerateAsync(model, request);
}
catch (ContextLimitExceededException ex)
{
    Console.WriteLine($"Context exceeded: {ex.RequestedSize} > {ex.MaxAllowed}");
    Console.WriteLine($"Remediation: {ex.Message}");
}
catch (BudgetExceededException ex)
{
    Console.WriteLine($"Budget exceeded: {ex.Message}");
}
```

### 4. Use `using` for Disposable Resources
```csharp
using var engine = SmallMind.Create();
using var model = await engine.LoadModelAsync(request);
using var chat = engine.CreateChatSession(model, options);
```

---

## Support and Feedback

SmallMind is **open source software** with no paid or guaranteed support.

**Community Support:**
- **Issues**: https://github.com/justinamiller/SmallMind/issues
- **Discussions**: https://github.com/justinamiller/SmallMind/discussions
- **Documentation**: `docs/` directory

**Maintainer Intent:**
- We aim to maintain stability and backwards compatibility for the public API
- We respond to issues on a best-effort basis
- We welcome contributions that align with project goals

**This is NOT a commercial support contract.** Use at your own risk. Always test thoroughly in your environment.

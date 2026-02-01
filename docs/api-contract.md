# SmallMind API Contract

## Overview

This document defines the **stable public API** for SmallMind, a local C# language model inference engine. The API is designed to provide a clean, predictable surface for commercial and production use.

## Stability Guarantees

### What is Stable (Public API)

The following are part of the **stable public API** and follow semantic versioning:

**Namespaces:**
- `SmallMind.Abstractions` - All public types in this namespace
- `SmallMind.Engine.SmallMind` - The static factory class

**Key Interfaces:**
- `ISmallMindEngine` - Main engine interface
- `IModelHandle` - Handle to a loaded model
- `IChatSession` - Chat session with conversation state
- `IRagEngine` - RAG (Retrieval-Augmented Generation) capabilities
- `IRagIndex` - RAG document index

**DTOs and Enums:**
- All request types: `ModelLoadRequest`, `GenerationRequest`, `RagBuildRequest`, etc.
- All result types: `GenerationResult`, `RagAnswer`, `TokenEvent`, etc.
- All options types: `SmallMindOptions`, `GenerationOptions`, `SessionOptions`, etc.
- All enums: `GenerationMode`, `ChatRole`, `TokenEventKind`

**Exceptions:**
- `SmallMindException` and all derived types
- See [Operational Notes](operational-notes.md) for exception taxonomy

### What is Internal (Implementation Details)

Everything else is considered **internal** and may change without notice:

- All classes in `SmallMind.Core`, `SmallMind.Runtime`, `SmallMind.Transformers`, etc.
- All internal types in `SmallMind.Engine` namespace
- Tensor representations, layer implementations, quantization details
- Caching strategies, batching implementation details
- Internal optimizations and performance tuning

**Do not depend on internal types.** They are subject to change and refactoring.

---

## Versioning Policy

SmallMind follows **Semantic Versioning 2.0.0** for the public API:

- **MAJOR** version: Breaking changes to public API
- **MINOR** version: New features added in backwards-compatible manner
- **PATCH** version: Backwards-compatible bug fixes

**Examples:**
- Adding a new optional parameter to an existing DTO: **MINOR**
- Adding a new method to `ISmallMindEngine`: **MINOR**
- Changing method signature or removing a public type: **MAJOR**
- Fixing a bug in generation logic: **PATCH**

**Internal implementation changes** (e.g., optimizing matrix multiplication) do NOT trigger version bumps unless they affect public behavior.

---

## Capability Discovery

The engine exposes its capabilities via `ISmallMindEngine.Capabilities`:

```csharp
var capabilities = engine.Capabilities;

if (capabilities.SupportsStreaming)
{
    // Use streaming API
}

if (capabilities.SupportsRag)
{
    // RAG is available
    var ragEngine = engine.Rag;
}
```

**Available capability flags:**
- `SupportsQuantizedInference` - Can load quantized models (SMQ format)
- `SupportsGgufImport` - Can import GGUF models
- `SupportsRag` - RAG engine is available
- `SupportsKvCache` - KV cache for efficient multi-turn conversations
- `SupportsBatching` - Batch processing for concurrent requests
- `SupportsDeterministicMode` - Reproducible generation with fixed seeds
- `SupportsStreaming` - Token-by-token streaming output

Always check capabilities before using optional features.

---

## Generation Modes

SmallMind supports two generation modes:

### Deterministic Mode

Fixed seed + same prompt = **identical output** every time.

```csharp
var options = new GenerationOptions
{
    Mode = GenerationMode.Deterministic,
    Seed = 42,
    MaxNewTokens = 100
};

var result1 = await engine.GenerateAsync(model, new GenerationRequest
{
    Prompt = "Once upon a time",
    Options = options
});

var result2 = await engine.GenerateAsync(model, new GenerationRequest
{
    Prompt = "Once upon a time",
    Options = options
});

// result1.Text == result2.Text (guaranteed)
```

**Use cases:**
- Automated testing and validation
- Debugging generation issues
- Reproducible benchmarks
- Compliance requirements

### Exploratory Mode

Randomized sampling for creative, varied outputs.

```csharp
var options = new GenerationOptions
{
    Mode = GenerationMode.Exploratory,
    Temperature = 0.8,
    TopK = 40,
    TopP = 0.95
};
```

**Use cases:**
- Production inference
- Creative writing
- Diverse completions

---

## Threading and Concurrency

### Model Loading
- **Thread-safe**: Multiple threads can call `LoadModelAsync` concurrently
- Each loaded model is independent (separate `IModelHandle`)

### Generation
- **Concurrent sessions**: Multiple `IChatSession` instances can run in parallel
- **Model weights are read-only**: Shared safely across all sessions
- Each session has isolated state (conversation history, KV cache)

### Thread Configuration

Control thread count via `ModelLoadRequest.Threads`:

```csharp
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq",
    Threads = Environment.ProcessorCount // Auto-detect cores
});
```

**Recommendations:**
- CPU-only: Set `Threads = Environment.ProcessorCount`
- Shared server: Limit threads to prevent resource starvation

---

## Memory Management

### Model Memory

Models are loaded into memory and held until disposed:

```csharp
using var model = await engine.LoadModelAsync(request);
// Model weights in memory
// ... use model ...
// Dispose releases memory
```

**Memory budgets:**

```csharp
var request = new ModelLoadRequest
{
    Path = "model.smq",
    MaxMemoryBytes = 4L * 1024 * 1024 * 1024 // 4 GB limit
};
```

### KV Cache Memory

Chat sessions accumulate KV cache over time:

```csharp
var options = new SessionOptions
{
    EnableKvCache = true,
    MaxKvCacheTokens = 4096 // Limit cache size
};

using var chat = engine.CreateChatSession(model, options);
```

**Recommendation:** Call `chat.Reset()` periodically to clear cache.

---

## Error Handling

All public API methods throw **typed exceptions** for operational failures:

```csharp
try
{
    var model = await engine.LoadModelAsync(request);
}
catch (UnsupportedModelException ex)
{
    Console.WriteLine($"Model format not supported: {ex.Extension}");
    Console.WriteLine($"Supported: .smq, .gguf (with AllowGgufImport)");
}
catch (SmallMindException ex)
{
    Console.WriteLine($"Error: {ex.ErrorCode} - {ex.Message}");
}
```

See [Operational Notes](operational-notes.md) for complete exception taxonomy.

---

## API Evolution

### Adding New Features

New features are introduced via:
1. New optional parameters (defaults preserve old behavior)
2. New methods/interfaces (existing code unaffected)
3. New enum values (switch statements should have `default`)

**Example:**

```csharp
// v1.0
public sealed class GenerationOptions
{
    public int MaxNewTokens { get; set; } = 100;
}

// v1.1 - adds new optional property (backwards compatible)
public sealed class GenerationOptions
{
    public int MaxNewTokens { get; set; } = 100;
    public string[]? Stop { get; set; } // NEW, optional
}
```

### Deprecation Policy

Deprecated APIs:
1. Marked with `[Obsolete]` attribute
2. Removed in next **MAJOR** version
3. Documented in CHANGELOG

---

## Best Practices

### 1. Always use `using` for disposable resources

```csharp
using var engine = SmallMind.Create();
using var model = await engine.LoadModelAsync(request);
using var chat = engine.CreateChatSession(model, options);
```

### 2. Check capabilities before using features

```csharp
if (engine.Capabilities.SupportsRag && engine.Rag != null)
{
    // Safe to use RAG
}
```

### 3. Handle exceptions appropriately

Catch specific exceptions and provide remediation:

```csharp
catch (ContextLimitExceededException ex)
{
    Console.WriteLine($"Context too long: {ex.RequestedSize} > {ex.MaxAllowed}");
    Console.WriteLine("Solution: Reduce input or increase MaxContextTokens");
}
```

### 4. Use deterministic mode for testing

```csharp
[Fact]
public async Task Generation_IsDeterministic()
{
    var options = new GenerationOptions
    {
        Mode = GenerationMode.Deterministic,
        Seed = 42
    };

    var result1 = await engine.GenerateAsync(model, request);
    var result2 = await engine.GenerateAsync(model, request);

    Assert.Equal(result1.Text, result2.Text);
}
```

---

## Migration Guide

### From Internal APIs

If you were using internal APIs (e.g., `SmallMind.Runtime` directly), migrate to the stable facade:

**Before:**
```csharp
var model = new TransformerModel(...);
var tokenizer = new CharTokenizer(...);
var session = new InferenceSession(model, tokenizer, ...);
var text = await session.GenerateAsync(prompt, ...);
```

**After:**
```csharp
using var engine = SmallMind.Create();
using var model = await engine.LoadModelAsync(new ModelLoadRequest { Path = "model.smq" });
var result = await engine.GenerateAsync(model, new GenerationRequest { Prompt = prompt });
var text = result.Text;
```

---

## Support and Feedback

- **Issues**: https://github.com/justinamiller/SmallMind/issues
- **Discussions**: https://github.com/justinamiller/SmallMind/discussions
- **Documentation**: `docs/` directory in the repository

For API questions or feature requests, please open a GitHub issue with the `api` label.

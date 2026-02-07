# SmallMind NuGet Packages Guide

## Quick Start: Which Package Should I Use?

SmallMind offers multiple NuGet packages to support different use cases. Here's how to choose:

### 🌟 For Production Applications

**Use: `SmallMind.Public` (v1.0.0)**

```bash
dotnet add package SmallMind.Public
```

This is the **recommended entry point** for all production applications. It provides:
- ✅ Stable API with semantic versioning guarantees
- ✅ Text generation with streaming support
- ✅ Resource governance (budgets, timeouts)
- ✅ Model loading (.smq, .gguf formats)
- ✅ Diagnostics and observability hooks
- ✅ Thread-safe engine with session management

**Example:**
```csharp
using SmallMind.Public;

var options = new SmallMindOptions 
{ 
    ModelPath = "model.smq",
    EnableKvCache = true
};

using var engine = SmallMindFactory.Create(options);
using var session = engine.CreateTextGenerationSession(new TextGenerationOptions
{
    Temperature = 0.8f,
    MaxOutputTokens = 100
});

var result = await session.GenerateAsync(new TextGenerationRequest 
{ 
    Prompt = "Once upon a time" 
});
Console.WriteLine(result.Text);
```

**Stability Guarantee**: Your code will not break between minor versions (e.g., 1.0 → 1.1). See [API Stability Policy](API_STABILITY.md).

---

## 🚀 Specialized Features

### Model Quantization (Memory Optimization)

**Use: `SmallMind.Quantization` (v0.3.0)**

```bash
dotnet add package SmallMind.Quantization
```

**When to use:**
- Running large models on limited memory
- Production deployments with memory constraints
- Converting GGUF models to SmallMind format

**Benefits:**
- 87% memory reduction with Q8 quantization
- 93.7% memory reduction with Q4 quantization
- Minimal accuracy loss
- GGUF import support

**Example:**
```csharp
using SmallMind.Quantization;

// Quantize a model to Q8
var quantizer = new ModelQuantizer();
await quantizer.QuantizeToQ8Async("model.smq", "model_q8.smq");

// Import GGUF and quantize
await quantizer.ImportGgufAsync("model.gguf", "model_q4.smq", QuantizationLevel.Q4);
```

### Retrieval-Augmented Generation (RAG)

**Use: `SmallMind.Rag` (v0.1.0)**

```bash
dotnet add package SmallMind.Rag
```

**When to use:**
- Building question-answering systems
- Document search and retrieval
- Chat over your documents

**Features:**
- BM25 sparse retrieval
- Dense embeddings
- Hybrid search (BM25 + dense)
- Document chunking and indexing
- Zero 3rd-party dependencies

**Example:**
```csharp
using SmallMind.Rag;

var ragEngine = new RagEngine();
await ragEngine.IndexDocumentsAsync(documents);

var answer = await ragEngine.AnswerQuestionAsync(
    "What is SmallMind?",
    topK: 5
);
```

---

## 🔧 Advanced / Educational Use

These packages provide granular control for custom implementations and learning:

### Core Tensor Operations

**Use: `SmallMind.Core` (v0.3.0)**

```bash
dotnet add package SmallMind.Core
```

**When to use:**
- Building custom neural network architectures
- Implementing research papers
- Learning how transformers work
- Performance optimization experiments

**Features:**
- Tensor class with automatic differentiation
- SIMD-optimized matrix operations (29+ GFLOPS)
- Memory pooling with ArrayPool
- Custom layer implementations

**Example:**
```csharp
using SmallMind.Core;

var tensor = new Tensor(new float[] { 1, 2, 3, 4 }, shape: new[] { 2, 2 });
var result = tensor.MatMul(other);
result.Backward(); // Automatic differentiation
```

### Transformer Architecture

**Use: `SmallMind.Transformers` (v0.3.0)**

```bash
dotnet add package SmallMind.Transformers
```

**When to use:**
- Customizing attention mechanisms
- Implementing new transformer variants
- Understanding transformer internals

**Includes:**
- Multi-head attention
- Feed-forward networks
- Layer normalization
- Positional encodings

### Text Generation Runtime

**Use: `SmallMind.Runtime` (v0.3.0)**

```bash
dotnet add package SmallMind.Runtime
```

**When to use:**
- Customizing sampling strategies
- Building chat applications with custom logic
- Advanced conversation state management

**Features:**
- Sampling (greedy, temperature, top-k, top-p)
- KV cache optimization
- Conversation sessions
- Token-by-token generation

### Tokenization

**Use: `SmallMind.Tokenizers` (v0.3.0)**

```bash
dotnet add package SmallMind.Tokenizers
```

**When to use:**
- Implementing custom tokenization schemes
- Working with multiple languages
- Preprocessing text data

**Includes:**
- Character-level tokenizer
- BPE (Byte Pair Encoding) tokenizer
- Extensible `ITokenizer` interface

---

## Package Comparison

| Package | Stability | Use Case | Complexity | Dependencies |
|---------|-----------|----------|------------|--------------|
| SmallMind.Public | ✅ Stable API | Production inference | Low | Engine, Abstractions |
| SmallMind.Quantization | ⚠️ May change | Model compression | Medium | Core, Tokenizers |
| SmallMind.Rag | ⚠️ May change | Document Q&A | Medium | Core, Runtime, Tokenizers, Transformers |
| SmallMind.Core | ⚠️ May change | Custom architectures | High | None |
| SmallMind.Transformers | ⚠️ May change | Architecture research | High | Core, Abstractions |
| SmallMind.Runtime | ⚠️ May change | Custom generation logic | Medium | Core, Transformers, Tokenizers, Quantization |
| SmallMind.Tokenizers | ⚠️ May change | Custom tokenization | Low | Core |

**Key:**
- ✅ **Stable API**: Semantic versioning guarantees, won't break between minor versions
- ⚠️ **May change**: Internal implementation, may change without notice (for advanced/educational use)

---

## Decision Tree

```
┌─────────────────────────────────────────┐
│  What do you want to do?               │
└─────────────────────────────────────────┘
           │
           ├─► Run inference in production?
           │   ✅ SmallMind.Public (v1.0.0) ⭐ RECOMMENDED
           │
           ├─► Reduce model memory usage?
           │   ✅ SmallMind.Quantization (v0.3.0)
           │
           ├─► Build a RAG application?
           │   ✅ SmallMind.Rag (v0.1.0)
           │
           ├─► Build custom neural architectures?
           │   ✅ SmallMind.Core (v0.3.0)
           │   + SmallMind.Transformers (v0.3.0)
           │
           ├─► Customize text generation logic?
           │   ✅ SmallMind.Runtime (v0.3.0)
           │
           ├─► Implement custom tokenization?
           │   ✅ SmallMind.Tokenizers (v0.3.0)
           │
           └─► Learn how LLMs work (educational)?
               ✅ Clone the repo and explore the source code
```

---

## Package Dependencies

Understanding package dependencies helps you choose the right combination:

```
SmallMind.Public (Production Entry Point)
├── SmallMind.Abstractions (internal)
└── SmallMind.Engine (internal)
    ├── SmallMind.Core
    ├── SmallMind.Runtime
    │   ├── SmallMind.Transformers
    │   ├── SmallMind.Tokenizers
    │   └── SmallMind.Quantization
    ├── SmallMind.Transformers
    ├── SmallMind.Tokenizers
    ├── SmallMind.Rag
    └── SmallMind.Quantization

SmallMind.Rag (Specialized)
├── SmallMind.Core
├── SmallMind.Runtime
├── SmallMind.Tokenizers
├── SmallMind.Transformers
└── SmallMind (meta-package)

SmallMind.Quantization (Specialized)
├── SmallMind.Core
└── SmallMind.Tokenizers
```

---

## Installation Examples

### Minimal Production Setup
Just inference, nothing else:
```bash
dotnet add package SmallMind.Public
```

### Production with Quantization
Inference + memory optimization:
```bash
dotnet add package SmallMind.Public
dotnet add package SmallMind.Quantization
```

### RAG Application
Document Q&A system:
```bash
dotnet add package SmallMind.Public
dotnet add package SmallMind.Rag
```

### Custom Architecture Development
Building from scratch:
```bash
dotnet add package SmallMind.Core
dotnet add package SmallMind.Transformers
dotnet add package SmallMind.Runtime
dotnet add package SmallMind.Tokenizers
```

---

## Version Compatibility

All packages follow semantic versioning:
- **Major version changes** (e.g., 1.x → 2.0): Breaking changes
- **Minor version changes** (e.g., 1.0 → 1.1): New features, backward compatible
- **Patch version changes** (e.g., 1.0.0 → 1.0.1): Bug fixes, performance improvements

### Current Versions
- `SmallMind.Public`: **1.0.0** (Stable)
- `SmallMind.Core`: 0.3.0
- `SmallMind.Transformers`: 0.3.0
- `SmallMind.Runtime`: 0.3.0
- `SmallMind.Tokenizers`: 0.3.0
- `SmallMind.Quantization`: 0.3.0
- `SmallMind.Rag`: 0.1.0

---

## Getting Help

- 📖 [API Stability Policy](API_STABILITY.md)
- 📖 [Public API Reference](PublicApi.md)
- 📖 [Quantization Guide](quantization.md)
- 📖 [RAG Documentation](RAG_AND_CHAT.md)
- 🐛 [Issue Tracker](https://github.com/justinamiller/SmallMind/issues)
- 💬 [Discussions](https://github.com/justinamiller/SmallMind/discussions)

---

## Summary

**For most users**: Start with `SmallMind.Public` - it's stable, production-ready, and provides everything you need for inference.

**For specialized needs**: Add `SmallMind.Quantization` (memory optimization) or `SmallMind.Rag` (document Q&A).

**For advanced users**: Use the lower-level packages (`Core`, `Transformers`, `Runtime`, `Tokenizers`) for custom implementations, but be aware these APIs may change.

**For learners**: Clone the repository and explore the source code directly - it's designed to be readable and educational!

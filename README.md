# SmallMind

[![Build and Test](https://github.com/justinamiller/SmallMind/actions/workflows/build.yml/badge.svg)](https://github.com/justinamiller/SmallMind/actions/workflows/build.yml)
[![CodeQL](https://github.com/justinamiller/SmallMind/actions/workflows/codeql.yml/badge.svg)](https://github.com/justinamiller/SmallMind/actions/workflows/codeql.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Version](https://img.shields.io/badge/.NET-10.0%2B-512BD4)](https://dotnet.microsoft.com/download)
[![NuGet](https://img.shields.io/badge/NuGet-Coming%20Soon-blue)](https://www.nuget.org/)

**Production-ready LLM inference runtime for .NET with zero external dependencies.**

SmallMind is a pure C# implementation of decoder-only transformer models (GPT-style) optimized for CPU inference. Built entirely in .NET 10+ with no third-party ML libraries, it provides a stable, documented API for running language models locally on any platform that supports .NET.

## Production Readiness

SmallMind is engineered for production deployment with enterprise-grade reliability:

✅ **Multi-Platform CI**: Validated on Ubuntu, Windows, and macOS  
✅ **Security Scanned**: CodeQL analysis with zero high/critical findings  
✅ **Deterministic Tests**: Golden output regression tests ensure cross-platform consistency  
✅ **Performance Monitored**: Automated benchmarks with regression detection  
✅ **OpenAI-Compatible Server**: Production HTTP API with streaming, rate limiting, and backpressure  
✅ **Comprehensive Testing**: 500+ unit tests, integration tests, negative tests for error handling  
✅ **API Stability**: Semantic versioning with stable public contract (`SmallMind` namespace)

See [Release Checklist](docs/release-checklist.md) for full production validation criteria.

## Quickstart

### Installation

```bash
# Install the stable API package
dotnet add package SmallMind

# Or clone and build from source
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind
./scripts/build.sh     # Linux/macOS
# or
.\scripts\build.ps1    # Windows
```

### Basic Usage

```csharp
using SmallMind;

// Initialize the engine with a model
var options = new SmallMindOptions
{
    ModelPath = "model.smq",     // .smq or .gguf format
    MaxContextTokens = 2048,
    EnableKvCache = true
};

using var engine = SmallMindFactory.Create(options);

// Create a generation session
var sessionOptions = new TextGenerationOptions
{
    Temperature = 0.7f,
    TopP = 0.9f,
    MaxOutputTokens = 100
};

using var session = engine.CreateTextGenerationSession(sessionOptions);

// Generate text
var request = new TextGenerationRequest
{
    Prompt = "The future of AI is".AsMemory()
};

var result = session.Generate(request);
Console.WriteLine(result.Text);
Console.WriteLine($"Tokens: {result.Usage.CompletionTokens}");
Console.WriteLine($"Speed: {result.Timings.TokensPerSecond:F2} tok/s");
```

### Streaming Generation

```csharp
await foreach (var token in session.GenerateStreaming(request))
{
    Console.Write(token.TokenText);
    Console.Out.Flush();
}
```

**For complete examples, see [examples/](examples/).**

## Architecture Overview

SmallMind implements a modular transformer inference pipeline:

```
┌─────────────────────────────────────────────────────────┐
│                    SmallMind API                        │
│  (Stable public interface: ISmallMindEngine, Sessions)  │
└─────────────────────────────────────────────────────────┘
                           │
        ┌──────────────────┼──────────────────┐
        ▼                  ▼                  ▼
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│   Runtime    │  │ Transformers │  │  Tokenizers  │
│              │  │              │  │              │
│ • Inference  │  │ • Attention  │  │ • BPE        │
│ • Sessions   │  │ • Feedforward│  │ • Vocab      │
│ • KV Cache   │  │ • LayerNorm  │  │ • Encoding   │
└──────────────┘  └──────────────┘  └──────────────┘
        │                  │                  │
        └──────────────────┼──────────────────┘
                           ▼
                  ┌──────────────┐
                  │     Core     │
                  │              │
                  │ • Tensors    │
                  │ • MatMul     │
                  │ • SIMD Ops   │
                  │ • Autograd   │
                  └──────────────┘
                           │
                           ▼
                  ┌──────────────┐
                  │Quantization  │
                  │              │
                  │ • Q8/Q4/Q6   │
                  │ • GGUF       │
                  │ • Dequant    │
                  └──────────────┘
```

### Core Components

- **SmallMind API**: Stable public interface with semantic versioning guarantees
- **Runtime**: Session management, inference orchestration, resource governance
- **Transformers**: Model implementations (GPT-2, LLaMA architectures)
- **Tokenizers**: BPE tokenization with vocabulary management
- **Core**: Low-level tensor operations with SIMD acceleration
- **Quantization**: Q8/Q4/Q6 quantization with GGUF format support

### Key Design Principles

1. **Zero Dependencies**: Pure C# implementation - no external ML libraries
2. **CPU-First**: Optimized for CPU inference with SIMD vectorization
3. **Memory Efficient**: ArrayPool usage, quantization, KV caching
4. **Production Ready**: Thread-safe engine, per-session state, resource limits
5. **Educational**: Readable, documented code for learning transformer internals

### Performance Characteristics

- **Matrix Multiplication**: ~30 GFLOPS (comparable to PyTorch CPU)
- **Inference Speed**: 37-83 tokens/sec (model-dependent)
- **Memory**: 87-93% reduction with Q8/Q4 quantization
- **Allocation**: Minimal GC pressure via ArrayPool

**Detailed benchmarks**: [docs/benchmarks/](docs/PERFORMANCE_ANALYSIS_REPORT_2026-02-11.md)

## Supported Models

### Model Architectures

SmallMind supports decoder-only transformer architectures:

- **GPT-2 Style**: Causal attention, absolute positional encoding
- **LLaMA Style**: RoPE positional encoding, RMSNorm
- **Custom**: Train your own models with included training utilities

### Model Formats

| Format | Description | Use Case |
|--------|-------------|----------|
| `.smq` | SmallMind native format | Trained models, optimized loading |
| `.gguf` | GGUF import (read-only) | Import pre-quantized models from llama.cpp |

### Quantization Levels

| Level | Bits/Weight | Size Reduction | Accuracy | Speed |
|-------|-------------|----------------|----------|-------|
| FP32 | 32 | Baseline | Baseline | Baseline |
| Q8 | 8 | 4× smaller | ~99% | 1.5-2× faster |
| Q4 | 4-5 | 6-8× smaller | ~95% | 2-3× faster |
| Q6 | 6 | 5× smaller | ~97% | 1.8-2.5× faster |

### Size Limits

| Component | Limit | Notes |
|-----------|-------|-------|
| Vocabulary Size | ~100K tokens | BPE tokenizer |
| Context Length | 2048 tokens (default) | Configurable, KV cache limited |
| Model Parameters | Up to 2B | .NET array size limits apply |
| Embedding Dimension | 4096 | Typical max for consumer hardware |
| Attention Heads | 32 | Typical max |

**For billion-parameter models, see**: [docs/LARGE_MODEL_SUPPORT.md](docs/LARGE_MODEL_SUPPORT.md)

### Example Model Sizes

| Model | Parameters | Memory (FP32) | Memory (Q8) | Memory (Q4) |
|-------|-----------|---------------|-------------|-------------|
| GPT-2 Small | 124M | ~500 MB | ~125 MB | ~62 MB |
| GPT-2 Medium | 350M | ~1.4 GB | ~350 MB | ~175 MB |
| GPT-2 Large | 774M | ~3.1 GB | ~775 MB | ~388 MB |
| SmolLM-135M | 135M | ~540 MB | ~135 MB | ~68 MB |
| SmolLM-360M | 360M | ~1.4 GB | ~360 MB | ~180 MB |
| LLaMA-1B | 1B | ~4 GB | ~1 GB | ~500 MB |

### GGUF Import

SmallMind can import pre-quantized GGUF models from the llama.cpp ecosystem:

```csharp
var options = new SmallMindOptions
{
    ModelPath = "model-Q4_K_M.gguf",
    AllowGgufImport = true
};
```

**Supported GGUF quantizations**: Q8_0, Q4_K, Q6_K  
**See**: [docs/GGUF_MILESTONE_SUMMARY.md](docs/GGUF_MILESTONE_SUMMARY.md)

## Documentation

### Getting Started
- [Quickstart Guide](docs/quickstart.md) - Step-by-step tutorial
- [API Documentation](docs/PublicApi.md) - Complete API reference
- [Configuration](docs/configuration.md) - Engine and generation options
- [Examples](examples/) - Code samples and demos

### Advanced Topics
- [Large Model Support](docs/LARGE_MODEL_SUPPORT.md) - Billion-parameter models
- [Quantization Guide](docs/Q4K_Q6K_IMPLEMENTATION_SUMMARY.md) - Q4/Q6/Q8 details
- [Performance Tuning](docs/PERFORMANCE_OPTIMIZATION_SUMMARY.md) - Optimization techniques
- [Concurrency](docs/CONCURRENCY.md) - Thread-safety and multi-user scenarios

### Model Support
- [GGUF Import](docs/GGUF_MILESTONE_SUMMARY.md) - Using llama.cpp models
- [Pretrained Models](docs/pretrained-models.md) - Available pretrained models
- [Training Guide](docs/PHASE3_IMPLEMENTATION_SUMMARY.md) - Training your own models
- [Model Registry](docs/SUPPORTED_MODELS.md) - Supported architectures

### API Stability
- [API Stability Policy](docs/API_STABILITY.md) - Versioning and guarantees
- [Compatibility Matrix](docs/compatibility-matrix.md) - Version compatibility
- [CHANGELOG](CHANGELOG.md) - Version history

## Project Structure

```
SmallMind/
├── src/                    # Source code
│   ├── SmallMind/          # Stable public API
│   ├── SmallMind.Core/     # Tensor operations, SIMD
│   ├── SmallMind.Runtime/  # Inference engine
│   ├── SmallMind.Transformers/  # Model implementations
│   ├── SmallMind.Tokenizers/    # Tokenization
│   ├── SmallMind.Quantization/  # Quantization support
│   └── ...
├── tests/                  # Unit and integration tests
├── examples/               # Code samples and demos
├── docs/                   # Documentation
├── scripts/                # Build and utility scripts
└── tools/                  # Development tools
```

## Contributing

Contributions are welcome! Please see [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for guidelines.

### Development Setup

```bash
# Clone and build
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind
./scripts/build.sh

# Run tests
dotnet test

# Run benchmarks
./scripts/run-benchmarks.sh
```

## License

MIT License - see [LICENSE](LICENSE) for details.

Copyright (c) 2024 Justin Miller

## Acknowledgments

SmallMind is an educational implementation of modern transformer architectures. It draws inspiration from:

- **llama.cpp** - Efficient C++ inference implementation
- **PyTorch** - Deep learning framework
- **GPT-2** - Transformer architecture
- **GGUF Format** - Quantization format specification

## Support

- **Issues**: [GitHub Issues](https://github.com/justinamiller/SmallMind/issues)
- **Discussions**: [GitHub Discussions](https://github.com/justinamiller/SmallMind/discussions)
- **Documentation**: [docs/](docs/)

---

**Status**: Production-ready for CPU inference with models up to ~2B parameters.

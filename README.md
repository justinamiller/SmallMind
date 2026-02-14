# SmallMind

[![Build and Test](https://github.com/justinamiller/SmallMind/actions/workflows/build.yml/badge.svg)](https://github.com/justinamiller/SmallMind/actions/workflows/build.yml)
[![CodeQL](https://github.com/justinamiller/SmallMind/actions/workflows/codeql.yml/badge.svg)](https://github.com/justinamiller/SmallMind/actions/workflows/codeql.yml)
[![NuGet](https://img.shields.io/nuget/v/SmallMind.svg)](https://www.nuget.org/packages/SmallMind/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Pure C# LLM inference runtime for CPU execution with zero native dependencies.

## What it is

- **CPU-only inference engine** for decoder-only transformer models (GPT-2, LLaMA architectures)
- **Pure .NET 10.0** implementation—no external ML frameworks, no native binaries, no Python
- **Production-grade**: 500+ tests, multi-platform CI, security scanning, semantic versioning
- **Memory efficient**: Q4/Q6/Q8 quantization (4–8× size reduction), KV caching, ArrayPool allocation
- **Cross-platform**: Runs on Windows/Linux/macOS with SIMD acceleration (AVX-512, AVX2, NEON)

## Why it exists

- **Portability**: Single .NET assembly runs anywhere .NET 10.0 runs—no Python, no Docker, no native build chains
- **Determinism**: Reproducible outputs across platforms; ideal for testing, debugging, and educational purposes
- **Performance**: CPU-optimized with SIMD kernels, quantization, and efficient memory management

## Quickstart

### Install via NuGet

```bash
dotnet add package SmallMind
```

### Install from source

```bash
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind
dotnet build SmallMind.sln -c Release
```

### Basic usage

```csharp
using SmallMind;

// Create engine with model
var options = new SmallMindOptions
{
    ModelPath = "model.smq",  // or .gguf format
    MaxContextTokens = 2048,
    EnableKvCache = true
};

using var engine = SmallMindFactory.Create(options);

// Generate text
var sessionOpts = new TextGenerationOptions
{
    Temperature = 0.7f,
    MaxOutputTokens = 100
};

using var session = engine.CreateTextGenerationSession(sessionOpts);
var request = new TextGenerationRequest { Prompt = "The future of AI is".AsMemory() };
var result = session.Generate(request);

Console.WriteLine(result.Text);
Console.WriteLine($"Speed: {result.Timings.TokensPerSecond:F2} tok/s");
```

**Streaming:**

```csharp
await foreach (var token in session.GenerateStreaming(request))
{
    Console.Write(token.TokenText);
}
```

See [examples/GoldenPath](examples/GoldenPath) for complete working code.

## Supported platforms

| OS      | Arch     | SIMD Paths               | Notes                          |
|---------|----------|--------------------------|--------------------------------|
| Windows | x64      | AVX-512, AVX2, fallback  | Tested in CI (windows-latest)  |
| Linux   | x64      | AVX-512, AVX2, fallback  | Tested in CI (ubuntu-latest)   |
| macOS   | x64      | AVX2, fallback           | Tested in CI (macos-latest)    |
| macOS   | arm64    | NEON, fallback           | M1/M2/M3 Macs                  |

**Runtime**: .NET 10.0 or later

## Supported model formats

### Architectures

- **GPT-2 style**: Causal attention, absolute positional encoding
- **LLaMA style**: RoPE positional encoding, RMSNorm
- **Custom models**: Train with included utilities

### File formats

| Format | Purpose                  | Quantization Support                 |
|--------|--------------------------|--------------------------------------|
| `.smq` | SmallMind native format  | F32, F16, Q8_0, Q4_0                 |
| `.gguf`| GGUF import (read-only)  | 14 formats (see below)               |

### GGUF quantization types (supported)

**Floating point**: F32, F16  
**Basic quant**: Q4_0, Q4_1, Q5_0, Q5_1, Q8_0  
**K-quant**: Q4_K, Q5_K, Q6_K, Q8_K

> **Note**: Importance-weighted quantization (IQ2_XXS, IQ3_S, etc.) and Q2_K/Q3_K are not supported.

### Typical memory footprint

| Model          | Params | FP32   | Q8_0  | Q4_0  |
|----------------|--------|--------|-------|-------|
| GPT-2 Small    | 124M   | 500 MB | 125 MB| 62 MB |
| GPT-2 Medium   | 350M   | 1.4 GB | 350 MB| 175 MB|
| GPT-2 Large    | 774M   | 3.1 GB | 775 MB| 388 MB|
| SmolLM-135M    | 135M   | 540 MB | 135 MB| 68 MB |
| LLaMA-1B       | 1B     | 4 GB   | 1 GB  | 500 MB|

**Size limits**: Models up to ~2B parameters (constrained by .NET array limits). See [docs/LARGE_MODEL_SUPPORT.md](docs/LARGE_MODEL_SUPPORT.md).

## Performance & benchmarks

**Benchmark infrastructure**: Automated CI on every PR ([bench-ci.yml](.github/workflows/bench-ci.yml)) and nightly extended runs ([bench-nightly.yml](.github/workflows/bench-nightly.yml)).

**Metrics measured**:
- **Throughput**: tokens/sec (prefill and decode phases)
- **Latency**: Time-to-first-token (TTFT) in milliseconds
- **Memory**: Peak RSS, allocations/token, GC pressure
- **GFLOPS**: Matrix multiplication performance (15-24 GFLOPS on CPU)

**Run benchmarks locally**:

```bash
# Matrix multiplication benchmark
dotnet run --project benchmarks/specialized/MatMulBenchmark -c Release

# Full profiler suite
dotnet run --project benchmarks/specialized/ProfilerBenchmarks -c Release

# Inference allocation profiling
dotnet run --project benchmarks/specialized/InferenceAllocationBenchmark -c Release
```

**Performance reports**: See [docs/PERFORMANCE_ANALYSIS_REPORT_2026-02-11.md](docs/PERFORMANCE_ANALYSIS_REPORT_2026-02-11.md) and [docs/benchmarks/](docs/benchmarks/).

> **Note**: CI benchmarks use structural test fixtures, not full production models. Throughput estimates (15-25 tok/s for 7B Q4 models) are documented but not empirically validated in CI.

## API surface (high level)

Entry point: `SmallMindFactory.Create(SmallMindOptions)` → returns `ISmallMindEngine`

**Main abstractions**:

- `ISmallMindEngine` — Thread-safe factory for creating sessions
- `ITextGenerationSession` — Stateful generation context (not thread-safe)
- `TextGenerationOptions` — Sampling params (Temperature, TopP, TopK, StopSequences)
- `TextGenerationRequest` — Input prompt
- `GenerationResult` — Output with text, usage stats, timings

**Disposable pattern**: Both engine and sessions implement `IDisposable` for resource cleanup.

See [docs/PublicApi.md](docs/PublicApi.md) for complete API reference.

## Limitations

- **No GPU support**: CPU-only; no CUDA, ROCm, or Metal backends
- **Model size**: Practical limit ~2B parameters due to .NET array constraints
- **GGUF support**: Import only (read-only); cannot export to GGUF
- **Quantization formats**: IQ (importance-weighted) and Q2_K/Q3_K not supported
- **Tokenizer**: BPE and basic vocab only; no SentencePiece, tiktoken, or Unigram
- **API server**: No built-in HTTP server (example implementations exist in `tools/` and `examples/`)
- **Concurrency**: Sessions are not thread-safe; create one session per request/thread

## Roadmap

### Next (0–3 months)

- Expand GGUF quantization support (Q2_K, Q3_K)
- Improve tokenizer compatibility (SentencePiece, tiktoken)
- Optimize memory for 7B+ models

### Later

- GPU acceleration exploration (experimental)
- Additional model architectures (Mistral, Phi)
- Advanced sampling (nucleus, beam search improvements)

See [docs/roadmap/](docs/roadmap/) for detailed plans.

## Contributing

Contributions welcome! See [docs/CONTRIBUTING.md](docs/CONTRIBUTING.md) for guidelines.

**Development setup**:

```bash
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind
dotnet build SmallMind.sln -c Release
dotnet test
```

## Security

Report vulnerabilities via [SECURITY.md](SECURITY.md).

**Security tooling**:
- CodeQL analysis on every PR
- OpenSSF Scorecard monitoring
- Dependabot for dependency updates

## License

MIT License — see [LICENSE](LICENSE)

Copyright (c) 2024 Justin Miller

---

**Project status**: Production-ready for CPU inference with models up to ~2B parameters. Actively maintained.

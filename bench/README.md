# SmallMind Real-Model Benchmarking System

A comprehensive, reproducible, CI-friendly benchmarking system for SmallMind - a CPU-only .NET 10 LLM implementation. This system runs real GGUF models and measures performance across different CPU architectures and configurations.

## 🎯 Goals

1. **Real Model Testing**: Run actual GGUF models (not synthetic workloads)
2. **Reproducible**: Deterministic seeding, stable prompts, versioned outputs
3. **CI-Friendly**: Small models for GitHub Actions, larger models for manual testing
4. **Cross-Platform**: Support x64/ARM64 on Windows/Linux/macOS
5. **Comprehensive Metrics**: TTFT, tok/s, memory, GC stats, thread scaling
6. **Normalized Comparison**: Per-core, per-GHz metrics to compare implementations fairly

## 📁 Directory Structure

```
bench/
├── SmallMind.Benchmarks.Core/     # Core benchmarking library
│   ├── Models/                     # Model download & manifest
│   ├── Environment/                # System info capture
│   ├── Measurement/                # Benchmark harness & scenarios
│   ├── Normalization/              # Efficiency metrics
│   └── Output/                     # JSON/Markdown/CSV writers
├── SmallMind.Benchmarks/           # Console application
│   └── Commands/                   # run, download, merge commands
├── models/                         # Model cache (not committed)
│   └── models.manifest.json        # Model definitions
├── prompts/                        # Standard prompts for testing
│   ├── simple.txt
│   ├── story.txt
│   └── instruction.txt
└── results/                        # Benchmark outputs (not committed)
    └── <timestamp>_<sha>_<os>_<arch>.<ext>
```

## 🚀 Quick Start

### 1. Download CI Models

```bash
cd bench
dotnet run --project SmallMind.Benchmarks --configuration Release -- download --ci-only
```

### 2. Run Benchmarks

```bash
# Run all CI models with default settings
dotnet run --project SmallMind.Benchmarks --configuration Release -- run --ci-only

# Run specific model with custom configuration
dotnet run --project SmallMind.Benchmarks --configuration Release -- run \
  --model tinyllama-1.1b-q4_0 \
  --context 256,1024 \
  --threads 1,4,8 \
  --iterations 5 \
  --output ./results
```

### 3. View Results

Results are saved in three formats:
- `results/<timestamp>_<sha>_<os>_<arch>.json` - Machine-readable JSON
- `results/<timestamp>_<sha>_<os>_<arch>.md` - Human-readable Markdown
- `results/<timestamp>_<sha>_<os>_<arch>.csv` - Data analysis CSV

## 📊 What Is Measured

### Core Metrics

| Metric | Description |
|--------|-------------|
| **TTFT** | Time To First Token (ms) - latency from prompt to first generated token |
| **Tok/s** | Tokens per second (end-to-end including TTFT) |
| **Tok/s (SS)** | Tokens per second steady-state (excluding TTFT) |
| **Peak RSS** | Peak Resident Set Size - maximum process memory usage |
| **Alloc/tok** | Bytes allocated per token during generation |
| **Gen0/1/2** | Garbage collection counts during benchmark run |

### Normalized Metrics (Single CPU Unit)

To compare implementations fairly across different hardware:

| Metric | Formula | Purpose |
|--------|---------|---------|
| **Tok/s per Core** | `tok/s ÷ threads` | Efficiency per thread |
| **Tok/s per GHz/Core** | `tok/s ÷ (GHz × threads)` | Accounts for clock speed differences |
| **Cycles/token** | `(GHz × 1e9) ÷ tok/s` | CPU cycles needed per token (1-thread only) |

**Important**: Normalized metrics emphasize **implementation efficiency**, not hardware power. A higher "tok/s per GHz/Core" means the implementation uses CPU resources more effectively.

## 🔧 Command Reference

### `run` - Execute Benchmarks

```bash
dotnet run --project SmallMind.Benchmarks -c Release -- run [options]
```

**Options:**
- `--model <name|path>` - Model name from manifest or direct GGUF path
- `--manifest <path>` - Path to manifest (default: `../models/models.manifest.json`)
- `--ci-only` - Run only CI models from manifest
- `--context <sizes>` - Context sizes to test (comma-separated, e.g., `256,1024,4096`)
- `--threads <counts>` - Thread counts (comma-separated, e.g., `1,4,8,max`)
- `--warmup <n>` - Warmup iterations (default: 3)
- `--iterations <n>` - Measured iterations (default: 5)
- `--tokens <n>` - Tokens to generate (default: 100)
- `--output <dir>` - Output directory (default: `../results`)
- `--format <fmt>` - Output formats: `json`, `markdown`, `csv`, `all` (default: `all`)

**Examples:**

```bash
# CI mode - fast, small models only
dotnet run -c Release -- run --ci-only --context 256,1024 --threads 1,4

# Full benchmark - all context sizes and thread counts
dotnet run -c Release -- run \
  --model tinyllama-1.1b-q4_0 \
  --context 256,1024,4096 \
  --threads 1,2,4,8,max \
  --iterations 10

# Direct GGUF file
dotnet run -c Release -- run --model /path/to/model.gguf --context 2048 --threads 4
```

### `download` - Download Models

```bash
dotnet run --project SmallMind.Benchmarks -c Release -- download [options]
```

**Options:**
- `--manifest <path>` - Path to manifest
- `--model <name>` - Specific model to download (or all if not specified)
- `--ci-only` - Download only CI models
- `--continue-on-error` - Continue downloading other models if one fails

**Examples:**

```bash
# Download all CI models
dotnet run -c Release -- download --ci-only

# Download specific model
dotnet run -c Release -- download --model tinyllama-1.1b-q4_0

# Download all models (warning: large!)
dotnet run -c Release -- download
```

### `merge` - Merge Results

```bash
dotnet run --project SmallMind.Benchmarks -c Release -- merge [options]
```

**Options:**
- `--input <pattern>` - Input file pattern (e.g., `results/*.json`)
- `--output <file>` - Output merged markdown table

**Examples:**

```bash
# Merge all results in results directory
dotnet run -c Release -- merge --input "results/*.json" --output summary.md

# Merge specific runs
dotnet run -c Release -- merge --input "results/2024-*_ubuntu*.json" --output ubuntu_summary.md
```

## 🏗️ Model Manifest

The `models/models.manifest.json` file defines available models:

```json
{
  "version": "1.0",
  "models": [
    {
      "name": "tinyllama-1.1b-q4_0",
      "displayName": "TinyLlama 1.1B Q4_0 (CI Model)",
      "url": "https://huggingface.co/.../model.gguf",
      "sha256": "...",
      "sizeBytes": 669138976,
      "quantType": "Q4_0",
      "contextLength": 2048,
      "ci": true,
      "description": "Small model suitable for CI testing"
    }
  ]
}
```

- **ci: true** - Marks models suitable for fast CI runs
- **sha256** - Required for integrity verification
- **sizeBytes** - Used for download progress and verification

## 🔬 Methodology

### Measurement Process

1. **Model Load** (excluded from metrics)
   - Load GGUF model
   - Measure peak RSS after load

2. **Warmup Phase** (excluded from metrics)
   - Run N warmup iterations to stabilize JIT, caches
   - Default: 3 iterations

3. **Measured Phase** (captured)
   - Run N measured iterations
   - Track: TTFT, total time, tokens generated, memory, GC
   - Calculate statistics: median, P90, stddev

4. **Output Generation**
   - Write JSON (machine-readable)
   - Write Markdown (human-readable)
   - Write CSV (data analysis)

### Reproducibility

- **Deterministic Seeding**: Each run uses consistent random seed
- **Stable Prompts**: Use standard prompts from `prompts/` directory
- **Versioned Outputs**: Include git commit SHA in results
- **Environment Capture**: Record OS, CPU, .NET version, SIMD capabilities

## 📈 Normalization Explanation

### Why Normalize?

Raw tok/s numbers are misleading when comparing across hardware:
- A 16-core 5GHz CPU will always beat a 4-core 2GHz CPU
- This doesn't tell us which **implementation** is better

### How to Use Normalized Metrics

**Tok/s per GHz/Core** is the key metric for comparing implementations:

```
Implementation A: 1.5 tok/s per GHz/Core on Intel i9
Implementation B: 2.0 tok/s per GHz/Core on AMD Ryzen

→ Implementation B is more efficient (uses CPU resources better)
```

**Limitations:**
- CPU frequency may not be available on all platforms (falls back to "N/A")
- Different CPU architectures have different IPC (instructions per cycle)
- These are **estimates** for rough comparison, not precise measurements

## 🔄 CI Integration

### GitHub Actions Workflow

The benchmark system integrates with CI via `.github/workflows/bench-ci.yml`:

```yaml
- Run only CI models (fast, ~5-10 minutes)
- Test on matrix: Windows, Linux, macOS (x64 + ARM64)
- Upload results as artifacts
- (Optional) Comment summary on PR
```

### Environment Variables

- `SMALLMIND_BENCH_MODEL_CACHE` - Override model cache directory
- `SMALLMIND_BENCH_SKIP_CHECKSUM=1` - Skip SHA256 verification (faster, less safe)
- `SMALLMIND_BENCH_VERBOSE=1` - Show verbose error messages

## 🎨 Example Output

### Markdown Summary

```markdown
# SmallMind Benchmark Results

**Run ID:** `550e8400-e29b-41d4-a716-446655440000`
**Start Time:** 2024-02-13 20:00:00 UTC
**Git Commit:** `a1b2c3d4e5f6`

## Environment

- **OS:** Ubuntu 24.04.3 LTS
- **Runtime:** .NET 10.0.2
- **CPU:** AMD EPYC 7763 64-Core Processor
- **Logical Cores:** 4
- **CPU Max Frequency:** 2450 MHz
- **SIMD:** SSE2, AVX2
- **GC Mode:** Server, Latency: Interactive

## Performance Results

| Model | Ctx | Threads | TTFT (ms) | Tok/s | Tok/s (SS) | Peak RSS (MB) | Alloc/tok (KB) | Gen0/1/2 |
|-------|-----|---------|-----------|-------|------------|---------------|----------------|----------|
| tinyllama-1.1b | 256 | 1 | 45.2 | 12.50 | 13.10 | 1024.5 | 8.2 | 10/2/0 |
| tinyllama-1.1b | 256 | 4 | 48.1 | 38.20 | 40.30 | 1028.3 | 8.5 | 15/3/0 |

## Normalized Efficiency Metrics

| Model | Ctx | Threads | Tok/s per Core | Tok/s per GHz/Core | Cycles/tok |
|-------|-----|---------|----------------|--------------------|-----------:|
| tinyllama-1.1b | 256 | 1 | 12.50 | 5.10 | 196000000 |
| tinyllama-1.1b | 256 | 4 | 9.55 | 3.90 | N/A |
```

### JSON Schema

```json
{
  "runId": "...",
  "startTime": "...",
  "endTime": "...",
  "gitCommitSha": "...",
  "environment": { ... },
  "results": [
    {
      "scenarioName": "...",
      "modelName": "...",
      "contextSize": 256,
      "threadCount": 1,
      "ttftMilliseconds": 45.2,
      "tokensPerSecond": 12.50,
      ...
    }
  ],
  "normalizedResults": [ ... ]
}
```

## 🔐 Security

- All model downloads verify SHA256 checksums
- No external dependencies beyond .NET BCL
- No network access beyond model downloads
- No code generation or dynamic compilation

## 🤝 Contributing

To add a new model to the manifest:

1. Edit `bench/models/models.manifest.json`
2. Calculate SHA256: `sha256sum model.gguf`
3. Set `"ci": true` only for models <1GB suitable for CI
4. Submit PR with updated manifest

## 📚 References

- [SmallMind Repository](https://github.com/justinamiller/SmallMind)
- [GGUF Format Spec](https://github.com/ggerganov/ggml/blob/master/docs/gguf.md)
- [Hugging Face GGUF Models](https://huggingface.co/models?library=gguf)

## 📝 License

Same as SmallMind project (see root LICENSE file).

---

*Generated by SmallMind Benchmarks v1.0*

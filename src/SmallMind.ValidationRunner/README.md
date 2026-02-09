# SmallMind GGUF Validation Runner

A standalone tool for validating GGUF model files in SmallMind.

## Features

- **Automatic Model Download**: Downloads models from HuggingFace or custom URLs with resume support
- **Comprehensive Validation**: 5 test suite covering tokenization, inference, and generation
- **Model Diagnostics**: Reports architecture, vocab size, context length, and weight statistics
- **Performance Metrics**: Time-to-first-token (TTFT) and tokens/second measurements
- **Exit Codes**: Returns 0 for success, 1 for failure (CI-friendly)

## Usage

```bash
# Use default TinyLlama-1.1B-Chat Q4_0 model
dotnet run --project src/SmallMind.ValidationRunner

# Use custom model from HuggingFace
dotnet run --project src/SmallMind.ValidationRunner -- \
  --model https://huggingface.co/TheBloke/Llama-2-7B-GGUF/resolve/main/llama-2-7b.Q4_0.gguf

# Use local model file
dotnet run --project src/SmallMind.ValidationRunner -- \
  --model /path/to/model.gguf

# Enable verbose diagnostics
dotnet run --project src/SmallMind.ValidationRunner -- \
  --model /path/to/model.gguf \
  --verbose \
  --seed 42

# Custom cache directory
dotnet run --project src/SmallMind.ValidationRunner -- \
  --cache-dir ./my-models
```

## Command-Line Options

| Option | Description | Default |
|--------|-------------|---------|
| `--model <path\|url>` | Model file path or HuggingFace URL | TinyLlama Q4_0 |
| `--cache-dir <path>` | Directory for cached models | `~/.smallmind/models/` |
| `--verbose` | Enable detailed diagnostics | false |
| `--seed <int>` | Random seed for deterministic generation | 42 |
| `--help`, `-h` | Show help message | - |

## Validation Tests

The runner executes 5 comprehensive tests:

### Test A — Tokenizer Round Trip
- Encodes and decodes sample text
- Verifies tokenizer handles chat templates
- Reports BOS/EOS token IDs

### Test B — Forward Pass Sanity
- Runs model.Forward() on short prompt
- Verifies logits shape matches vocab size
- Checks for NaN/Inf values and variance

### Test C — Greedy Determinism
- Generates tokens with temperature ≈ 0.0 (greedy)
- Verifies identical output across multiple runs
- Checks for coherent continuation

### Test D — Sampled Generation
- Realistic generation (temp=0.7, topP=0.9, topK=40)
- Reports TTFT and throughput (tok/s)
- Detects repetition loops

### Test E — Stop Sequences
- Tests stop sequence detection (`"\n\n"`, `"<|"`)
- Verifies FinishReason is set correctly

## Output Example

```
=== SmallMind GGUF Validation Runner ===

Using cached model: ~/.smallmind/models/tinyllama-1.1b-chat-v1.0.Q4_0.gguf

Loading GGUF model...

=== Model Information ===
Architecture:      llama
Vocabulary size:   32,000 (tokenizer reports 32,000)
Context length:    2,048 tokens
Embedding dim:     2048
Layers:            22
Attention heads:   32 (KV heads: 4)
RoPE freq base:    10000
Load time:         2.34 seconds
BOS token:         1
EOS token:         2

=== Running Validation Tests ===

Test A — Tokenizer Round Trip
  PASS
  
Test B — Forward Pass Sanity
  PASS
  
Test C — Greedy Determinism
  PASS
  
Test D — Sampled Generation
  TTFT: 145.23 ms
  Throughput: 12.45 tok/s
  PASS
  
Test E — Stop Sequences
  PASS (stopped on stop sequence)

=== Validation Summary ===
✓ ALL TESTS PASSED
```

## Verbose Mode

Enable `--verbose` for additional diagnostics:

- Weight samples (first 8 values of key tensors)
- Logits statistics (min/max/mean/std)
- Top-10 predicted tokens with probabilities
- Detailed failure hints when tests fail

## Exit Codes

- `0`: All tests passed
- `1`: One or more tests failed or fatal error occurred

## Requirements

- .NET 10.0 or later
- No external dependencies (uses built-in HttpClient)
- Internet connection (for downloading models)
- Sufficient disk space for model cache

## Architecture

The ValidationRunner uses `InternalsVisibleTo` to access SmallMind's internal APIs directly, avoiding the need for complex public API wrappers. This enables comprehensive validation while maintaining the library's internal-only architecture.

## Integration

For CI/CD integration:

```bash
# In your CI pipeline
dotnet run --project src/SmallMind.ValidationRunner -- \
  --model <model-url> \
  --seed 42

# Check exit code
if [ $? -eq 0 ]; then
  echo "✓ Validation passed"
else
  echo "✗ Validation failed"
  exit 1
fi
```

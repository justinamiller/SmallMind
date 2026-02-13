# SmallMind Quick Start: GGUF to Inference

This guide walks you through the complete workflow from downloading a GGUF model to running inference.

## Prerequisites

- .NET 10.0 SDK installed
- SmallMind built and installed
- Sufficient RAM (model size + ~2GB overhead)
- Internet connection (for model download)

## Complete Workflow

### Step 1: Download a GGUF Model

Download a model from HuggingFace. We recommend starting with TinyLlama for testing:

```bash
smallmind model download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF tinyllama-1.1b-chat-v1.0.Q8_0.gguf
```

**What happens**:
- Downloads file from HuggingFace (~1.2 GB for TinyLlama Q8_0)
- Saves to `models/tinyllama-1.1b-chat-v1.0.Q8_0.gguf`
- Shows progress bar

**Alternative**: Download manually from HuggingFace website and place in `models/` directory

### Step 2: Import to SMQ Format

Convert the GGUF file to SmallMind's native SMQ format:

```bash
smallmind import-gguf models/tinyllama-1.1b-chat-v1.0.Q8_0.gguf models/tinyllama.smq
```

**What happens**:
- Reads GGUF file structure
- Validates quantization type (Q8_0 or Q4_0 required)
- Converts to SMQ format with 64-element blocks
- Creates two files:
  - `models/tinyllama.smq` (model weights)
  - `models/tinyllama.smq.manifest.json` (metadata)

**Expected output**:
```
Importing GGUF model from: models/tinyllama-1.1b-chat-v1.0.Q8_0.gguf
Output SMQ file: models/tinyllama.smq

✓ Import complete!
  Output: models/tinyllama.smq
  Manifest: models/tinyllama.smq.manifest.json
  GGUF size: 1.17 GB
  SMQ size: 1.18 GB
```

### Step 3: Run Inference

Generate text using the imported model:

```bash
smallmind generate models/tinyllama.smq "What is the capital of France?" --chat-template auto
```

**What happens**:
- Loads SMQ model into memory
- Auto-detects chat template (Llama 2 for TinyLlama)
- Applies chat formatting to prompt
- Generates response using streaming
- Displays output in real-time

**Expected output**:
```
Loading model: models/tinyllama.smq
Max tokens: 128
Temperature: 0.7
Top-P: 0.9
Model loaded successfully!
  Format: SMQ
  Quantization: Q8_0
  Auto-detected template: Llama 2 ([INST]...)

Generating...
────────────────────────────────────────────────────────────
The capital of France is Paris.
────────────────────────────────────────────────────────────
Generation complete!
```

## Advanced Usage

### Custom Generation Parameters

Control generation quality and length:

```bash
smallmind generate models/tinyllama.smq "Explain quantum computing" \
  --max-tokens 256 \
  --temperature 0.8 \
  --top-p 0.95 \
  --chat-template llama2
```

**Parameters**:
- `--max-tokens`: Maximum tokens to generate (default: 128)
- `--temperature`: Randomness (0.0 = deterministic, 2.0 = very random, default: 0.7)
- `--top-p`: Nucleus sampling threshold (default: 0.9)
- `--chat-template`: Force specific template (chatml, llama2, llama3, mistral, phi, auto, none)

### Using Different Models

#### Mistral 7B (larger, more capable)

```bash
# Download Q4_0 for better memory efficiency
smallmind model download TheBloke/Mistral-7B-Instruct-v0.2-GGUF mistral-7b-instruct-v0.2.Q4_0.gguf

# Import
smallmind import-gguf models/mistral-7b-instruct-v0.2.Q4_0.gguf models/mistral.smq

# Generate
smallmind generate models/mistral.smq "Write a Python function to calculate Fibonacci numbers" \
  --max-tokens 300 \
  --chat-template mistral
```

#### Phi-2 (efficient for coding)

```bash
# Download
smallmind model download microsoft/phi-2-GGUF phi-2-q8_0.gguf

# Import
smallmind import-gguf models/phi-2-q8_0.gguf models/phi-2.smq

# Generate
smallmind generate models/phi-2.smq "Debug this code: def add(a b): return a + b" \
  --chat-template phi \
  --max-tokens 200
```

## Chat Templates Explained

### Auto-Detection

Use `--chat-template auto` to automatically select the template based on the model name:

```bash
smallmind generate models/tinyllama.smq "Hello!" --chat-template auto
# Detects: Llama 2 template

smallmind generate models/mistral.smq "Hello!" --chat-template auto
# Detects: Mistral template
```

### Manual Templates

#### Llama 2 Format
```bash
smallmind generate models/model.smq "Question" --chat-template llama2
```
Generates: `[INST] Question [/INST]`

#### Llama 3 Format
```bash
smallmind generate models/model.smq "Question" --chat-template llama3
```
Generates: `<|start_header_id|>user<|end_header_id|>\n\nQuestion<|eot_id|>...`

#### Mistral Format
```bash
smallmind generate models/model.smq "Question" --chat-template mistral
```
Generates: `[INST] Question [/INST]`

#### Phi Format
```bash
smallmind generate models/model.smq "Question" --chat-template phi
```
Generates: `User: Question\nAssistant:`

#### ChatML Format
```bash
smallmind generate models/model.smq "Question" --chat-template chatml
```
Generates: `<|im_start|>user\nQuestion<|im_end|>\n<|im_start|>assistant\n`

#### No Template (raw completion)
```bash
smallmind generate models/model.smq "Once upon a time" --chat-template none
```
Generates: Raw text continuation without any formatting

## Inspecting Models

### View GGUF Metadata

Before importing, inspect the GGUF file:

```bash
smallmind inspect models/tinyllama-1.1b-chat-v1.0.Q8_0.gguf
```

**Shows**:
- Model architecture
- Vocabulary size
- Context length
- Quantization types
- Tensor list
- Metadata fields

### View SMQ Metadata

After importing, inspect the SMQ file:

```bash
smallmind model inspect models/tinyllama.smq
```

**Shows**:
- Model format version
- Quantization type
- Tensor count
- File size
- Manifest contents

## Troubleshooting

### "Unsupported tensor type" Error

**Problem**: Model uses K-quants or other unsupported quantization

**Solution**: Download Q8_0 or Q4_0 variant instead

```bash
# ❌ This won't work:
smallmind model download TheBloke/Llama-2-7B-GGUF llama-2-7b.Q5_K_M.gguf

# ✅ This will work:
smallmind model download TheBloke/Llama-2-7B-GGUF llama-2-7b.Q8_0.gguf
```

### Out of Memory

**Problem**: Model + KV cache doesn't fit in RAM

**Solutions**:
1. Use Q4_0 instead of Q8_0 (smaller but lower quality)
2. Use a smaller model (TinyLlama instead of 7B)
3. Reduce max context: `--max-tokens 64`

### Poor Generation Quality

**Problem**: Output is nonsensical or repetitive

**Solutions**:
1. Check chat template is correct: `--chat-template auto`
2. Adjust temperature: `--temperature 0.8` (try 0.7-1.0 range)
3. Try Q8_0 instead of Q4_0 for better quality
4. Increase max tokens if output is cut off

### Model Not Found on HuggingFace

**Problem**: Download command fails with 404

**Solutions**:
1. Check model exists: Visit `https://huggingface.co/{owner}/{repo}`
2. Check filename is exact: File names are case-sensitive
3. Try different quantization: Not all models have all quant types

### Generation is Too Slow

**Problem**: Tokens generate very slowly

**Explanations**:
- SmallMind is CPU-only (by design, no GPU)
- First token is slower (prefill phase)
- Larger models are naturally slower

**Optimizations**:
1. Use smaller models (TinyLlama, Phi-2)
2. Use Q4_0 for slightly faster inference
3. Reduce context length
4. Use a machine with more CPU cores
5. Close other applications

## Example Workflows

### Code Generation

```bash
smallmind generate models/phi-2.smq \
  "Write a Python function to reverse a linked list" \
  --chat-template phi \
  --max-tokens 400 \
  --temperature 0.3
```

### Creative Writing

```bash
smallmind generate models/tinyllama.smq \
  "Write a short story about a robot learning to paint" \
  --chat-template llama2 \
  --max-tokens 500 \
  --temperature 1.0
```

### Question Answering

```bash
smallmind generate models/mistral.smq \
  "What are the main differences between Python and JavaScript?" \
  --chat-template mistral \
  --max-tokens 300 \
  --temperature 0.7
```

### Translation

```bash
smallmind generate models/mistral.smq \
  "Translate to French: 'The quick brown fox jumps over the lazy dog'" \
  --chat-template mistral \
  --max-tokens 100 \
  --temperature 0.3
```

## Next Steps

- Explore `SUPPORTED_MODELS.md` for full model compatibility list
- Check `README.md` for architecture details
- Join discussions for questions and tips
- Report issues on GitHub

## Quick Reference

```bash
# Download
smallmind model download <owner>/<repo> <filename.gguf>

# Import
smallmind import-gguf <input.gguf> <output.smq>

# Generate
smallmind generate <model.smq> <prompt> [options]

# Inspect
smallmind inspect <file.gguf>
smallmind model inspect <file.smq>

# Options
--max-tokens <n>         # Max output tokens
--temperature <t>        # Sampling randomness (0.0-2.0)
--top-p <p>              # Nucleus sampling (0.0-1.0)
--chat-template <type>   # Chat format (auto, llama2, mistral, phi, etc.)
```

---

**Have questions?** Open a GitHub Discussion!  
**Found a bug?** Open a GitHub Issue!

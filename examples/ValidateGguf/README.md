# ValidateGguf - GGUF Model Validation Tool

A command-line tool for validating GGUF model loading and text generation in SmallMind.

## Purpose

This tool validates that SmallMind can successfully:
1. Load a GGUF model file
2. Extract configuration and tokenizer
3. Load and inject weights correctly
4. Generate coherent text output

## Acceptance Criteria

The tool verifies:
- ✅ Generate ≥ 20 tokens
- ✅ No NaN values in output
- ✅ No excessive character repetition
- ✅ At least 3 multi-letter words in output
- ✅ Coherent English text (not random garbage)

## Usage

### Basic Usage
```bash
dotnet run --model path/to/model.gguf
```

### With Custom Prompt
```bash
dotnet run --model path/to/model.gguf --prompt "Once upon a time" --max-tokens 100
```

### Full Options
```bash
dotnet run --model path/to/model.gguf \
           --prompt "The capital of France is" \
           --max-tokens 50 \
           --seed 42
```

## Arguments

| Argument | Required | Default | Description |
|----------|----------|---------|-------------|
| `--model` | Yes | - | Path to GGUF model file |
| `--prompt` | No | "The capital of France is" | Text prompt for generation |
| `--max-tokens` | No | 50 | Maximum number of tokens to generate |
| `--seed` | No | 42 | Random seed for reproducibility |

## Example Output

```
=== GGUF Model Validation ===
Model: models/smollm2-135m-instruct.Q8_0.gguf
Prompt: "The capital of France is"
Max tokens: 50
Seed: 42

Loading model...
Model architecture: llama
Context length: 2048, Embedding: 576
Layers: 30, Heads: 9 (KV: 3)
Loading weights from GGUF...
  token_embd.weight: Loaded 28963200 elements from token_embd.weight
  blk.0.attn_qkv.weight: Merged Q(331776) + K(110592) + V(110592) = 552960 elements
  ...
Model loaded in 2345ms

Encoding prompt...
Prompt tokens: 6

Generating tokens...
---
The capital of France is Paris. It is located in the north-central part of the country,
on the River Seine. Paris is known for its beautiful architecture, including the Eiffel
Tower, Notre-Dame Cathedral, and the Louvre Museum.
(EOS after 38 tokens)
---

=== Generation Metrics ===
Generated tokens: 38
Time to first token (TTFT): 245.32ms
Total generation time: 1823ms
Tokens per second: 20.47

=== Validation ===
✓ Generated 38 tokens
✓ Contains 25 multi-letter words
✓ No NaN values detected
✓ No excessive repetition

SUCCESS: Model validation passed!
```

## Exit Codes

- **0**: Success - Model passed all validation checks
- **1**: Failure - Model failed one or more checks, or error occurred

## Supported Models

This tool works with any GGUF model compatible with SmallMind:
- **Llama** (including SmolLM2)
- **Mistral**
- **Phi-3**

## Supported Quantization

- F32 (FP32)
- F16 (FP16, converted to FP32)
- Q8_0 (8-bit quantization)
- Q4_0 (4-bit quantization)

## Troubleshooting

### Model not loading
```
ERROR: GGUF file not found: path/to/model.gguf
```
→ Check the file path is correct and file exists

### Shape mismatch errors
```
ERROR: Shape mismatch for blk.0.attn_qkv.weight
```
→ Model architecture may not be fully supported. Check console output for details.

### NaN in output
```
FAIL: NaN detected in logits!
```
→ Weight loading may have failed. Check that the model architecture is supported.

### Low token count
```
FAIL: Generated only 5 tokens (expected >= 20)
```
→ Model may have hit EOS early. Try a different prompt or increase max-tokens.

## Integration Testing

To test the SmallMind GGUF integration with real models:

1. Download a compatible GGUF model:
   ```bash
   # Example: SmolLM2-135M (recommended for testing)
   wget https://huggingface.co/HuggingFaceTB/SmolLM2-135M-Instruct-GGUF/resolve/main/smollm2-135m-instruct-q8_0.gguf
   ```

2. Run validation:
   ```bash
   dotnet run --model smollm2-135m-instruct-q8_0.gguf
   ```

3. Check exit code:
   ```bash
   echo $?  # Should be 0 for success
   ```

## Technical Details

### Generation Strategy
- Uses greedy sampling (argmax) for deterministic output
- Enables KV cache for efficient autoregressive generation
- Handles BOS token prepending automatically based on model config
- Detects EOS tokens and stops generation early

### Validation Heuristics
1. **Token Count**: Must generate at least 20 tokens (or hit EOS)
2. **NaN Detection**: Checks logits for NaN values at each step
3. **Repetition Check**: Detects excessive repetition (>50 consecutive same chars)
4. **Word Count**: Requires at least 3 multi-letter words for coherence

These heuristics ensure the model is producing meaningful output rather than random noise or repeated patterns.

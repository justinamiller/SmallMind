# Standard Benchmark Prompts

This directory contains stable, deterministic prompts used for benchmarking.

## Prompts

### simple.txt
- **Purpose**: Quick sanity check, minimal context
- **Tokens**: ~6 tokens
- **Use**: Fast validation runs

### story.txt
- **Purpose**: Creative text generation  
- **Tokens**: ~10 tokens
- **Use**: General-purpose benchmarking

### instruction.txt
- **Purpose**: Instruction-following format
- **Tokens**: ~20 tokens
- **Use**: Testing instruct-tuned models

## Usage

Prompts are automatically used by the benchmark harness. You can also specify custom prompts via command-line:

```bash
dotnet run -c Release -- run --model <model> --prompt "Your custom prompt here"
```

## Adding New Prompts

1. Create `<name>.txt` file in this directory
2. Keep prompts short and deterministic
3. Avoid time-sensitive or variable content
4. Document expected token count and purpose

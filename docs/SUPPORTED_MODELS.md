# Supported Models and Architectures

This document describes the models, architectures, and quantization formats supported by SmallMind.

## Supported Architectures

SmallMind supports the following transformer architectures:

### ✅ Llama 2 and Llama 3
- **Supported Features**:
  - Grouped-Query Attention (GQA)
  - RoPE (Rotary Position Embeddings)
  - SwiGLU activation (Gated MLP with SiLU)
  - Multi-head attention
  - Layer normalization (RMSNorm)

- **Typical Configurations**:
  - Context length: 2048-4096 tokens
  - Hidden dimensions: 4096+ (scaled down for smaller models)
  - Number of heads: 32+ (scaled)
  - GQA groups: Typically 8 KV heads

- **Chat Template**: Llama 2 (`[INST]...`) or Llama 3 (`<|start_header_id|>...`)

### ✅ Mistral / Mistral-Instruct
- **Supported Features**:
  - Grouped-Query Attention (GQA)
  - RoPE (Rotary Position Embeddings)
  - SwiGLU activation
  - Sliding window attention ⚠️ **Partial**

- **Typical Configurations**:
  - Context length: 4096-8192 tokens
  - Hidden dimensions: 4096
  - Number of heads: 32
  - GQA: 8 KV heads

- **Chat Template**: Mistral Instruct (`[INST]...`)

- **⚠️ Note on Sliding Window Attention**:
  - Sliding window attention is **detected** but not yet fully implemented
  - Models using sliding window will load but may not generate optimal results
  - Full implementation planned for future release

### ✅ Phi (Microsoft)
- **Supported Features**:
  - Multi-head attention or GQA (depending on version)
  - Standard position embeddings or RoPE
  - GELU or SwiGLU activation
  - Layer normalization

- **Typical Configurations**:
  - Context length: 2048 tokens
  - Smaller hidden dimensions (2048-2560)
  - Fewer layers (24-32)

- **Chat Template**: Phi format (`User:.../Assistant:...`)

### ⚠️ Partially Supported / Future

#### Qwen / Qwen2
- Most features supported
- May require minor adjustments

#### Gemma
- Core architecture similar to Llama
- Should work with Llama template

## Supported Quantization Formats

### GGUF Quantization Types

SmallMind currently supports **TWO** quantization schemes when importing from GGUF:

| Quantization | Bits per Weight | Block Size | Status | Notes |
|--------------|----------------|------------|--------|-------|
| **Q8_0** | 8-bit | 32 elements | ✅ Fully Supported | Recommended for quality |
| **Q4_0** | 4-bit | 32 elements | ✅ Fully Supported | Recommended for memory savings |
| Q4_1 | 4-bit | 32 elements | ❌ Not Supported | - |
| Q5_0 | 5-bit | 32 elements | ❌ Not Supported | - |
| Q5_1 | 5-bit | 32 elements | ❌ Not Supported | - |
| Q6_K | 6-bit | 256 elements | ❌ Not Supported | K-quant format |
| Q4_K_S/M/L | 4-bit | 256 elements | ❌ Not Supported | K-quant format |
| Q5_K_S/M | 5-bit | 256 elements | ❌ Not Supported | K-quant format |
| Q8_K | 8-bit | 256 elements | ❌ Not Supported | K-quant format |

**Why only Q8_0 and Q4_0?**
- SmallMind uses a custom 64-element block size for its quantization format (SMQ)
- GGUF uses 32-element blocks for Q8_0/Q4_0 (2:1 ratio, easy conversion)
- K-quants use 256-element super-blocks (incompatible without major re-engineering)
- This design choice optimizes for SIMD vectorization on modern CPUs

### SMQ Format

After importing, models are stored in SmallMind's native SMQ format:

- **Block Size**: 64 elements (optimized for AVX-512/AVX2)
- **Quantization**: Q8_0 or Q4_0 (converted from GGUF)
- **Metadata**: Preserved from GGUF with model architecture info
- **Format**: Binary with separate manifest JSON

## Recommended Models

### Small Models (< 2GB, good for testing)

#### TinyLlama 1.1B Chat
- **HuggingFace**: `TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF`
- **Recommended File**: `tinyllama-1.1b-chat-v1.0.Q8_0.gguf` (~1.2 GB)
- **Architecture**: Llama 2
- **Context**: 2048 tokens
- **Use Case**: Testing, development, constrained devices

```bash
# Download
smallmind model download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF tinyllama-1.1b-chat-v1.0.Q8_0.gguf

# Import
smallmind import-gguf models/tinyllama-1.1b-chat-v1.0.Q8_0.gguf models/tinyllama.smq

# Generate
smallmind generate models/tinyllama.smq "What is the capital of France?" --chat-template llama2
```

### Medium Models (2-8GB)

#### Mistral 7B Instruct
- **HuggingFace**: `TheBloke/Mistral-7B-Instruct-v0.2-GGUF`
- **Recommended File**: `mistral-7b-instruct-v0.2.Q4_0.gguf` (~4 GB)
- **Architecture**: Mistral
- **Context**: 8192 tokens (use 4096 for better performance)
- **Use Case**: General instruction following, chat

```bash
# Download (Q4_0 for memory efficiency)
smallmind model download TheBloke/Mistral-7B-Instruct-v0.2-GGUF mistral-7b-instruct-v0.2.Q4_0.gguf

# Import
smallmind import-gguf models/mistral-7b-instruct-v0.2.Q4_0.gguf models/mistral-7b.smq

# Generate
smallmind generate models/mistral-7b.smq "Explain quantum computing" --chat-template mistral --max-tokens 256
```

#### Phi-2
- **HuggingFace**: `microsoft/phi-2-GGUF`
- **Recommended File**: `phi-2-q8_0.gguf` (~2.8 GB)
- **Architecture**: Phi
- **Context**: 2048 tokens
- **Use Case**: Coding, reasoning, efficient inference

### Larger Models (8GB+)

#### Llama 2 7B Chat
- **HuggingFace**: `TheBloke/Llama-2-7B-Chat-GGUF`
- **Recommended File**: `llama-2-7b-chat.Q4_0.gguf` (~3.8 GB)
- **Architecture**: Llama 2
- **Context**: 4096 tokens
- **Use Case**: General chat, instruction following

## Architecture Detection

SmallMind attempts to auto-detect the model architecture from:

1. **GGUF Metadata** (when available)
   - `general.architecture` field
   - `general.name` field
   - Specific architecture parameters

2. **Model Filename/Path** (fallback)
   - Matches keywords: "llama", "mistral", "phi", etc.
   - Used for chat template selection

3. **Manual Override** (always possible)
   - Specify chat template explicitly: `--chat-template llama2`
   - Configure architecture parameters in import

## Known Limitations

### ❌ Not Supported

1. **Quantization Formats**
   - K-quants (Q4_K, Q5_K, Q6_K, Q8_K, etc.)
   - Q4_1, Q5_0, Q5_1, Q6_0
   - F16, F32 GGUF files

2. **Architecture Features**
   - Sliding window attention (detected but not implemented)
   - Flash attention
   - ALiBi positional embeddings
   - Sparse attention patterns

3. **Model Types**
   - Vision-language models
   - Audio models
   - Mixture of Experts (MoE) - planned
   - RWKV/State Space Models

### ⚠️ Performance Notes

- **Context Length**: Models with very long context (>4096) may run slowly
- **Memory**: Quantized models still require significant RAM for KV cache
- **CPU-Only**: No GPU acceleration (by design)
- **Batch Size**: Currently limited to batch=1 for generation

## Compatibility Check

Before downloading a model, ensure:

1. ✅ Model is available in GGUF format
2. ✅ A Q8_0 or Q4_0 variant exists
3. ✅ Model uses supported architecture (Llama 2/3, Mistral, Phi)
4. ✅ Model size fits in your available RAM (model size + ~2GB for overhead)

### Quick Compatibility Test

```bash
# Download a small test model
smallmind model download TheBloke/TinyLlama-1.1B-Chat-v1.0-GGUF tinyllama-1.1b-chat-v1.0.Q8_0.gguf

# Import (will fail if unsupported)
smallmind import-gguf models/tinyllama-1.1b-chat-v1.0.Q8_0.gguf models/test.smq

# If successful, your system can run SmallMind models!
smallmind generate models/test.smq "Hello!" --chat-template auto
```

## Future Roadmap

### Planned Support

- [ ] Full sliding window attention
- [ ] Mixture of Experts (MoE) architectures
- [ ] Additional quantization formats (Q6_0 as middle ground)
- [ ] Batch inference
- [ ] Speculative decoding
- [ ] KV cache quantization

### Under Consideration

- [ ] Flash attention algorithms
- [ ] Longer context (via techniques like YaRN)
- [ ] Model merging/fine-tuning
- [ ] LoRA adapters

## Troubleshooting

### Error: "Unsupported tensor type"

**Cause**: Model uses a quantization format other than Q8_0 or Q4_0

**Solution**: Download a different variant of the model. Look for files ending in `Q8_0.gguf` or `Q4_0.gguf`

### Error: "Sliding window attention not supported"

**Cause**: Model uses sliding window attention (common in newer Mistral models)

**Solution**: SmallMind will detect this and warn you. The model may still work but with reduced quality.

### Poor Generation Quality

**Possible causes**:
- Using Q4_0 instead of Q8_0 (more quantization error)
- Wrong chat template selected
- Temperature/top-p settings too high or low
- Model not suited for task

**Solutions**:
- Try Q8_0 variant for better quality
- Use `--chat-template auto` to auto-detect
- Adjust `--temperature` (try 0.7-1.0)
- Try a different model architecture

## Getting Help

- **GitHub Issues**: Report bugs or compatibility issues
- **Discussions**: Ask questions about model support
- **Documentation**: Check the main README for workflow examples

---

**Last Updated**: 2026-02-07  
**SmallMind Version**: 1.0  
**Supported GGUF Version**: 3.x

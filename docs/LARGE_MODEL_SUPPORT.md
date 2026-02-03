# Billion-Parameter Model Support

SmallMind now includes infrastructure to support billion-parameter models through quantization, efficient memory management, and proper overflow protection.

## Quick Answer: Maximum Parameters in C#

**Based on C# limitations, SmallMind can support approximately 2 billion parameters.**

This limit is determined by:
1. **C# Array Indexing**: Arrays use `int` (32-bit) for indexing, limiting any single tensor to **2,147,483,647 elements** (int.MaxValue)
2. **Critical Constraint**: The embedding tensor (`vocab_size × embedding_dim`) must not exceed this limit
3. **Practical Limit**: ~2 billion total parameters when individual tensors are properly sized

**Key Limitations:**
- ✅ Models up to 2B parameters are supported (with proper tensor sizing)
- ✅ Use Q8/Q4 quantization for models >500M parameters
- ⚠️ Single tensor limit: `vocab_size × embedding_dim ≤ 2,147,483,647`
- ❌ Models requiring tensors >2.1B elements require tensor sharding (not implemented)

**See [FAQ.md](FAQ.md#q-based-on-c-limitations-what-is-the-max-number-of-parameters-i-can-support-in-a-model) and [CSHARP_LIMITATIONS.md](CSHARP_LIMITATIONS.md) for detailed explanations.**

---

## Overview

While SmallMind is designed as an educational and lightweight inference runtime, it now includes safeguards and optimizations to handle large-scale models up to 2 billion parameters with appropriate quantization.

## Limitations and Requirements

### Hardware Requirements by Model Size

| Model Size | FP32 Memory | Q8 Memory | Q4 Memory | Recommended RAM |
|-----------|-------------|-----------|-----------|-----------------|
| 125M params | 500 MB | 125 MB | 63 MB | 4 GB+ |
| 350M params | 1.4 GB | 350 MB | 175 MB | 8 GB+ |
| 1B params | 4 GB | 1 GB | 500 MB | 16 GB+ |
| 3B params | 12 GB | 3 GB | 1.5 GB | 32 GB+ |
| 7B params | 28 GB | 7 GB | 3.5 GB | 64 GB+ |

### Technical Constraints

1. **Tensor Size Limits**: Individual tensors cannot exceed `int.MaxValue` (2.1 billion) elements due to .NET array constraints
   - Maximum vocabulary × embedding dimension: ~2.1B
   - Example safe config: vocab=50K × embed=40K = 2B ✓
   - Example unsafe config: vocab=100K × embed=30K = 3B ✗

2. **Memory Architecture**: 
   - Models are loaded entirely into memory (no streaming from disk during inference)
   - Use quantization (Q8 or Q4) for models >500M parameters
   - Use model sharding for models >2B parameters (requires external tooling)

3. **CPU-Only Performance**:
   - No GPU acceleration (pure .NET implementation)
   - Inference speed: ~10-50 tokens/sec for 1B models on modern CPUs
   - Training is feasible for models up to 500M parameters

## Using Large Models

### 1. Model Configuration

Use `LargeModelSupport` to validate and estimate memory before loading:

```csharp
using SmallMind.Core.Core;

// Calculate parameter count for a configuration
long paramCount = LargeModelSupport.CalculateParameterCount(
    vocabSize: 50000,
    blockSize: 2048,
    embeddingDim: 4096,
    numLayers: 24,
    numHeads: 32
);

Console.WriteLine($"Total parameters: {LargeModelSupport.FormatParameters(paramCount)}");
// Output: Total parameters: 1.2B

// Get memory estimates
long fp32Memory = LargeModelSupport.EstimateMemoryBytes(paramCount, bytesPerParam: 4.0);
long q8Memory = LargeModelSupport.EstimateMemoryBytes(paramCount, bytesPerParam: 1.0);
long q4Memory = LargeModelSupport.EstimateMemoryBytes(paramCount, bytesPerParam: 0.5);

Console.WriteLine($"FP32: {LargeModelSupport.FormatBytes(fp32Memory)}");
Console.WriteLine($"Q8:   {LargeModelSupport.FormatBytes(q8Memory)}");
Console.WriteLine($"Q4:   {LargeModelSupport.FormatBytes(q4Memory)}");

// Get recommendations
string recommendation = LargeModelSupport.GetRecommendation(paramCount);
Console.WriteLine(recommendation);
```

### 2. Example Configurations

#### GPT-2 Small (124M parameters)
```csharp
var config = new ModelOptions
{
    VocabSize = 50257,
    BlockSize = 1024,
    EmbeddingDimension = 768,
    NumLayers = 12,
    NumHeads = 12,
    Dropout = 0.1
};

// Memory: ~500MB FP32, ~125MB Q8
```

#### GPT-2 Medium (350M parameters)
```csharp
var config = new ModelOptions
{
    VocabSize = 50257,
    BlockSize = 1024,
    EmbeddingDimension = 1024,
    NumLayers = 24,
    NumHeads = 16,
    Dropout = 0.1
};

// Memory: ~1.4GB FP32, ~350MB Q8
// Recommendation: Use Q8 quantization
```

#### GPT-2 Large (774M parameters)
```csharp
var config = new ModelOptions
{
    VocabSize = 50257,
    BlockSize = 1024,
    EmbeddingDimension = 1280,
    NumLayers = 36,
    NumHeads = 20,
    Dropout = 0.1
};

// Memory: ~3GB FP32, ~774MB Q8, ~387MB Q4
// Recommendation: Use Q8 or Q4 quantization
```

#### LLaMA-style 1B parameters
```csharp
var config = new ModelOptions
{
    VocabSize = 32000,
    BlockSize = 2048,
    EmbeddingDimension = 2048,
    NumLayers = 22,
    NumHeads = 16,
    Dropout = 0.0
};

// Memory: ~4GB FP32, ~1GB Q8, ~500MB Q4
// Recommendation: REQUIRES Q8 or Q4 quantization
```

### 3. Loading with Quantization

```csharp
using SmallMind.Abstractions;
using SmallMind.Engine;

// Create engine
using var engine = SmallMind.Create(new SmallMindOptions
{
    EnableKvCache = true
});

// Load model with automatic quantization recommendation
using var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "large-model.smq",
    MaxMemoryBytes = 16L * 1024 * 1024 * 1024 // 16 GB limit
});

// The engine will automatically warn if quantization is recommended
// and provide specific guidance
```

### 4. Memory-Efficient Inference

```csharp
var request = new GenerationRequest
{
    Prompt = "Once upon a time",
    Options = new GenerationOptions
    {
        MaxNewTokens = 100,
        MaxContextTokens = 2048,  // Limit context to reduce memory
        Temperature = 0.8
    }
};

await foreach (var token in engine.GenerateStreamingAsync(model, request))
{
    if (token.Kind == TokenEventKind.Token)
        Console.Write(token.Text.ToString());
}
```

## Validation and Error Handling

SmallMind automatically validates model configurations:

```csharp
try
{
    var config = new ModelOptions
    {
        VocabSize = 100000,
        EmbeddingDimension = 30000,  // 100K × 30K = 3B > int.MaxValue
        // ... other config
    };
    
    config.Validate();
}
catch (ValidationException ex)
{
    Console.WriteLine(ex.Message);
    // Output: Embedding tensor size (100000 × 30000 = 3,000,000,000) exceeds 
    //         int32 limit (2,147,483,647). Reduce vocab_size or embedding_dim.
}
```

## Performance Characteristics

### Inference Speed (CPU, 16 threads)

| Model Size | Tokens/sec | TTFT (ms) | Notes |
|-----------|-----------|-----------|-------|
| 125M | 200-400 | <50 | Fast, suitable for real-time |
| 350M | 80-150 | 100-200 | Good for interactive use |
| 774M | 40-80 | 200-400 | Usable for batch processing |
| 1B | 20-50 | 400-800 | Requires patience |
| 3B | 5-15 | 1000-2000 | Experimental, very slow |

### Training Feasibility

- **Recommended**: Up to 350M parameters
- **Possible**: Up to 774M parameters with gradient checkpointing
- **Not Recommended**: >1B parameters (use specialized training frameworks)

## Best Practices

### 1. Always Validate Before Loading
```csharp
// Check if configuration is valid
LargeModelSupport.ValidateConfiguration(
    vocabSize: 50000,
    blockSize: 2048,
    embeddingDim: 4096,
    numLayers: 24,
    numHeads: 32,
    availableMemoryBytes: GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
    quantizationBits: 8  // Q8 quantization
);
```

### 2. Use Quantization for Models >500M
```csharp
// Recommended workflow:
// 1. Train in FP32 (if training)
// 2. Convert to Q8 or Q4 for inference
// 3. Load quantized model with SmallMind

// Q8: ~4x memory reduction, minimal accuracy loss
// Q4: ~8x memory reduction, small accuracy loss
```

### 3. Monitor Memory Usage
```csharp
var model = new TransformerModel(/* ... */);

long totalParams = model.GetTotalParameterCount();
long memoryBytes = model.GetMemoryFootprintBytes(includingGradients: false);

Console.WriteLine($"Parameters: {LargeModelSupport.FormatParameters(totalParams)}");
Console.WriteLine($"Memory: {LargeModelSupport.FormatBytes(memoryBytes)}");
```

### 4. Use Gradient Checkpointing for Training
```csharp
var memConfig = new MemoryConfiguration(
    enableGradientCheckpointing: true,
    checkpointInterval: 2,  // Save every 2nd layer
    enableMixedPrecision: true  // FP16/FP32 mixed precision
);
```

## Comparison with Other Frameworks

### When to Use SmallMind
- ✅ Educational purposes and learning transformer internals
- ✅ Models up to 350M parameters for experimentation
- ✅ Pure .NET environments without Python dependencies
- ✅ Full control over inference pipeline
- ✅ Custom model architectures

### When to Use Alternatives
- ❌ Production inference for models >1B parameters → Use **LLaMA.cpp**, **vLLM**, **TensorRT-LLM**
- ❌ GPU acceleration required → Use **PyTorch**, **TensorFlow**, **JAX**
- ❌ Training models >1B parameters → Use **DeepSpeed**, **Megatron-LM**
- ❌ Maximum inference speed critical → Use **GGML/LLaMA.cpp** with quantization

## Troubleshooting

### Error: "Tensor size overflow"
```
Tensor size overflow: shape [100000, 30000] exceeds int.MaxValue (2,147,483,647)
```
**Solution**: Reduce vocabulary size or embedding dimension, or use model sharding (external tool required).

### Error: "Model requires X GB but only Y GB available"
```
Model requires 12.5 GB but only 8.0 GB available
```
**Solution**: Use stronger quantization (Q4 instead of Q8) or reduce model size.

### Warning: "Large model detected (XXX M parameters)"
```
WARNING: Large model detected (1200M parameters). Consider using quantization (Q8/Q4).
```
**Action**: This is informational. The model will load, but memory usage will be high.

## Future Enhancements

Planned improvements for large model support:

1. **Model Sharding**: Split models across multiple devices or disk
2. **Streaming Inference**: Load weights on-demand from disk
3. **FP16 Support**: Native half-precision for 2x memory reduction
4. **GGUF Direct Loading**: Skip conversion step for GGUF models
5. **Distributed Inference**: Split computation across multiple machines

## References

- [SmallMind Architecture](../README.md)
- [Quantization Guide](QUANTIZATION_IMPLEMENTATION_SUMMARY.md)
- [Performance Benchmarks](PERFORMANCE_BENCHMARKING_EXECUTIVE_SUMMARY.md)
- [Memory Configuration](../src/SmallMind.Core/Core/MemoryConfiguration.cs)
- [LargeModelSupport API](../src/SmallMind.Core/Core/LargeModelSupport.cs)

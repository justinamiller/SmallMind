# Parameter Limits Demo

This example demonstrates the C# and .NET limitations that constrain the maximum number of parameters in SmallMind models.

## What This Demo Shows

1. **C# Array Indexing Limit**: The fundamental constraint of `int.MaxValue` (2,147,483,647 elements)
2. **Safe Configurations**: Examples of models that work within the limits
3. **Unsafe Configurations**: What happens when you exceed the limits
4. **Memory Estimation**: How to calculate memory requirements for different quantization levels
5. **Validation Workflow**: Best practices for validating model configurations before loading

## Running the Demo

```bash
cd examples/ParameterLimitsDemo
dotnet run
```

## Key Takeaways

### Maximum Parameters: ~2 Billion

Based on C# limitations, SmallMind can support approximately **2 billion parameters** when:
- Individual tensors don't exceed `int.MaxValue` (2,147,483,647 elements)
- The embedding tensor (`vocab_size × embedding_dim`) stays under this limit
- Appropriate quantization (Q8/Q4) is used for models >500M parameters

### Critical Constraint

The largest tensor in most transformer models is the embedding tensor:
```
vocab_size × embedding_dim ≤ 2,147,483,647
```

**Safe Examples:**
- ✅ vocab=50,000 × embed=40,000 = 2,000,000,000 (93% of limit)
- ✅ vocab=32,000 × embed=2,048 = 65,536,000 (3% of limit)

**Unsafe Example:**
- ❌ vocab=100,000 × embed=30,000 = 3,000,000,000 (exceeds limit)

### Recommended Limits by Use Case

| Use Case | Model Size | SmallMind Suitability |
|----------|-----------|----------------------|
| Learning & Experimentation | <350M | ✅ Excellent |
| Development & Testing | 350M-774M | ✅ Good with Q8 |
| Small-scale Inference | 774M-1B | ⚠️ Possible with Q4 |
| Medium-scale Inference | 1B-2B | ⚠️ Slow, use Q4 |
| Production Inference | >2B | ❌ Use LLaMA.cpp/vLLM |

## Code Examples

### Calculate Parameter Count

```csharp
using SmallMind.Core.Core;

long paramCount = LargeModelSupport.CalculateParameterCount(
    vocabSize: 32000,
    blockSize: 2048,
    embeddingDim: 2048,
    numLayers: 22,
    numHeads: 16
);

Console.WriteLine($"Parameters: {LargeModelSupport.FormatParameters(paramCount)}");
// Output: Parameters: 1.24B
```

### Validate Configuration

```csharp
try
{
    LargeModelSupport.ValidateConfiguration(
        vocabSize: 50000,
        blockSize: 2048,
        embeddingDim: 4096,
        numLayers: 24,
        numHeads: 32,
        availableMemoryBytes: 16L * 1024 * 1024 * 1024, // 16GB
        quantizationBits: 8 // Q8 quantization
    );
    Console.WriteLine("✓ Configuration is valid");
}
catch (ValidationException ex)
{
    Console.WriteLine($"✗ Error: {ex.Message}");
}
```

### Estimate Memory Requirements

```csharp
long paramCount = 1_000_000_000L; // 1B parameters

long fp32 = LargeModelSupport.EstimateMemoryBytes(paramCount, 4.0);
long q8 = LargeModelSupport.EstimateMemoryBytes(paramCount, 1.0);
long q4 = LargeModelSupport.EstimateMemoryBytes(paramCount, 0.5);

Console.WriteLine($"FP32: {LargeModelSupport.FormatBytes(fp32)}"); // 4.00 GB
Console.WriteLine($"Q8:   {LargeModelSupport.FormatBytes(q8)}");   // 1.00 GB
Console.WriteLine($"Q4:   {LargeModelSupport.FormatBytes(q4)}");   // 512.00 MB
```

## Understanding the Output

The demo shows:

1. **Safe configurations** that work within C# limits
2. **Unsafe configurations** that trigger validation errors
3. **Memory estimates** for different quantization levels
4. **Recommendations** based on model size

Pay attention to:
- The "Largest Tensor" size relative to int.MaxValue
- Recommendations for quantization based on parameter count
- Memory requirements for inference vs. training

## Related Documentation

- [FAQ.md](../../docs/FAQ.md) - Frequently asked questions
- [CSHARP_LIMITATIONS.md](../../docs/CSHARP_LIMITATIONS.md) - Technical details on .NET constraints
- [LARGE_MODEL_SUPPORT.md](../../docs/LARGE_MODEL_SUPPORT.md) - Comprehensive large model guide

## Why These Limits Exist

C# arrays use `int` (32-bit signed integer) for indexing, which is a .NET CLR limitation. This means:
- Any single array can have at most 2,147,483,647 elements
- Each tensor in SmallMind uses a single `float[]` array
- Large tensors (like embeddings) must respect this limit

Other frameworks (Python/NumPy, C++/PyTorch) can exceed this limit because:
- NumPy uses `intp` (64-bit on 64-bit systems)
- C++ has no intrinsic array size limits
- They use native memory management instead of the CLR

## Next Steps

After running this demo:
1. Review the [FAQ](../../docs/FAQ.md) for more details
2. Check the [C# Limitations Reference](../../docs/CSHARP_LIMITATIONS.md) for technical deep-dive
3. Explore the [Large Model Support Guide](../../docs/LARGE_MODEL_SUPPORT.md) for practical guidance

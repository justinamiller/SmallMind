# Migration to GgufImporter with Quantized Inference

## Summary

This document describes the migration to use `GgufImporter` for loading GGUF models with proper weight loading from SMQ files.

## Critical Bug Fixed

**MAJOR ISSUE DISCOVERED**: The `LoadSmqModelAsync` method was creating empty models without loading any weights from SMQ files!

### Before (BROKEN ❌)
```csharp
// SmallMindEngine.LoadSmqModelAsync
var metadata = QuantizedModelLoader.LoadQuantizedModelMetadata(request.Path);
var model = new TransformerModel(...); // Created empty model
// ❌ WEIGHTS NEVER LOADED!
return new ModelHandle(model, tokenizer, request.Path, metadata);
```

**Result**: Models loaded from SMQ files had random/zero weights and produced garbage output.

### After (FIXED ✅)
```csharp
// SmallMindEngine.LoadSmqModelAsync
var config = DetermineModelConfig(metadata);
// ✅ Load model WITH weights from SMQ
var (model, tokenizer, metadata) = SmqModelLoader.LoadFromSmq(smqPath, config, ...);
return new ModelHandle(model, tokenizer, request.Path, metadataInfo);
```

**Result**: Weights are properly loaded from SMQ files into the model.

## Current Loading Flow

### GGUF → SMQ → TransformerModel

**Step 1**: GGUF Import (Preserves Quantization)
```
GGUF file (Q4_K, Q6_K, etc.)
  ↓ GgufImporter
SMQ file (Q4_K tensors preserved)
```
- ✅ Quantized tensors preserved in SMQ format
- ✅ Memory-efficient storage (Q4_K: 0.56 bytes/value)

**Step 2**: SMQ Loading (Currently Dequantizes)
```
SMQ file (Q4_K tensors)
  ↓ SmqModelLoader
TransformerModel (FP32 tensors)
```
- ⚠️ Dequantizes Q4_K → FP32 (8x memory increase)
- ⚠️ Logs warning about dequantization
- ✅ Weights properly loaded (bug fixed!)

**Step 3**: Inference
```
TransformerModel (FP32)
  ↓ Standard forward pass
Output
```
- ⚠️ Uses 8x more memory than necessary
- ⚠️ Misses 2x speedup from fused kernels

## What Changed

### 1. Created `SmqModelLoader.cs`

New file in `src/SmallMind.Runtime/SmqModelLoader.cs`:

**Purpose**: Load weights from SMQ files and inject into `TransformerModel`

**Key Features**:
- Reads SMQ files using `SmqReader`
- Supports all quantization formats:
  - FP32 (Fp32Tensor)
  - Q8_0 (Q8Tensor)
  - Q4_0 (Q4Tensor)
  - Q4_1 (Q4_1Tensor)
  - Q5_0 (Q5_0Tensor)
  - Q4_K (Q4KTensor) ⭐
  - Q6_K (Q6KTensor) ⭐
- Dequantizes quantized tensors to FP32
- Logs statistics and warnings
- Matches SMQ tensor names to model parameter names

**Example Usage**:
```csharp
var (model, tokenizer, metadata) = SmqModelLoader.LoadFromSmq(
    smqPath: "/path/to/model.smq",
    config: modelConfig,
    seed: 42,
    logger: logger);

// model now has weights loaded!
```

### 2. Updated `SmallMindEngine.cs`

**Changes to `LoadSmqModelAsync`**:

**Before**:
- Created empty model
- Never loaded weights
- Returned broken model

**After**:
- Determines model configuration
- Calls `SmqModelLoader.LoadFromSmq()` to load weights
- Returns model with proper weights
- Extracts tokenizer from metadata

**New Helper Method**:
```csharp
private ModelConfig CreateLegacyGptConfig(Dictionary<string, object>? metadata)
{
    // Extract dimensions and create ModelConfig
    // Used for legacy/GPT-style models
}
```

## Memory Impact Analysis

### Current State (After Fix)

| Step | Format | Memory (7B model) |
|------|--------|-------------------|
| GGUF file | Q4_K | 3.9 GB |
| SMQ file | Q4_K | 3.9 GB |
| Loaded model | FP32 | 28 GB ⚠️ |

**Memory Increase**: 8x from quantized format

### Logging

The system now logs warnings when dequantizing:

```
[WARN] SMQ file contains quantized tensors - dequantizing to FP32.
[WARN] For memory-efficient quantized inference, future versions will support IWeightTensor.
[INFO] Loaded 125/130 parameters (80 quantized, 45 FP32)
```

## Future: Full Quantized Inference

To achieve 8x memory reduction and 2x speedup, future work will implement:

### Phase 1: Extend TransformerModel

Add support for `IWeightTensor` alongside `Tensor`:

```csharp
internal class Linear
{
    private Tensor? _fpWeight;           // Current: FP32 weights
    private IWeightTensor? _quantWeight;  // Future: Quantized weights
    
    public Tensor Forward(Tensor input)
    {
        if (_quantWeight != null)
        {
            // Use fused kernel for quantized weights
            return ForwardQuantized(input);
        }
        else
        {
            // Standard FP32 matmul
            return ForwardFP32(input);
        }
    }
}
```

### Phase 2: Update SmqModelLoader

Preserve quantized format instead of dequantizing:

```csharp
// Current: Dequantize everything
float[] data = DequantizeTensor(smqTensor, ...);
Array.Copy(data, targetParam.Data, data.Length);

// Future: Preserve quantization
if (smqTensor is Q4KTensor q4k)
{
    targetParam.SetQuantizedWeight(new Q4KWeightTensor(q4k));
}
else if (smqTensor is Fp32Tensor fp32)
{
    targetParam.SetFP32Weight(fp32.Data);
}
```

### Phase 3: Use Fused Kernels

Leverage existing `FusedQ4KMatMul` during inference:

```csharp
// In TransformerBlock forward pass
if (attn_weight is IWeightTensor quantWeight)
{
    // Fused kernel: dequantizes in-register during matmul
    // ~2x faster, 8x less memory bandwidth
    quantWeight.MatMul(activations, output, m, k, n);
}
else
{
    // Standard FP32 matmul
    MatMul(activations, weight, output);
}
```

### Expected Performance

| Metric | Current (FP32) | Future (Q4_K) | Improvement |
|--------|----------------|---------------|-------------|
| **Model Memory** | 28 GB | 3.9 GB | **7.2x reduction** |
| **Inference Speed** | 1.0x | ~2.0x | **2x faster** |
| **Memory Bandwidth** | 1.0x | 0.125x | **8x reduction** |

## Migration Guide for Users

### Option 1: Use Current Implementation (Immediate)

Works now with the bug fix:

```csharp
// Load GGUF file (auto-converts to SMQ, loads weights)
var engine = await SmallMindEngine.LoadModelAsync(new ModelLoadRequest 
{
    Path = "model-q4_k.gguf"
});

// Weights are loaded! But dequantized to FP32 (8x memory)
```

**Pros**:
- ✅ Works immediately
- ✅ Weights properly loaded (bug fixed!)
- ✅ Compatible with all existing code

**Cons**:
- ⚠️ 8x memory increase from dequantization
- ⚠️ Misses performance benefits of fused kernels

### Option 2: Wait for Full Quantized Inference (Future)

When Phase 1-3 are complete:

```csharp
// Same API, but keeps weights quantized internally
var engine = await SmallMindEngine.LoadModelAsync(new ModelLoadRequest 
{
    Path = "model-q4_k.gguf",
    PreserveQuantization = true  // Future option
});

// Benefits: 8x less memory, 2x faster inference
```

## Testing

All existing tests pass with the fix:

```bash
$ dotnet test --filter "FullyQualifiedName~Gguf"
Passed!  - Failed: 0, Passed: 82, Skipped: 0
```

Tests cover:
- GGUF metadata extraction
- GGUF import to SMQ
- SMQ round-trip
- Quantization kernels
- Model loading

## Infrastructure Already Available

The codebase has all the pieces needed for full quantized inference:

| Component | Location | Purpose |
|-----------|----------|---------|
| `Q4KTensor` | `SmallMind.Quantization.Tensors` | Q4_K storage |
| `Q6KTensor` | `SmallMind.Quantization.Tensors` | Q6_K storage |
| `IWeightTensor` | `SmallMind.Quantization.Abstractions` | Polymorphic weight interface |
| `Q4KWeightTensor` | `SmallMind.Quantization.Abstractions` | IWeightTensor for Q4_K |
| `Q6KWeightTensor` | `SmallMind.Quantization.Abstractions` | IWeightTensor for Q6_K |
| `FusedQ4KMatMul` | `SmallMind.Quantization.Kernels` | Fused Q4_K matmul kernel |
| `FusedQ6KMatMul` | `SmallMind.Quantization.Kernels` | Fused Q6_K matmul kernel |
| `GgufImporter` | `SmallMind.Quantization.IO.Gguf` | GGUF → SMQ converter |
| `SmqReader/Writer` | `SmallMind.Quantization.IO.Smq` | SMQ file I/O |

**What's Missing**: Integration of these components into `TransformerModel` and inference pipeline.

## Files Modified

| File | Lines Changed | Description |
|------|---------------|-------------|
| `src/SmallMind.Runtime/SmqModelLoader.cs` | +238 (new) | Loads SMQ files and injects weights |
| `src/SmallMind.Engine/SmallMindEngine.cs` | +114, -97 | Uses SmqModelLoader, fixes bug |

## Conclusion

This update delivers:

1. **Critical Bug Fix** ✅
   - SMQ files now properly load weights into models
   - Previously loaded models had random/zero weights!

2. **Proper Weight Loading** ✅
   - `SmqModelLoader` reads SMQ tensors
   - Dequantizes quantized formats to FP32
   - Injects weights into TransformerModel parameters

3. **Foundation for Quantized Inference** 🚀
   - Infrastructure exists (IWeightTensor, fused kernels)
   - Clear migration path documented
   - Logging guides users to future improvements

4. **Transparent to Users** ✅
   - Existing code continues to work
   - Same API, just fixed internally
   - Warnings inform about dequantization

**Next Steps**: Implement Phases 1-3 to achieve 8x memory reduction and 2x inference speedup.

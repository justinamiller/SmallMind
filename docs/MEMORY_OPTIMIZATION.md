# Memory Optimization for Large Context Windows

This document describes the memory optimization features added to SmallMind to support large context windows (32k+ tokens) on systems with limited RAM (16GB+).

## Overview

SmallMind now includes comprehensive memory optimization techniques that enable training and inference with context sizes up to 128k tokens, depending on available system RAM. These optimizations are inspired by modern large language model implementations while using only native .NET libraries.

## Features

### 1. Sliding Window Attention

Process very large sequences by breaking them into overlapping windows. This allows you to work with sequences that exceed your memory capacity by processing them in chunks.

**Key Benefits:**
- Handle 32k+ token sequences on limited RAM
- Configurable window size and overlap
- Multiple merge strategies (averaging, max)

**Usage:**

```csharp
using SmallMind.Core.Core;

// Create processor with 4k windows and 2k overlap (50%)
var processor = new SlidingWindowProcessor(
    windowSize: 4096,
    stride: 2048);

// Process a 32k token sequence
var tokens = new int[32768];
foreach (var window in processor.GetWindows(tokens))
{
    // Process each window through your transformer
    var output = model.Forward(window);
}

// Combine outputs from all windows
var combined = processor.CombineWindowOutputs(outputs, originalSeqLength: 32768);
```

### 2. Memory-Mapped KV Cache

Offload key-value attention caches to disk using memory-mapped files. This dramatically reduces RAM usage for large context windows at the cost of some I/O overhead.

**Key Benefits:**
- Reduce in-memory storage of attention caches
- Support for multi-layer caching
- Automatic cleanup of temporary files

**Usage:**

```csharp
// In-memory cache (for smaller contexts)
using var cache = new KVCache(capacity: 100000);
cache.Write(0, 1.5f);
float value = cache.Read(0);

// Memory-mapped cache (for larger contexts)
using var cache = new KVCache(
    fileName: "/tmp/kv_cache.bin",
    capacity: 1000000,
    useMemoryMapping: true);

// Multi-layer cache for transformer
using var mlCache = new MultiLayerKVCache(
    numLayers: 6,
    maxSeqLen: 32768,
    numHeads: 8,
    headDim: 64,
    useMemoryMapping: true,
    cacheDirectory: "/tmp/cache");

var keyCache = mlCache.GetKeyCache(layerIndex: 0);
var valueCache = mlCache.GetValueCache(layerIndex: 0);
```

### 3. Dynamic Memory Configuration

Automatically configure model parameters based on available system RAM.

**Key Benefits:**
- Auto-detection of available RAM
- Recommended token limits for different RAM sizes
- Memory usage estimation
- Configuration validation

**RAM-to-Token Mapping:**
- 8 GB → ~8k tokens
- 16 GB → 32k tokens
- 32 GB → 48k tokens
- 64 GB → 64k tokens
- 128 GB → 128k tokens

**Usage:**

```csharp
// Auto-detect system memory
var config = new MemoryConfiguration(
    enableGradientCheckpointing: true,
    enableMixedPrecision: true,
    enableMemoryMapping: false,
    checkpointInterval: 2);

Console.WriteLine(config.GetSummary());
// Output:
// Memory Configuration:
//   System Memory: 16.00 GB
//   Max Context Tokens: 32,768
//   Gradient Checkpointing: Enabled (Interval: 2)
//   Mixed Precision: Enabled (FP16/FP32)
//   ...

// Check if your model will fit
bool canFit = config.CanFitInMemory(
    vocabSize: 50000,
    embeddingDim: 512,
    numLayers: 6,
    numHeads: 8,
    batchSize: 1,
    seqLength: 4096);

// Estimate memory usage
long memoryBytes = config.EstimateMemoryUsage(
    vocabSize: 50000,
    embeddingDim: 512,
    numLayers: 6,
    numHeads: 8,
    batchSize: 1,
    seqLength: 4096);
```

### 4. Gradient Checkpointing (Already Exists)

Trade computation for memory by selectively storing activations during the forward pass and recomputing them during backpropagation.

**Key Benefits:**
- Significant memory savings (up to 50-75%)
- Configurable checkpoint intervals
- Automatic calculation of optimal intervals

**Usage:**

```csharp
// Calculate optimal checkpoint interval
int interval = GradientCheckpointing.GetOptimalCheckpointInterval(
    numLayers: 12,
    availableMemoryBytes: 8L * 1024 * 1024 * 1024, // 8GB
    perLayerBytes: 100 * 1024 * 1024, // 100MB per layer
    strategy: CheckpointStrategy.SqrtLayers);

// Estimate memory savings
var (memWithout, memWith, savings) = 
    GradientCheckpointing.EstimateMemorySavings(
        numLayers: 12,
        perLayerBytes: 100 * 1024 * 1024,
        checkpointInterval: interval);

Console.WriteLine($"Memory savings: {savings:F1}%");

// Use CheckpointManager in your training loop
var checkpointMgr = new CheckpointManager(
    checkpointInterval: interval,
    enabled: true);
```

### 5. Mixed Precision (Already Exists)

Use FP16 for memory-intensive operations and FP32 for precision-critical operations.

**Key Benefits:**
- ~50% memory savings for model weights
- Faster computation on modern hardware
- Dynamic loss scaling to prevent underflow

**Usage:**

```csharp
// Convert between precisions
var fp32Data = new float[] { 1.5f, 2.5f, 3.5f };
var fp16Data = new Half[fp32Data.Length];

MixedPrecision.FloatToHalf(fp32Data, fp16Data);
// ... use FP16 data ...

var fp32Result = new float[fp16Data.Length];
MixedPrecision.HalfToFloat(fp16Data, fp32Result);

// Check for gradient overflow
bool hasOverflow = MixedPrecision.HasGradientOverflow(gradients);

// Use MixedPrecisionTrainer for training
var trainer = new MixedPrecisionTrainer(
    optimizer: adamOptimizer,
    parameters: modelParameters,
    initialLossScale: 65536f);

// Training loop
trainer.SyncToFP16(parameters);
var loss = ComputeLoss();
if (trainer.CheckAndUnscaleGradients(parameters))
{
    optimizer.Step();
    trainer.UpdateMasterWeights(parameters);
}
```

## Complete Example

Here's a complete example combining all optimizations:

```csharp
using SmallMind.Core.Core;
using System;

// 1. Configure for your system
var config = new MemoryConfiguration(
    memoryGB: 16, // Or auto-detect
    enableGradientCheckpointing: true,
    enableMixedPrecision: true,
    enableMemoryMapping: true,
    checkpointInterval: 2);

Console.WriteLine(config.GetSummary());

// 2. Verify your model will fit
bool canFit = config.CanFitInMemory(
    vocabSize: 50000,
    embeddingDim: 384,
    numLayers: 6,
    numHeads: 6,
    batchSize: 1,
    seqLength: 4096);

if (!canFit)
{
    Console.WriteLine("Model too large! Consider:");
    Console.WriteLine("- Reducing embedding dimension");
    Console.WriteLine("- Using fewer layers");
    Console.WriteLine("- Enabling more aggressive checkpointing");
    return;
}

// 3. Set up sliding window processor
var windowProcessor = new SlidingWindowProcessor(
    windowSize: config.SlidingWindowSize, // Auto-configured
    stride: config.SlidingWindowStride);

// 4. Create KV cache
using var kvCache = new MultiLayerKVCache(
    numLayers: 6,
    maxSeqLen: config.SlidingWindowSize,
    numHeads: 6,
    headDim: 64,
    useMemoryMapping: config.EnableMemoryMapping,
    cacheDirectory: "/tmp/kv_cache");

// 5. Process a large sequence
var largeContext = new int[32768]; // 32k tokens
// ... fill with actual tokens ...

var windowOutputs = new List<Tensor>();
foreach (var window in windowProcessor.GetWindows(largeContext))
{
    // Process window through transformer
    var output = model.Forward(window);
    windowOutputs.Add(output);
}

// 6. Combine results
var finalOutput = windowProcessor.CombineWindowOutputs(
    windowOutputs,
    originalSeqLength: largeContext.Length);

Console.WriteLine($"Successfully processed {largeContext.Length:N0} tokens!");
```

## Performance Characteristics

### Memory Usage

With all optimizations enabled:
- **Gradient Checkpointing**: ~50-75% reduction in activation memory
- **Mixed Precision**: ~50% reduction in weight memory
- **Memory Mapping**: KV cache moved to disk (near-zero RAM usage)
- **Sliding Windows**: Constant memory regardless of sequence length

### Computation Overhead

- **Gradient Checkpointing**: ~33% increase in training time (worth it for memory savings)
- **Mixed Precision**: ~10-20% faster on modern CPUs with FP16 support
- **Memory Mapping**: Slower than RAM but enables much larger contexts
- **Sliding Windows**: Linear scaling with sequence length

## Best Practices

1. **Start Conservative**: Begin with aggressive checkpointing and sliding windows, then relax if memory allows
2. **Profile First**: Use `MemoryConfiguration.EstimateMemoryUsage()` before training
3. **Monitor Memory**: Watch for OOM errors and adjust checkpoint intervals
4. **Batch Size**: Keep batch size = 1 for very large contexts
5. **Test Incrementally**: Start with smaller sequences and scale up
6. **Disk I/O**: For memory mapping, use SSD for better performance

## Troubleshooting

### Out of Memory Errors

1. Increase checkpoint interval (more aggressive checkpointing)
2. Enable memory mapping for KV cache
3. Reduce window size
4. Use smaller model (fewer layers, smaller embedding)
5. Enable mixed precision if not already on

### Slow Performance

1. Reduce checkpoint interval (trade memory for speed)
2. Disable memory mapping if RAM is available
3. Increase window size (fewer windows to process)
4. Use batch processing if memory allows
5. Ensure SSD is used for memory-mapped files

### Accuracy Issues

1. Mixed precision can reduce accuracy - try disabling it
2. Increase loss scaling if seeing gradient underflow
3. Ensure proper normalization in sliding window combination
4. Verify checkpoint interval isn't too aggressive

## See Also

- [MemoryOptimizationExample.cs](../../samples/MemoryOptimizationExample.cs) - Complete working examples
- [MemoryOptimizationIntegrationTests.cs](../../tests/SmallMind.Tests/MemoryOptimizationIntegrationTests.cs) - Integration tests
- [SmallMind Performance Guide](../PERFORMANCE.md) - General performance tips

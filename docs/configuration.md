# Configuration Guide

This guide covers configuration options for SmallMind, including dependency injection setup, training configuration, and runtime options.

## Table of Contents

- [Dependency Injection Setup](#dependency-injection-setup)
- [Training Configuration](#training-configuration)
- [Model Configuration](#model-configuration)
- [Logging Configuration](#logging-configuration)
- [Performance Configuration](#performance-configuration)

## Dependency Injection Setup

SmallMind integrates with .NET's dependency injection container for production use.

### Basic Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmallMind.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

// Add SmallMind services
builder.Services.AddSmallMind(options =>
{
    options.ModelPath = "path/to/model.json";
    options.VocabSize = 256;
    options.BlockSize = 128;
    options.EmbeddingDimension = 64;
    options.NumLayers = 4;
    options.NumHeads = 4;
    options.Dropout = 0.1;
});

var host = builder.Build();
await host.RunAsync();
```

### Service Lifetime

All SmallMind services are registered as **Singleton** by default for optimal performance and resource sharing:

- `TransformerModel`: Singleton (thread-safe for inference)
- `Tokenizer`: Singleton (immutable after creation)
- `Sampling`: Scoped (if registered separately for request-specific state)

## Training Configuration

Training behavior can be customized through `TrainingConfig` and `TrainingOptions`.

### TrainingConfig Properties

```csharp
var config = new TrainingConfig
{
    // Mixed precision training (experimental)
    UseMixedPrecision = false,  // Default: false
    
    // Gradient checkpointing to reduce memory usage
    UseGradientCheckpointing = false,  // Default: false
    CheckpointStrategy = CheckpointStrategy.None,  // or EveryLayer
    
    // Training diagnostics and monitoring
    EnableDiagnostics = true,  // Default: true
    
    // Gradient accumulation
    AccumulationSteps = 1  // Default: 1 (no accumulation)
};
```

### Training Options (via DI)

```csharp
builder.Services.Configure<TrainingOptions>(options =>
{
    options.DefaultLearningRate = 0.001;
    options.DefaultBatchSize = 32;
    options.DefaultBlockSize = 128;
    options.SaveCheckpointEvery = 1000;  // steps
    options.LogEvery = 100;  // steps
    options.ValidateEvery = 500;  // steps
    options.CheckpointDirectory = "./checkpoints";
});
```

## Model Configuration

Configure the Transformer model architecture:

```csharp
var model = new TransformerModel(
    vocabSize: 256,           // Number of unique tokens
    blockSize: 128,           // Context length
    nEmbd: 64,                // Embedding dimension
    nLayer: 4,                // Number of transformer layers
    nHead: 4,                 // Number of attention heads
    dropout: 0.1,             // Dropout rate (0.0 to 1.0)
    seed: 42                  // Random seed for reproducibility
);
```

### Recommended Configurations

#### Tiny Model (Fast Training, Limited Capacity)
```csharp
vocabSize: 256
blockSize: 64
nEmbd: 32
nLayer: 2
nHead: 2
dropout: 0.0
```

#### Small Model (Balanced)
```csharp
vocabSize: 256
blockSize: 128
nEmbd: 64
nLayer: 4
nHead: 4
dropout: 0.1
```

#### Medium Model (Better Quality, Slower)
```csharp
vocabSize: 256
blockSize: 256
nEmbd: 128
nLayer: 6
nHead: 8
dropout: 0.1
```

## Logging Configuration

SmallMind uses `Microsoft.Extensions.Logging` for structured logging.

### Setup Logging

```csharp
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Set minimum log level
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Filter SmallMind logs
builder.Logging.AddFilter("SmallMind", LogLevel.Debug);
```

### Log Levels

- **Trace**: Detailed execution flow (rarely needed)
- **Debug**: Training metrics, gradient health, performance diagnostics
- **Information**: Training progress, checkpoint saves, validation results
- **Warning**: Numerical instability detected, suboptimal configurations
- **Error**: Training failures, checkpoint errors, validation errors
- **Critical**: Unrecoverable errors

### Key Log Events

| Event ID | Category | Description |
|----------|----------|-------------|
| 1000 | TrainingStep | Logged every N steps with loss and timing |
| 1001 | ValidationLoss | Logged after validation runs |
| 1002 | CheckpointSaved | Logged when checkpoint is saved |
| 1003 | TrainingCompleted | Logged at end of training |
| 1004 | TrainingCancelled | Logged when training is cancelled |
| 2001 | GradientHealth | Logged during gradient health checks |
| 2002 | NumericalInstability | Logged when NaN/Inf detected |

## Performance Configuration

### SIMD Acceleration

SIMD is automatically enabled and detected at runtime. No configuration needed.

To verify SIMD support:
```csharp
using SmallMind.Simd;

var caps = SimdCapabilities.Detect();
Console.WriteLine($"Using: {caps.InstructionSet}");
Console.WriteLine($"Vector size: {caps.VectorSize} floats");
```

### Thread Pool Configuration

For CPU-bound operations, configure the thread pool:

```csharp
// In Program.cs or Startup
ThreadPool.SetMinThreads(
    workerThreads: Environment.ProcessorCount,
    completionPortThreads: Environment.ProcessorCount
);
```

### Memory Pool Configuration

SmallMind uses `MemoryPool<float>` for temporary allocations. Default configuration is optimal for most scenarios.

Custom pool (advanced):
```csharp
var pool = new MemoryPool(
    maxBufferSize: 1024 * 1024,  // 1M floats
    maxBuffersPerBucket: 16
);
```

### Parallel Processing Thresholds

Matrix operations parallelize automatically above certain thresholds:

- MatMul: Parallelizes when M >= 32 rows
- Softmax: Parallelizes when batch size >= 32

These are hardcoded for optimal CPU utilization but may be exposed in future versions.

## Environment Variables

| Variable | Purpose | Example |
|----------|---------|---------|
| `DOTNET_TieredCompilation` | Enable tiered JIT | `1` (recommended) |
| `DOTNET_ReadyToRun` | Use pre-compiled code | `1` (recommended) |
| `ASPNETCORE_ENVIRONMENT` | Runtime environment | `Production`, `Development` |
| `RUN_PERF_TESTS` | Enable performance tests | `true` |

## Configuration Files

SmallMind supports `appsettings.json` for configuration:

```json
{
  "SmallMind": {
    "ModelPath": "./models/trained_model.json",
    "VocabSize": 256,
    "BlockSize": 128,
    "EmbeddingDimension": 64,
    "NumLayers": 4,
    "NumHeads": 4,
    "Dropout": 0.1
  },
  "Training": {
    "DefaultLearningRate": 0.001,
    "DefaultBatchSize": 32,
    "SaveCheckpointEvery": 1000,
    "CheckpointDirectory": "./checkpoints"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "SmallMind": "Debug"
    }
  }
}
```

Load configuration:
```csharp
builder.Configuration.AddJsonFile("appsettings.json", optional: false);
builder.Services.Configure<SmallMindOptions>(
    builder.Configuration.GetSection("SmallMind"));
```

## Best Practices

1. **Use DI in production** for lifecycle management and testability
2. **Configure logging** to appropriate levels (Debug in dev, Information in prod)
3. **Enable metrics** for production monitoring
4. **Set checkpointDirectory** to persistent storage
5. **Use seed** for reproducible experiments
6. **Monitor memory usage** with large models
7. **Tune batch size** based on available RAM
8. **Use Release builds** for training/inference (5-10x faster than Debug)

## Troubleshooting

See [docs/troubleshooting.md](troubleshooting.md) for common configuration issues and solutions.

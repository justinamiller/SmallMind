# Threading and Resource Management Guide

This guide covers thread safety, concurrency patterns, and resource disposal in SmallMind.

## Table of Contents

- [Thread Safety Model](#thread-safety-model)
- [Concurrency Patterns](#concurrency-patterns)
- [Resource Disposal](#resource-disposal)
- [Memory Management](#memory-management)
- [Best Practices](#best-practices)

## Thread Safety Model

SmallMind follows a clear thread safety model for each component.

### Thread-Safe Components (Immutable After Construction)

These components are safe to use concurrently from multiple threads:

#### Tokenizer
```csharp
public class Tokenizer
{
    // Thread-safe: vocabulary is immutable after construction
    // Safe to call from multiple threads:
    // - Encode(string)
    // - Decode(IEnumerable<int>)
}
```

**Usage**:
```csharp
// Register as Singleton in DI
services.AddSingleton<Tokenizer>(sp => new Tokenizer(trainingData));

// Safe to use from multiple threads
var tokens1 = Task.Run(() => tokenizer.Encode("prompt1"));
var tokens2 = Task.Run(() => tokenizer.Encode("prompt2"));
```

### Thread-Safe for Inference (Read-Only Operations)

These components are safe for **concurrent inference** but **NOT safe for concurrent training**:

#### TransformerModel (Inference)
```csharp
public class TransformerModel
{
    // Thread-safe for inference (Eval mode):
    // - Forward(int[])
    // - Eval()
    
    // NOT thread-safe:
    // - Backward()
    // - Training mode
}
```

**Usage**:
```csharp
// Set model to evaluation mode once
model.Eval();

// Safe: Multiple threads can perform inference
var result1 = Task.Run(() => model.Forward(input1));
var result2 = Task.Run(() => model.Forward(input2));
```

**Important**: Call `model.Eval()` before concurrent inference to disable gradient computation.

### Not Thread-Safe Components

These components maintain mutable state and are **NOT thread-safe**:

#### Training
```csharp
public class Training
{
    // NOT thread-safe - maintains training state
    // Use from single thread or with external synchronization
}
```

#### Sampling
```csharp
public class Sampling
{
    // NOT thread-safe - maintains internal state
    // Create per-thread or per-request instances
}
```

**Safe Pattern**:
```csharp
// Per-request instance
services.AddScoped<Sampling>(sp =>
{
    var model = sp.GetRequiredService<TransformerModel>();
    var tokenizer = sp.GetRequiredService<Tokenizer>();
    return new Sampling(model, tokenizer, blockSize: 128);
});
```

## Concurrency Patterns

### Pattern 1: Singleton Model with Scoped Generation

```csharp
// Startup.cs
services.AddSingleton<TransformerModel>(sp => LoadModel());
services.AddSingleton<Tokenizer>(sp => LoadTokenizer());
services.AddScoped<Sampling>();

// Controller.cs
public class TextController : ControllerBase
{
    private readonly Sampling _sampling;
    
    public TextController(Sampling sampling)
    {
        _sampling = sampling;  // New instance per request
    }
    
    [HttpPost("generate")]
    public async Task<IActionResult> Generate(GenerateRequest request)
    {
        // Safe: Each request has its own Sampling instance
        var result = await Task.Run(() => 
            _sampling.Generate(request.Prompt, request.MaxTokens));
        return Ok(result);
    }
}
```

### Pattern 2: Object Pooling for High Throughput

```csharp
using Microsoft.Extensions.ObjectPool;

// Create a pool of Sampling instances
var policy = new DefaultPooledObjectPolicy<Sampling>
{
    Create = () => new Sampling(model, tokenizer, blockSize),
    Return = obj => true
};

var pool = new DefaultObjectPool<Sampling>(policy, maximumRetained: 10);

// Usage
var sampling = pool.Get();
try
{
    var result = sampling.Generate(prompt, maxTokens);
    return result;
}
finally
{
    pool.Return(sampling);
}
```

### Pattern 3: Parallel Training with Data Parallelism

```csharp
// NOT RECOMMENDED for SmallMind (no gradient synchronization yet)
// Training must be single-threaded per model

// Instead, train multiple independent models:
var models = Enumerable.Range(0, 4)
    .Select(i => new TransformerModel(..., seed: i))
    .ToArray();

Parallel.ForEach(models, model =>
{
    var training = new Training(model, tokenizer, data, ...);
    training.Train(steps: 1000, ...);
});
```

### Pattern 4: Async Inference with SemaphoreSlim

Limit concurrent inference requests to prevent resource exhaustion:

```csharp
public class ThrottledInferenceService
{
    private readonly TransformerModel _model;
    private readonly Tokenizer _tokenizer;
    private readonly SemaphoreSlim _semaphore;
    
    public ThrottledInferenceService(
        TransformerModel model,
        Tokenizer tokenizer,
        int maxConcurrency = 4)
    {
        _model = model;
        _tokenizer = tokenizer;
        _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        _model.Eval();
    }
    
    public async Task<string> GenerateAsync(string prompt, int maxTokens)
    {
        await _semaphore.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var sampling = new Sampling(_model, _tokenizer, blockSize: 128);
                return sampling.Generate(prompt, maxTokens);
            });
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

## Resource Disposal

SmallMind components implement `IDisposable` where appropriate.

### Disposable Components

#### MemoryPool
```csharp
public class MemoryPool : IDisposable
{
    // Releases pooled memory buffers
    public void Dispose()
    {
        // Clear pools, release resources
    }
}
```

**Usage**:
```csharp
using (var pool = new MemoryPool())
{
    // Use pool
}  // Automatically disposed
```

#### VectorIndex
```csharp
public class VectorIndex : IDisposable
{
    // Clears index entries
    public void Dispose()
    {
        // Clear entries, release memory
    }
}
```

### Disposal Patterns

#### Pattern 1: Using Statement

```csharp
using var pool = new MemoryPool();
// Use pool
// Automatically disposed when out of scope
```

#### Pattern 2: DI Container Disposal

```csharp
// Register as Singleton
services.AddSingleton<MemoryPool>();

// Container disposes on shutdown
await host.RunAsync();  // Disposes all singleton services
```

#### Pattern 3: Manual Disposal

```csharp
var index = new VectorIndex();
try
{
    // Use index
}
finally
{
    index.Dispose();
}
```

### Disposal Validation

Check for disposed objects:

```csharp
public class DisposableExample : IDisposable
{
    private bool _disposed;
    
    public void DoWork()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        // Work...
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        // Cleanup
        _disposed = true;
    }
}
```

## Memory Management

### Memory Pooling

SmallMind uses `ArrayPool<T>` and custom `MemoryPool` for efficient memory reuse.

```csharp
using System.Buffers;

// Rent from pool
var buffer = ArrayPool<float>.Shared.Rent(minLength: 1024);
try
{
    // Use buffer
}
finally
{
    // Return to pool
    ArrayPool<float>.Shared.Return(buffer, clearArray: true);
}
```

### Memory Pressure

For large models, inform GC of memory pressure:

```csharp
public class LargeModel : IDisposable
{
    private readonly long _memorySize;
    
    public LargeModel(int modelSize)
    {
        _memorySize = modelSize * sizeof(float);
        GC.AddMemoryPressure(_memorySize);
    }
    
    public void Dispose()
    {
        GC.RemoveMemoryPressure(_memorySize);
    }
}
```

### Memory Leaks

Common causes and prevention:

| Cause | Prevention |
|-------|-----------|
| Undisposed `MemoryPool` | Always dispose or use `using` |
| Event handler leaks | Unsubscribe in `Dispose()` |
| Static references | Avoid or use `WeakReference` |
| Large object heap fragmentation | Reuse buffers via pooling |

### Monitoring Memory

```csharp
public static void PrintMemoryUsage()
{
    var totalMemory = GC.GetTotalMemory(forceFullCollection: false);
    var gen0 = GC.CollectionCount(0);
    var gen1 = GC.CollectionCount(1);
    var gen2 = GC.CollectionCount(2);
    
    Console.WriteLine($"Memory: {totalMemory / 1024 / 1024}MB");
    Console.WriteLine($"GC: Gen0={gen0}, Gen1={gen1}, Gen2={gen2}");
}
```

## Best Practices

### 1. Model Lifecycle

```csharp
// ✅ GOOD: Singleton model for inference
services.AddSingleton<TransformerModel>(sp =>
{
    var model = LoadModel();
    model.Eval();  // Set to inference mode
    return model;
});

// ❌ BAD: Creating new model per request
services.AddScoped<TransformerModel>(); // Wasteful
```

### 2. Cancellation Support

Always support cancellation for long-running operations:

```csharp
public async Task<string> GenerateWithCancellationAsync(
    string prompt,
    int maxTokens,
    CancellationToken cancellationToken = default)
{
    return await Task.Run(() =>
    {
        // Training.Train supports CancellationToken
        // Sampling.Generate does not yet - wrap in Task.Run with cancellation
        cancellationToken.ThrowIfCancellationRequested();
        
        var sampling = new Sampling(model, tokenizer, blockSize);
        return sampling.Generate(prompt, maxTokens);
    }, cancellationToken);
}
```

### 3. Avoid Blocking

Don't block async code:

```csharp
// ❌ BAD: Blocking async
var result = GenerateAsync(prompt, maxTokens).Result;

// ✅ GOOD: Async all the way
var result = await GenerateAsync(prompt, maxTokens);
```

### 4. Resource Cleanup

Always clean up resources:

```csharp
// ✅ GOOD: Using statement
using var pool = new MemoryPool();

// ✅ GOOD: Try-finally
var pool = new MemoryPool();
try
{
    // Use pool
}
finally
{
    pool.Dispose();
}

// ❌ BAD: No disposal
var pool = new MemoryPool();
// Leak!
```

### 5. Thread Safety Documentation

Document thread safety in your code:

```csharp
/// <summary>
/// Generates text from a prompt.
/// </summary>
/// <remarks>
/// This method is NOT thread-safe. Create separate instances for concurrent use.
/// </remarks>
public string Generate(string prompt, int maxTokens) { ... }
```

## Troubleshooting

### Issue: "Collection was modified" Exception

**Cause**: Concurrent modification of shared state  
**Solution**: Use thread-safe collections or synchronization

```csharp
// Use ConcurrentDictionary instead of Dictionary
private readonly ConcurrentDictionary<string, Model> _models = new();
```

### Issue: High Memory Usage

**Cause**: Not disposing resources  
**Solution**: Ensure all `IDisposable` objects are disposed

```csharp
// Check for undisposed objects
using var tracker = new MemoryTracker();
// ... code
// Check tracker.ActiveAllocations
```

### Issue: Slow Inference Under Load

**Cause**: Too many concurrent requests  
**Solution**: Use `SemaphoreSlim` to throttle concurrency

See [Pattern 4: Async Inference with SemaphoreSlim](#pattern-4-async-inference-with-semaphoreslim)

## See Also

- [Configuration Guide](configuration.md) - Configuration options
- [Observability Guide](observability.md) - Monitoring and logging
- [Troubleshooting Guide](troubleshooting.md) - Common issues and solutions

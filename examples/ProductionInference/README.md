# SmallMind Production Inference Example

This example demonstrates production-grade inference features added to SmallMind for commercial deployments.

## Features Demonstrated

### 1. Resource Governance
- **MaxInputTokens**: Limit or reject oversized inputs
- **MaxContextTokens**: Bound KV cache memory usage
- **MaxNewTokens**: Control generation length
- **MaxTimeMs**: Enforce generation timeouts
- **TruncateInput**: Handle oversized inputs gracefully

### 2. Deterministic Generation
- Set a **Seed** for reproducible outputs
- Same seed + prompt + options = identical output
- Critical for testing and debugging

### 3. Streaming Token Generation
- Emit tokens incrementally as they're generated
- Use `IAsyncEnumerable<GeneratedToken>` for real-time display
- Minimal latency overhead

### 4. Concurrent Request Handling
- **InferenceEngine** facade manages multiple sessions
- Bounded concurrency with semaphore-based control
- Model weights shared safely across sessions
- Session state isolated per request

### 5. Memory Estimation
- Pre-calculate memory requirements before allocation
- Plan capacity for concurrent sessions
- Understand KV cache memory usage

## Running the Example

```bash
cd examples/ProductionInference
dotnet run
```

## Code Structure

### Basic Usage

```csharp
// Create options with resource limits
var options = new ProductionInferenceOptions
{
    MaxInputTokens = 100,
    MaxContextTokens = 512,
    MaxNewTokens = 20,
    MaxTimeMs = 5000,  // 5 second timeout
    Temperature = 0.8,
    TopK = 10,
    Seed = 123  // Deterministic
};

// Create isolated session
using var session = new InferenceSession(model, tokenizer, options, blockSize);

// Generate
var result = await session.GenerateAsync("Hello world");
```

### Streaming

```csharp
await foreach (var token in session.GenerateStreamAsync("Hello"))
{
    Console.Write(token.Text);  // Display in real-time
}
```

### Concurrent Requests

```csharp
// Create engine with concurrency limit
using var engine = new InferenceEngine(
    model, 
    tokenizer, 
    blockSize, 
    maxConcurrentSessions: 3
);

// Handle concurrent requests (automatically queued beyond limit)
var result1 = await engine.GenerateAsync("prompt1", options);
var result2 = await engine.GenerateAsync("prompt2", options);
```

### Memory Estimation

```csharp
long sessionBytes = MemoryEstimator.EstimateSessionBytes(
    modelParams, options, nEmbd, nLayer, nHead
);

long kvCacheBytes = MemoryEstimator.EstimateKvCacheBytes(
    maxContextTokens, nEmbd, nLayer, nHead
);

long totalBytes = MemoryEstimator.EstimateEngineBytes(
    modelParams, options, nEmbd, nLayer, nHead, 
    maxConcurrentSessions: 10
);

Console.WriteLine($"Memory: {MemoryEstimator.FormatBytes(totalBytes)}");
```

## Architecture

### Thread Safety
- **Model weights**: Immutable, shared across all sessions
- **Session state**: Isolated per request, not shared
- **Concurrency control**: Semaphore-based, bounded by MaxConcurrentSessions

### Performance
- No LINQ in hot paths
- ArrayPool for temporary buffers
- Span<T> for zero-copy slicing
- Minimal allocations per token
- Deterministic RNG (XorShift128) for reproducibility

### Error Handling
- **ResourceLimitException**: Input/context limits exceeded
- **InferenceTimeoutException**: Generation timeout
- **ValidationException**: Invalid options
- **OperationCanceledException**: Request cancelled

## Production Deployment Patterns

### API Server Pattern

```csharp
// Startup: Create engine once
var engine = new InferenceEngine(model, tokenizer, blockSize, maxConcurrentSessions: 10);

// Per-request handler
app.MapPost("/generate", async (GenerateRequest req, CancellationToken ct) =>
{
    var options = new ProductionInferenceOptions
    {
        MaxInputTokens = 1000,
        MaxContextTokens = 2048,
        MaxNewTokens = req.MaxTokens,
        MaxTimeMs = 30000,  // 30s timeout
        Temperature = req.Temperature,
        TopK = req.TopK,
        Seed = req.Seed
    };

    var metrics = new PerformanceMetrics();
    metrics.Start();

    try
    {
        var result = await engine.GenerateAsync(req.Prompt, options, metrics, ct);
        return Results.Ok(new { text = result, metrics = metrics.GetSummary() });
    }
    catch (ResourceLimitException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (InferenceTimeoutException ex)
    {
        return Results.StatusCode(504, new { error = "Generation timeout" });
    }
});
```

### Streaming API Pattern

```csharp
app.MapPost("/generate/stream", async (GenerateRequest req, CancellationToken ct) =>
{
    var options = new ProductionInferenceOptions { /* ... */ };

    return Results.Stream(async (stream) =>
    {
        await using var writer = new StreamWriter(stream);
        
        await foreach (var token in engine.GenerateStreamAsync(req.Prompt, options, null, ct))
        {
            await writer.WriteLineAsync(JsonSerializer.Serialize(token));
            await writer.FlushAsync();
        }
    }, "application/x-ndjson");
});
```

## Performance Tuning

### Memory Optimization
- Set `MaxContextTokens` to the minimum needed for your use case
- KV cache is the largest per-session memory consumer
- Use `MemoryEstimator` to plan capacity

### Throughput Optimization
- Increase `MaxConcurrentSessions` based on available memory
- Each session needs ~(KV cache + working memory)
- Monitor `EngineStatistics.AvailableSlots`

### Latency Optimization
- Use streaming for real-time display (lower perceived latency)
- Set reasonable `MaxTimeMs` to fail fast on stuck generations
- Use `Seed` during development/testing for predictable timing

## Testing

### Deterministic Tests

```csharp
[Fact]
public async Task Generation_SameSeed_ProducesIdenticalOutput()
{
    var options = new ProductionInferenceOptions { Seed = 42 };
    
    using var session1 = new InferenceSession(model, tokenizer, options, blockSize);
    using var session2 = new InferenceSession(model, tokenizer, options, blockSize);
    
    var result1 = await session1.GenerateAsync("test");
    var result2 = await session2.GenerateAsync("test");
    
    Assert.Equal(result1, result2);
}
```

### Resource Limit Tests

```csharp
[Fact]
public async Task Generation_ExceedsInputLimit_ThrowsException()
{
    var options = new ProductionInferenceOptions 
    { 
        MaxInputTokens = 10,
        TruncateInput = false  // Reject oversized inputs
    };
    
    using var session = new InferenceSession(model, tokenizer, options, blockSize);
    
    await Assert.ThrowsAsync<ResourceLimitException>(
        async () => await session.GenerateAsync("very long input...")
    );
}
```

## See Also

- [Main README](../../README.md) for general SmallMind documentation
- [MinimalGenerate](../MinimalGenerate) for a simpler getting started example
- [MultiThreadedGeneration](../../samples/MultiThreadedGeneration) for advanced concurrency patterns

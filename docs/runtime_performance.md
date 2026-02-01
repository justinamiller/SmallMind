# Runtime Performance: KV Cache & Batching

SmallMind provides production-grade runtime optimizations for efficient inference:
- **KV Cache**: Accelerates multi-turn conversations and sequential generation
- **Batching**: Improves throughput by processing multiple requests concurrently

## KV Cache

### Overview

Key-Value (KV) caching dramatically reduces computation for autoregressive text generation by storing and reusing attention keys and values from previous tokens.

**Benefits:**
- **13x speedup** for token generation (based on benchmarks)
- Lower latency for multi-turn conversations
- Reduced CPU usage
- Memory-efficient with bounded cache sizes

### How It Works

During text generation:
1. **Prefill**: Process the entire prompt once, cache all K/V tensors
2. **Decode**: For each new token, compute only new K/V, append to cache
3. **Attention**: Attend over all cached K/V (prompt + generated tokens)

This avoids recomputing attention for tokens we've already seen.

### Configuration

```csharp
using SmallMind.Runtime.Cache;

var cacheOptions = new KvCacheOptions
{
    Enabled = true,                    // Enable KV caching
    MaxTokensPerSession = 4096,        // Max tokens per session
    MaxSessions = 1000,                // Max concurrent sessions
    MaxBytesTotal = 1L * 1024 * 1024 * 1024,  // 1GB total memory
    Policy = KvEvictionPolicy.LRU,     // Eviction policy
    PersistAcrossRequests = true       // Keep cache between requests
};

var cacheStore = new LruKvCacheStore(cacheOptions);
```

### Using KV Cache

```csharp
using SmallMind.Runtime;
using SmallMind.Runtime.Cache;

// Create cache store
var cacheStore = new LruKvCacheStore(new KvCacheOptions());

// Create session with cache
var sessionId = SessionId.NewId();
var modelShape = new ModelShape(
    layers: model.NumLayers,
    heads: model.NumHeads,
    headDim: model.EmbedDim / model.NumHeads
);

var cacheEntry = cacheStore.GetOrCreate(
    sessionId,
    modelShape,
    maxTokens: 2048
);

// Use in generation (integration with InferenceSession pending)
// The cache automatically stores K/V tensors during generation
```

### Memory Management

KV cache uses `ArrayPool<float>` for efficient memory allocation:
- Buffers are rented from a shared pool
- Returned to pool when sessions are evicted
- No GC pressure from frequent allocations

### Eviction Policy (LRU)

When memory limits are reached:
1. Least Recently Used (LRU) sessions are evicted first
2. Eviction triggers when:
   - `MaxSessions` limit is reached
   - `MaxBytesTotal` limit is exceeded
3. O(1) eviction using linked list + dictionary

### Monitoring

```csharp
var stats = cacheStore.GetStats();

Console.WriteLine($"Sessions: {stats.CurrentSessions}");
Console.WriteLine($"Memory: {stats.CurrentBytes / 1024 / 1024} MB");
Console.WriteLine($"Hit Rate: {stats.HitRate:P2}");
Console.WriteLine($"Evictions: {stats.Evictions}");
Console.WriteLine($"Reused Tokens: {stats.ReusedTokens}");
```

## Batching

### Overview

Request batching processes multiple concurrent inference requests together in a single forward pass, dramatically improving throughput.

**Benefits:**
- Higher tokens/second under load
- Better CPU/GPU utilization
- Reduced per-request latency when concurrent
- Automatic queue management

### How It Works

1. **Queue**: Incoming requests enter a queue
2. **Batch Formation**: Scheduler groups compatible requests
3. **Execution**: Single forward pass processes entire batch
4. **Distribution**: Results streamed back to individual requests

**Batch Formation Criteria:**
- Same model instance
- Compatible sampling parameters
- Size limit: `MaxBatchSize` (default: 8)
- Time limit: `MaxBatchWaitMs` (default: 10ms)

### Configuration

```csharp
using SmallMind.Runtime.Batching;

var batchingOptions = new BatchingOptions
{
    Enabled = true,               // Enable batching
    MaxBatchSize = 8,             // Max requests per batch
    MaxBatchWaitMs = 10,          // Max wait time for batch
    MaxTotalQueuedRequests = 100, // Queue size limit
    PrefillOnly = true            // Batch prefill only (not decode)
};
```

### Using Batched Inference

```csharp
using SmallMind.Runtime.Batching;

// Create batched engine
var batchedEngine = new BatchedInferenceEngine(
    model,
    tokenizer,
    blockSize: model.BlockSize,
    batchingOptions,
    kvCacheStore,
    maxConcurrentSessions: 100
);

// Submit requests (automatically batched)
var tasks = new List<Task<string>>();
for (int i = 0; i < 10; i++)
{
    tasks.Add(batchedEngine.GenerateAsync(
        $"Prompt {i}",
        new ProductionInferenceOptions { MaxNewTokens = 50 }
    ));
}

var results = await Task.WhenAll(tasks);
```

### Streaming with Batching

```csharp
await foreach (var token in batchedEngine.GenerateStreamingAsync(
    "Tell me a story",
    new ProductionInferenceOptions { MaxNewTokens = 100 }))
{
    Console.Write(token.Text);
}
```

### Performance Characteristics

**Prefill Batching (Current Implementation):**
- Batches initial prompt processing
- Each request still decodes independently
- Best for variable-length prompts

**Future: Full Decode Batching:**
- Batch both prefill and decode steps
- Maximum throughput gains
- More complex due to diverging sequences

### Monitoring

```csharp
var metrics = new InMemoryRuntimeMetrics();
batchedEngine.Metrics = metrics;

// Check metrics
Console.WriteLine($"Avg Batch Size: {metrics.AverageBatchSize}");
Console.WriteLine($"Avg Wait Time: {metrics.AverageBatchWaitMs}ms");
Console.WriteLine($"Avg Queue Depth: {metrics.AverageQueueDepth}");
```

## Combined: KV Cache + Batching

When used together, KV cache and batching provide maximum efficiency:

1. **Concurrent Requests**: Multiple users submit prompts
2. **Batched Prefill**: Process all prompts in one forward pass
3. **KV Cache Population**: Each request's K/V stored in its session
4. **Independent Decode**: Each request generates tokens using its cache
5. **Session Reuse**: Follow-up requests benefit from cached context

```csharp
// Setup
var cacheStore = new LruKvCacheStore(new KvCacheOptions());
var batchingOptions = new BatchingOptions { Enabled = true };

var engine = new BatchedInferenceEngine(
    model,
    tokenizer,
    blockSize,
    batchingOptions,
    cacheStore,
    maxConcurrentSessions: 100
);

// Multi-turn conversation with batching + caching
var sessionId = SessionId.NewId();

// First turn (batched prefill, populates cache)
var response1 = await engine.GenerateAsync(
    "What is machine learning?",
    new ProductionInferenceOptions { MaxNewTokens = 50 },
    sessionId: sessionId
);

// Second turn (cache hit, reuses previous context)
var response2 = await engine.GenerateAsync(
    "Can you give an example?",
    new ProductionInferenceOptions { MaxNewTokens = 50 },
    sessionId: sessionId
);
```

## Limitations

### Current Limitations

1. **Prefill-Only Batching**: Decode steps run independently
2. **CPU-Only**: No GPU acceleration
3. **No Cross-Request Caching**: Each session has isolated cache
4. **Fixed Model**: Cannot batch requests for different models

### Future Enhancements

1. **Full Decode Batching**: Batch generation steps
2. **Continuous Batching**: Dynamic batch formation during decode
3. **Speculative Decoding**: Accelerate generation with draft models
4. **Quantization**: Reduce memory footprint (Q8/Q4 already supported)

## Best Practices

### For Low Latency
```csharp
- Disable batching (set Enabled = false)
- Enable KV cache for multi-turn
- Use smaller MaxNewTokens
- Set MaxTimeMs timeout
```

### For High Throughput
```csharp
- Enable batching (MaxBatchSize = 8-16)
- Enable KV cache
- Tune MaxBatchWaitMs (trade latency for throughput)
- Increase MaxConcurrentSessions
```

### For Memory Efficiency
```csharp
- Set MaxTokensPerSession conservatively
- Limit MaxSessions based on available RAM
- Monitor KvCacheStats.CurrentBytes
- Enable PersistAcrossRequests = false for stateless workloads
```

## Benchmarks

### KV Cache (4-layer, 128-dim model, 50 tokens)
```
Without Cache:  5.29 tokens/sec  (9457ms)
With Cache:    69.73 tokens/sec  (717ms)
Speedup:       13.2x
```

### Batching (8 concurrent requests, 100 tokens each)
```
Sequential:    ~40 tokens/sec total
Batched:      ~200 tokens/sec total
Speedup:       5x
```

## Troubleshooting

### High Memory Usage
- Reduce `MaxSessions` or `MaxBytesTotal`
- Decrease `MaxTokensPerSession`
- Check `KvCacheStats.PeakBytes`

### Low Cache Hit Rate
- Verify `PersistAcrossRequests = true`
- Check session IDs are consistent
- Monitor eviction count

### Batching Not Working
- Ensure `BatchingOptions.Enabled = true`
- Verify requests are compatible (same model)
- Check `MaxBatchWaitMs` isn't too short
- Monitor `IRuntimeMetrics.AverageBatchSize`

### Cancellation Issues
- Use `CancellationToken` properly
- Don't reuse cancelled tokens
- Check `MaxTimeMs` timeout

## API Reference

See inline XML documentation in:
- `SmallMind.Runtime.Cache.*`
- `SmallMind.Runtime.Batching.*`
- `SmallMind.Runtime.Telemetry.*`

## Examples

Complete working examples in:
- `examples/KVCacheExample/` - Basic KV cache usage
- `examples/ProductionInference/` - Combined cache + batching

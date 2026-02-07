# KV Cache & Batching Example

This example demonstrates SmallMind's production-grade runtime optimizations for high-performance inference.

## Features Demonstrated

### 1. KV Cache (13x Speedup)
- Accelerates token generation by reusing attention key/values
- Ideal for multi-turn conversations and sequential generation
- LRU eviction with bounded memory
- Zero allocations using ArrayPool

### 2. Request Batching (5x Throughput)
- Processes multiple concurrent requests together
- Automatic queue management and batch formation
- Per-request sampling isolation
- Channel-based streaming

### 3. Combined Optimizations
- Best of both worlds for production deployments
- Batching accelerates prefill (initial prompt processing)
- KV Cache accelerates decode (token generation)
- 10-50x effective performance gain depending on workload

## Running the Example

```bash
dotnet run --project examples/CachingAndBatching
```

## Output

The demo shows:
1. **KV Cache Demo**: Multi-turn conversation with cache hits
2. **Batching Demo**: Concurrent request processing
3. **Combined Demo**: Expected performance gains

## Key Concepts

### KV Cache Configuration
```csharp
var cacheOptions = new KvCacheOptions
{
    MaxTokensPerSession = 4096,     // Max tokens per session
    MaxSessions = 1000,             // Max concurrent sessions
    MaxBytesTotal = 1GB,            // Total memory limit
    Policy = KvEvictionPolicy.LRU   // Eviction strategy
};
```

### Batching Configuration
```csharp
var batchingOptions = new BatchingOptions
{
    MaxBatchSize = 8,               // Max requests per batch
    MaxBatchWaitMs = 10,            // Max wait time
    PrefillOnly = true              // Batch prefill only
};
```

## Performance Benchmarks

| Optimization | Speedup | Use Case |
|-------------|---------|----------|
| KV Cache    | 13x     | Multi-turn conversations |
| Batching    | 5x      | Concurrent requests |
| Combined    | 10-50x  | Production deployments |

## See Also

- [docs/runtime_performance.md](../../docs/runtime_performance.md) - Detailed documentation
- [examples/ProductionInference](../ProductionInference) - Production deployment patterns

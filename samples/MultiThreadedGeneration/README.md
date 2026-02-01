# Multi-Threaded Generation Sample

This sample demonstrates thread-safe, concurrent text generation with SmallMind's pure C# transformer implementation. It showcases production-ready patterns for batch inference, progress tracking, and performance benchmarking.

## Overview

The application provides three interactive scenarios that demonstrate different aspects of multi-threaded inference:

1. **Basic Concurrent Generation** - Simple parallel text generation with multiple prompts
2. **Batch Processing with Progress** - Practical batch inference with visual progress tracking
3. **Performance Benchmark** - Compare throughput across different concurrency levels

## Features

### Thread-Safe Inference
- Shared model instance across threads for memory efficiency
- Simple locking mechanism for thread safety (suitable for CPU-bound workloads)
- Demonstrates that SmallMind models can safely handle concurrent requests

### Batch Processing
- `BatchProcessor<TInput, TOutput>` - Generic batch processing class
- Semaphore-based concurrency control
- Maintains result ordering
- Graceful cancellation support

### Progress Tracking
- `ProgressBar` - Custom console progress bar
- Real-time statistics: completion percentage, items/sec, ETA
- Thread-safe updates from concurrent workers

### Performance Monitoring
- Benchmark mode for comparing concurrency levels
- Throughput metrics (items/second)
- Speedup calculations relative to baseline

## Building and Running

### Prerequisites
- .NET 10.0 SDK or later
- SmallMind project built

### Build
```bash
cd samples/MultiThreadedGeneration
dotnet build
```

### Run
```bash
dotnet run
```

## Usage

When you run the application, you'll see an interactive menu:

```
═══════════════════════════════════════════════════════════
   SmallMind Multi-Threaded Generation Sample
═══════════════════════════════════════════════════════════

Select a scenario:
  1. Basic Concurrent Generation
  2. Batch Processing with Progress
  3. Performance Benchmark
  0. Exit

Choice:
```

### Scenario 1: Basic Concurrent Generation

Generates completions for 4 different prompts concurrently:

```
Prompts:
- "The future of AI"
- "Once upon a time"
- "In the year 2050"
- "The quick brown fox"

Parameters: maxTokens=30, temperature=0.8, topK=20
```

**Key Learning**: Demonstrates that the same model instance can handle multiple concurrent requests with proper synchronization.

### Scenario 2: Batch Processing with Progress

Process a batch of prompts with configurable parameters:

```
Processing 12 prompts with concurrency=3...

[████████████████████████░░░░░░░░░░░░░░░░░░░░░░░░░] 8/12 (67%) | 2.4 items/s | ETA: 00:02
```

**Key Learning**: Shows practical batch inference with progress feedback, similar to production use cases.

**Customization**:
- Max tokens per generation
- Concurrency level (how many prompts to process simultaneously)

### Scenario 3: Performance Benchmark

Compares throughput across different concurrency levels:

```
Benchmark: 20 prompts, 20 tokens each
Testing concurrency levels: 1, 2, 4, 8

═══════════════════════════════════════════════════════════
Benchmark Results
═══════════════════════════════════════════════════════════

Concurrency | Time (ms) | Throughput (items/s) | Speedup
─────────────────────────────────────────────────────────
      1     |      8520 |                 2.35 |    1.00x
      2     |      4410 |                 4.54 |    1.93x
      4     |      2680 |                 7.46 |    3.18x
      8     |      2250 |                 8.89 |    3.79x
```

**Key Learning**: 
- Demonstrates scalability with CPU cores
- Shows diminishing returns at higher concurrency (due to GIL-like behavior in the simple locking strategy)
- Helps identify optimal concurrency level for your hardware

## Code Architecture

### Program.cs Structure

```
Program
├── Main()                           # Entry point with interactive menu
├── Scenario1_BasicConcurrent()      # Simple concurrent generation
├── Scenario2_BatchProcessing()      # Batch processing with progress
├── Scenario3_PerformanceBenchmark() # Performance comparison
├── InitializeModel()                # Model initialization
├── CreateTokenizer()                # Tokenizer setup
└── GenerateSamplePrompts()          # Test data generation

BatchProcessor<TInput, TOutput>
├── ProcessBatchAsync()              # Main batch processing logic
└── _processFunc                     # User-provided processing function

ProgressBar
├── Increment()                      # Update progress counter
├── Complete()                       # Mark as finished
└── Render()                         # Draw progress bar
```

## Key Implementation Details

### Thread Safety Strategy

The sample uses a simple `lock(model)` approach:

```csharp
lock (model)
{
    return sampling.Generate(
        prompt,
        maxTokens,
        temperature,
        topK,
        seed,
        showPerf: false,
        isPerfJsonMode: true);
}
```

**Why this works**:
- SmallMind inference is CPU-bound, not I/O-bound
- Model parameters are read-only during inference (eval mode)
- Simple locking ensures sequential access to model state
- Sufficient for demonstration; production systems might use model pools

### Concurrency Control

The `BatchProcessor` uses `SemaphoreSlim` for limiting concurrent operations:

```csharp
var semaphore = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);

await semaphore.WaitAsync(cancellationToken);
try
{
    var result = await _processFunc(item, index, cancellationToken);
    results[index] = result;
    progressBar.Increment();
}
finally
{
    semaphore.Release();
}
```

**Benefits**:
- Prevents resource exhaustion
- Controls memory usage (limits in-flight requests)
- Adjustable based on available CPU cores

### Progress Tracking

Thread-safe progress updates using lock:

```csharp
public void Increment()
{
    lock (_lock)
    {
        _current++;
        Render();
    }
}
```

**Features**:
- Real-time ETA calculation
- Items/second rate tracking
- Visual progress bar with Unicode characters

## Performance Considerations

### CPU Utilization
- SmallMind is CPU-bound (no GPU acceleration)
- Optimal concurrency ≈ number of physical CPU cores
- Hyper-threading provides limited benefit for compute-heavy workloads

### Memory Usage
- Single model instance shared across threads (memory efficient)
- Each concurrent request allocates temporary tensors
- Higher concurrency = higher memory usage

### Scalability Limits
- Simple locking serializes model access
- For high-throughput production systems, consider:
  - Model pooling (multiple model instances)
  - Batched inference (process multiple prompts in single forward pass)
  - Request queuing with worker threads

## Extending This Sample

### Add Model Pooling

Replace single model with a pool:

```csharp
public class ModelPool
{
    private readonly ConcurrentBag<(TransformerModel, Sampling)> _pool;
    
    public ModelPool(int poolSize)
    {
        _pool = new ConcurrentBag<(TransformerModel, Sampling)>();
        for (int i = 0; i < poolSize; i++)
        {
            var model = InitializeModel();
            var sampling = new Sampling(model, tokenizer, model.BlockSize);
            _pool.Add((model, sampling));
        }
    }
    
    public async Task<string> GenerateAsync(string prompt, int maxTokens)
    {
        if (!_pool.TryTake(out var instance))
            throw new InvalidOperationException("Pool exhausted");
        
        try
        {
            return await Task.Run(() => 
                instance.Item2.Generate(prompt, maxTokens));
        }
        finally
        {
            _pool.Add(instance);
        }
    }
}
```

### Add Request Queue

Implement a producer-consumer pattern:

```csharp
public class InferenceQueue
{
    private readonly BlockingCollection<InferenceRequest> _queue;
    private readonly Task[] _workers;
    
    public InferenceQueue(int workerCount, TransformerModel model, Sampling sampling)
    {
        _queue = new BlockingCollection<InferenceRequest>();
        _workers = new Task[workerCount];
        
        for (int i = 0; i < workerCount; i++)
        {
            _workers[i] = Task.Run(() => ProcessQueue(model, sampling));
        }
    }
    
    private void ProcessQueue(TransformerModel model, Sampling sampling)
    {
        foreach (var request in _queue.GetConsumingEnumerable())
        {
            var result = sampling.Generate(request.Prompt, request.MaxTokens);
            request.CompletionSource.SetResult(result);
        }
    }
}
```

## Related Samples

- **Basic Generation** - Single-threaded text generation fundamentals
- **Training Sample** - Model training and checkpointing
- **Workflow Examples** - Integration with SmallMind workflows

## Troubleshooting

### Out of Memory
- Reduce concurrency level
- Reduce max tokens per generation
- Use smaller model configuration

### Slow Performance
- Check CPU usage (should be near 100% during processing)
- Ensure .NET is in Release mode: `dotnet run -c Release`
- Try different concurrency levels to find optimal value

### Thread Safety Errors
- Ensure all model access is within `lock(model)` blocks
- Don't share `Sampling` instances across threads without locking
- Each thread should have deterministic seeds for reproducibility

## References

- [SmallMind Documentation](../../docs/)
- [.NET Task Parallel Library](https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/)
- [SemaphoreSlim Class](https://docs.microsoft.com/en-us/dotnet/api/system.threading.semaphoreslim)
- [ConcurrentDictionary](https://docs.microsoft.com/en-us/dotnet/api/system.collections.concurrent.concurrentdictionary-2)

## License

This sample is part of the SmallMind project and follows the same license terms.

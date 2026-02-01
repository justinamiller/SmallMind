# Tutorial 2: Concurrent Inference - Multi-threaded Text Generation

Learn how to safely and efficiently perform text generation with multiple threads in SmallMind.

## Overview

SmallMind supports concurrent inference with proper thread safety. This tutorial covers:
- Thread-safe model sharing
- Parallel batch processing
- Performance optimization
- Memory management

## Key Principle: Model Immutability During Inference

The TransformerModel is **read-only during inference** (Eval mode), making it safe to share across threads:

```csharp
model.Eval(); // Switch to evaluation mode - makes model read-only
// Now safe to use from multiple threads simultaneously
```

## Basic Concurrent Generation

### Simple Parallel Processing

```csharp
using SmallMind.Core;
using SmallMind.Transformers;
using SmallMind.Tokenizers;
using SmallMind.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

class ConcurrentExample
{
    static async Task Main()
    {
        // Load model once
        var store = new BinaryCheckpointStore();
        var checkpoint = await store.LoadAsync("model.smnd");
        var model = CheckpointExtensions.FromCheckpoint(checkpoint);
        model.Eval(); // IMPORTANT: Set to eval mode before sharing
        
        // Create tokenizer (thread-safe for reading)
        var tokenizer = new CharTokenizer("abcdefghijklmnopqrstuvwxyz ");
        
        // Prompts to process
        var prompts = new[]
        {
            "The quick brown fox",
            "Once upon a time",
            "In a distant galaxy",
            "The secret to happiness"
        };
        
        // Process all prompts concurrently
        var stopwatch = Stopwatch.StartNew();
        
        var tasks = new List<Task<string>>();
        foreach (var prompt in prompts)
        {
            tasks.Add(Task.Run(() =>
            {
                // Each thread creates its own generator
                var generator = new Sampling(model, tokenizer, model.BlockSize);
                return generator.Generate(prompt, maxNewTokens: 50, temperature: 0.8);
            }));
        }
        
        var results = await Task.WhenAll(tasks);
        stopwatch.Stop();
        
        // Display results
        for (int i = 0; i < prompts.Length; i++)
        {
            Console.WriteLine($"Prompt: {prompts[i]}");
            Console.WriteLine($"Result: {results[i]}");
            Console.WriteLine();
        }
        
        Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average: {stopwatch.ElapsedMilliseconds / prompts.Length}ms per prompt");
    }
}
```

## Thread-Safe Generator Pattern

### Option 1: One Generator Per Thread (Recommended)

Most efficient - avoids synchronization overhead:

```csharp
public class InferenceService
{
    private readonly TransformerModel _model;
    private readonly ITokenizer _tokenizer;
    
    public InferenceService(TransformerModel model, ITokenizer tokenizer)
    {
        _model = model;
        _model.Eval(); // Ensure eval mode
        _tokenizer = tokenizer;
    }
    
    public async Task<string> GenerateAsync(string prompt, GenerationOptions options)
    {
        return await Task.Run(() =>
        {
            // Create generator for this request
            var generator = new Sampling(_model, _tokenizer, _model.BlockSize);
            return generator.Generate(
                prompt,
                options.MaxTokens,
                options.Temperature,
                options.TopK,
                options.Seed
            );
        });
    }
    
    public async Task<List<string>> GenerateBatchAsync(
        List<string> prompts,
        GenerationOptions options)
    {
        var tasks = prompts.Select(p => GenerateAsync(p, options));
        return (await Task.WhenAll(tasks)).ToList();
    }
}
```

### Option 2: Thread-Local Generators

For high-throughput scenarios with thread pooling:

```csharp
public class HighThroughputService
{
    private readonly TransformerModel _model;
    private readonly ITokenizer _tokenizer;
    private readonly ThreadLocal<Sampling> _generatorPool;
    
    public HighThroughputService(TransformerModel model, ITokenizer tokenizer)
    {
        _model = model;
        _model.Eval();
        _tokenizer = tokenizer;
        
        // Create generator per thread (reused across requests)
        _generatorPool = new ThreadLocal<Sampling>(
            () => new Sampling(_model, _tokenizer, _model.BlockSize),
            trackAllValues: false
        );
    }
    
    public string Generate(string prompt, GenerationOptions options)
    {
        var generator = _generatorPool.Value!;
        return generator.Generate(
            prompt,
            options.MaxTokens,
            options.Temperature,
            options.TopK,
            options.Seed
        );
    }
    
    public void Dispose()
    {
        _generatorPool.Dispose();
    }
}
```

## Parallel Batch Processing

### Process Large Batches Efficiently

```csharp
using System.Collections.Concurrent;
using System.Linq;

public class BatchProcessor
{
    private readonly TransformerModel _model;
    private readonly ITokenizer _tokenizer;
    private readonly int _maxParallelism;
    
    public BatchProcessor(TransformerModel model, ITokenizer tokenizer, int? maxParallelism = null)
    {
        _model = model;
        _model.Eval();
        _tokenizer = tokenizer;
        _maxParallelism = maxParallelism ?? Environment.ProcessorCount;
    }
    
    public async Task<List<string>> ProcessBatchAsync(
        List<string> prompts,
        GenerationOptions options,
        IProgress<int>? progress = null)
    {
        var results = new ConcurrentBag<(int index, string result)>();
        var completed = 0;
        
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxParallelism
        };
        
        await Task.Run(() =>
        {
            Parallel.For(0, prompts.Count, parallelOptions, i =>
            {
                var generator = new Sampling(_model, _tokenizer, _model.BlockSize);
                var result = generator.Generate(
                    prompts[i],
                    options.MaxTokens,
                    options.Temperature,
                    options.TopK,
                    options.Seed.HasValue ? options.Seed.Value + i : null // Vary seed per prompt
                );
                
                results.Add((i, result));
                
                var count = Interlocked.Increment(ref completed);
                progress?.Report(count);
            });
        });
        
        // Return in original order
        return results.OrderBy(r => r.index).Select(r => r.result).ToList();
    }
}

// Usage
var processor = new BatchProcessor(model, tokenizer);
var progress = new Progress<int>(count => 
    Console.WriteLine($"Completed: {count}/{prompts.Count}"));

var results = await processor.ProcessBatchAsync(prompts, options, progress);
```

## Performance Benchmarking

### Measure Concurrent Throughput

```csharp
using SmallMind.Core.Core;

public class ConcurrentBenchmark
{
    public static async Task RunBenchmark(
        TransformerModel model,
        ITokenizer tokenizer,
        int numRequests,
        int concurrency)
    {
        model.Eval();
        
        var prompt = "The quick brown fox";
        var options = new GenerationOptions
        {
            MaxTokens = 50,
            Temperature = 0.8f,
            TopK = 40
        };
        
        var metrics = new PerformanceMetrics();
        metrics.Start();
        
        var tasks = new List<Task>();
        for (int i = 0; i < numRequests; i++)
        {
            int requestId = i;
            tasks.Add(Task.Run(() =>
            {
                var reqId = metrics.RecordRequestStart();
                metrics.RecordInferenceStart(reqId);
                
                var generator = new Sampling(model, tokenizer, model.BlockSize);
                generator.Generate(prompt, options.MaxTokens, options.Temperature, options.TopK);
                
                metrics.RecordInferenceEnd(reqId);
                metrics.RecordRequestEnd(reqId);
            }));
            
            // Limit concurrency
            if (tasks.Count >= concurrency)
            {
                await Task.WhenAny(tasks);
                tasks.RemoveAll(t => t.IsCompleted);
            }
        }
        
        await Task.WhenAll(tasks);
        metrics.Stop();
        
        var summary = metrics.GetSummary();
        Console.WriteLine($"\n=== Benchmark Results (Concurrency: {concurrency}) ===");
        Console.WriteLine($"Total Requests: {numRequests}");
        Console.WriteLine($"Duration: {summary.DurationSeconds:F2}s");
        Console.WriteLine($"Throughput: {summary.RequestsPerSecond:F2} req/s");
        Console.WriteLine($"Tokens/sec: {summary.TokensPerSecond:F2}");
        Console.WriteLine($"P95 Latency: {summary.E2eLatencyP95:F2}ms");
    }
}

// Run benchmarks with different concurrency levels
await ConcurrentBenchmark.RunBenchmark(model, tokenizer, numRequests: 100, concurrency: 1);
await ConcurrentBenchmark.RunBenchmark(model, tokenizer, numRequests: 100, concurrency: 4);
await ConcurrentBenchmark.RunBenchmark(model, tokenizer, numRequests: 100, concurrency: 8);
```

## Memory Management

### Pool Generators to Reduce Allocations

```csharp
using System.Collections.Concurrent;

public class GeneratorPool : IDisposable
{
    private readonly ConcurrentBag<Sampling> _pool;
    private readonly TransformerModel _model;
    private readonly ITokenizer _tokenizer;
    private readonly int _maxPoolSize;
    
    public GeneratorPool(TransformerModel model, ITokenizer tokenizer, int maxPoolSize = 10)
    {
        _model = model;
        _model.Eval();
        _tokenizer = tokenizer;
        _maxPoolSize = maxPoolSize;
        _pool = new ConcurrentBag<Sampling>();
    }
    
    public Sampling Rent()
    {
        if (_pool.TryTake(out var generator))
            return generator;
        
        return new Sampling(_model, _tokenizer, _model.BlockSize);
    }
    
    public void Return(Sampling generator)
    {
        if (_pool.Count < _maxPoolSize)
            _pool.Add(generator);
    }
    
    public void Dispose()
    {
        _pool.Clear();
    }
}

// Usage
using var pool = new GeneratorPool(model, tokenizer);

var tasks = prompts.Select(async prompt =>
{
    var generator = pool.Rent();
    try
    {
        return await Task.Run(() => 
            generator.Generate(prompt, 50, 0.8));
    }
    finally
    {
        pool.Return(generator);
    }
});

var results = await Task.WhenAll(tasks);
```

## Best Practices

1. **Always call `model.Eval()`** before sharing model across threads
2. **Create one Sampling instance per thread** for best performance
3. **Limit concurrency** to avoid CPU oversubscription (typically `Environment.ProcessorCount`)
4. **Use `Task.Run`** for CPU-bound generation work
5. **Monitor memory** with large batches - consider processing in chunks
6. **Use async/await** for I/O operations (loading checkpoints, writing results)
7. **Vary random seeds** across concurrent requests for diverse outputs

## Performance Tips

### Optimal Concurrency

```csharp
// Find optimal concurrency for your hardware
int optimalConcurrency = Environment.ProcessorCount;

// For hyper-threaded CPUs, you might try:
int withHyperThreading = Environment.ProcessorCount * 2;

// But measure actual throughput - more threads isn't always better
```

### Reduce GC Pressure

```csharp
// Warm up the model and JIT before benchmarking
for (int i = 0; i < 10; i++)
{
    var warmupGen = new Sampling(model, tokenizer, model.BlockSize);
    warmupGen.Generate("warmup", 10, 0.8);
}

// Force GC before benchmark
GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();
```

## Complete Example: Web API Service

See [samples/ApiServer](../../samples/ApiServer/) for a complete implementation of:
- ASP.NET Core web API
- Request queueing
- Rate limiting
- Connection pooling
- Metrics collection

## Next Steps

- [Tutorial 3: Binary Checkpoints](03-binary-checkpoints.md) - Efficient model persistence
- [Tutorial 4: Training Basics](04-training-basics.md) - Train your own model
- [Tutorial 5: Advanced Training](05-advanced-training.md) - Optimization techniques

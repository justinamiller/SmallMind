# BenchmarkHarness and BenchmarkScenario Usage

## Overview

The `BenchmarkHarness` and `BenchmarkScenario` classes provide a complete framework for running performance benchmarks on SmallMind language models.

## Quick Start

```csharp
using SmallMind.Benchmarks.Core.Measurement;
using System;
using System.Threading.Tasks;

// Create a benchmark scenario
var scenario = new BenchmarkScenario
{
    ScenarioName = "gpt2-124m-baseline",
    ModelPath = "/path/to/model.gguf",
    ContextSize = 1024,
    ThreadCount = 0, // Auto-detect
    PromptText = "The quick brown fox",
    NumTokensToGenerate = 50,
    WarmupIterations = 2,
    MeasuredIterations = 5,
    Temperature = 0.8,
    TopK = 40,
    TopP = 0.95,
    Seed = 42 // For reproducibility
};

// Run the benchmark
using var harness = new BenchmarkHarness();
var result = await harness.RunScenarioAsync(scenario);

// Display results
Console.WriteLine($"Scenario: {result.ScenarioName}");
Console.WriteLine($"Model: {result.ModelName}");
Console.WriteLine($"TTFT: {result.TTFTMilliseconds:F2} ms");
Console.WriteLine($"Throughput: {result.TokensPerSecond:F2} tok/s");
Console.WriteLine($"Steady-state: {result.TokensPerSecondSteadyState:F2} tok/s");
Console.WriteLine($"Median: {result.MedianTokensPerSecond:F2} tok/s");
Console.WriteLine($"P90: {result.P90TokensPerSecond:F2} tok/s");
Console.WriteLine($"Peak RSS: {result.PeakRssBytes / 1024 / 1024} MB");
Console.WriteLine($"GC Gen0/1/2: {result.Gen0Collections}/{result.Gen1Collections}/{result.Gen2Collections}");
```

## Creating Scenarios

### Default Scenario
```csharp
var scenario = BenchmarkScenario.CreateDefault("/path/to/model.gguf");
```

### Custom Scenario
```csharp
var scenario = new BenchmarkScenario
{
    ScenarioName = "custom-scenario",
    ModelPath = "/models/llama-2-7b-q4.gguf",
    ContextSize = 2048,
    ThreadCount = 8,
    PromptText = "Write a story about",
    NumTokensToGenerate = 100,
    WarmupIterations = 3,
    MeasuredIterations = 10,
    Temperature = 0.9,
    TopK = 50,
    TopP = 0.95,
    Seed = null, // Non-deterministic
    CacheDirectory = "/tmp/gguf-cache"
};

// Validate before running
scenario.Validate(); // Throws if invalid
```

## Metrics Collected

### Timing Metrics
- **TTFT (Time To First Token)**: Latency from decode start to first token
- **Total Time**: End-to-end generation time
- **Tokens Per Second**: Overall throughput (total tokens / total time)
- **Steady-State Throughput**: Throughput excluding TTFT ((tokens - 1) / (total time - TTFT))

### Statistical Metrics
- **Median Tokens/Sec**: Median across measured iterations
- **P90 Tokens/Sec**: 90th percentile throughput
- **StdDev Tokens/Sec**: Standard deviation of throughput

### Memory Metrics
- **Peak RSS**: Maximum resident set size (working set)
- **Model Load RSS**: RSS after model loading
- **Bytes Allocated Per Token**: Average GC allocations per token
- **Bytes Allocated Per Second**: Allocation rate
- **GC Collections**: Gen0, Gen1, Gen2 counts

## Advanced Usage

### Multiple Scenarios
```csharp
var scenarios = new[]
{
    new BenchmarkScenario { ScenarioName = "baseline", /* ... */ },
    new BenchmarkScenario { ScenarioName = "optimized", /* ... */ },
};

using var harness = new BenchmarkHarness();
var results = new List<BenchmarkResult>();

foreach (var scenario in scenarios)
{
    var result = await harness.RunScenarioAsync(scenario);
    results.Add(result);
}

// Compare results
foreach (var result in results)
{
    Console.WriteLine($"{result.ScenarioName}: {result.TokensPerSecond:F2} tok/s");
}
```

### With Cancellation
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

try
{
    var result = await harness.RunScenarioAsync(scenario, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Benchmark cancelled");
}
```

## Configuration Guidelines

### Warmup Iterations
- **Minimum**: 1-2 iterations
- **Purpose**: Allow JIT compilation and cache warming
- **Excluded**: Not included in final metrics

### Measured Iterations
- **Recommended**: 5-10 iterations
- **Purpose**: Statistical stability
- **Trade-off**: More iterations = more accurate but longer runtime

### Thread Count
- **0 (Auto)**: Recommended for most cases
- **Explicit**: Set to specific count for reproducibility
- **Consideration**: SmallMind is CPU-bound, more threads ≠ faster

### Context Size
- **Match Model**: Use model's native context length
- **Memory**: Larger context = more memory usage
- **Performance**: Longer context may impact throughput

### Token Generation Count
- **Short (10-50)**: TTFT-dominated, quick benchmarks
- **Medium (100-256)**: Balanced TTFT + throughput
- **Long (512+)**: Throughput-focused, memory stress test

## Performance Tips

1. **Disable other workloads**: Run on dedicated hardware
2. **Fix CPU frequency**: Disable turbo boost for consistency
3. **Use deterministic seed**: For reproducible comparisons
4. **Warm filesystem cache**: Run once before benchmarking
5. **Monitor system**: Check for thermal throttling, memory pressure

## Example Output

```
Scenario: gpt2-124m-q4-baseline
Model: gpt2-124m-q4
Context Size: 1024
Thread Count: 16
Prompt Tokens: 0
Generated Tokens: 50

TTFT: 45.23 ms
Total Time: 2,345.67 ms
Throughput: 21.32 tok/s
Steady-State: 22.01 tok/s

Statistics (5 iterations):
  Median: 21.45 tok/s
  P90: 23.12 tok/s
  StdDev: 1.23 tok/s

Memory:
  Peak RSS: 487 MB
  Model Load RSS: 412 MB
  Allocated/Token: 4,523 bytes
  Allocated/Sec: 96,432 bytes

GC Collections: 2/0/0
Timestamp: 2024-02-13 20:30:45 UTC
```

## Integration with CI/CD

The harness is designed for automated benchmarking:

```csharp
// Run benchmark
var result = await harness.RunScenarioAsync(scenario);

// Check performance regression
const double BASELINE_TPS = 20.0;
const double TOLERANCE = 0.10; // 10%

if (result.MedianTokensPerSecond < BASELINE_TPS * (1 - TOLERANCE))
{
    Console.Error.WriteLine($"Performance regression detected: {result.MedianTokensPerSecond:F2} < {BASELINE_TPS * (1 - TOLERANCE):F2} tok/s");
    Environment.Exit(1);
}

Console.WriteLine($"Performance OK: {result.MedianTokensPerSecond:F2} tok/s");
```

## See Also

- `BenchmarkResult.cs`: Result data structure
- `Statistics.cs`: Statistical utilities
- `MemoryMeasurement.cs`: Memory tracking helpers

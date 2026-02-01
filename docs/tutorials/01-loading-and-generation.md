# Tutorial 1: Loading Models and Generating Text

This tutorial covers the basics of loading a trained model and using it to generate text with SmallMind.

## Overview

SmallMind supports two checkpoint formats:
- **Binary (.smnd)** - Fast, compact format (recommended)
- **JSON (.json)** - Human-readable format (for debugging)

## Loading a Checkpoint

### Binary Checkpoint (Recommended)

Binary checkpoints are faster to load and more space-efficient:

```csharp
using SmallMind.Core;
using SmallMind.Transformers;
using System.Threading;
using System.Threading.Tasks;

// Create checkpoint store
ICheckpointStore store = new BinaryCheckpointStore();

// Load checkpoint with cancellation support
using var cts = new CancellationTokenSource();
var checkpoint = await store.LoadAsync("model.smnd", cts.Token);

// Convert to model
var model = CheckpointExtensions.FromCheckpoint(checkpoint);

Console.WriteLine($"Loaded model:");
Console.WriteLine($"  Vocabulary: {model.VocabSize}");
Console.WriteLine($"  Context: {model.BlockSize} tokens");
Console.WriteLine($"  Layers: {model.NumLayers}");
Console.WriteLine($"  Heads: {model.NumHeads}");
Console.WriteLine($"  Embed Dim: {model.EmbedDim}");
```

### JSON Checkpoint (Legacy)

For backward compatibility or debugging:

```csharp
ICheckpointStore store = new JsonCheckpointStore();
var checkpoint = await store.LoadAsync("model.json", cts.Token);
var model = CheckpointExtensions.FromCheckpoint(checkpoint);
```

## Creating a Tokenizer

SmallMind supports character-level and BPE tokenization:

### Character Tokenizer

Simple and effective for small vocabularies:

```csharp
using SmallMind.Tokenizers;

// Create tokenizer with alphabet
var tokenizer = new CharTokenizer("abcdefghijklmnopqrstuvwxyz .,!?");

// Or create from training text
string trainingText = File.ReadAllText("data.txt");
var tokenizer2 = new CharTokenizer(trainingText);

Console.WriteLine($"Vocabulary size: {tokenizer.VocabSize}");
```

### BPE Tokenizer

For larger vocabularies and better compression:

```csharp
// Load pre-trained BPE tokenizer
var tokenizer = BpeTokenizer.FromFiles("vocab.txt", "merges.txt");

// Or train from scratch
var bpeTokenizer = BpeTokenizer.Train(
    text: trainingText,
    vocabSize: 1000,
    minFrequency: 2
);
```

## Basic Text Generation

### Greedy Generation

Simplest approach - always picks most likely token:

```csharp
using SmallMind.Runtime;

var generator = new Sampling(model, tokenizer, model.BlockSize);

var text = generator.Generate(
    prompt: "Once upon a time",
    maxNewTokens: 50,
    temperature: 0.0  // Greedy (deterministic)
);

Console.WriteLine(text);
```

### Temperature Sampling

Add randomness for more creative outputs:

```csharp
var text = generator.Generate(
    prompt: "The quick brown fox",
    maxNewTokens: 100,
    temperature: 0.8,  // Higher = more random (0.1-2.0)
    seed: 42           // For reproducibility
);

Console.WriteLine(text);
```

### Top-K Sampling

Limit sampling to top K most likely tokens:

```csharp
var text = generator.Generate(
    prompt: "In a galaxy far away",
    maxNewTokens: 100,
    temperature: 1.0,
    topK: 40,  // Only sample from top 40 tokens
    seed: 123
);

Console.WriteLine(text);
```

## Async Generation

For better responsiveness in applications:

```csharp
var options = new GenerationOptions
{
    MaxTokens = 100,
    Temperature = 0.8f,
    TopK = 40,
    Seed = 42
};

using var cts = new CancellationTokenSource();

var text = await generator.GenerateAsync(
    "Hello world",
    options,
    cts.Token
);

Console.WriteLine(text);
```

## Streaming Generation

Generate tokens one at a time for real-time display:

```csharp
var options = new GenerationOptions
{
    MaxTokens = 100,
    Temperature = 0.8f,
    TopK = 40
};

await foreach (var tokenId in generator.GenerateStreamAsync("Hello", options))
{
    var token = tokenizer.Decode(new[] { tokenId });
    Console.Write(token);
    await Task.Delay(50); // Simulate typing effect
}
Console.WriteLine();
```

## Complete Example

```csharp
using SmallMind.Core;
using SmallMind.Transformers;
using SmallMind.Tokenizers;
using SmallMind.Runtime;
using System;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        // Load checkpoint
        var store = new BinaryCheckpointStore();
        var checkpoint = await store.LoadAsync("model.smnd");
        var model = CheckpointExtensions.FromCheckpoint(checkpoint);
        
        // Create tokenizer
        var tokenizer = new CharTokenizer("abcdefghijklmnopqrstuvwxyz .,!?");
        
        // Create generator
        var generator = new Sampling(model, tokenizer, model.BlockSize);
        
        // Generate with different settings
        Console.WriteLine("=== Greedy (Deterministic) ===");
        var greedy = generator.Generate("Hello", 50, temperature: 0.0);
        Console.WriteLine(greedy);
        
        Console.WriteLine("\n=== Creative (High Temperature) ===");
        var creative = generator.Generate("Hello", 50, temperature: 1.5, seed: 42);
        Console.WriteLine(creative);
        
        Console.WriteLine("\n=== Balanced (Top-K) ===");
        var balanced = generator.Generate("Hello", 50, temperature: 0.8, topK: 40, seed: 42);
        Console.WriteLine(balanced);
        
        Console.WriteLine("\n=== Streaming ===");
        var options = new GenerationOptions { MaxTokens = 50, Temperature = 0.8f, TopK = 40 };
        await foreach (var tokenId in generator.GenerateStreamAsync("Hello", options))
        {
            Console.Write(tokenizer.Decode(new[] { tokenId }));
        }
        Console.WriteLine();
    }
}
```

## Best Practices

1. **Use binary checkpoints** for production - they're faster and smaller
2. **Set a seed** for reproducible results in testing
3. **Adjust temperature** based on use case:
   - 0.0-0.5: More deterministic, focused outputs
   - 0.5-1.0: Balanced creativity and coherence
   - 1.0-2.0: More creative but less coherent
4. **Use top-K** to prevent unlikely tokens while maintaining diversity
5. **Use async methods** in applications to keep UI responsive
6. **Use streaming** for better user experience with long generations

## Common Issues

### Empty or Nonsensical Output

- Model may not be trained enough
- Check that tokenizer matches training tokenizer
- Try lower temperature (e.g., 0.5)

### Repetitive Output

- Increase temperature (e.g., 1.0-1.5)
- Use top-K sampling (e.g., topK: 40)
- May indicate model needs more training

### Slow Generation

- Binary checkpoints load ~10x faster than JSON
- Consider using a smaller model for development
- See [Concurrent Inference Tutorial](02-concurrent-inference.md) for optimization

## Next Steps

- [Tutorial 2: Concurrent Inference](02-concurrent-inference.md) - Multi-threaded generation
- [Tutorial 3: Binary Checkpoints](03-binary-checkpoints.md) - Checkpoint management
- [Tutorial 4: Training Basics](04-training-basics.md) - Train your own model

# SmallMind Tutorials

This directory contains step-by-step tutorials for using the SmallMind library.

## Getting Started

1. [Loading Models and Generating Text](01-loading-and-generation.md) - Learn how to load checkpoints and generate text
2. [Concurrent Inference](02-concurrent-inference.md) - Best practices for multi-threaded text generation
3. [Binary Checkpoints](03-binary-checkpoints.md) - Working with efficient binary checkpoint format
4. [Training Your First Model](04-training-basics.md) - Train a small language model from scratch
5. [Advanced Training](05-advanced-training.md) - Learning rate schedules, gradient clipping, and optimization

## Sample Applications

- [Multi-threaded Text Generation](../../samples/MultiThreadedGeneration/) - Concurrent inference example
- [API Server](../../samples/ApiServer/) - REST API for serving inference
- [Document Summarization](../../samples/DocumentSummarization/) - Practical summarization workflow

## Key Principles

All SmallMind tutorials and examples follow these principles:

- **Pure C#** - No third-party dependencies, only .NET standard libraries
- **Educational** - Code is clear and well-documented for learning
- **Production-Ready** - Patterns suitable for real applications
- **CPU-Optimized** - SIMD acceleration for maximum CPU performance

## Prerequisites

- .NET 10.0 SDK or later
- Basic understanding of C# and language models
- Familiarity with async/await for concurrent examples

## Installation

```bash
# Install NuGet packages
dotnet add package SmallMind.Core
dotnet add package SmallMind.Transformers
dotnet add package SmallMind.Tokenizers
dotnet add package SmallMind.Runtime
```

## Quick Start

```csharp
using SmallMind.Core;
using SmallMind.Transformers;
using SmallMind.Tokenizers;
using SmallMind.Runtime;

// Load a checkpoint
var store = new BinaryCheckpointStore();
var checkpoint = await store.LoadAsync("model.smnd");
var model = CheckpointExtensions.FromCheckpoint(checkpoint);

// Generate text
var tokenizer = new CharTokenizer("abcdefghijklmnopqrstuvwxyz ");
var generator = new Sampling(model, tokenizer, model.BlockSize);

var options = new GenerationOptions 
{ 
    MaxTokens = 100,
    Temperature = 0.8f,
    TopK = 40,
    Seed = 42
};

var result = await generator.GenerateAsync("Hello", options);
Console.WriteLine(result);
```

## Support

- [GitHub Issues](https://github.com/justinamiller/SmallMind/issues)
- [Documentation](../../README.md)
- [API Reference](../README.md)

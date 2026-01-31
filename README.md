# SmallMind
SmallMind is a deliberately tiny, educational language model built in C#

## Overview
SmallMind demonstrates how to build a fast, efficient neural language model using only C# core libraries, without any external dependencies. It achieves high performance through careful application of modern C# optimization techniques.

## Performance Optimizations

This implementation uses several C# core library features to maximize training speed:

### 1. **SIMD Vectorization** (`System.Numerics.Vectors`)
- Uses hardware-accelerated SIMD operations for matrix multiplication
- Processes multiple floating-point operations in parallel using CPU vector units
- Automatically detects and uses available hardware acceleration (SSE, AVX)

### 2. **Parallel Processing** (`System.Threading.Tasks`)
- Trains on multiple sentences concurrently using `Parallel.ForEach`
- Scales automatically based on available CPU cores
- Thread-safe gradient accumulation using lock-based synchronization

### 3. **Memory Efficiency** (`System.Buffers`)
- Uses `ArrayPool<T>` to reuse temporary arrays and reduce GC pressure
- Zero-allocation operations with `Span<T>` and `Memory<T>`
- Minimizes heap allocations during training hot paths

### 4. **Method Inlining** (`System.Runtime.CompilerServices`)
- `AggressiveInlining` attribute on critical path methods
- Reduces function call overhead in tight loops
- Compiler-optimized performance for hot code paths

### 5. **Cache-Friendly Data Layout**
- Contiguous memory layout for weight matrices
- Sequential memory access patterns
- Optimized for CPU cache line utilization

## Performance Results

On a 4-core system:
- **Training Speed**: ~2ms per epoch
- **Throughput**: ~4,600 sentences/second
- **Total Training Time**: ~100ms for 50 epochs on 10 sentences
- **Hardware Acceleration**: SIMD enabled (automatically detected)

## Building and Running

```bash
# Build
dotnet build

# Run with Release configuration for best performance
dotnet run --configuration Release
```

## Architecture

- **Tokenizer**: Simple word-level tokenization with concurrent vocabulary management
- **Embeddings**: 32-dimensional word embeddings
- **Hidden Layer**: 64 neurons with ReLU activation
- **Output Layer**: Softmax classification
- **Loss Function**: Cross-entropy loss
- **Optimizer**: Stochastic Gradient Descent (SGD)

## Input Data Format

The model accepts **plain text string arrays** (`string[]`) as input:

```csharp
string[] trainingData = {
    "The cat sat on the mat",
    "The dog sat on the log",
    "Birds can fly high"
};

model.Train(trainingData);
```

You can load data from any source (JSON, XML, CSV, databases, web APIs) by converting it to a string array.

**📖 See [DATA_FORMATS.md](DATA_FORMATS.md) for comprehensive examples** of loading data from:
- Plain text files
- JSON files
- XML files
- CSV files
- Databases
- Web APIs

**Why plain text strings?**
- ✅ Simple and flexible
- ✅ Easy to load from any data source
- ✅ No forced dependency on specific file formats
- ✅ Works with streaming, batching, and real-time data
- ✅ Minimal memory overhead

## Key Features

✅ **Zero External Dependencies**: Uses only C# core libraries  
✅ **Fast Training**: Optimized for speed using SIMD and parallel processing  
✅ **Memory Efficient**: ArrayPool and Span<T> reduce allocations  
✅ **Thread-Safe**: Concurrent tokenization and parallel batch processing  
✅ **Educational**: Clean, well-documented code demonstrating optimization techniques  

## Requirements

- .NET 8.0 or later
- x64/ARM64 processor with SIMD support (for hardware acceleration)


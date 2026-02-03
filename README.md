# SmallMind - Production-Ready OSS Local Inference Runtime for .NET

[![Build and Test](https://github.com/justinamiller/SmallMind/actions/workflows/build.yml/badge.svg)](https://github.com/justinamiller/SmallMind/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET Version](https://img.shields.io/badge/.NET-10.0%2B-512BD4)](https://dotnet.microsoft.com/download)

SmallMind is a **production-ready, open source, local, CPU-first LLM inference runtime** built entirely in C# (.NET 10+) with **NO 3rd party dependencies**. It provides a stable public API for running decoder-only Transformers (GPT-style) on your infrastructure.

## Project Intent

**SmallMind is designed for production-grade local inference, not training at scale.**

The **stable public API** (`SmallMind.Abstractions` + `SmallMind.Engine` facade) is the recommended adoption surface for:
- Loading models for inference (`.smq`, `.gguf` import)
- Text generation with streaming and cancellation
- Multi-turn conversations with KV cache optimization
- Resource governance (token/time/memory budgets)
- Deterministic generation for testing and reproducibility

**Internals and experimental modules** (training, research utilities) may change between versions. The stable facade provides predictable, versioned access to the inference engine while keeping internal implementation transparent for learning and customization.

See [docs/stability-and-compatibility.md](docs/stability-and-compatibility.md) for detailed stability guarantees.

## Why SmallMind?

- ✅ **Zero Dependencies**: No black-box libraries - full control over your inference stack
- ✅ **Local & Private**: Runs entirely on your infrastructure, no external API calls
- ✅ **Stable Public API**: Semantic versioning with clear stability guarantees (no paid support)
- ✅ **Performance Optimized**: SIMD acceleration, quantization (Q8/Q4), KV caching
- ✅ **Production Ready**: Resource governance, budgets, deterministic mode, safe loading
- ✅ **RAG Built-in**: Document retrieval and citation-backed generation
- ✅ **Platform Native**: Pure .NET - runs on Windows, Linux, macOS, containers

## Installation

### NuGet Packages

```bash
# Stable public API (recommended for production)
dotnet add package SmallMind.Engine
dotnet add package SmallMind.Abstractions

# Optional: Core libraries for advanced scenarios
dotnet add package SmallMind.Core        # Tensor operations, SIMD
dotnet add package SmallMind.Transformers # Model implementations
dotnet add package SmallMind.Runtime     # Inference runtime
dotnet add package SmallMind.Rag         # RAG capabilities
```

### From Source

```bash
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind
dotnet build
```

## Quick Start (Stable API)

```csharp
using SmallMind.Abstractions;
using SmallMind.Engine;

// Create engine
using var engine = SmallMind.Create(new SmallMindOptions
{
    EnableKvCache = true,
    EnableRag = true
});

// Load model (.smq or .gguf)
using var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq",
    AllowGgufImport = true  // Auto-convert .gguf files
});

// Generate text with streaming
var request = new GenerationRequest
{
    Prompt = "Once upon a time",
    Options = new GenerationOptions
    {
        MaxNewTokens = 100,
        Temperature = 0.8,
        Mode = GenerationMode.Exploratory
    }
};

await foreach (var token in engine.GenerateStreamingAsync(model, request))
{
    if (token.Kind == TokenEventKind.Token)
        Console.Write(token.Text.ToString());
}
```

**See [docs/quickstart.md](docs/quickstart.md) for complete examples.**

## Documentation

### Stable API & Compatibility
- **[Stability and Compatibility](docs/stability-and-compatibility.md)** - What's stable, versioning policy, determinism, concurrency
- **[Compatibility Matrix](docs/compatibility-matrix.md)** - Model formats, quantization, tokenizers, runtime limits
- **[API Contract](docs/api-contract.md)** - Public API stability guarantees and usage patterns
- **[Operational Notes](docs/operational-notes.md)** - Exception handling, resource management, production deployment

### Getting Started
- **[Quick Start Guide](docs/quickstart.md)** - Installation, first steps, basic examples
- **[Pretrained Models](docs/pretrained-models.md)** - Using pretrained models and GGUF import
- **[Configuration](docs/configuration.md)** - Engine options, generation parameters, tuning

### Large Model Support 🆕
- **[Billion-Parameter Models](docs/LARGE_MODEL_SUPPORT.md)** - Support for models up to 2B parameters
  - Memory estimation and validation
  - Quantization recommendations (Q8/Q4)
  - Performance characteristics and hardware requirements
  - Example configurations (GPT-2 124M, 350M, 774M; LLaMA-style 1B+)

## Key Features

### 🔒 Stable Public API

Use the **stable contract** for production deployments:
- **Single entry point**: `SmallMind.Create()`
- **Clean interfaces**: `ISmallMindEngine`, `IModelHandle`, `IChatSession`, `IRagEngine`
- **Typed exceptions**: Actionable error messages with remediation
- **Semantic versioning**: Predictable API evolution

See [docs/api-contract.md](docs/api-contract.md) and [docs/stability-and-compatibility.md](docs/stability-and-compatibility.md) for detailed stability guarantees.

### ⚡ Production Features

- **Streaming**: Token-by-token output with cancellation support
- **KV Cache**: 13x speedup for multi-turn conversations
- **Batching**: 5x throughput for concurrent requests
- **Resource Governance**: Hard budgets (tokens, time, memory)
- **Deterministic Mode**: Same seed + prompt = identical output
- **Quantization**: Q8/Q4 for 4-7x memory reduction

### 📚 RAG (Retrieval-Augmented Generation)

Built-in document retrieval with citations:

```csharp
// Build index from documents
var index = await engine.Rag.BuildIndexAsync(new RagBuildRequest
{
    SourcePaths = new[] { "docs/" },
    IndexDirectory = "rag-index"
});

// Ask questions with citations
var answer = await engine.Rag.AskAsync(model, new RagAskRequest
{
    Query = "What is SmallMind?",
    Index = index,
    TopK = 5
});

Console.WriteLine(answer.Answer);
foreach (var citation in answer.Citations)
    Console.WriteLine($"  [{citation.Confidence:F2}] {citation.SourceUri}");
```

**🎯 Try the ITIL v4 Mastery Pack Demo**: A complete end-to-end demo showcasing RAG with real ITSM content:

```bash
# Run the interactive demo
./run-itil-demo.sh    # Linux/Mac
run-itil-demo.bat      # Windows

# Or manually
cd examples/ItilPackDemo
dotnet run
```

The demo shows:
- Loading knowledge packs (20 ITIL documents)
- Running Q&A queries with citation retrieval
- Structured JSON output for consulting scenarios
- Schema validation and evaluation

See [ITIL_DEMO_GUIDE.md](ITIL_DEMO_GUIDE.md) for full walkthrough.

### Run a GGUF Model in 2 Minutes

SmallMind supports importing quantized models from the popular GGUF format (Q8_0 and Q4_0 only):

```bash
# Import GGUF model to SMQ format
dotnet run --project src/SmallMind.Console import-gguf model.gguf model.smq

# Inspect the model
dotnet run --project src/SmallMind.Console inspect model.smq --tensors

# Verify integrity
dotnet run --project src/SmallMind.Console verify model.smq
```

**Supported quantization schemes:**
- **Q8_0**: 8-bit symmetric quantization (excellent accuracy, 4x compression)
- **Q4_0**: 4-bit symmetric quantization (good accuracy, 7x compression)

**Note**: K-quants (Q4_K_M, Q5_K_S, etc.) and other quantization types are not supported. The import will fail gracefully if unsupported types are encountered.

See [docs/quantization.md](docs/quantization.md) for detailed usage.

### Streaming Generation

SmallMind supports streaming token generation with full cancellation and hard budgets:

```csharp
using SmallMind.Runtime;

var options = new ProductionInferenceOptions
{
    MaxNewTokens = 100,
    MaxContextTokens = 2048,
    MaxTimeMs = 30000,  // 30 second timeout
    Temperature = 0.8,
    Seed = 42  // Deterministic generation
};

var engine = new InferenceEngine(model, tokenizer, blockSize, maxConcurrentSessions: 4);

await foreach (var token in engine.GenerateStreamAsync(prompt, options, cancellationToken))
{
    Console.Write(token.Text);  // Print tokens as they're generated
}
```

**Features:**
- **Streaming**: Tokens emitted as they're generated via `IAsyncEnumerable<GeneratedToken>`
- **Cancellation**: Full `CancellationToken` support for graceful shutdown
- **Hard budgets**: `MaxNewTokens`, `MaxContextTokens`, `TimeoutMs` strictly enforced
- **Deterministic mode**: Fixed seed produces identical output
- **Concurrent sessions**: Bounded concurrency for production deployments

See [examples/ProductionInference](examples/ProductionInference) for complete examples.

See [examples/MinimalGenerate](examples/MinimalGenerate) and [samples/](samples/) for complete working examples.

### Production-Grade Runtime Optimizations

SmallMind includes enterprise-ready performance features for high-throughput, low-latency inference:

#### KV Cache (13x Speedup)
```csharp
using SmallMind.Runtime.Cache;

// Enable KV caching for multi-turn conversations
var cacheOptions = new KvCacheOptions
{
    MaxTokensPerSession = 4096,
    MaxSessions = 1000,
    MaxBytesTotal = 1L * 1024 * 1024 * 1024  // 1GB
};

var cacheStore = new LruKvCacheStore(cacheOptions);
// 13x faster token generation by reusing attention key/values
```

**Benefits:**
- **13x speedup** in token generation (benchmarked)
- Lower latency for follow-up questions
- LRU eviction with bounded memory
- Zero allocations using `ArrayPool<float>`

#### Request Batching (5x Throughput)
```csharp
using SmallMind.Runtime.Batching;

// Batch concurrent requests for higher throughput
var batchingOptions = new BatchingOptions
{
    Enabled = true,
    MaxBatchSize = 8,
    MaxBatchWaitMs = 10
};

var engine = new BatchedInferenceEngine(
    model, tokenizer, blockSize, 
    batchingOptions, cacheStore
);

// Submit concurrent requests - automatically batched
var tasks = prompts.Select(p => engine.GenerateAsync(p, options));
var results = await Task.WhenAll(tasks);
// 5x higher tokens/second under load
```

**Benefits:**
- **5x throughput** improvement under concurrent load
- Automatic queue management and batch formation
- Per-request sampling isolation and cancellation
- Channel-based streaming for zero-copy responses

**See [docs/runtime_performance.md](docs/runtime_performance.md) for detailed usage and benchmarks.**

## What's New in v0.3.0

### Advanced Training Features
- **6 Learning Rate Schedulers**: Constant, Warmup, Cosine Annealing, Step Decay, Exponential, One-Cycle
- **Gradient Clipping**: Both value-based and norm-based clipping to prevent exploding gradients
- **Enhanced Optimizer**: AdamW with configurable gradient clipping

### Improved Developer Experience
- **TransformerModelBuilder**: Fluent API with preset configurations (Tiny, Small, Medium, Large)
- **Comprehensive Tutorials**: Step-by-step guides for loading, inference, and advanced training
- **Sample Applications**: Production-quality multi-threaded generation example

### Better Performance
- **Performance Benchmark Utility**: Built-in benchmarking with percentile metrics
- **Multi-threaded Sample**: Demonstrates concurrent inference patterns

## Features

- **Pure C# implementation** - No TorchSharp, no ONNX, no external ML libraries
- **Custom automatic differentiation** - Tensor class with backpropagation
- **SIMD-accelerated operations** - Hardware intrinsics for maximum CPU performance (NEW!)
  - AVX2+FMA, AVX, SSE, and NEON support with automatic CPU detection
  - Optimized matrix multiplication, activations, and element-wise operations
  - 8-30 GFLOPS on modern CPUs without GPU
- **Decoder-only Transformer architecture** with:
  - Token and positional embeddings
  - Masked multi-head self-attention
  - Feed-forward MLP with GELU activation
  - LayerNorm and residual connections
  - Final linear head to vocabulary
- **Flexible tokenization** with automatic selection:
  - **CharTokenizer** (default) - Simple character-level tokenization
  - **BpeTokenizer** - Production-ready Byte Pair Encoding
  - **Auto mode** - Automatically selects BPE if assets exist, falls back to Char
- **Training from scratch** on CPU with next-token prediction
- **Enhanced training mode** with:
  - Gradient accumulation for larger effective batch sizes
  - Learning rate warmup and cosine annealing
  - Validation loss tracking with best model saving
- **Text generation** with temperature sampling and top-k filtering
- **Production runtime optimizations** (NEW!)
  - KV cache for 13x faster multi-turn conversations
  - Request batching for 5x throughput under load
  - LRU eviction with bounded memory
  - Channel-based streaming responses
- **Workflow-aware generation** - Multi-step, deterministic, schema-safe AI workflows (NEW!)
  - Structured outputs (JSON, Enum, Regex-constrained)
  - Step-level validation and automatic repair
  - Budget enforcement and retry policies
  - Deterministic execution with seed control
- **Retrieval-Augmented Generation (RAG)** - Knowledge-grounded responses (NEW!)
  - BM25 sparse retrieval and dense vector search
  - Hybrid search combining multiple methods
  - Document ingestion with automatic chunking
  - Source citations in all responses
  - CLI tool for quick document indexing
  - Zero external dependencies
- **Question-answering capability** - Answer questions based on training data or indexed documents
- **Interactive conversation mode** - Multi-turn conversations with session context
- **Session context management** - Maintain conversation history across turns
- **Checkpointing** to save and load model weights (JSON format)
- **Multiple data loading formats** - Load training data from JSON, XML, CSV, text files, or directories
- **Fully self-contained** - only uses System.* namespaces

## SIMD Performance Optimization

SmallMind now includes **SIMD (Single Instruction Multiple Data)** optimizations that dramatically improve CPU performance without any external dependencies. The library automatically detects your CPU's capabilities and uses the best available instruction set.

### Supported CPU Features

| Platform | Instruction Set | Vector Width | Speedup |
|----------|----------------|--------------|---------|
| x86/x64  | AVX2 + FMA     | 256-bit (8 floats) | 8-16x |
| x86/x64  | AVX            | 256-bit (8 floats) | 6-12x |
| x86/x64  | SSE/SSE2       | 128-bit (4 floats) | 4-8x  |
| ARM64    | NEON (AdvSimd) | 128-bit (4 floats) | 4-8x  |
| Fallback | Vector&lt;T&gt;   | Runtime-detected   | 2-6x  |

### Performance Benchmarks

On a modern Intel CPU with AVX2+FMA (measured in Release mode):

| Operation | Throughput/Performance | Details |
|-----------|----------------------|---------|
| Element-wise Add | 33.9 GB/s | 10M elements |
| ReLU Activation | 30.0 GB/s | 10M elements |
| Matrix Multiply | 29.4 GFLOPS | 512×512 matrices |
| Dot Product | 8.4 GFLOPS | 10M elements |
| Softmax | 6.8 ms | 1000×1000 matrix |

**Note:** Performance varies by CPU. Run benchmarks on your hardware to see actual results.

### Running Benchmarks

```bash
# Build and run SIMD benchmarks
dotnet run --project benchmarks/SimdBenchmarks.csproj -c Release

# Run SIMD correctness tests
dotnet test --filter "FullyQualifiedName~SimdKernelTests" -c Release
```

### SIMD Implementation Details

SmallMind uses .NET's hardware intrinsics to implement optimized kernels for:

1. **Matrix Operations**: AVX2+FMA accelerated matrix multiplication with cache-friendly tiling
2. **Activation Functions**: SIMD ReLU, GELU (fast approximation), Leaky ReLU
3. **Element-wise Operations**: Vectorized add, subtract, multiply, FMA (fused multiply-add)
4. **Softmax**: Parallel row processing with SIMD max-finding and normalization
5. **Dot Product**: Horizontal SIMD reduction for inner products

For more details, see [.github/copilot-instructions-simd.md](.github/copilot-instructions-simd.md).

## Benchmarking

SmallMind includes a comprehensive benchmarking harness for measuring published/observable performance metrics with no third-party dependencies.

### Quick Start

```bash
# Run all benchmark scenarios
cd tools/SmallMind.Benchmarks
dotnet run -c Release -- --model /path/to/model.smq --scenario all

# Run specific scenarios
dotnet run -c Release -- --model model.smq --scenario ttft --iterations 50
dotnet run -c Release -- --model model.smq --scenario concurrency --concurrency 1,2,4,8,16
```

### Measured Metrics

- **TTFT (Time to First Token)**: p50/p90/p95/p99 latency to first token
- **Tokens/Sec**: Steady-state and overall throughput with percentiles
- **End-to-End Latency**: Complete request latency distributions
- **Concurrency Throughput**: Requests/sec and tail latency under load
- **Memory Footprint**: Working set, private memory, managed heap
- **GC & Allocations**: Collection counts, allocation rates, time in GC
- **Runtime Counters**: CPU usage, alloc rate, thread pool metrics

### Output

Benchmarks generate two files:
- `report.md` - Human-readable Markdown with full environment metadata
- `results.json` - Machine-readable JSON for CI/CD integration

Example:
```
benchmarks/results/20260202-123045/
  ├── report.md
  └── results.json
```

### Features

- **No dependencies**: Pure .NET 10 implementation
- **Cold & warm start**: Measure process startup and steady-state performance
- **Runtime counters**: Automatic capture via EventListener
- **Reproducible**: Full environment metadata (OS, CPU, .NET version, commit hash)

**See [tools/SmallMind.Benchmarks/README.md](tools/SmallMind.Benchmarks/README.md) for complete documentation.**

## Workflow-Aware Generation

SmallMind supports **multi-step, deterministic, schema-safe workflows** for producing structured, machine-consumable outputs instead of free-form chat responses.

### Key Features

- **Structured outputs**: JSON, Enum, Regex-constrained, or PlainText
- **Automatic validation and repair**: Invalid outputs are detected and automatically repaired
- **Deterministic execution**: Same seed + inputs → same outputs
- **Budget enforcement**: Control token usage and execution time
- **Step-level retry policies**: Automatic retry with repair prompts
- **Stateful context**: Share data across workflow steps

### Quick Example

```csharp
using SmallMind.Workflows;

// Define a workflow
var workflow = new WorkflowDefinition
{
    Name = "IT Ticket Triage",
    Version = "1.0",
    RunnerOptions = new WorkflowRunnerOptions
    {
        Deterministic = true,
        Seed = 42,
        Temperature = 0.3
    },
    Steps = new List<WorkflowStep>
    {
        new WorkflowStep
        {
            StepId = "classify",
            Title = "Classify Ticket Type",
            Instruction = "Classify this as incident, request, or problem.",
            InputSpec = new StepInputSpec
            {
                RequiredStateKeys = new List<string> { "ticket_description" }
            },
            OutputSpec = new StepOutputSpec
            {
                Format = OutputFormat.EnumOnly,
                AllowedValues = new List<string> { "incident", "request", "problem" },
                Strict = true
            }
        }
    }
};

// Create initial state
var state = new WorkflowState();
state.Set("ticket_description", "Database is down, affecting all users.");

// Execute workflow
var runner = new WorkflowRunner(model, tokenizer, blockSize);
var result = await runner.RunAsync(new WorkflowRunRequest
{
    Workflow = workflow,
    InitialState = state
});

// Access results
if (result.Status == WorkflowRunStatus.Success)
{
    var classification = result.FinalState.GetStepOutput("classify");
    Console.WriteLine($"Ticket type: {classification}");
}
```

### Example Workflows

Two complete workflow examples are included:

1. **IT Ticket Triage** (`samples/Workflows/ItTicketTriageWorkflow.cs`)
   - Classifies ticket type (incident/request/problem)
   - Determines severity (low/medium/high/critical)
   - Assigns to support group
   - Recommends next action (JSON output)

2. **Policy Decision** (`samples/Workflows/PolicyDecisionWorkflow.cs`)
   - Extracts relevant policy clause (JSON)
   - Determines compliance (enum: compliant/noncompliant/unknown)
   - Generates decision record with justification (JSON)

For comprehensive documentation, see [docs/WORKFLOWS.md](docs/WORKFLOWS.md).

## Pre-Trained Models

SmallMind now supports pre-trained models for common NLP tasks including **sentiment analysis** and **text classification**. These models are built on the same Transformer architecture but specialized for specific tasks.

### Supported Tasks

1. **Sentiment Analysis** - Classify text as Positive, Negative, or Neutral
2. **Text Classification** - Categorize text into custom labels
3. **Summarization** - Coming soon
4. **Question Answering** - Coming soon

### Quick Example: Sentiment Analysis

```csharp
using SmallMind.Runtime.PretrainedModels;

// Create a sentiment analysis model
var model = PretrainedModelFactory.CreateSentimentModel(
    vocabSize: tokenizer.VocabSize,
    domain: DomainType.Finance  // Finance, Healthcare, Legal, ECommerce, or General
);

// Analyze sentiment
var sentiment = model.AnalyzeSentiment("Stock prices surged on positive earnings!");
// Returns: "Positive"

// Get detailed scores
var scores = model.AnalyzeSentimentWithScores("Market remains uncertain.");
// Returns: { "Positive": 0.25, "Negative": 0.30, "Neutral": 0.45 }

// Save model as .smnd checkpoint
await PretrainedModelFactory.SaveAsync(model, "sentiment-finance.smnd");
```

### Quick Example: Text Classification

```csharp
// Create classifier with custom categories
var labels = new[] { "Technology", "Sports", "Politics", "Entertainment" };
var classifier = PretrainedModelFactory.CreateClassificationModel(
    vocabSize: tokenizer.VocabSize,
    labels: labels,
    domain: DomainType.General
);

// Classify text
var category = classifier.Classify("The team won the championship!");
// Returns: "Sports"

// Get probabilities for all categories
var probs = classifier.ClassifyWithProbabilities("New AI breakthrough announced.");
// Returns: { "Technology": 0.75, "Sports": 0.10, "Politics": 0.10, "Entertainment": 0.05 }
```

### Domain-Specific Models

SmallMind supports domain specialization for:
- **Finance** - Financial news, market analysis
- **Healthcare** - Medical records, health articles  
- **Legal** - Contracts, legal documents
- **E-commerce** - Product reviews, shopping trends
- **General** - All-purpose models

### Working with Datasets

Load and process labeled datasets for training:

```csharp
using SmallMind.Runtime.PretrainedModels;

// Load sentiment data
var samples = DatasetLoader.LoadSentimentData("data/pretrained/sentiment/sample-sentiment.txt");

// Split into train/validation
var (train, val) = DatasetLoader.SplitDataset(samples, trainRatio: 0.8);

// Print statistics
DatasetLoader.PrintStatistics(train, "Training Set");
// Output:
// Training Set Statistics:
// Total samples: 24
// Label distribution:
//   positive: 8 (33.3%)
//   negative: 8 (33.3%)
//   neutral: 8 (33.3%)
```

### Sample Datasets

The `data/pretrained/` directory includes sample datasets:
- `sentiment/sample-sentiment.txt` - General sentiment examples
- `finance/finance-sentiment.txt` - Financial sentiment examples
- `classification/topic-classification.txt` - Topic classification examples

### Model Checkpoints

All pre-trained models use the `.smnd` (SmallMind) binary checkpoint format:

```csharp
// Save model
await PretrainedModelFactory.SaveAsync(model, "my-model.smnd");

// Load model (automatically detects task type)
var loadedModel = await PretrainedModelFactory.LoadAsync("my-model.smnd", tokenizer);
Console.WriteLine($"Task: {loadedModel.Task}, Domain: {loadedModel.Domain}");
```

See [examples/PretrainedModels](examples/PretrainedModels) for complete working examples and [docs/pretrained-models.md](docs/pretrained-models.md) for comprehensive documentation.

## Retrieval-Augmented Generation (RAG)

SmallMind includes a **zero-dependency RAG system** that combines document retrieval with text generation for knowledge-grounded responses. The RAG system supports both sparse (BM25) and dense (vector) retrieval methods.

### Key Features

- **BM25 Sparse Retrieval** - Fast lexical matching using BM25 algorithm
- **Dense Vector Retrieval** - Optional feature hashing embeddings
- **Hybrid Search** - Combine sparse and dense methods for better results
- **Document Ingestion** - Automatic chunking and indexing
- **Citation Support** - All generated answers include source citations
- **Security & Authorization** - Built-in access control for chunks
- **LLM Integration** - Plug in any text generator (including SmallMind's own)
- **CLI Tool** - Command-line interface for quick document indexing and querying

### Quick Start

```csharp
using SmallMind.Rag;
using SmallMind.Rag.Pipeline;
using SmallMind.Rag.Generation;

// 1. Create and initialize RAG pipeline
var options = new RagOptions
{
    IndexDirectory = "./my-index",
    Chunking = new RagOptions.ChunkingOptions
    {
        MaxChunkSize = 512,
        OverlapSize = 64
    },
    Retrieval = new RagOptions.RetrievalOptions
    {
        TopK = 5,
        MinScore = 0.0f
    }
};

var pipeline = new RagPipeline(options);
pipeline.Initialize();

// 2. Ingest documents
pipeline.IngestDocuments("./my-documents", rebuild: true, 
    includePatterns: "*.txt;*.md");

// 3. Ask questions (returns prompt with context)
var prompt = pipeline.AskQuestion("What is SmallMind?");
Console.WriteLine(prompt);
```

### Using with LLM Generation

To get actual generated answers, plug in a text generator:

```csharp
using SmallMind.Rag.Generation;

// Create a text generator from your trained model
var generator = new SmallMindTextGenerator(model, tokenizer, blockSize);

// Create pipeline with generator
var pipeline = new RagPipeline(
    options,
    textGenerator: generator
);

pipeline.Initialize();
pipeline.IngestDocuments("./my-documents", rebuild: true);

// Now AskQuestion generates actual answers
var answer = pipeline.AskQuestion(
    "How do I train SmallMind?",
    maxTokens: 200,
    temperature: 0.7
);

Console.WriteLine(answer);
// Output: Based on the provided sources, to train SmallMind...
```

### CLI Tool (`smrag`)

The RAG CLI provides a convenient command-line interface:

```bash
# Ingest documents
dotnet run --project samples/SmallMind.Rag.Cli -- \
    ingest --path ./docs --index ./my-index

# Ask questions
dotnet run --project samples/SmallMind.Rag.Cli -- \
    ask --index ./my-index \
    --question "What is SmallMind?" \
    --topk 5
```

### Advanced Features

**Dense Retrieval:**
```csharp
// Enable vector-based retrieval
pipeline.EnableDenseRetrieval(embeddingDim: 256);
```

**Hybrid Search:**
```csharp
// Combine sparse and dense retrieval
pipeline.EnableDenseRetrieval(embeddingDim: 256);
pipeline.EnableHybridRetrieval(sparseWeight: 0.5f, denseWeight: 0.5f);
```

**Incremental Updates:**
```csharp
// Add new documents without rebuilding
pipeline.IngestDocuments("./new-docs", rebuild: false);
```

**Security & Authorization:**
```csharp
using SmallMind.Rag.Security;

var userContext = new UserContext("user123");
userContext.AllowedLabels.Add("public");
userContext.AllowedTags.Add("documentation");

// Retrieve with access control
var results = pipeline.Retrieve("query", userContext: userContext);
```

See [docs/RAG_AND_CHAT.md](docs/RAG_AND_CHAT.md) for comprehensive documentation, [examples/RAG_WITH_LLM.md](examples/RAG_WITH_LLM.md) for a complete code example, and [samples/RagChatExample.cs](samples/RagChatExample.cs) for a working sample application.

## Data Loading

SmallMind now supports loading training data from multiple sources using the `DataLoader` class:

### Supported Formats

1. **Text Files** - `FromTextFile()` - Plain text, one sentence per line
2. **JSON Files** - `FromJsonFile()` - Expected format: `{ "sentences": [...] }`
3. **XML Files** - `FromXmlFile()` - Extracts text from specified XML elements
4. **CSV Files** - `FromCsvFile()` - Column-based extraction with header handling
5. **Directories** - `FromDirectory()` - Batch load from multiple files
6. **Custom Delimiters** - `FromTextWithDelimiters()` - Split text by custom delimiters

### Quick Example

```csharp
using SmallMind;

// Load from JSON file
var trainingText = DataLoader.FromJsonFile("data.json");

// Load from multiple files in a directory
var trainingText = DataLoader.FromDirectory("training_data/");

// Load from XML with custom element
var trainingText = DataLoader.FromXmlFile("data.xml", elementName: "sentence");
```

See the `examples/` directory for comprehensive usage examples and the `sample_data/` directory for sample files in all supported formats.

### Sample Data

The repository includes sample data files in the `sample_data/` directory:
- `sample.txt` - Plain text format
- `sample.json` - JSON format
- `sample.xml` - XML format  
- `sample.csv` - CSV format

All files contain identical content to demonstrate format equivalence.

## Tokenization

SmallMind supports **two tokenization strategies** to balance simplicity and production readiness:

### 1. CharTokenizer (Default)
**Character-level tokenization** - Simple and works with any text without external assets.
- **Best for**: Learning, prototyping, small datasets, languages with large character sets
- **Vocabulary**: Built from unique characters in training text
- **No assets required**: Works out-of-the-box

```csharp
using SmallMind.Text;

// Create from training text
var tokenizer = new CharTokenizer("Hello World");
var tokens = tokenizer.Encode("Hello");
var text = tokenizer.Decode(tokens);
```

### 2. BpeTokenizer (Production)
**Byte Pair Encoding** - Production-oriented subword tokenization for better compression and generalization.
- **Best for**: Production deployments, larger models, multilingual text
- **Vocabulary**: Loaded from `vocab.json` and `merges.txt` files
- **Better compression**: Typically 3-5x fewer tokens than character-level
- **Requires assets**: `vocab.json` (token→ID mapping) and `merges.txt` (merge rules)

```csharp
using SmallMind.Text;

// Load from assets directory
var tokenizer = new BpeTokenizer("assets/tokenizers/default");
var tokens = tokenizer.Encode("Hello World");
var text = tokenizer.Decode(tokens);
```

### TokenizerFactory - Automatic Selection

Use `TokenizerFactory` to automatically select the right tokenizer based on available assets:

```csharp
using SmallMind.Text;

// Auto mode: Uses BPE if assets exist, otherwise falls back to CharTokenizer
var options = new TokenizerOptions 
{ 
    Mode = TokenizerMode.Auto,  // Auto | Char | Bpe
    TokenizerName = "default",
    Strict = false  // false = fallback to Char if BPE fails
};

var tokenizer = TokenizerFactory.Create(options, trainingText);
```

### Tokenizer Modes

| Mode | Behavior |
|------|----------|
| `Auto` | Tries BPE if assets exist, falls back to CharTokenizer otherwise |
| `Char` | Always uses CharTokenizer (requires training text) |
| `Bpe` | Always uses BPE (throws if assets missing in strict mode, falls back if non-strict) |

### Asset Discovery

TokenizerFactory searches for assets in this order:
1. **Explicit path**: `options.TokenizerPath` (if specified)
2. **Relative path**: `./assets/tokenizers/<TokenizerName>/`
3. **App directory**: `<AppContext.BaseDirectory>/assets/tokenizers/<TokenizerName>/`

### Creating BPE Assets

BPE tokenizer requires two files:

**vocab.json** - Maps tokens to IDs:
```json
{
  " ": 0,
  "a": 1,
  "hello": 42,
  "world": 43,
  "[UNK]": 100,
  "[EOT]": 101
}
```

**merges.txt** - Merge rules (one per line):
```
h e
l l
e l
hel lo
```

Sample assets are included in `assets/tokenizers/default/` with 103 tokens and 67 merge rules.

### Strict Mode

```csharp
// Strict mode: throws exception if BPE assets are missing
var strictOptions = new TokenizerOptions 
{ 
    Mode = TokenizerMode.Bpe,
    Strict = true  // Throw instead of fallback
};

try 
{
    var tokenizer = TokenizerFactory.Create(strictOptions);
}
catch (TokenizationException ex)
{
    // Provides actionable error message with:
    // - Searched locations
    // - Expected file formats
    // - How to fix the issue
}
```

### Backwards Compatibility

The original `Tokenizer` class is now an alias for `CharTokenizer`:
```csharp
// These are equivalent:
var tokenizer1 = new Tokenizer(text);
var tokenizer2 = new CharTokenizer(text);
```

All existing code using `Tokenizer` continues to work unchanged.

## Requirements

- .NET 10 SDK
- **No other dependencies!**
- No GPU required (CPU-only, training will be slow)

## Project Structure

SmallMind follows best practices for .NET library projects:

```
SmallMind/
├── src/                          # Source code
│   ├── SmallMind/               # Main library project (reusable)
│   │   ├── Core/                # Core neural network components
│   │   │   ├── Tensor.cs        # Custom tensor with autograd
│   │   │   ├── NeuralNet.cs     # Neural network layers
│   │   │   ├── Transformer.cs   # Transformer model
│   │   │   ├── Training.cs      # Training loop and optimizer
│   │   │   ├── MatrixOps.cs     # Optimized matrix operations
│   │   │   └── ...
│   │   ├── Simd/                # SIMD-accelerated kernels (NEW!)
│   │   │   ├── SimdCapabilities.cs   # CPU feature detection
│   │   │   ├── MatMulOps.cs          # SIMD matrix multiply
│   │   │   ├── ActivationOps.cs      # SIMD activations
│   │   │   ├── ElementWiseOps.cs     # SIMD element-wise ops
│   │   │   └── SoftmaxOps.cs         # SIMD softmax
│   │   ├── Text/                # Text processing utilities
│   │   │   ├── ITokenizer.cs     # Tokenizer interface
│   │   │   ├── Tokenizer.cs      # Alias for CharTokenizer (backwards compat)
│   │   │   ├── CharTokenizer.cs  # Character-level tokenizer (default)
│   │   │   ├── BpeTokenizer.cs   # Byte Pair Encoding tokenizer
│   │   │   ├── TokenizerFactory.cs # Auto-selection factory
│   │   │   ├── TokenizerOptions.cs # Tokenizer configuration
│   │   │   ├── DataLoader.cs     # Multi-format data loading
│   │   │   └── Sampling.cs       # Text generation sampling
│   │   ├── RAG/                 # Retrieval-Augmented Generation
│   │   ├── Embeddings/          # Embedding providers (TF-IDF)
│   │   └── Indexing/            # Vector indexing
│   └── SmallMind.Console/       # Demo console application
│       └── Program.cs           # CLI entry point
├── tests/                       # Unit and integration tests
│   └── SmallMind.Tests/        # Test project
│       ├── SimdKernelTests.cs  # SIMD correctness tests (NEW!)
│       └── ...
├── benchmarks/                  # Performance benchmarks
│   ├── SimdBenchmarks.cs       # SIMD performance tests (NEW!)
│   └── SimdBenchmarks.csproj
├── samples/                     # Example code and usage demos
│   ├── DataLoaderExample.cs    # Data loading examples
│   ├── Phase2OptimizationsExample.cs
│   └── sample_data/            # Sample data files
├── docs/                        # Documentation
│   ├── FEATURES.md             # Feature documentation
│   ├── LIBRARY_USAGE.md        # Library usage guide
│   └── ...
├── .github/
│   └── copilot-instructions-simd.md  # SIMD optimization guide (NEW!)
├── SmallMind.sln               # Solution file
└── README.md                    # This file
```

### Using SmallMind as a Library

SmallMind is designed to be used as a library in your own .NET projects:

```bash
# Reference the library in your project
dotnet add reference /path/to/SmallMind/src/SmallMind/SmallMind.csproj

# Or use as a NuGet package (when published)
dotnet add package SmallMind
```

Example usage:
```csharp
using SmallMind.Core;
using SmallMind.Text;

// Create and train a model
var tokenizer = new Tokenizer(trainingText);
var model = new TransformerModel(
    vocabSize: tokenizer.VocabSize,
    blockSize: 512,
    nEmbd: 128,
    nLayer: 4,
    nHead: 4,
    dropout: 0.1
);

// Train the model
var trainer = new Training(model, tokenizer, trainingText, learningRate: 3e-4);
trainer.Train(steps: 2000);

// Generate text
var generator = new Sampling(model, tokenizer);
string result = generator.Generate("Once upon a time", maxTokens: 200);
```

See [LIBRARY_USAGE.md](docs/LIBRARY_USAGE.md) for more details.

## Quick Start

### 1. Build the Project

```bash
# Clone the repository
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind

# Build the entire solution (library + console app + tests)
dotnet build

# OR build in release mode for better performance
dotnet build -c Release
```

### 2. Run Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal
```

### 3. Train the Model

Train on the default `data.txt` file (created automatically if missing):

```bash
# Run the console application
dotnet run --project src/SmallMind.Console --project src/SmallMind.Console

# OR for better performance
dotnet run --project src/SmallMind.Console --project src/SmallMind.Console -c Release
```

This will:
- Load or create `data.txt` with sample text
- Build a character-level vocabulary
- Train a tiny Transformer for 2000 steps (takes ~10-30 minutes on CPU)
- Save checkpoints to `checkpoints/model.json` every 500 steps
- Generate sample text after training

### 4. Generate Text from a Checkpoint

Skip training and generate text from an existing checkpoint:

```bash
dotnet run --project src/SmallMind.Console --project src/SmallMind.Console -- --no-train --load --prompt "Once upon a time" --steps 200

# OR with release build
dotnet run --project src/SmallMind.Console --project src/SmallMind.Console -c Release -- --no-train --load --prompt "Once upon a time" --steps 200
```

### 5. Custom Training Data

Replace `data.txt` with your own text file (at least 1000 characters recommended):

```bash
# Create your own data.txt
echo "Your custom training text here..." > data.txt

# Train on your data
dotnet run --project src/SmallMind.Console --project src/SmallMind.Console
```

## Command-Line Arguments

| Argument | Default | Description |
|----------|---------|-------------|
| `--model-preset NAME` | "default" | Choose a model architecture preset (default, tiny, mistral-medium, mistral-7b, deepseek) |
| `--list-presets` | - | List all available model presets with their configurations |
| `--no-train` | (train enabled) | Skip training, only generate |
| `--load` | (auto-detect) | Force load checkpoint before generation |
| `--prompt "text"` | "Once upon a time" | Starting text for generation |
| `--steps N` | 200 | Number of tokens to generate |
| `--temperature T` | 1.0 | Sampling temperature (0.1-2.0, lower=more conservative) |
| `--top-k K` | 0 | Top-k filtering (0=disabled, 40 is typical) |
| `--perf` | (disabled) | Show performance metrics with detailed timing and throughput statistics |
| `--perf-json` | (disabled) | Output performance metrics as JSON (machine-readable format) |
| `--bench` | (disabled) | Run benchmark mode with sweeps over different max_tokens configurations |
| `--block-size N` | (preset default) | Context window size (max: 32768, overrides preset) |
| `--max-block-size N` | 32768 | Override maximum block size limit for extremely large contexts |
| `--batch-size N` | (preset default) | Batch size for training (overrides preset, higher = better throughput, more memory) |
| `--auto-config` | (disabled) | Auto-configure block size and batch size based on system RAM and CPU |
| `--enhanced-training` | (disabled) | Use enhanced training with gradient accumulation and LR scheduling |
| `--grad-accum N` | 1 | Gradient accumulation steps (effective batch = batch-size × grad-accum) |
| `--warmup N` | 100 | Learning rate warmup steps |
| `--qa` | (disabled) | Question-answering mode - answer a question based on training data |
| `--interactive` | (disabled) | Interactive conversation mode with session context |

## Examples

```bash
# List all available model presets
dotnet run --project src/SmallMind.Console -- --list-presets

# Train with a specific model preset
dotnet run --project src/SmallMind.Console -- --model-preset mistral-medium

# Train with Mistral 7B inspired architecture
dotnet run --project src/SmallMind.Console -- --model-preset mistral-7b --enhanced-training

# Train with DeepSeek inspired architecture (larger model)
dotnet run --project src/SmallMind.Console -- --model-preset deepseek --enhanced-training --perf

# Use tiny preset for fast testing
dotnet run --project src/SmallMind.Console -- --model-preset tiny

# Generate with a specific preset without training
dotnet run --project src/SmallMind.Console -- --model-preset mistral-medium --no-train --prompt "Knowledge is" --steps 150

# Train and generate with default settings
dotnet run

# Generate with custom prompt and temperature
dotnet run --project src/SmallMind.Console -- --no-train --prompt "The wise owl" --steps 300 --temperature 0.8

# Generate with top-k sampling for more focused output
dotnet run --project src/SmallMind.Console -- --no-train --prompt "Knowledge is" --steps 150 --top-k 40 --temperature 1.2

# Train with real-time performance metrics
dotnet run --project src/SmallMind.Console -- --perf

# Generate with performance tracking
dotnet run --project src/SmallMind.Console -- --no-train --prompt "Once upon a time" --steps 200 --perf

# Use auto-configuration to determine optimal block size based on system resources
dotnet run --project src/SmallMind.Console -- --auto-config

# Use a custom block size (larger context window)
dotnet run --project src/SmallMind.Console -- --block-size 1024

# Use maximum block size with performance tracking
dotnet run --project src/SmallMind.Console -- --block-size 32768 --perf --no-train --prompt "Test" --steps 50

# Use extremely large block size with override (requires significant RAM, 128GB+)
dotnet run --project src/SmallMind.Console -- --block-size 65536 --max-block-size 65536 --batch-size 2 --perf

# Use custom batch size for better throughput (requires more memory)
dotnet run --project src/SmallMind.Console -- --batch-size 32 --block-size 512

# Enhanced training with gradient accumulation and learning rate scheduling
dotnet run --project src/SmallMind.Console -- --enhanced-training --grad-accum 4 --warmup 200 --perf

# Question-answering mode - ask a question based on training data
dotnet run --project src/SmallMind.Console -- --no-train --qa --prompt "What is knowledge?"

# Interactive conversation mode with session context
dotnet run --project src/SmallMind.Console -- --no-train --interactive

# Performance tracking with detailed metrics
dotnet run --project src/SmallMind.Console -- --no-train --prompt "Once upon a time" --steps 200 --perf

# JSON performance output for automated testing
dotnet run --project src/SmallMind.Console -- --no-train --prompt "Test" --steps 100 --perf-json

# Benchmark mode to test different configurations
dotnet run --project src/SmallMind.Console -- --no-train --bench --prompt "Knowledge is"
```

## Model Presets

SmallMind now supports multiple model architecture presets inspired by popular LLM approaches. Choose different presets to experiment with various model sizes and configurations:

### Available Presets

1. **default** - Original tiny model for educational purposes
   - 128 embedding dimensions, 4 layers, 4 attention heads
   - 512 token context window, batch size 16
   - Fast training on CPU, good for learning

2. **tiny** - Very small model for quick testing
   - 64 embedding dimensions, 2 layers, 2 attention heads
   - 256 token context window, batch size 32
   - Fastest training, ideal for prototyping

3. **mistral-medium** - Medium-sized balanced configuration
   - 192 embedding dimensions, 6 layers, 6 attention heads
   - 1024 token context window, batch size 12
   - Inspired by Mistral architecture, balanced performance

4. **mistral-7b** - Larger model with more capacity
   - 256 embedding dimensions, 8 layers, 8 attention heads
   - 2048 token context window, batch size 8
   - Inspired by Mistral 7B architecture, better quality but slower

5. **deepseek** - Large model optimized for reasoning
   - 320 embedding dimensions, 10 layers, 8 attention heads
   - 4096 token context window, batch size 4
   - Inspired by DeepSeek architecture, best for complex tasks

### Using Model Presets

```bash
# List all available presets with detailed information
dotnet run --project src/SmallMind.Console -- --list-presets

# Train with a specific preset
dotnet run --project src/SmallMind.Console -- --model-preset mistral-medium

# Override preset settings with custom values
dotnet run --project src/SmallMind.Console -- --model-preset tiny --block-size 512 --batch-size 16
```

**Note:** Larger presets (mistral-7b, deepseek) require more memory and train significantly slower on CPU. Start with smaller presets (tiny, default) for testing, then scale up as needed.

## Performance Feedback Mode

SmallMind now includes comprehensive performance tracking and benchmarking capabilities inspired by llama.cpp. These features help analyze both capacity (throughput) and user experience (latency) metrics.

### Basic Performance Tracking

Use the `--perf` flag to get detailed performance metrics after generation:

```bash
dotnet run --project src/SmallMind.Console -- --no-train --prompt "Once upon a time" --steps 200 --perf
```

Example output:
```
=== Performance Summary ===
Concurrency: 1
Max tokens: 200
Duration: 45.32s
Requests: total=1 completed=1 failed=0
Throughput: 4.41 tok/s, 0.02 req/s
Tokens: input=4 output=200

Latency (ms):
  TTFT:  p50=892.3, p95=892.3, p99=892.3, mean=892.3
  E2E:   p50=45324.1, p95=45324.1, p99=45324.1, mean=45324.1

Tokens/Request: p50=200.0, p95=200.0, p99=200.0, mean=200.0
```

### JSON Output

For machine-readable metrics, use `--perf-json`:

```bash
dotnet run --project src/SmallMind.Console -- --no-train --prompt "Test" --steps 100 --perf-json
```

Example output:
```json
{
  "concurrency": 1,
  "maxTokensRequested": 100,
  "totalRequests": 1,
  "completedRequests": 1,
  "failedRequests": 0,
  "durationSeconds": 22.45,
  "totalInputTokens": 1,
  "totalOutputTokens": 100,
  "tokensPerSecond": 4.45,
  "requestsPerSecond": 0.045,
  "ttft": {
    "min": 445.2,
    "mean": 445.2,
    "p50": 445.2,
    "p95": 445.2,
    "p99": 445.2,
    "max": 445.2
  },
  "e2eLatency": {
    "min": 22451.3,
    "mean": 22451.3,
    "p50": 22451.3,
    "p95": 22451.3,
    "p99": 22451.3,
    "max": 22451.3
  },
  "tokensPerRequest": {
    "min": 100.0,
    "mean": 100.0,
    "p50": 100.0,
    "p95": 100.0,
    "p99": 100.0,
    "max": 100.0
  }
}
```

### Benchmark Mode

Run sweeps over different `max_tokens` configurations to find optimal settings:

```bash
dotnet run --project src/SmallMind.Console -- --no-train --bench --prompt "Knowledge is"
```

Example output:
```
=== Benchmark Mode ===
Running performance sweeps over different configurations...

Running: concurrency=1, max_tokens=64
  Completed: 4.52 tok/s

Running: concurrency=1, max_tokens=128
  Completed: 4.48 tok/s

Running: concurrency=1, max_tokens=256
  Completed: 4.41 tok/s

Best throughput: 4.52 tok/s (concurrency=1, max_tokens=64)

=== Detailed Results (sorted by throughput) ===

Rank   | Concurrency  | Max Tokens  | Tok/s      | Req/s      | Duration   | Requests   | TTFT p95   | E2E p95   
--------------------------------------------------------------------------------------------------------------------------
1      | 1            | 64          | 4.52       | 0.02       | 14.16s     | 1          | 892.3ms    | 14159.2ms 
2      | 1            | 128         | 4.48       | 0.01       | 28.57s     | 1          | 889.1ms    | 28571.4ms 
3      | 1            | 256         | 4.41       | 0.00       | 58.05s     | 1          | 895.7ms    | 58046.8ms 
```

### Metrics Explained

**Throughput/Capacity Metrics:**
- `concurrency`: Number of in-flight requests (max and average)
- `max_tokens`: Requested maximum output tokens
- `total_requests`: Total requests started
- `completed_requests`: Successfully completed requests
- `failed_requests`: Requests that failed or timed out
- `duration_seconds`: Wall clock time for the run
- `total_output_tokens`: Sum of all generated tokens
- `total_input_tokens`: Sum of all prompt tokens
- `tok_per_sec`: Aggregate throughput (output tokens / duration)
- `req_per_sec`: Request throughput (completed requests / duration)

**Latency/UX Metrics:**
- `TTFT` (Time To First Token): Time from request start to first token emission
- `E2E` (End-to-End): Total time from request start to completion
- `Tokens/Request`: Distribution of output tokens per request
- All latency metrics include: min, mean, p50, p95, p99, max

### Notes

- Performance tracking has minimal overhead when `--perf` is not enabled
- CPU-only execution means true concurrency testing isn't applicable
- Benchmark mode tests different token lengths to find sweet spots
- JSON output is suitable for automated performance testing pipelines

## New Features

### Enhanced Training

SmallMind now supports enhanced training with:
- **Gradient Accumulation**: Simulate larger batch sizes without additional memory
- **Learning Rate Scheduling**: Cosine annealing with warmup for better convergence
- **Validation Loss Tracking**: Monitor overfitting and save best model
- **Best Model Saving**: Automatically save the model with lowest validation loss

Use `--enhanced-training` to enable these features:
```bash
dotnet run --project src/SmallMind.Console -- --enhanced-training --grad-accum 4 --warmup 200 --perf
```

### Question-Answering Mode

Ask questions based on the model's training data using the `--qa` flag:
```bash
dotnet run --project src/SmallMind.Console -- --no-train --qa --prompt "What is the quick brown fox?"
```

The Q&A engine:
- Extracts relevant context from training corpus using keyword matching
- Formats the question with context in a Q&A template
- Generates focused answers using lower temperature sampling
- Cleans up the response to extract just the answer

### Interactive Conversation Mode

Have multi-turn conversations with session context using `--interactive`:
```bash
dotnet run --project src/SmallMind.Console -- --no-train --interactive
```

Features:
- **Persistent Context**: Maintains conversation history across turns
- **Intelligent Truncation**: Automatically manages context window limits
- **Session Commands**:
  - `exit` - Exit the conversation
  - `clear` - Clear conversation history
  - `save` - Save session to JSON file
  - `history` - Show full conversation history
- **Session Persistence**: Save and load conversations for later

Example interaction:
```
You: What is the quick brown fox?
Assistant: The quick brown fox jumps over the lazy dog.
You: What else do you know?
Assistant: [Continues conversation with full context...]
```

## Model Architecture

**SmallMind supports multiple model architectures through presets.** Use `--model-preset` to choose between different configurations inspired by popular LLM approaches (default, tiny, mistral-medium, mistral-7b, deepseek). See the "Model Presets" section above for details.

**Default hyperparameters** (small for CPU training):

- Context length (block size): 512 tokens (configurable, max: 32768, can be overridden further)
- Embedding dimension: 128
- Number of layers: 4
- Number of attention heads: 4
- Dropout: 0.1 (training only)
- Batch size: 16 (configurable, auto-scales with block size)
- Learning rate: 3e-4 (AdamW optimizer)
- Training steps: 2000
- Vocabulary: Character-level (built from data.txt)

**Block Size Configuration:**

The context window (block size) can be configured in three ways:
1. **Default**: 512 tokens - good balance for CPU training
2. **Manual**: Use `--block-size N` to specify any size up to 32768
3. **Auto-configured**: Use `--auto-config` to automatically determine optimal size based on:
   - Available system RAM (primary factor)
   - CPU cores
   - Memory usage estimates for the model architecture

Auto-configuration algorithm:
- 128GB+ available RAM → 32768 tokens (maximum)
- 64GB+ available RAM → 16384 tokens
- 32GB+ available RAM → 8192 tokens
- 16GB+ available RAM → 6144 tokens
- 8GB+ available RAM → 4096 tokens
- 4-8GB available RAM → 2048 tokens
- 2-4GB available RAM → 1024 tokens
- 1-2GB available RAM → 512 tokens (default)
- <1GB available RAM → 256 tokens

**Maximum Block Size Override:**

For users with very high RAM (128GB+), you can override the maximum block size limit:
- Use `--max-block-size N` to set a higher limit (e.g., 32768, 65536)
- Note: Extremely large block sizes require proportionally more memory
- Memory usage grows with O(blockSize²) due to attention mechanism

**Batch Size Configuration:**

The batch size controls how many sequences are processed in parallel:
1. **Default**: 16 - good balance for most systems
2. **Manual**: Use `--batch-size N` to specify custom batch size
3. **Auto-configured**: Use `--auto-config` to automatically scale batch size inversely with block size

Auto-configuration scales batch size based on block size and available memory:
- Larger block sizes → smaller batches (to fit in memory)
- Smaller block sizes → larger batches (for better throughput)

## Project Structure

```
SmallMind/
├── SmallMind.csproj       # .NET 10 project file (NO dependencies!)
├── Program.cs             # CLI entry point and argument parsing
├── Tokenizer.cs           # Character-level tokenization
├── DataLoader.cs          # Load training data from JSON, XML, CSV, text files
├── Tensor.cs              # Custom tensor with automatic differentiation
├── NeuralNet.cs           # Neural network layers (Linear, Embedding, LayerNorm, etc.)
├── Transformer.cs         # Transformer model (attention, MLP, blocks)
├── Training.cs            # Dataset batching and training loop (with enhanced training)
├── Sampling.cs            # Text generation with temperature and top-k
├── Optimizer.cs           # AdamW optimizer with learning rate scheduling
├── QuestionAnsweringEngine.cs  # Q&A engine for answering questions
├── ConversationSession.cs # Session context management for conversations
├── data.txt               # Training corpus (auto-generated if missing)
├── sample_data/           # Sample data files in multiple formats
│   ├── sample.txt         # Plain text format
│   ├── sample.json        # JSON format
│   ├── sample.xml         # XML format
│   └── sample.csv         # CSV format
├── examples/              # Example code and documentation
│   ├── DataLoaderExample.cs  # Comprehensive data loading examples
│   └── README.md          # Examples documentation
├── Tests/                 # Unit tests
│   ├── SmallMind.Tests.csproj  # Test project file
│   └── DataLoaderTests.cs    # DataLoader unit tests
├── checkpoints/           # Model checkpoints (created during training)
│   ├── model.json         # Saved model weights
│   └── model_best.json    # Best model (lowest validation loss)
├── sessions/              # Conversation sessions (created when using --interactive)
└── README.md              # This file
```

## Implementation Details

### Pure C# - No Dependencies

This project implements everything from scratch using only C# standard library:

- **Tensor.cs**: Custom tensor class with:
  - N-dimensional array storage
  - Automatic differentiation (gradients)
  - Matrix multiplication
  - Element-wise operations
  - Broadcasting support
  
- **NeuralNet.cs**: Neural network building blocks:
  - Linear (fully connected) layers
  - Embedding layers
  - LayerNorm (layer normalization)
  - Dropout (regularization)
  - Activation functions (GELU, ReLU, Tanh)
  
- **Transformer.cs**: Complete Transformer implementation:
  - Multi-head attention with causal masking
  - Feed-forward MLP
  - Transformer blocks
  - Model forward pass
  
- **Optimizer.cs**: AdamW optimizer with:
  - First moment estimation
  - Second moment estimation
  - Bias correction
  - Weight decay
  - Learning rate scheduling support

- **Training.cs**: Training infrastructure:
  - Mini-batch generation
  - Cross-entropy loss computation
  - Gradient computation
  - Enhanced training with gradient accumulation
  - Learning rate warmup and cosine annealing
  - Validation loss tracking
  - Checkpoint save/load (JSON format)

- **QuestionAnsweringEngine.cs**: Q&A capabilities:
  - Question answering based on training data
  - Keyword-based context retrieval
  - Q&A prompt templates
  - Answer extraction and cleaning

- **ConversationSession.cs**: Session management:
  - Conversation history tracking
  - Context window management
  - Intelligent truncation
  - Session persistence (save/load)

### Performance Notes

Since this is pure C# without optimized linear algebra libraries, performance is **very limited**:
- Training is **extremely slow** (~1-2 hours or more for 2000 steps on modern CPU)
- No GPU acceleration available
- Matrix operations are not vectorized/optimized (but use parallel processing where beneficial)
- Batch size and model size severely impact speed

**Performance Optimizations Added:**
- Parallel processing for matrix multiplication in Tensor operations (for matrices with M >= 4)
- Parallel processing for attention score computation (for batch * heads >= 4)
- Parallel processing for softmax and attention application
- Auto-scaling batch size based on block size and available memory

**This is intentional for educational purposes!** The goal is to understand how everything works, not to train production models.

**For Testing:**
- Reduce `TRAIN_STEPS` to 100-200 for quick tests
- Reduce `BATCH_SIZE` to 4
- Reduce `BLOCK_SIZE` to 32
- Reduce `N_EMBD` to 64
- Reduce `N_LAYER` to 2

**Expected Training Times (approximate):**
- 100 steps with small model: ~2-5 minutes
- 500 steps with small model: ~10-25 minutes
- 2000 steps with default model: ~1-3 hours
- Times vary significantly based on CPU and number of cores

For production use, consider:
- Using TorchSharp or ML.NET with GPU support
- Optimizing matrix operations with SIMD
- Using native libraries like MKL or OpenBLAS

## Troubleshooting

### Training is Too Slow

Training on CPU with pure C# is inherently slow. To speed up:
- Build in Release mode: `dotnet run -c Release`
- Reduce `TRAIN_STEPS` in Program.cs (try 500-1000)
- Reduce `BATCH_SIZE` (try 8 or 4)
- Reduce `BLOCK_SIZE` (try 64)
- Reduce `N_EMBD` (try 64)

### Out of Memory

If training runs out of memory:
- Reduce `BATCH_SIZE` in Program.cs (try 8 or 4)
- Reduce `BLOCK_SIZE` (try 64)
- Reduce `N_EMBD` (try 64)
- Use smaller training data

### Generated Text is Gibberish

This is expected with:
- Very short training (< 500 steps)
- Very small training data (< 500 characters)
- Model hasn't converged yet

Try:
- Training for more steps (5000+)
- Using more training data (10KB+ text)
- Lowering temperature for more conservative generation
- Waiting longer - the model learns slowly without GPU

### Checkpoint Loading Fails

- Checkpoints are saved as JSON in `checkpoints/model.json`
- Make sure the checkpoint was created successfully
- Check that the model architecture hasn't changed
- If needed, delete old checkpoints and retrain


## Production Usage Guide

SmallMind has been enhanced with production-ready features for deployment in real-world applications.

### Dependency Injection Setup

For production services, use .NET's dependency injection. See [docs/configuration.md](docs/configuration.md) for complete examples.

### Training with Cancellation

Production training supports graceful cancellation:

```csharp
var cts = new CancellationTokenSource();
training.Train(..., cancellationToken: cts.Token);
```

### Observability

- **Logging**: Structured logging via Microsoft.Extensions.Logging
- **Metrics**: Prometheus/OpenTelemetry compatible via System.Diagnostics.Metrics
- **Health Checks**: Kubernetes-ready readiness/liveness probes

See [docs/observability.md](docs/observability.md) for details.

### Thread Safety

| Component | Thread Safety | Usage |
|-----------|--------------|--------|
| `Tokenizer` | ✅ Thread-safe | Singleton |
| `TransformerModel` (inference) | ✅ Thread-safe | Singleton with `model.Eval()` |
| `Training` | ❌ Not thread-safe | Single thread only |
| `Sampling` | ❌ Not thread-safe | Per-request scope |

See [docs/threading-and-disposal.md](docs/threading-and-disposal.md) for concurrency patterns.

### Exception Handling

SmallMind uses a custom exception hierarchy with error codes:

- `ValidationException` - Input validation failures
- `TrainingException` - Training operation failures
- `CheckpointException` - Checkpoint I/O failures
- `ShapeMismatchException` - Tensor shape incompatibilities

### Performance Tips

1. **Always use Release builds** (5-10x faster than Debug)
2. **SIMD is automatic** - uses best available CPU instructions
3. **Tune batch size** based on available memory
4. **Use checkpoints** frequently during long training

### Documentation

- [Configuration Guide](docs/configuration.md)
- [Observability Guide](docs/observability.md)
- [Threading & Disposal Guide](docs/threading-and-disposal.md)
- [Troubleshooting Guide](docs/troubleshooting.md)
- [Versioning Policy](docs/VERSIONING.md)
- [CHANGELOG.md](CHANGELOG.md)

### Implementation Roadmaps

- [Monte Carlo Planning (MCP) Framework Roadmap](MCP_FRAMEWORK_ROADMAP.md) - Comprehensive guide for implementing agent-based planning with MCTS

## Educational Notes

### Why Pure C#?

This implementation deliberately avoids external libraries to demonstrate:
1. How automatic differentiation works
2. How backpropagation flows through neural networks
3. How matrix operations compose to form complex models
4. The mathematical foundations of Transformers

It's a learning tool, not a production framework!

### Causal Masking

The attention mechanism uses a causal mask (lower triangular) to prevent tokens from attending to future positions. This ensures the model learns to predict the next token based only on previous context.

### Character-Level Tokenization

Unlike modern LLMs that use subword tokenization (BPE, WordPiece), this implementation uses character-level tokens for simplicity. This means:
- Vocabulary size is very small (~50-100 characters)
- Model must learn to compose characters into words
- Generation is slower (more tokens per word)
- Good for educational purposes and small datasets

### Mini-Batching

Training uses random batches of sequences sampled from the training data. Each sequence is `BLOCK_SIZE` tokens long, and the model learns to predict the next token at each position.

### Temperature Sampling

- Temperature = 1.0: Use raw model probabilities
- Temperature < 1.0: More conservative (sharpen distribution)
- Temperature > 1.0: More random (flatten distribution)

### Top-K Filtering

Only sample from the K most likely tokens. Helps prevent the model from generating very unlikely tokens.

## Extending the Project

### Increase Model Size

Edit `Program.cs` constants:
```csharp
private const int N_EMBD = 256;      // Increase embedding dimension
private const int N_LAYER = 8;       // More layers
private const int N_HEAD = 8;        // More attention heads
private const int BLOCK_SIZE = 256;  // Longer context
```

Note: Larger models train much slower without GPU!

### Optimize Performance

- Use `Span<T>` and `Memory<T>` for better memory efficiency
- Implement SIMD vectorization for matrix operations
- Add parallel processing for batch operations
- Consider using `System.Numerics.Tensors` in .NET 9+

### Add Features

- Validation loss tracking
- Learning rate scheduling
- Gradient clipping
- Early stopping
- BPE tokenization
- Model architecture search

## Comparison: TorchSharp vs Pure C#

| Feature | TorchSharp Version | Pure C# Version |
|---------|-------------------|-----------------|
| Dependencies | TorchSharp NuGet | None |
| Training Speed | Fast (optimized) | Slow (unoptimized) |
| GPU Support | Yes | No |
| Code Complexity | Lower (uses library) | Higher (implements everything) |
| Educational Value | Medium | Very High |
| Production Ready | Yes | No |
| Binary Size | Large (~500MB+) | Small (~200KB) |

## Documentation

Comprehensive documentation for the stable public API:

### Core Documentation
- **[API Contract](docs/api-contract.md)** - Stability guarantees, versioning policy, capability discovery
- **[QuickStart Guide](docs/quickstart.md)** - Copy-paste examples for common scenarios
- **[Operational Notes](docs/operational-notes.md)** - Error taxonomy, budgets, performance tuning

### Samples
- **[SmallMind.QuickStart](samples/SmallMind.QuickStart/)** - Demonstrates the stable API "golden path"
- **[Production Examples](examples/)** - Advanced scenarios (KV cache, batching, RAG)

### Architecture (Educational)
- **[Quantization](docs/quantization.md)** - SMQ format, GGUF import, Q8/Q4 schemes
- **[RAG Implementation](docs/rag.md)** - Document ingestion, retrieval, citation
- **[Workflows](docs/WORKFLOWS.md)** - LLM-powered decision automation

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for:
- How to report bugs and request features
- Pull request process and code style guidelines
- Development setup instructions
- Community standards and expectations

For questions or discussions, please open an issue on GitHub.

## License

MIT License - Copyright © 2024-2026 Justin Miller

This is an educational project. Feel free to use and modify for learning purposes.

See [LICENSE](LICENSE) for full license text.

## References

- Attention Is All You Need (Vaswani et al., 2017)
- Language Models are Unsupervised Multitask Learners (Radford et al., 2019)
- The Annotated Transformer: http://nlp.seas.harvard.edu/annotated-transformer/
- Understanding Automatic Differentiation in 30 lines of Python

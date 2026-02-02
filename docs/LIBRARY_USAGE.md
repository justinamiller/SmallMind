# SmallMind Library Structure

SmallMind is now organized as a reusable C# library with a clean folder structure and well-defined interfaces.

## Folder Structure

```
SmallMind/
├── Core/                    # Core neural network components
│   ├── Tensor.cs           # Tensor operations with automatic differentiation
│   ├── NeuralNet.cs        # Neural network layers (Linear, LayerNorm, etc.)
│   ├── Transformer.cs      # Transformer model architecture
│   ├── Optimizer.cs        # Optimization algorithms (AdamW)
│   └── Training.cs         # Training loop and dataset handling
│
├── Embeddings/              # Embedding providers
│   ├── IEmbeddingProvider.cs        # Embedding provider interface
│   └── TfidfEmbeddingProvider.cs    # TF-IDF based embeddings (no external deps)
│
├── Indexing/                # Vector indexing and search
│   └── VectorIndex.cs      # kNN vector search with cosine similarity
│
├── RAG/                     # Retrieval-Augmented Generation
│   ├── IRetriever.cs       # Retriever interface
│   ├── Retriever.cs        # Chunk retrieval from vector index
│   ├── IPromptBuilder.cs   # Prompt builder interface
│   ├── PromptBuilder.cs    # RAG prompt formatting
│   ├── Answerer.cs         # LLM answering with citations
│   └── QuestionAnsweringEngine.cs  # Q&A engine
│
├── Text/                    # Text processing
│   ├── ITokenizer.cs       # Tokenizer interface
│   ├── Tokenizer.cs        # Character-level tokenizer
│   ├── Sampling.cs         # Text generation (greedy, temperature, top-k)
│   ├── DataLoader.cs       # Data loading from various formats
│   └── ConversationSession.cs  # Conversation management
│
├── Tests/                   # Unit tests
│   ├── DataLoaderTests.cs  # Data loader tests
│   └── EmbeddingTests.cs   # Embedding and RAG tests
│
└── Program.cs              # Console application demo
```

## Using SmallMind as a Library

### 1. Reference the Project

Add a project reference to `SmallMind.csproj` in your application:

```xml
<ProjectReference Include="../SmallMind/SmallMind.csproj" />
```

### 2. Add Using Directives

```csharp
using SmallMind.Core;
using SmallMind.Text;
using SmallMind.Embeddings;
using SmallMind.Indexing;
using SmallMind.RAG;
```

### 3. Example: Basic LLM Usage

```csharp
// Load training data
string trainingText = DataLoader.FromTextFile("data.txt");

// Create tokenizer
var tokenizer = new Tokenizer(trainingText);

// Create and train model
var model = new TransformerModel(
    vocabSize: tokenizer.VocabSize,
    blockSize: 256,
    nEmbed: 128,
    nLayers: 4,
    nHeads: 4,
    dropout: 0.1
);

// Train the model
var training = new Training(model, tokenizer, trainingText, 
    blockSize: 256, batchSize: 16, seed: 42);
training.Train(iterations: 1000, learningRate: 3e-4);

// Generate text
var sampler = new Sampling(model, tokenizer, blockSize: 256);
string output = sampler.Generate("Once upon a time", maxNewTokens: 100);
```

### 4. Example: RAG (Retrieval-Augmented Generation)

```csharp
// Create embedding provider
var embedder = new TfidfEmbeddingProvider(maxFeatures: 512);

// Build vector index from documents
var documents = new List<string>
{
    "Machine learning is a subset of AI.",
    "Neural networks are inspired by the brain.",
    "Deep learning uses multiple layers."
};

var index = new VectorIndex(embedder, indexDirectory: "./index");
index.Rebuild(documents);
index.Save();

// Create RAG components
var retriever = new Retriever(index, defaultK: 5);
var promptBuilder = new PromptBuilder();
var answerer = new Answerer(retriever, promptBuilder, sampler);

// Ask a question
var result = answerer.Answer("What is machine learning?");
Console.WriteLine(result.Answer);

// Display citations
foreach (var citation in result.Citations)
{
    Console.WriteLine($"[Score: {citation.Score:F3}] {citation.Text}");
}
```

## Key Interfaces

### IEmbeddingProvider
Convert text to vector embeddings for semantic search.

### ITokenizer
Convert between text and token IDs for language models.

### IRetriever
Retrieve relevant text chunks for a query.

### IPromptBuilder
Build prompts for RAG with context formatting.

## Performance Optimizations

All LINQ usage has been removed from hot paths:
- ✅ Tensor operations use manual loops
- ✅ Tokenization avoids allocations
- ✅ Retrieval uses efficient sorting
- ✅ Data loading minimizes intermediate collections

## Console Demo

Run `Program.cs` to see the library in action:

```bash
dotnet run
```

The demo includes:
- Model training
- Text generation
- Conversation mode
- Q&A mode
- Multiple model presets (tiny, default, mistral-7b, deepseek)

## Testing

Run all tests:

```bash
cd Tests
dotnet test
```

All 18 tests pass, covering:
- Data loading from various formats
- TF-IDF embeddings
- Vector indexing and search
- RAG retrieval and prompting

## No External Dependencies

SmallMind is 100% pure C# with no third-party NuGet packages (except for testing).
Everything is implemented from scratch for educational purposes.

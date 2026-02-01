# RAG with LLM Integration Example

This example demonstrates how to use SmallMind's RAG system with actual LLM generation.

## Prerequisites

1. A trained SmallMind model (`.smnd` checkpoint file)
2. A tokenizer compatible with your model
3. Documents to index

## Complete Example

```csharp
using System;
using System.IO;
using SmallMind.Core;
using SmallMind.Text;
using SmallMind.Tokenizers;
using SmallMind.Rag;
using SmallMind.Rag.Pipeline;
using SmallMind.Rag.Generation;

class RagWithLLMExample
{
    static async Task Main(string[] args)
    {
        // Step 1: Load your trained model
        Console.WriteLine("Loading model...");
        var store = new BinaryCheckpointStore();
        var checkpoint = await store.LoadAsync("path/to/your/model.smnd");
        var model = CheckpointExtensions.FromCheckpoint(checkpoint);
        
        // Step 2: Create tokenizer
        var tokenizer = new CharTokenizer("abcdefghijklmnopqrstuvwxyz ");
        int blockSize = model.BlockSize;
        
        // Step 3: Create text generator
        Console.WriteLine("Creating text generator...");
        var generator = new SmallMindTextGenerator(model, tokenizer, blockSize);
        
        // Step 4: Configure RAG options
        var options = new RagOptions
        {
            IndexDirectory = "./rag-index",
            Chunking = new RagOptions.ChunkingOptions
            {
                MaxChunkSize = 512,    // Max characters per chunk
                OverlapSize = 64,      // Overlap between chunks
                MinChunkSize = 50      // Minimum chunk size
            },
            Retrieval = new RagOptions.RetrievalOptions
            {
                TopK = 5,              // Retrieve top 5 chunks
                MinScore = 0.0f        // No minimum score threshold
            },
            Deterministic = true,
            Seed = 42
        };
        
        // Step 5: Create RAG pipeline with generator
        Console.WriteLine("Creating RAG pipeline...");
        var pipeline = new RagPipeline(
            options,
            textGenerator: generator  // Enable LLM generation
        );
        
        // Step 6: Initialize pipeline
        pipeline.Initialize();
        Console.WriteLine($"Pipeline initialized: {pipeline.IsTextGenerationEnabled}");
        
        // Step 7: Ingest documents
        Console.WriteLine("Ingesting documents...");
        string documentsPath = "./my-documents";
        
        if (!Directory.Exists(documentsPath))
        {
            Console.WriteLine($"Creating sample document directory: {documentsPath}");
            Directory.CreateDirectory(documentsPath);
            
            // Create sample document
            File.WriteAllText(
                Path.Combine(documentsPath, "sample.txt"),
                "SmallMind is a pure C# language model. It uses a Transformer architecture " +
                "with self-attention. The model supports training and inference on CPU. " +
                "Key features include automatic differentiation and SIMD optimizations."
            );
        }
        
        pipeline.IngestDocuments(
            documentsPath,
            rebuild: true,
            includePatterns: "*.txt;*.md"
        );
        
        Console.WriteLine($"Indexed {pipeline.ChunkCount} chunks");
        
        // Step 8: Ask questions and get generated answers
        Console.WriteLine("\n=== Asking Questions ===\n");
        
        var questions = new[]
        {
            "What is SmallMind?",
            "What architecture does it use?",
            "What are the key features?"
        };
        
        foreach (var question in questions)
        {
            Console.WriteLine($"Q: {question}");
            
            // Generate answer using RAG
            var answer = pipeline.AskQuestion(
                question,
                maxTokens: 100,
                temperature: 0.7
            );
            
            Console.WriteLine($"A: {answer}");
            Console.WriteLine();
        }
        
        // Step 9: Enable dense retrieval for better results (optional)
        Console.WriteLine("\n=== Enabling Dense Retrieval ===\n");
        pipeline.EnableDenseRetrieval(embeddingDim: 256);
        Console.WriteLine($"Dense retrieval enabled: {pipeline.IsDenseRetrievalEnabled}");
        
        // Step 10: Enable hybrid search (optional)
        pipeline.EnableHybridRetrieval(sparseWeight: 0.5f, denseWeight: 0.5f);
        Console.WriteLine($"Hybrid retrieval enabled: {pipeline.IsHybridRetrievalEnabled}");
        
        // Ask questions with hybrid retrieval
        var hybridAnswer = pipeline.AskQuestion(
            "Tell me about SmallMind",
            maxTokens: 150,
            temperature: 0.7
        );
        Console.WriteLine($"\nHybrid RAG Answer: {hybridAnswer}");
    }
}
```

## Using the CLI Tool

Alternatively, you can use the `smrag` CLI tool:

```bash
# Build the CLI
cd samples/SmallMind.Rag.Cli
dotnet build

# Ingest documents
dotnet run -- ingest --path ../../docs --index ./my-index

# Ask questions (returns context only, as CLI doesn't have LLM integration yet)
dotnet run -- ask --index ./my-index --question "What is SmallMind?" --topk 5
```

## Advanced Usage

### Custom Authorizer

```csharp
using SmallMind.Rag.Security;

// Create custom authorizer
public class MyAuthorizer : IAuthorizer
{
    public bool IsAuthorized(UserContext user, Chunk chunk)
    {
        // Implement your custom authorization logic
        return user.AllowedLabels.Contains(chunk.SecurityLabel);
    }
}

// Use in pipeline
var pipeline = new RagPipeline(
    options,
    authorizer: new MyAuthorizer(),
    textGenerator: generator
);
```

### Custom Logger

```csharp
using SmallMind.Rag.Telemetry;

public class MyLogger : IRagLogger
{
    public void LogInfo(string traceId, string message)
    {
        Console.WriteLine($"[{traceId}] INFO: {message}");
    }
    
    public void LogWarning(string traceId, string message)
    {
        Console.WriteLine($"[{traceId}] WARN: {message}");
    }
    
    public void LogError(string traceId, string operation, Exception ex)
    {
        Console.Error.WriteLine($"[{traceId}] ERROR in {operation}: {ex.Message}");
    }
}

var pipeline = new RagPipeline(
    options,
    logger: new MyLogger(),
    textGenerator: generator
);
```

## Performance Tips

1. **Chunk Size**: Use 300-600 characters for best results
2. **Overlap**: 10-20% overlap maintains context continuity
3. **TopK**: Start with 3-5 chunks, adjust based on quality
4. **Temperature**: Use 0.3-0.7 for factual responses
5. **Hybrid Search**: Combine sparse + dense for best accuracy

## Next Steps

- See [docs/RAG_AND_CHAT.md](../../docs/RAG_AND_CHAT.md) for comprehensive documentation
- Check [samples/RagChatExample.cs](../../samples/RagChatExample.cs) for more examples
- Review the CLI README at [samples/SmallMind.Rag.Cli/README.md](../../samples/SmallMind.Rag.Cli/README.md)

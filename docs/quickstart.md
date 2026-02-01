# SmallMind Quickstart Guide

This guide provides copy/paste examples for common SmallMind operations. All examples use the stable public API from `SmallMind.Abstractions` and `SmallMind.Engine`.

## Prerequisites

Add the SmallMind NuGet package to your project:

```bash
dotnet add package SmallMind
```

Required using directives for all examples:

```csharp
using SmallMind.Abstractions;
using SmallMind.Engine;
using System;
using System.Threading;
using System.Threading.Tasks;
```

---

## Example 1: Load a Model

### Load .smq Model (Native Format)

```csharp
using var engine = SmallMind.Create();

var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq",
    Threads = Environment.ProcessorCount // Use all CPU cores
});

Console.WriteLine($"Loaded model: {model.Info.Name}");
Console.WriteLine($"Vocab size: {model.Info.VocabSize}");
Console.WriteLine($"Max context: {model.Info.MaxContextLength} tokens");
Console.WriteLine($"Quantization: {string.Join(", ", model.Info.QuantizationSchemes)}");
```

### Load .gguf Model (with Auto-Import)

```csharp
using var engine = SmallMind.Create();

var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model-q8_0.gguf",
    AllowGgufImport = true,  // Enable GGUF import
    ImportCacheDirectory = "./cache",  // Cache converted model
    Threads = Environment.ProcessorCount
});

Console.WriteLine($"GGUF model imported and loaded: {model.Info.Name}");
```

**Note:** The first load of a GGUF file converts it to `.smq` format and caches the result. Subsequent loads reuse the cached `.smq` file for faster startup.

### Handle Unsupported Model Format

```csharp
using var engine = SmallMind.Create();

try
{
    var model = await engine.LoadModelAsync(new ModelLoadRequest
    {
        Path = "model.safetensors"
    });
}
catch (UnsupportedModelException ex)
{
    Console.WriteLine($"Model format not supported: {ex.Extension}");
    Console.WriteLine($"File: {ex.FilePath}");
    Console.WriteLine($"Supported formats: .smq, .gguf (with AllowGgufImport=true)");
}
```

---

## Example 2: Generate Text (Non-Streaming)

### Basic Generation

```csharp
using var engine = SmallMind.Create();
using var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq"
});

var result = await engine.GenerateAsync(model, new GenerationRequest
{
    Prompt = "Once upon a time",
    Options = new GenerationOptions
    {
        MaxNewTokens = 100,
        Temperature = 0.8,
        TopK = 40,
        TopP = 0.95
    }
});

Console.WriteLine($"Generated text: {result.Text}");
Console.WriteLine($"Tokens generated: {result.GeneratedTokens}");
Console.WriteLine($"Stop reason: {result.StopReason}");
```

### Generation with Stop Sequences

```csharp
var result = await engine.GenerateAsync(model, new GenerationRequest
{
    Prompt = "List three items:\n1.",
    Options = new GenerationOptions
    {
        MaxNewTokens = 200,
        Stop = new[] { "\n\n", "4." }  // Stop at double newline or "4."
    }
});

Console.WriteLine(result.Text);
```

---

## Example 3: Streaming Generation with Cancellation

### Real-Time Token Streaming

```csharp
using var engine = SmallMind.Create();
using var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq"
});

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\n[Cancelled by user]");
};

Console.WriteLine("Generating text (Ctrl+C to cancel):");
Console.WriteLine("---");

try
{
    await foreach (var tokenEvent in engine.GenerateStreamingAsync(
        model,
        new GenerationRequest
        {
            Prompt = "Explain quantum computing in simple terms:",
            Options = new GenerationOptions
            {
                MaxNewTokens = 500,
                Temperature = 0.7
            }
        },
        cts.Token))
    {
        switch (tokenEvent.Kind)
        {
            case TokenEventKind.Started:
                Console.Write("Generation started: ");
                break;

            case TokenEventKind.Token:
                // Write each token as it's generated
                Console.Write(tokenEvent.Text);
                break;

            case TokenEventKind.Completed:
                Console.WriteLine($"\n\n[Completed: {tokenEvent.GeneratedTokens} tokens]");
                break;

            case TokenEventKind.Error:
                Console.WriteLine($"\n\n[Error: {tokenEvent.Error}]");
                break;
        }
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Generation cancelled.");
}
```

### Streaming with Token-Level Metrics

```csharp
int tokenCount = 0;
var startTime = DateTime.UtcNow;

await foreach (var tokenEvent in engine.GenerateStreamingAsync(
    model,
    new GenerationRequest
    {
        Prompt = "Write a short story:",
        Options = new GenerationOptions { MaxNewTokens = 200 }
    }))
{
    if (tokenEvent.Kind == TokenEventKind.Token)
    {
        tokenCount++;
        Console.Write(tokenEvent.Text);
        
        // Show real-time throughput every 10 tokens
        if (tokenCount % 10 == 0)
        {
            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            var throughput = tokenCount / elapsed;
            Console.Write($" [{throughput:F1} tok/s]");
        }
    }
    else if (tokenEvent.Kind == TokenEventKind.Completed)
    {
        var totalTime = (DateTime.UtcNow - startTime).TotalSeconds;
        Console.WriteLine($"\n\nFinal throughput: {tokenCount / totalTime:F2} tokens/sec");
    }
}
```

---

## Example 4: Chat Session with KV Cache

### Multi-Turn Conversation

```csharp
using var engine = SmallMind.Create(new SmallMindOptions
{
    EnableKvCache = true  // Enable KV cache for fast multi-turn chat
});

using var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq"
});

using var chat = engine.CreateChatSession(model, new SessionOptions
{
    SessionId = "user-123",
    EnableKvCache = true,
    MaxKvCacheTokens = 4096  // Limit cache size
});

// Set system prompt (done once)
await chat.AddSystemAsync(
    "You are a helpful assistant. Be concise and accurate.",
    CancellationToken.None);

// Conversation turns
var turns = new[]
{
    "What is the capital of France?",
    "What is the population of that city?",
    "Tell me an interesting fact about it."
};

foreach (var userMessage in turns)
{
    Console.WriteLine($"\n[User]: {userMessage}");
    
    var response = await chat.SendAsync(
        new ChatMessage
        {
            Role = ChatRole.User,
            Content = userMessage
        },
        new GenerationOptions
        {
            MaxNewTokens = 150,
            Temperature = 0.7
        },
        CancellationToken.None);
    
    Console.WriteLine($"[Assistant]: {response.Text}");
    Console.WriteLine($"[KV Cache: {chat.Info.KvCacheTokens} tokens, Turn: {chat.Info.TurnCount}]");
}
```

### Streaming Chat Response

```csharp
using var chat = engine.CreateChatSession(model, new SessionOptions
{
    EnableKvCache = true
});

await chat.AddSystemAsync("You are a helpful coding assistant.");

Console.Write("[Assistant]: ");

await foreach (var tokenEvent in chat.SendStreamingAsync(
    new ChatMessage
    {
        Role = ChatRole.User,
        Content = "How do I implement binary search in C#?"
    },
    new GenerationOptions
    {
        MaxNewTokens = 300,
        Temperature = 0.6
    },
    CancellationToken.None))
{
    if (tokenEvent.Kind == TokenEventKind.Token)
    {
        Console.Write(tokenEvent.Text);
    }
}

Console.WriteLine();
```

### Session Reset (Clear KV Cache)

```csharp
// After many turns, reset the session to free memory
Console.WriteLine($"Before reset: {chat.Info.KvCacheTokens} tokens in cache");

chat.Reset();  // Clears conversation history and KV cache

Console.WriteLine($"After reset: {chat.Info.KvCacheTokens} tokens in cache");

// Can start a fresh conversation
await chat.AddSystemAsync("You are a helpful assistant.");
```

---

## Example 5: RAG - Ingest and Ask with Citations

### Build RAG Index from Documents

```csharp
using var engine = SmallMind.Create(new SmallMindOptions
{
    EnableRag = true  // Enable RAG capabilities
});

if (engine.Rag == null)
{
    Console.WriteLine("RAG not supported in this build.");
    return;
}

// Build index from source documents
using var index = await engine.Rag.BuildIndexAsync(
    new RagBuildRequest
    {
        SourcePaths = new[]
        {
            "./docs/manual.txt",
            "./docs/faq.md",
            "./docs/api-reference.md"
        },
        IndexDirectory = "./rag-index",
        ChunkSize = 512,         // Characters per chunk
        ChunkOverlap = 128,      // Overlap for context preservation
        UseDenseRetrieval = false  // Use BM25 (sparse) retrieval only
    },
    CancellationToken.None);

Console.WriteLine($"Index built:");
Console.WriteLine($"  Documents: {index.Info.DocumentCount}");
Console.WriteLine($"  Chunks: {index.Info.ChunkCount}");
Console.WriteLine($"  Created: {index.Info.CreatedAt}");

// Save index for later use
await index.SaveAsync("./rag-index", CancellationToken.None);
```

### Ask Questions with Citations

```csharp
using var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq"
});

var answer = await engine.Rag!.AskAsync(
    model,
    new RagAskRequest
    {
        Query = "How do I configure the inference timeout?",
        Index = index,
        TopK = 5,  // Retrieve top 5 relevant chunks
        MinConfidence = 0.1,  // Filter low-confidence chunks
        GenerationOptions = new GenerationOptions
        {
            MaxNewTokens = 300,
            Temperature = 0.5  // Lower temperature for factual answers
        }
    },
    CancellationToken.None);

Console.WriteLine($"Answer: {answer.Answer}");
Console.WriteLine($"\nCitations ({answer.Citations.Length}):");

foreach (var citation in answer.Citations)
{
    Console.WriteLine($"  - {citation.SourceUri}");
    Console.WriteLine($"    Lines {citation.LineRange?.Start}-{citation.LineRange?.End}");
    Console.WriteLine($"    Confidence: {citation.Confidence:P1}");
    Console.WriteLine($"    Snippet: {citation.Snippet.Substring(0, Math.Min(100, citation.Snippet.Length))}...");
    Console.WriteLine();
}
```

### Streaming RAG with Live Citations

```csharp
Console.WriteLine("Question: How does KV cache work?");
Console.WriteLine("\nAnswer: ");

var citationsSeen = new HashSet<string>();

await foreach (var tokenEvent in engine.Rag!.AskStreamingAsync(
    model,
    new RagAskRequest
    {
        Query = "How does KV cache work?",
        Index = index,
        TopK = 3,
        GenerationOptions = new GenerationOptions
        {
            MaxNewTokens = 400
        }
    },
    CancellationToken.None))
{
    if (tokenEvent.Kind == TokenEventKind.Token)
    {
        Console.Write(tokenEvent.Text);
    }
    else if (tokenEvent.Kind == TokenEventKind.Completed)
    {
        // Final event may include citations
        Console.WriteLine("\n\n[Generation complete]");
    }
}
```

### Handle Insufficient Evidence

```csharp
try
{
    var answer = await engine.Rag!.AskAsync(
        model,
        new RagAskRequest
        {
            Query = "What is the meaning of life?",  // Not in docs
            Index = index,
            MinConfidence = 0.5,  // High confidence required
            GenerationOptions = new GenerationOptions { MaxNewTokens = 200 }
        });
    
    Console.WriteLine(answer.Answer);
}
catch (RagInsufficientEvidenceException ex)
{
    Console.WriteLine($"Cannot answer: {ex.Query}");
    Console.WriteLine($"Minimum confidence: {ex.MinConfidence}");
    Console.WriteLine("Remediation: Add more documents or rephrase the question.");
}
```

---

## Example 6: Deterministic Generation

### Reproducible Outputs

```csharp
using var engine = SmallMind.Create();
using var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq"
});

var options = new GenerationOptions
{
    Mode = GenerationMode.Deterministic,  // Fixed seed mode
    Seed = 42,
    MaxNewTokens = 50,
    // Temperature, TopK, TopP are ignored in deterministic mode
};

var request = new GenerationRequest
{
    Prompt = "The future of AI is",
    Options = options
};

// Generate 3 times - results will be identical
var result1 = await engine.GenerateAsync(model, request);
var result2 = await engine.GenerateAsync(model, request);
var result3 = await engine.GenerateAsync(model, request);

Console.WriteLine($"Result 1: {result1.Text}");
Console.WriteLine($"Result 2: {result2.Text}");
Console.WriteLine($"Result 3: {result3.Text}");
Console.WriteLine($"\nAll identical: {result1.Text == result2.Text && result2.Text == result3.Text}");
```

### Testing with Deterministic Mode

```csharp
// Ideal for unit tests
[Fact]
public async Task TestGenerationOutput()
{
    using var engine = SmallMind.Create();
    using var model = await engine.LoadModelAsync(new ModelLoadRequest
    {
        Path = "test-model.smq"
    });

    var result = await engine.GenerateAsync(model, new GenerationRequest
    {
        Prompt = "Hello",
        Options = new GenerationOptions
        {
            Mode = GenerationMode.Deterministic,
            Seed = 123,
            MaxNewTokens = 10
        }
    });

    // Assert exact output (deterministic)
    Assert.Equal("Hello world! How are you doing", result.Text);
}
```

### Different Seeds = Different Outputs

```csharp
var baseOptions = new GenerationOptions
{
    Mode = GenerationMode.Deterministic,
    MaxNewTokens = 30
};

var prompt = "In the beginning";

// Seed 1
var result1 = await engine.GenerateAsync(model, new GenerationRequest
{
    Prompt = prompt,
    Options = baseOptions with { Seed = 1 }
});

// Seed 2
var result2 = await engine.GenerateAsync(model, new GenerationRequest
{
    Prompt = prompt,
    Options = baseOptions with { Seed = 2 }
});

Console.WriteLine($"Seed 1: {result1.Text}");
Console.WriteLine($"Seed 2: {result2.Text}");
Console.WriteLine($"Different outputs: {result1.Text != result2.Text}");
```

---

## Example 7: Budget Controls

### Token Budget

```csharp
try
{
    var result = await engine.GenerateAsync(model, new GenerationRequest
    {
        Prompt = "Write a long essay:",
        Options = new GenerationOptions
        {
            MaxNewTokens = 50,  // Hard limit
            MaxContextTokens = 4096
        }
    });

    if (result.StoppedByBudget)
    {
        Console.WriteLine("Generation stopped due to token budget.");
        Console.WriteLine($"Generated: {result.GeneratedTokens} tokens");
    }
}
catch (BudgetExceededException ex)
{
    Console.WriteLine($"Budget exceeded: {ex.BudgetType}");
    Console.WriteLine($"Consumed: {ex.Consumed}, Max: {ex.MaxAllowed}");
}
```

### Timeout Budget

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

try
{
    var result = await engine.GenerateAsync(model, new GenerationRequest
    {
        Prompt = "Explain the universe:",
        Options = new GenerationOptions
        {
            MaxNewTokens = 10000,  // Very high
            TimeoutMs = 5000  // 5 second timeout
        }
    }, cts.Token);
}
catch (BudgetExceededException ex)
{
    if (ex.BudgetType == "Time")
    {
        Console.WriteLine("Generation timed out after 5 seconds.");
    }
}
catch (OperationCanceledException)
{
    Console.WriteLine("Cancelled by CancellationToken.");
}
```

### Context Limit

```csharp
try
{
    var result = await engine.GenerateAsync(model, new GenerationRequest
    {
        Prompt = new string('A', 10000),  // Very long prompt
        Options = new GenerationOptions
        {
            MaxContextTokens = 2048  // Model limit
        }
    });
}
catch (ContextLimitExceededException ex)
{
    Console.WriteLine($"Context too long!");
    Console.WriteLine($"Requested: {ex.RequestedSize} tokens");
    Console.WriteLine($"Max allowed: {ex.MaxAllowed} tokens");
    Console.WriteLine("Solution: Reduce input or increase MaxContextTokens");
}
```

---

## Example 8: Engine Capabilities

### Feature Detection

```csharp
using var engine = SmallMind.Create(new SmallMindOptions
{
    EnableRag = true,
    EnableKvCache = true,
    EnableBatching = false
});

var caps = engine.Capabilities;

Console.WriteLine("Engine Capabilities:");
Console.WriteLine($"  Quantized Inference: {caps.SupportsQuantizedInference}");
Console.WriteLine($"  GGUF Import: {caps.SupportsGgufImport}");
Console.WriteLine($"  RAG: {caps.SupportsRag}");
Console.WriteLine($"  KV Cache: {caps.SupportsKvCache}");
Console.WriteLine($"  Batching: {caps.SupportsBatching}");
Console.WriteLine($"  Deterministic Mode: {caps.SupportsDeterministicMode}");
Console.WriteLine($"  Streaming: {caps.SupportsStreaming}");

// Use capability checks before features
if (caps.SupportsRag && engine.Rag != null)
{
    Console.WriteLine("\nRAG engine is available.");
    // Use RAG features...
}
else
{
    Console.WriteLine("\nRAG is not available. Enable with EnableRag=true.");
}
```

---

## Example 9: Complete Application

### Production Inference Service

```csharp
using SmallMind.Abstractions;
using SmallMind.Engine;
using System;
using System.Threading;
using System.Threading.Tasks;

class InferenceService
{
    private readonly ISmallMindEngine _engine;
    private readonly IModelHandle _model;

    public InferenceService(string modelPath)
    {
        _engine = SmallMind.Create(new SmallMindOptions
        {
            EnableKvCache = true,
            EnableRag = true,
            DefaultThreads = Environment.ProcessorCount
        });

        _model = _engine.LoadModelAsync(new ModelLoadRequest
        {
            Path = modelPath,
            AllowGgufImport = true,
            Threads = 0  // Use default
        }).GetAwaiter().GetResult();

        Console.WriteLine($"Loaded model: {_model.Info.Name}");
    }

    public async Task<string> GenerateAsync(
        string prompt,
        int maxTokens = 200,
        CancellationToken ct = default)
    {
        var result = await _engine.GenerateAsync(_model, new GenerationRequest
        {
            Prompt = prompt,
            Options = new GenerationOptions
            {
                MaxNewTokens = maxTokens,
                Temperature = 0.8,
                TopK = 40
            }
        }, ct);

        return result.Text;
    }

    public async Task StreamGenerateAsync(
        string prompt,
        Action<string> onToken,
        int maxTokens = 200,
        CancellationToken ct = default)
    {
        await foreach (var tokenEvent in _engine.GenerateStreamingAsync(
            _model,
            new GenerationRequest
            {
                Prompt = prompt,
                Options = new GenerationOptions
                {
                    MaxNewTokens = maxTokens,
                    Temperature = 0.8
                }
            },
            ct))
        {
            if (tokenEvent.Kind == TokenEventKind.Token)
            {
                onToken(tokenEvent.Text.ToString());
            }
        }
    }

    public void Dispose()
    {
        _model?.Dispose();
        _engine?.Dispose();
    }
}

// Usage:
class Program
{
    static async Task Main(string[] args)
    {
        using var service = new InferenceService("model.smq");

        // Non-streaming
        var result = await service.GenerateAsync("Hello, world!");
        Console.WriteLine($"Result: {result}");

        // Streaming
        Console.Write("Streaming: ");
        await service.StreamGenerateAsync(
            "Once upon a time",
            token => Console.Write(token),
            maxTokens: 100);
        Console.WriteLine();
    }
}
```

---

## Next Steps

- **[Operational Notes](operational-notes.md)** - Error handling, budgets, performance tuning
- **[API Contract](api-contract.md)** - Detailed API reference and versioning
- **[Troubleshooting](troubleshooting.md)** - Common issues and solutions

For more examples, see the `examples/` directory in the repository.

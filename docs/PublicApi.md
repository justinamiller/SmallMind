# SmallMind Public API Reference

This document provides quick reference examples for using the stable public API surface of SmallMind. For comprehensive API documentation, see the XML doc comments in the source code.

## Table of Contents

- [Installation](#installation)
- [Loading a Model](#loading-a-model)
- [Creating an Inference Session](#creating-an-inference-session)
- [Generating Text](#generating-text)
- [Streaming Text Generation](#streaming-text-generation)
- [Configuration Options](#configuration-options)
- [Model Compatibility](#model-compatibility)

---

## Installation

### Via NuGet (Recommended)

```bash
# Stable public API (recommended for production)
dotnet add package SmallMind.Public
```

### From Source

```bash
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind
dotnet build SmallMind.sln -c Release
```

---

## Loading a Model

SmallMind supports loading models in `.smq` format (SmallMind's native format) and can import `.gguf` files.

### Basic Model Loading

```csharp
using SmallMind.Public;

// Configure engine options
var options = new SmallMindOptions
{
    ModelPath = "path/to/model.smq",
    MaxContextTokens = 2048,
    MaxBatchSize = 1,
    ThreadCount = Environment.ProcessorCount,
    EnableKvCache = true
};

// Create the inference engine
using ISmallMindEngine engine = SmallMindFactory.Create(options);

// Check engine capabilities
var capabilities = engine.Capabilities;
Console.WriteLine($"Supports streaming: {capabilities.SupportsStreaming}");
Console.WriteLine($"Max context length: {capabilities.MaxContextLength}");
Console.WriteLine($"Model format: {capabilities.ModelFormat}");
```

### Loading GGUF Models

SmallMind can import GGUF models (commonly used by llama.cpp):

```csharp
var options = new SmallMindOptions
{
    ModelPath = "path/to/model.gguf",
    MaxContextTokens = 2048,
    ImportGguf = true  // Enable GGUF import
};

using ISmallMindEngine engine = SmallMindFactory.Create(options);
```

---

## Creating an Inference Session

Sessions provide thread-local inference contexts. Sessions are **not thread-safe** but the engine is.

```csharp
using ISmallMindEngine engine = SmallMindFactory.Create(options);

// Create a text generation session
var sessionOptions = new TextGenerationOptions
{
    Temperature = 0.8f,
    TopP = 0.95f,
    TopK = 40,
    MaxOutputTokens = 100
};

using ITextGenerationSession session = engine.CreateTextGenerationSession(sessionOptions);
```

---

## Generating Text

### Basic Text Generation

```csharp
var request = new TextGenerationRequest
{
    Prompt = "Once upon a time"
};

GenerationResult result = session.Generate(request);

Console.WriteLine($"Generated: {result.Text}");
Console.WriteLine($"Tokens: {result.Usage.TotalTokens}");
Console.WriteLine($"Speed: {result.Timings.TokensPerSecond:F2} tok/s");
Console.WriteLine($"Finish reason: {result.FinishReason}");
```

### With Stop Sequences

```csharp
var request = new TextGenerationRequest
{
    Prompt = "Question: What is AI?\nAnswer:",
    StopSequences = new[] { "\n", "Question:" }
};

GenerationResult result = session.Generate(request);
```

### With Deterministic Generation (Seeded)

```csharp
var request = new TextGenerationRequest
{
    Prompt = "The quick brown fox",
    Seed = 42  // Same seed produces same output
};

GenerationResult result = session.Generate(request);
```

---

## Streaming Text Generation

For real-time token-by-token generation (useful for chat interfaces):

```csharp
var request = new TextGenerationRequest
{
    Prompt = "Write a short poem about coding:"
};

await foreach (TokenResult token in session.GenerateStreaming(request))
{
    if (!token.IsSpecial)
    {
        Console.Write(token.Text);
    }
    
    // Check if generation is complete
    if (token.FinishReason.HasValue)
    {
        Console.WriteLine($"\n\nFinished: {token.FinishReason}");
        Console.WriteLine($"Total tokens: {token.Usage?.TotalTokens}");
        Console.WriteLine($"Speed: {token.Timings?.TokensPerSecond:F2} tok/s");
    }
}
```

### With Cancellation

```csharp
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(10));  // Cancel after 10 seconds

try
{
    await foreach (TokenResult token in session.GenerateStreaming(request, cts.Token))
    {
        Console.Write(token.Text);
    }
}
catch (RequestCancelledException)
{
    Console.WriteLine("Generation cancelled");
}
```

---

## Configuration Options

### SmallMindOptions (Engine Configuration)

```csharp
var options = new SmallMindOptions
{
    // Required: Path to model file
    ModelPath = "model.smq",
    
    // Optional: Context window size (default: model's max)
    MaxContextTokens = 2048,
    
    // Optional: Batch size for parallel processing (default: 1)
    MaxBatchSize = 1,
    
    // Optional: Number of threads for inference (default: CPU count)
    ThreadCount = Environment.ProcessorCount,
    
    // Optional: Request timeout in milliseconds (default: no timeout)
    RequestTimeoutMs = 30000,
    
    // Optional: Enable KV cache for faster inference (default: true)
    EnableKvCache = true,
    
    // Optional: Import GGUF format (default: false)
    ImportGguf = false,
    
    // Optional: Diagnostics sink for observability
    DiagnosticsSink = new MyCustomDiagnosticsSink()
};
```

### TextGenerationOptions (Session Configuration)

```csharp
var sessionOptions = new TextGenerationOptions
{
    // Temperature: Controls randomness (0.0 = deterministic, 2.0 = very random)
    // Default: 0.8
    Temperature = 0.8f,
    
    // TopP: Nucleus sampling threshold (0.0-1.0)
    // Default: 0.95
    TopP = 0.95f,
    
    // TopK: Limit sampling to top K tokens
    // Default: 40
    TopK = 40,
    
    // MaxOutputTokens: Maximum tokens to generate
    // Default: 100
    MaxOutputTokens = 100,
    
    // StopSequences: Sequences that stop generation
    // Default: empty
    StopSequences = Array.Empty<string>()
};
```

### Sampling Parameter Guidelines

**Temperature:**
- `0.0-0.3`: Focused, deterministic (code completion, factual Q&A)
- `0.7-0.9`: Balanced creativity (chatbots, general text)
- `1.0-2.0`: High creativity (creative writing, brainstorming)

**TopP (Nucleus Sampling):**
- `0.9`: Conservative, coherent
- `0.95`: Balanced (default)
- `0.99`: More diverse

**TopK:**
- `1`: Greedy decoding (most likely token)
- `40`: Balanced (default)
- `100+`: More diversity

---

## Model Compatibility

### Supported Formats

- **`.smq`** - SmallMind's native quantized format (Q8, Q4)
- **`.gguf`** - Import from llama.cpp ecosystem (automatic conversion)

### Supported Architectures

- **Decoder-only Transformers** (GPT-style)
- **Multi-head attention** with KV caching
- **Quantization**: Q8 (8-bit), Q4 (4-bit)

### Recommended Model Sizes

For CPU inference without GPU:

- **Small models (< 1M params)**: Excellent performance (50-100 tok/s)
- **Medium models (1-10M params)**: Good performance (20-50 tok/s)
- **Large models (> 10M params)**: Slower but usable (5-20 tok/s)

### Model Sources

See [SUPPORTED_MODELS.md](../SUPPORTED_MODELS.md) for a list of compatible pre-trained models and training instructions.

---

## Exception Handling

SmallMind provides strongly-typed exceptions for error handling:

```csharp
try
{
    using ISmallMindEngine engine = SmallMindFactory.Create(options);
    using ITextGenerationSession session = engine.CreateTextGenerationSession(sessionOptions);
    
    GenerationResult result = session.Generate(request);
}
catch (InvalidOptionsException ex)
{
    // Configuration errors (invalid paths, parameters)
    Console.WriteLine($"Invalid options: {ex.Message}");
}
catch (ModelLoadFailedException ex)
{
    // Model file errors (corrupt, not found, wrong format)
    Console.WriteLine($"Failed to load model: {ex.Message}");
}
catch (UnsupportedModelFormatException ex)
{
    // Unsupported model format
    Console.WriteLine($"Unsupported format: {ex.Message}");
}
catch (ContextOverflowException ex)
{
    // Input too long for model context window
    Console.WriteLine($"Context overflow: {ex.Message}");
    Console.WriteLine($"Attempted: {ex.AttemptedLength}, Max: {ex.MaxLength}");
}
catch (RequestCancelledException ex)
{
    // Request was cancelled
    Console.WriteLine("Request cancelled");
}
catch (InferenceFailedException ex)
{
    // Inference error during generation
    Console.WriteLine($"Inference failed: {ex.Message}");
}
catch (SmallMindException ex)
{
    // Base exception for all SmallMind errors
    Console.WriteLine($"SmallMind error: {ex.ErrorCode} - {ex.Message}");
}
```

---

## Observability

SmallMind supports diagnostic events for monitoring and telemetry:

```csharp
public class MyDiagnosticsSink : ISmallMindDiagnosticsSink
{
    public void OnEvent(SmallMindDiagnosticEvent evt)
    {
        switch (evt.EventType)
        {
            case DiagnosticEventType.ModelLoaded:
                Console.WriteLine($"Model loaded: {evt.Message}");
                break;
            case DiagnosticEventType.InferenceStarted:
                Console.WriteLine($"Inference started: {evt.Message}");
                break;
            case DiagnosticEventType.InferenceCompleted:
                Console.WriteLine($"Inference completed: {evt.Message}");
                break;
            case DiagnosticEventType.Error:
                Console.WriteLine($"Error: {evt.Message}");
                break;
        }
    }
}

// Use with options
var options = new SmallMindOptions
{
    ModelPath = "model.smq",
    DiagnosticsSink = new MyDiagnosticsSink()
};
```

---

## Additional Resources

- **[README.md](../README.md)** - Project overview and performance benchmarks
- **[QUICKSTART_GGUF.md](../QUICKSTART_GGUF.md)** - Quick start guide for GGUF models
- **[SUPPORTED_MODELS.md](../SUPPORTED_MODELS.md)** - Compatible models and training
- **[API_STABILITY.md](API_STABILITY.md)** - API stability guarantees and versioning
- **[Source Code](https://github.com/justinamiller/SmallMind)** - Full source with XML documentation

---

## Building from Source

```bash
# Clone repository
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind

# Build using provided scripts
./build.sh              # Linux/macOS
# or
.\build.ps1             # Windows

# Or build manually
dotnet build SmallMind.sln -c Release
dotnet test SmallMind.sln -c Release
```

---

## License

SmallMind is licensed under the [MIT License](../LICENSE).

# SmallMind Operational Notes

This guide covers operational aspects of running SmallMind in production: error handling, budget tuning, performance optimization, and resource governance.

---

## Table of Contents

1. [Exception Taxonomy](#exception-taxonomy)
2. [Common Failure Modes](#common-failure-modes)
3. [Budget Settings](#budget-settings)
4. [Determinism Guarantees](#determinism-guarantees)
5. [Model Artifacts](#model-artifacts)
6. [RAG Index Artifacts](#rag-index-artifacts)
7. [Performance Tuning](#performance-tuning)
8. [Resource Governance](#resource-governance)

---

## Exception Taxonomy

All SmallMind exceptions inherit from `SmallMindException` with an `ErrorCode` property for programmatic handling.

### Base Exception

```csharp
public class SmallMindException : Exception
{
    public string ErrorCode { get; }
}
```

**Handling:**
```csharp
catch (SmallMindException ex)
{
    Console.WriteLine($"SmallMind error [{ex.ErrorCode}]: {ex.Message}");
    // Log, retry, or escalate
}
```

---

### 1. UnsupportedModelException

**Error Code:** `UNSUPPORTED_MODEL_FORMAT`

**When thrown:**
- Model file format is not `.smq` or `.gguf`
- `.gguf` file loaded without `AllowGgufImport = true`

**Properties:**
- `FilePath`: Path to the unsupported file
- `Extension`: File extension (e.g., `.safetensors`)

**Example:**
```csharp
catch (UnsupportedModelException ex)
{
    Console.WriteLine($"Unsupported format: {ex.Extension} ({ex.FilePath})");
    Console.WriteLine("Supported formats:");
    Console.WriteLine("  - .smq (native format)");
    Console.WriteLine("  - .gguf (requires AllowGgufImport=true)");
}
```

**Remediation:**
1. Convert model to `.smq` format using the SmallMind CLI
2. Enable GGUF import: `AllowGgufImport = true`
3. Use a supported model format

---

### 2. UnsupportedGgufTensorException

**Error Code:** `UNSUPPORTED_GGUF_TENSOR`

**When thrown:**
- GGUF file contains tensor quantization not supported by SmallMind

**Properties:**
- `TensorName`: Name of the unsupported tensor
- `TensorType`: GGUF tensor type ID (int)

**Example:**
```csharp
catch (UnsupportedGgufTensorException ex)
{
    Console.WriteLine($"Tensor '{ex.TensorName}' has unsupported type: {ex.TensorType}");
    Console.WriteLine("Supported GGUF quantizations: Q4_0, Q8_0");
    Console.WriteLine("File an issue if this is a common GGUF format.");
}
```

**Remediation:**
1. Use GGUF models with Q4_0 or Q8_0 quantization
2. Re-quantize the model with a supported format
3. Request support for the tensor type via GitHub issue

**Currently Supported GGUF Types:**
- `Q4_0` - 4-bit quantization (16 values per block)
- `Q8_0` - 8-bit quantization (32 values per block)

---

### 3. ContextLimitExceededException

**Error Code:** `CONTEXT_LIMIT_EXCEEDED`

**When thrown:**
- Input tokens + requested output tokens exceed `MaxContextTokens`
- Model's inherent context limit is exceeded

**Properties:**
- `RequestedSize`: Total tokens requested (input + output)
- `MaxAllowed`: Maximum context allowed

**Example:**
```csharp
catch (ContextLimitExceededException ex)
{
    Console.WriteLine($"Context limit exceeded:");
    Console.WriteLine($"  Requested: {ex.RequestedSize} tokens");
    Console.WriteLine($"  Max allowed: {ex.MaxAllowed} tokens");
    Console.WriteLine($"  Overflow: {ex.RequestedSize - ex.MaxAllowed} tokens");
}
```

**Remediation:**
1. **Reduce input length:**
   ```csharp
   // Truncate prompt if too long
   string prompt = longText;
   if (prompt.Length > maxChars)
   {
       prompt = prompt.Substring(0, maxChars);
   }
   ```

2. **Increase context budget:**
   ```csharp
   var options = new GenerationOptions
   {
       MaxContextTokens = 8192  // Increase from default 4096
   };
   ```

3. **Use sliding window in chat sessions:**
   ```csharp
   // Reset session periodically to clear old context
   if (chat.Info.KvCacheTokens > 3500)
   {
       chat.Reset();
       await chat.AddSystemAsync(systemPrompt);
   }
   ```

4. **Chunk long documents (RAG):**
   ```csharp
   var ragRequest = new RagBuildRequest
   {
       ChunkSize = 512,  // Smaller chunks fit in context
       ChunkOverlap = 64
   };
   ```

**Note:** The model's inherent context limit (from `ModelInfo.MaxContextLength`) cannot be exceeded regardless of `MaxContextTokens` setting.

---

### 4. BudgetExceededException

**Error Code:** `BUDGET_EXCEEDED`

**When thrown:**
- A resource budget is exhausted (tokens, time, memory)

**Properties:**
- `BudgetType`: Type of budget exceeded (e.g., `"Tokens"`, `"Time"`)
- `Consumed`: Amount consumed before limit hit
- `MaxAllowed`: Maximum allowed

**Example:**
```csharp
catch (BudgetExceededException ex)
{
    Console.WriteLine($"Budget exceeded: {ex.BudgetType}");
    Console.WriteLine($"  Consumed: {ex.Consumed:N0}");
    Console.WriteLine($"  Max allowed: {ex.MaxAllowed:N0}");
    
    if (ex.BudgetType == "Tokens")
    {
        Console.WriteLine("Increase MaxNewTokens to generate more tokens.");
    }
    else if (ex.BudgetType == "Time")
    {
        Console.WriteLine("Increase TimeoutMs or optimize inference speed.");
    }
}
```

**Common Budget Types:**

| BudgetType | Description | Controlled By |
|------------|-------------|---------------|
| `Tokens` | Maximum tokens generated | `MaxNewTokens` |
| `Time` | Maximum generation time (ms) | `TimeoutMs` |
| `Context` | Total context size | `MaxContextTokens` |

**Remediation:**
- Increase the corresponding budget parameter
- Accept partial results (generation is NOT rolled back)
- Optimize model size or quantization for faster inference

---

### 5. RagInsufficientEvidenceException

**Error Code:** `RAG_INSUFFICIENT_EVIDENCE`

**When thrown:**
- No documents in the RAG index meet the minimum confidence threshold
- Query cannot be answered from available documents

**Properties:**
- `Query`: The query that failed
- `MinConfidence`: Minimum confidence threshold (0.0-1.0)

**Example:**
```csharp
catch (RagInsufficientEvidenceException ex)
{
    Console.WriteLine($"Cannot answer query: '{ex.Query}'");
    Console.WriteLine($"No results met confidence threshold: {ex.MinConfidence:P0}");
    
    // Fallback: Use LLM without RAG
    var fallbackResult = await engine.GenerateAsync(model, new GenerationRequest
    {
        Prompt = $"Based on general knowledge: {ex.Query}",
        Options = new GenerationOptions { MaxNewTokens = 200 }
    });
    
    Console.WriteLine($"Fallback answer: {fallbackResult.Text}");
}
```

**Remediation:**
1. **Lower confidence threshold:**
   ```csharp
   var request = new RagAskRequest
   {
       Query = query,
       MinConfidence = 0.0  // Accept all results
   };
   ```

2. **Rephrase query:**
   - Use different keywords
   - Simplify or expand the query

3. **Add more documents:**
   ```csharp
   await rag.BuildIndexAsync(new RagBuildRequest
   {
       SourcePaths = new[] { "./additional-docs" }
   });
   ```

4. **Increase TopK (retrieve more chunks):**
   ```csharp
   var request = new RagAskRequest
   {
       TopK = 10  // Retrieve top 10 instead of default 5
   };
   ```

---

### 6. SecurityViolationException

**Error Code:** `SECURITY_VIOLATION`

**When thrown:**
- Malicious input detected (prompt injection, etc.)
- Unauthorized access attempt
- Security policy violation

**Properties:**
- `ViolationType`: Type of violation (e.g., `"PromptInjection"`, `"UnauthorizedAccess"`)

**Example:**
```csharp
catch (SecurityViolationException ex)
{
    Console.WriteLine($"Security violation: {ex.ViolationType}");
    Console.WriteLine($"Details: {ex.Message}");
    
    // Log to security monitoring
    securityLogger.LogWarning("SecurityViolation", new
    {
        ViolationType = ex.ViolationType,
        Message = ex.Message,
        Timestamp = DateTime.UtcNow
    });
}
```

**Remediation:**
1. **Review input:**
   - Sanitize user inputs
   - Implement input validation
   - Use allowlists for expected patterns

2. **Enable authorization:**
   ```csharp
   // If using RAG with authorization (advanced)
   var authorizer = new CustomAuthorizer();
   // Configure authorizer with access policies
   ```

3. **Monitor and alert:**
   - Log all security violations
   - Set up alerts for repeated violations
   - Implement rate limiting

---

## Common Failure Modes

### 1. Out of Memory (OOM)

**Symptoms:**
- `OutOfMemoryException` during model load or generation
- Process killed by OS (Linux OOM killer)

**Causes:**
- Model too large for available RAM
- KV cache accumulation in long chat sessions
- Large batch sizes

**Remediation:**

**a) Use quantized models:**
```csharp
// Q4_0 uses ~4x less memory than FP32
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model-q4_0.gguf",
    AllowGgufImport = true
});
```

**b) Limit KV cache:**
```csharp
var sessionOptions = new SessionOptions
{
    EnableKvCache = true,
    MaxKvCacheTokens = 2048  // Limit cache size
};
```

**c) Set memory budget:**
```csharp
var loadRequest = new ModelLoadRequest
{
    Path = "model.smq",
    MaxMemoryBytes = 4L * 1024 * 1024 * 1024  // 4 GB hard limit
};
```

**d) Monitor memory usage:**
```csharp
long memoryBefore = GC.GetTotalMemory(false);
var result = await engine.GenerateAsync(model, request);
long memoryAfter = GC.GetTotalMemory(false);
Console.WriteLine($"Memory delta: {(memoryAfter - memoryBefore) / 1024 / 1024} MB");
```

---

### 2. Slow Inference

**Symptoms:**
- Throughput < 1 token/sec
- High CPU usage but low output

**Causes:**
- Suboptimal thread count
- No SIMD optimization
- Large model on CPU

**Remediation:**

**a) Optimize thread count:**
```csharp
// Start with processor count
int threads = Environment.ProcessorCount;

// Benchmark different thread counts
var threadCounts = new[] { threads / 2, threads, threads * 2 };
foreach (var t in threadCounts)
{
    var sw = Stopwatch.StartNew();
    var model = await engine.LoadModelAsync(new ModelLoadRequest
    {
        Path = "model.smq",
        Threads = t
    });
    // Benchmark...
    Console.WriteLine($"Threads: {t}, Throughput: {tokensPerSec:F2} tok/s");
}
```

**b) Use smaller models:**
- Fewer layers: 4-6 layers for small tasks
- Smaller embedding: 128-256 dimensions
- Quantization: Q4_0 or Q8_0

**c) Enable KV cache (chat):**
```csharp
// KV cache eliminates redundant computation
var options = new SessionOptions
{
    EnableKvCache = true  // Default, but ensure it's enabled
};
```

**d) Profile with diagnostics:**
```csharp
// Add timing logs to identify bottlenecks
var startTime = DateTime.UtcNow;
await foreach (var token in engine.GenerateStreamingAsync(model, request))
{
    if (token.Kind == TokenEventKind.Token)
    {
        var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        Console.WriteLine($"Token {token.GeneratedTokens}: {elapsed}ms");
    }
}
```

---

### 3. Non-Deterministic Output (Unexpected)

**Symptoms:**
- Same prompt produces different outputs despite expecting determinism
- Tests fail intermittently

**Causes:**
- `GenerationMode.Exploratory` (default) uses randomness
- Different seed values
- Concurrent modifications (unlikely with stable API)

**Remediation:**

**a) Enable deterministic mode:**
```csharp
var options = new GenerationOptions
{
    Mode = GenerationMode.Deterministic,
    Seed = 42  // Fixed seed
};
```

**b) Verify seed consistency:**
```csharp
// Ensure same seed across runs
const uint FIXED_SEED = 42;
var options = new GenerationOptions
{
    Mode = GenerationMode.Deterministic,
    Seed = FIXED_SEED
};
```

**c) Avoid exploratory mode in tests:**
```csharp
[Fact]
public async Task TestGeneration()
{
    var options = new GenerationOptions
    {
        Mode = GenerationMode.Deterministic,  // NOT Exploratory
        Seed = 123,
        MaxNewTokens = 20
    };
    
    var result = await engine.GenerateAsync(model, new GenerationRequest
    {
        Prompt = "Test",
        Options = options
    });
    
    Assert.Equal(expectedOutput, result.Text);
}
```

---

### 4. RAG Returning Irrelevant Results

**Symptoms:**
- Retrieved chunks don't match query
- Low citation confidence scores
- Answers are off-topic

**Causes:**
- Poor chunking strategy
- Sparse index (not enough documents)
- BM25 limitations for semantic queries

**Remediation:**

**a) Tune chunking:**
```csharp
var buildRequest = new RagBuildRequest
{
    ChunkSize = 256,      // Smaller chunks for precision
    ChunkOverlap = 64,    // Preserve context across boundaries
};
```

**b) Increase TopK:**
```csharp
var askRequest = new RagAskRequest
{
    TopK = 10,  // Retrieve more candidates
    MinConfidence = 0.2  // Filter only very low scores
};
```

**c) Use dense retrieval (if available):**
```csharp
var buildRequest = new RagBuildRequest
{
    UseDenseRetrieval = true  // Vector embeddings (requires embedding model)
};
```

**d) Add more source documents:**
```csharp
// Expand document corpus
var buildRequest = new RagBuildRequest
{
    SourcePaths = new[]
    {
        "./docs",
        "./faqs",
        "./knowledge-base"  // Add more sources
    }
};
```

---

## Budget Settings

### MaxNewTokens

**Purpose:** Limits the number of tokens generated.

**Default:** `100`

**Sizing Guidelines:**

| Use Case | Recommended MaxNewTokens |
|----------|--------------------------|
| Short answers | 50-100 |
| Paragraph responses | 200-300 |
| Article generation | 500-1000 |
| Long-form content | 1000-2000 |
| Code generation | 300-500 |
| Chat messages | 100-200 |

**Example:**
```csharp
var options = new GenerationOptions
{
    MaxNewTokens = 500  // Generate up to 500 tokens
};
```

**Important:**
- Does NOT include input tokens
- Generation stops when limit reached or EOS token generated
- Partial output is returned (not rolled back)

---

### MaxContextTokens

**Purpose:** Limits total context size (input + output).

**Default:** `4096`

**Sizing Guidelines:**

1. **Must not exceed model's max context:**
   ```csharp
   int modelMax = model.Info.MaxContextLength;
   int maxContext = Math.Min(4096, modelMax);
   ```

2. **Account for input size:**
   ```csharp
   int inputTokens = EstimateTokens(prompt);  // Rough: chars / 4
   int maxNewTokens = maxContext - inputTokens - 100;  // Buffer
   ```

3. **Chat sessions (cumulative):**
   ```csharp
   // Chat accumulates context over turns
   int turnEstimate = 10;  // Expected turns
   int tokensPerTurn = 200;  // Avg tokens per turn
   int maxContext = turnEstimate * tokensPerTurn;  // 2000
   ```

**Example:**
```csharp
var options = new GenerationOptions
{
    MaxContextTokens = 2048,  // Limit total context
    MaxNewTokens = 500
};
```

**Warning:** Setting `MaxContextTokens` higher than model's inherent limit will fail with `ContextLimitExceededException`.

---

### TimeoutMs

**Purpose:** Maximum generation time in milliseconds.

**Default:** `0` (no timeout)

**Sizing Guidelines:**

1. **Interactive use (web apps):**
   ```csharp
   var options = new GenerationOptions
   {
       TimeoutMs = 10000  // 10 seconds max
   };
   ```

2. **Background jobs:**
   ```csharp
   var options = new GenerationOptions
   {
       TimeoutMs = 60000  // 1 minute max
   };
   ```

3. **No timeout (batch processing):**
   ```csharp
   var options = new GenerationOptions
   {
       TimeoutMs = 0  // No limit
   };
   ```

4. **Calculate based on throughput:**
   ```csharp
   // If throughput is ~5 tok/s, 500 tokens takes ~100s
   double estimatedTokPerSec = 5.0;
   int maxNewTokens = 500;
   int timeoutMs = (int)((maxNewTokens / estimatedTokPerSec) * 1000 * 1.5);  // 1.5x buffer
   ```

**Example:**
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var result = await engine.GenerateAsync(model, new GenerationRequest
    {
        Prompt = prompt,
        Options = new GenerationOptions
        {
            TimeoutMs = 30000,  // Enforce 30s timeout
            MaxNewTokens = 1000
        }
    }, cts.Token);
}
catch (BudgetExceededException ex)
{
    // Timeout hit
}
catch (OperationCanceledException)
{
    // CancellationToken cancelled
}
```

---

### Threads

**Purpose:** Number of CPU threads for model inference.

**Default:** `0` (auto-detect, uses `Environment.ProcessorCount`)

**Sizing Guidelines:**

| Environment | Recommended Threads |
|-------------|---------------------|
| Dedicated server | `Environment.ProcessorCount` |
| Shared server | `ProcessorCount / 2` |
| Cloud (limited CPU) | `2-4` |
| Development machine | `ProcessorCount - 2` (leave cores for IDE) |

**Example:**
```csharp
var loadRequest = new ModelLoadRequest
{
    Path = "model.smq",
    Threads = Environment.ProcessorCount  // Use all cores
};
```

**Tuning:**
```csharp
// Benchmark to find optimal thread count
int[] threadCounts = { 1, 2, 4, 8, 16 };
foreach (var threads in threadCounts)
{
    var sw = Stopwatch.StartNew();
    var model = await engine.LoadModelAsync(new ModelLoadRequest
    {
        Path = "model.smq",
        Threads = threads
    });
    
    // Generate text and measure throughput...
    double tokPerSec = MeasureThroughput(model);
    Console.WriteLine($"Threads: {threads}, Throughput: {tokPerSec:F2} tok/s");
    
    model.Dispose();
}
```

**Warning:** More threads != faster. Optimal thread count depends on model size, CPU architecture, and memory bandwidth. Always benchmark.

---

## Determinism Guarantees

### When Determinism Applies

**Guaranteed deterministic IF:**
1. `GenerationMode.Deterministic` is set
2. Same `Seed` value
3. Same prompt (exact string match)
4. Same model file (identical weights)
5. Same `MaxNewTokens`, `MaxContextTokens`
6. Single-threaded execution OR thread-safe RNG

**Example:**
```csharp
var options = new GenerationOptions
{
    Mode = GenerationMode.Deterministic,
    Seed = 42,
    MaxNewTokens = 100
};

var prompt = "Hello, world!";

// These will produce IDENTICAL output
var result1 = await engine.GenerateAsync(model, new GenerationRequest { Prompt = prompt, Options = options });
var result2 = await engine.GenerateAsync(model, new GenerationRequest { Prompt = prompt, Options = options });

Assert.Equal(result1.Text, result2.Text);  // PASSES
```

---

### When Determinism Does NOT Apply

**Non-deterministic IF:**
1. `GenerationMode.Exploratory` (default)
2. Different seeds
3. Different model versions
4. Different prompts
5. Concurrent requests with shared RNG state (implementation-dependent)

**Example (NON-deterministic):**
```csharp
var options = new GenerationOptions
{
    Mode = GenerationMode.Exploratory,  // Randomized
    Temperature = 0.8
};

var result1 = await engine.GenerateAsync(model, new GenerationRequest { Prompt = "Hello", Options = options });
var result2 = await engine.GenerateAsync(model, new GenerationRequest { Prompt = "Hello", Options = options });

// result1.Text != result2.Text (likely different)
```

---

### Determinism Across Versions

**Guarantee:** Determinism is guaranteed **within the same SmallMind version**.

**Cross-version:** NOT guaranteed. Internal changes (optimizations, bug fixes) may affect output even with the same seed.

**Recommendation:**
- Pin SmallMind version in production for reproducibility
- Re-baseline tests when upgrading SmallMind versions

```xml
<!-- Pin version for deterministic tests -->
<PackageReference Include="SmallMind" Version="0.3.0" />
```

---

### Seeding Best Practices

**Use distinct seeds for:**
- Different test cases
- Different environments (dev, staging, prod)
- Different model versions

**Example:**
```csharp
// Derive seed from context
uint seed = (uint)$"{environment}-{testCase}".GetHashCode();

var options = new GenerationOptions
{
    Mode = GenerationMode.Deterministic,
    Seed = seed
};
```

**Avoid:**
- Hardcoding seed = 0 everywhere (makes debugging harder)
- Random seeds in deterministic mode (defeats the purpose)

---

## Model Artifacts

### .smq Format (Native)

**Structure:**
- Binary format optimized for SmallMind
- Includes quantized weights, metadata, and tokenizer config

**Manifest Requirements:**
- File must be readable by current SmallMind version
- Version compatibility checked at load time

**Loading:**
```csharp
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model.smq"
});
```

**Creation:**
Use SmallMind CLI or quantization API:
```bash
# CLI example
smallmind quantize --input model.onnx --output model.smq --format q8_0
```

**Validation:**
```csharp
// Check model info after loading
var info = model.Info;
Console.WriteLine($"Model: {info.Name}");
Console.WriteLine($"Vocab: {info.VocabSize}");
Console.WriteLine($"Max context: {info.MaxContextLength}");
Console.WriteLine($"Quantization: {string.Join(", ", info.QuantizationSchemes)}");
```

---

### .gguf Format (Import)

**Structure:**
- GGUF (GPT-Generated Unified Format) from llama.cpp ecosystem
- Contains tensors, metadata, and tokenizer

**Supported Quantizations:**
- `Q4_0` - 4-bit quantization
- `Q8_0` - 8-bit quantization

**Loading with Import:**
```csharp
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model-q8_0.gguf",
    AllowGgufImport = true,
    ImportCacheDirectory = "./cache"
});
```

**Import Process:**
1. GGUF file is read
2. Tensors are converted to SMQ format
3. Converted model is cached in `ImportCacheDirectory`
4. Subsequent loads use cached `.smq` file

**Cache Location:**
```
ImportCacheDirectory/
  └── model-q8_0.gguf.smq  (cached conversion)
```

**Cache Invalidation:**
- Delete cached file to force re-import
- Cache is keyed by source file path and modification time

---

### Model Metadata

**Accessing Metadata:**
```csharp
var info = model.Info;

// Basic info
string name = info.Name;
int vocabSize = info.VocabSize;
int maxContext = info.MaxContextLength;

// Quantization info
string[] quantSchemes = info.QuantizationSchemes;
// e.g., ["Q8_0", "Q4_0"]

// Traceability
string engineVersion = info.EngineVersion;  // SmallMind version
string buildHash = info.BuildHash;  // Git commit hash
```

**Use Cases:**
- Logging for audit trails
- Compatibility checks
- Debugging model issues

---

## RAG Index Artifacts

### Index Directory Structure

```
rag-index/
  ├── manifest.json          # Index metadata
  ├── chunks.bin             # Chunked document data
  ├── sparse-index.bin       # BM25 inverted index
  └── dense-index.bin        # Vector embeddings (if UseDenseRetrieval=true)
```

---

### Manifest Requirements

**manifest.json** contains:
```json
{
  "version": "1.0",
  "createdAt": "2024-01-15T10:30:00Z",
  "chunkCount": 1523,
  "documentCount": 42,
  "chunkSize": 512,
  "chunkOverlap": 128,
  "useDenseRetrieval": false,
  "sourcePaths": [
    "./docs/manual.txt",
    "./docs/faq.md"
  ]
}
```

**Fields:**
- `version`: Manifest format version
- `createdAt`: Index creation timestamp
- `chunkCount`: Total chunks in index
- `documentCount`: Unique source documents
- `chunkSize`: Max characters per chunk
- `chunkOverlap`: Overlap between chunks
- `useDenseRetrieval`: Whether dense (vector) retrieval is enabled
- `sourcePaths`: List of source document paths

---

### Index Persistence

**Saving:**
```csharp
using var index = await rag.BuildIndexAsync(buildRequest);
await index.SaveAsync("./rag-index", CancellationToken.None);
```

**Loading:**
```csharp
// Load existing index (implementation detail - use BuildIndexAsync with existing directory)
var loadRequest = new RagBuildRequest
{
    IndexDirectory = "./rag-index",
    SourcePaths = Array.Empty<string>()  // Empty = load existing
};
var index = await rag.BuildIndexAsync(loadRequest);
```

---

### Index Validation

**Check index health:**
```csharp
var info = index.Info;

if (info.ChunkCount == 0)
{
    Console.WriteLine("WARNING: Index is empty!");
}

if (info.DocumentCount < 10)
{
    Console.WriteLine("WARNING: Index has very few documents. Consider adding more.");
}

Console.WriteLine($"Index coverage: {info.ChunkCount} chunks from {info.DocumentCount} documents");
Console.WriteLine($"Avg chunks/doc: {(double)info.ChunkCount / info.DocumentCount:F1}");
```

---

### Index Rebuild

**When to rebuild:**
- Source documents changed
- Chunking strategy changed
- Index corruption detected

**Rebuild:**
```csharp
var buildRequest = new RagBuildRequest
{
    SourcePaths = new[] { "./docs" },
    IndexDirectory = "./rag-index",
    ChunkSize = 512,
    ChunkOverlap = 128,
    UseDenseRetrieval = false
};

// This will overwrite existing index
using var index = await rag.BuildIndexAsync(buildRequest);
await index.SaveAsync("./rag-index");

Console.WriteLine("Index rebuilt successfully.");
```

---

## Performance Tuning

### 1. Model Selection

**Quantization Impact:**

| Format | Memory | Speed | Quality |
|--------|--------|-------|---------|
| FP32 | 1x | Baseline | Best |
| Q8_0 | 0.25x | 1.5-2x faster | ~99% quality |
| Q4_0 | 0.125x | 2-3x faster | ~95% quality |

**Recommendation:**
- **Production:** Q8_0 (good balance)
- **Resource-constrained:** Q4_0 (smaller, faster)
- **Research:** FP32 (highest quality)

```csharp
// Load Q8_0 model
var model = await engine.LoadModelAsync(new ModelLoadRequest
{
    Path = "model-q8_0.gguf",
    AllowGgufImport = true
});
```

---

### 2. Thread Tuning

**Optimal thread count depends on:**
- CPU core count
- Model size
- Memory bandwidth

**Benchmarking script:**
```csharp
async Task<double> BenchmarkThroughput(int threads)
{
    var model = await engine.LoadModelAsync(new ModelLoadRequest
    {
        Path = "model.smq",
        Threads = threads
    });

    var sw = Stopwatch.StartNew();
    var result = await engine.GenerateAsync(model, new GenerationRequest
    {
        Prompt = "Test prompt",
        Options = new GenerationOptions { MaxNewTokens = 100 }
    });
    sw.Stop();

    double tokPerSec = result.GeneratedTokens / sw.Elapsed.TotalSeconds;
    model.Dispose();
    return tokPerSec;
}

// Run benchmark
int[] threadCounts = { 1, 2, 4, 8, 16 };
foreach (var threads in threadCounts)
{
    double throughput = await BenchmarkThroughput(threads);
    Console.WriteLine($"Threads: {threads,2}, Throughput: {throughput:F2} tok/s");
}
```

---

### 3. KV Cache Tuning

**Benefits:**
- Eliminates recomputation of past tokens
- Faster multi-turn chat (2-10x speedup)

**Memory cost:**
- Proportional to cache size and model size
- Each cached token adds ~few KB (depends on model)

**Configuration:**
```csharp
var sessionOptions = new SessionOptions
{
    EnableKvCache = true,
    MaxKvCacheTokens = 2048  // Limit cache size
};

using var chat = engine.CreateChatSession(model, sessionOptions);
```

**Monitoring:**
```csharp
Console.WriteLine($"KV cache size: {chat.Info.KvCacheTokens} tokens");

if (chat.Info.KvCacheTokens > 1500)
{
    Console.WriteLine("WARNING: KV cache growing large. Consider resetting.");
}
```

**Reset strategy:**
```csharp
// Reset every N turns
const int MAX_TURNS = 20;
if (chat.Info.TurnCount >= MAX_TURNS)
{
    chat.Reset();
    await chat.AddSystemAsync(systemPrompt);
}
```

---

### 4. Batching (Advanced)

**Enable batching:**
```csharp
var engine = SmallMind.Create(new SmallMindOptions
{
    EnableBatching = true  // Enable request batching
});
```

**Benefits:**
- Process multiple requests concurrently
- Share computation across requests
- Higher throughput (requests/sec)

**Use case:**
- Web API serving multiple users
- Batch inference jobs

**Note:** Batching is an advanced feature. Consult documentation for batch-specific APIs.

---

### 5. Sampling Parameters

**Temperature:**
- Lower (0.1-0.5): More deterministic, focused
- Higher (0.8-1.2): More random, creative

**TopK:**
- Lower (10-20): Conservative, repetitive
- Higher (40-100): Diverse, exploratory

**TopP (nucleus sampling):**
- Lower (0.8-0.9): Focused
- Higher (0.95-1.0): Diverse

**Tuning for speed:**
```csharp
// Lower sampling complexity = faster
var options = new GenerationOptions
{
    Temperature = 0.5,  // Less random
    TopK = 20,          // Fewer candidates
    TopP = 0.9
};
```

---

## Resource Governance

### 1. Memory Governance

**Set memory limits:**
```csharp
var loadRequest = new ModelLoadRequest
{
    Path = "model.smq",
    MaxMemoryBytes = 4L * 1024 * 1024 * 1024  // 4 GB hard limit
};
```

**Monitor memory usage:**
```csharp
long memBefore = GC.GetTotalMemory(false);
var model = await engine.LoadModelAsync(loadRequest);
long memAfter = GC.GetTotalMemory(false);

Console.WriteLine($"Model memory: {(memAfter - memBefore) / 1024 / 1024} MB");
```

**Best practices:**
- Dispose models when not in use
- Limit KV cache size in long-running sessions
- Use quantized models (Q8_0, Q4_0)

---

### 2. CPU Governance

**Limit thread count:**
```csharp
var loadRequest = new ModelLoadRequest
{
    Path = "model.smq",
    Threads = 4  // Limit to 4 threads (shared environment)
};
```

**Monitor CPU usage:**
```csharp
var process = Process.GetCurrentProcess();
var startCpu = process.TotalProcessorTime;

var result = await engine.GenerateAsync(model, request);

var endCpu = process.TotalProcessorTime;
var cpuUsed = (endCpu - startCpu).TotalMilliseconds;

Console.WriteLine($"CPU time: {cpuUsed:F2}ms");
```

---

### 3. Request Timeouts

**Global timeout:**
```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

try
{
    var result = await engine.GenerateAsync(model, request, cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Request timed out after 30 seconds.");
}
```

**Per-request timeout:**
```csharp
var options = new GenerationOptions
{
    TimeoutMs = 10000  // 10 seconds per request
};
```

---

### 4. Rate Limiting

**Example using SemaphoreSlim:**
```csharp
public class RateLimitedInferenceService
{
    private readonly ISmallMindEngine _engine;
    private readonly IModelHandle _model;
    private readonly SemaphoreSlim _semaphore;

    public RateLimitedInferenceService(int maxConcurrentRequests = 5)
    {
        _engine = SmallMind.Create();
        _model = _engine.LoadModelAsync(new ModelLoadRequest
        {
            Path = "model.smq"
        }).GetAwaiter().GetResult();

        _semaphore = new SemaphoreSlim(maxConcurrentRequests);
    }

    public async Task<GenerationResult> GenerateAsync(
        string prompt,
        CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await _engine.GenerateAsync(_model, new GenerationRequest
            {
                Prompt = prompt,
                Options = new GenerationOptions { MaxNewTokens = 200 }
            }, ct);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

---

### 5. Observability

**Logging:**
```csharp
public async Task<GenerationResult> GenerateWithLogging(
    IModelHandle model,
    GenerationRequest request)
{
    var sw = Stopwatch.StartNew();
    var logger = LoggerFactory.CreateLogger<InferenceService>();

    logger.LogInformation("Generation started: {Prompt}", request.Prompt);

    try
    {
        var result = await _engine.GenerateAsync(model, request);
        sw.Stop();

        logger.LogInformation(
            "Generation completed: {Tokens} tokens in {Ms}ms ({TokPerSec:F2} tok/s)",
            result.GeneratedTokens,
            sw.ElapsedMilliseconds,
            result.GeneratedTokens / sw.Elapsed.TotalSeconds);

        return result;
    }
    catch (SmallMindException ex)
    {
        sw.Stop();
        logger.LogError(ex, "Generation failed: {ErrorCode}", ex.ErrorCode);
        throw;
    }
}
```

**Metrics:**
```csharp
public class InferenceMetrics
{
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public double AvgThroughput { get; set; }  // tok/s
    public double AvgLatency { get; set; }     // ms
}

// Track metrics
private void RecordMetrics(GenerationResult result, TimeSpan elapsed)
{
    metrics.TotalRequests++;
    metrics.SuccessfulRequests++;
    
    double throughput = result.GeneratedTokens / elapsed.TotalSeconds;
    metrics.AvgThroughput = (metrics.AvgThroughput + throughput) / 2;
    
    metrics.AvgLatency = (metrics.AvgLatency + elapsed.TotalMilliseconds) / 2;
}
```

---

## Summary

**Critical Points:**

1. **Exceptions:** Catch `SmallMindException` and handle specific subtypes
2. **Budgets:** Size `MaxNewTokens`, `MaxContextTokens`, `TimeoutMs` appropriately
3. **Determinism:** Use `GenerationMode.Deterministic` with fixed seed for reproducibility
4. **Models:** Use `.smq` natively or `.gguf` with import; prefer Q8_0 quantization
5. **RAG:** Build indices with appropriate chunk sizes; validate index health
6. **Performance:** Tune threads, enable KV cache, use quantized models
7. **Resources:** Set memory limits, thread limits, and timeouts

For more information:
- **[Quickstart Guide](quickstart.md)** - Code examples
- **[API Contract](api-contract.md)** - API reference
- **[Troubleshooting](troubleshooting.md)** - Common issues

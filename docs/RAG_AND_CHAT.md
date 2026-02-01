# SmallMind RAG and Chat System

## Overview

This document describes the Retrieval-Augmented Generation (RAG) and Chat capabilities added to SmallMind. These features enable knowledge-grounded responses through document retrieval and multi-turn conversations with persistent context.

## Architecture

The RAG and Chat system consists of several integrated components:

### 1. Document Management (`SmallMind.Retrieval`)

**Document Model**
- `Document`: Represents a knowledge source with ID, title, content, source URI, tags, and metadata
- `DocumentChunk`: A segment of a document with offsets, text, and metadata
- `ChunkingOptions`: Configuration for text segmentation (max chars, overlap, boundary preferences)

**Chunking**
- Deterministic text splitting with configurable options
- Prefers natural boundaries (paragraphs → sentences → words)
- Supports overlap for context continuity
- Preserves document metadata in chunks

### 2. Retrieval Index (`SmallMind.Retrieval`)

**InMemoryLexicalIndex**
- BM25-based scoring algorithm (no external dependencies)
- Deterministic retrieval with stable ordering
- Budget enforcement (TopK, MaxChunksPerDocument)
- Full cancellation token support

**Features**
- In-memory storage with O(n log n) search complexity
- Per-document chunk limits to ensure diversity
- Citation information for all retrieved chunks
- Handles document updates (re-indexing)

### 3. RAG Prompt Building (`SmallMind.Retrieval`)

**RagPromptBuilder**
- Assembles prompts with retrieved context
- Enforces MaxContextChars budget
- Generates inline citations: `[doc:Title#chunkId]`
- Creates Sources section listing all references
- Only cites actually retrieved chunks (no hallucinations)

### 4. Chat Sessions (`SmallMind.Chat`)

**ChatSession**
- Multi-turn conversation with persistent state
- Ordered list of ChatTurn (user/assistant pairs)
- Integrated WorkflowState for session memory
- Session metadata and timestamps

**ChatTurn**
- User message and assistant response
- Optional citations list
- Optional structured output
- Turn metadata

**ISessionStore**
- Abstract session persistence interface
- `InMemorySessionStore`: Default in-memory implementation
- Supports CRUD operations with async API
- Thread-safe for concurrent access

### 5. Chat Orchestrator (`SmallMind.Chat`)

**ChatOrchestrator**
- Implements `IChatCompletionService`
- Orchestrates retrieval → prompt → generation pipeline
- Supports both RAG and non-RAG modes
- Manages multi-turn context automatically
- Full budget and cancellation support

## Usage Examples

### Basic Document Indexing and Retrieval

```csharp
using SmallMind.Retrieval;

// Create index
var index = new InMemoryLexicalIndex();

// Add documents
var doc = new Document
{
    Id = "doc1",
    Title = "SmallMind Guide",
    Content = "SmallMind is a pure C# language model...",
    Tags = new HashSet<string> { "documentation" }
};

index.Upsert(doc);

// Search
var options = new RetrievalOptions
{
    TopK = 5,
    Deterministic = true
};

var results = index.Search("How does SmallMind work?", options);

foreach (var chunk in results.Chunks)
{
    Console.WriteLine($"Score: {chunk.Score:F4}");
    Console.WriteLine($"Source: {chunk.Citation.Title}");
    Console.WriteLine($"Text: {chunk.Text}\n");
}
```

### RAG Prompt Building

```csharp
using SmallMind.Retrieval;

// Retrieve relevant chunks
var retrievalResult = index.Search(userQuery, new RetrievalOptions { TopK = 3 });

// Build RAG prompt
var promptOptions = new RagPromptOptions
{
    MaxContextChars = 2000,
    MaxChunksToInclude = 3,
    IncludeSourcesSection = true
};

var prompt = RagPromptBuilder.Build(
    userQuery,
    retrievalResult.Chunks,
    promptOptions
);

// The prompt now includes:
// - Context section with retrieved chunks and inline citations
// - User question
// - Instructions to use only the provided context
// - Sources section listing all references
```

### Multi-Turn RAG Chat

```csharp
using SmallMind.Chat;
using SmallMind.Retrieval;

// Setup (one-time initialization)
var sessionStore = new InMemorySessionStore();
var retrievalIndex = new InMemoryLexicalIndex();
var orchestrator = new ChatOrchestrator(
    sessionStore,
    model,           // Your trained TransformerModel
    tokenizer,       // Your Tokenizer
    blockSize,       // Model block size
    retrievalIndex   // Optional, enables RAG
);

// Index your documents
indexMyDocuments(retrievalIndex);

// Chat session
string sessionId = "user-123";

var chatOptions = new ChatOptions
{
    Deterministic = true,
    Seed = 42,
    MaxTokens = 200,
    Temperature = 0.7,
    UseRag = true,
    ReturnCitations = true
};

// Turn 1
var response1 = await orchestrator.AskAsync(
    sessionId,
    "What is SmallMind?",
    chatOptions
);

Console.WriteLine($"Assistant: {response1.Text}");
Console.WriteLine($"Citations: {string.Join(", ", response1.Citations)}");

// Turn 2 (uses conversation history)
var response2 = await orchestrator.AskAsync(
    sessionId,
    "How do I train it?",
    chatOptions
);

Console.WriteLine($"Assistant: {response2.Text}");
```

### Custom Chunking Strategy

```csharp
var options = new ChunkingOptions
{
    MaxChars = 500,              // Max characters per chunk
    OverlapChars = 50,           // Overlap for context continuity
    MinChunkChars = 100,         // Minimum chunk size
    PreferParagraphBoundaries = true,  // Split at paragraphs first
    PreferSentenceBoundaries = true    // Then sentences
};

var chunks = DocumentChunker.Chunk(document, options);
```

## Configuration Options

### RetrievalOptions

| Option | Default | Description |
|--------|---------|-------------|
| TopK | 5 | Number of top chunks to retrieve |
| MaxChunksPerDocument | 2 | Max chunks from a single document |
| Deterministic | true | Stable ordering with tie-breakers |
| IncludeSnippets | true | Return text snippets |
| MaxSnippetChars | 280 | Max characters per snippet |

### RagPromptOptions

| Option | Default | Description |
|--------|---------|-------------|
| MaxContextChars | 4000 | Total prompt character budget |
| MaxChunksToInclude | 5 | Max chunks in context |
| SystemInstructionTemplate | null | Optional system instruction |
| IncludeSourcesSection | true | Include sources list |
| CitationFormat | `[doc:{title}#{chunkId}]` | Citation format string |

### ChatOptions

| Option | Default | Description |
|--------|---------|-------------|
| Deterministic | true | Fixed seed for reproducibility |
| Seed | 42 | Random seed (if deterministic) |
| MaxTokens | 200 | Max tokens to generate |
| Temperature | 0.7 | Sampling temperature |
| TopK | 40 | Top-K sampling |
| MaxContextChars | 4000 | Prompt budget |
| TopKRetrieval | 5 | Chunks to retrieve |
| UseRag | true | Enable RAG |
| ReturnCitations | true | Include citations |

## Best Practices

### Document Preparation

1. **Clean Text**: Remove formatting artifacts, excess whitespace
2. **Meaningful Titles**: Use descriptive document titles for citations
3. **Source URIs**: Provide links to original sources when available
4. **Tags**: Use tags for document categorization and filtering
5. **Metadata**: Add relevant metadata for provenance tracking

### Chunking Strategy

1. **Chunk Size**: 300-600 characters works well for most use cases
2. **Overlap**: 10-20% overlap maintains context continuity
3. **Boundaries**: Prefer sentence boundaries for coherent chunks
4. **Minimum Size**: Set MinChunkChars to avoid tiny fragments

### Retrieval Configuration

1. **TopK**: Start with 3-5 chunks, adjust based on quality
2. **Diversity**: Use MaxChunksPerDocument to ensure diverse sources
3. **Determinism**: Enable for reproducible results and debugging
4. **Budget**: Set MaxContextChars based on your model's context window

### Chat Session Management

1. **Session IDs**: Use meaningful identifiers (user ID, conversation ID)
2. **Cleanup**: Implement session expiration for long-running systems
3. **State**: Use WorkflowState for session-specific configuration
4. **Persistence**: Consider file-based or database-backed stores for production

### Performance Optimization

1. **Index Size**: Monitor memory usage for large document collections
2. **Chunk Count**: Balance chunk size vs. total chunks (affects search speed)
3. **Sampling Reuse**: ChatOrchestrator reuses Sampling instance (already optimized)
4. **Batch Operations**: Use `Upsert(IEnumerable<Document>)` for bulk indexing

## Determinism Guarantees

The RAG system provides determinism guarantees when configured appropriately:

### Chunking
- Same document + same options → identical chunks (deterministic algorithm)
- No randomness in boundary detection
- Stable chunk IDs based on document ID and index

### Retrieval
- Same query + same index + deterministic mode → identical results
- BM25 scores are deterministic
- Tie-breaking uses stable sorting (by chunk ID)

### Generation
- Same prompt + same seed → identical generated text (when using deterministic mode)
- ChatOrchestrator passes seed through to Sampling

### End-to-End
For fully deterministic RAG:
```csharp
var chatOptions = new ChatOptions
{
    Deterministic = true,
    Seed = 42,           // Fixed seed
    UseRag = true
};

var retrievalOptions = new RetrievalOptions
{
    Deterministic = true  // Stable retrieval ordering
};
```

## Testing

The implementation includes comprehensive tests:

### DocumentChunkerTests (6 tests)
- Chunking correctness (small/long documents)
- Determinism (same input → same output)
- Boundary preferences
- Metadata propagation

### InMemoryLexicalIndexTests (9 tests)
- Deterministic search
- TopK enforcement
- Score ordering
- Per-document limits
- Upsert and update
- Citation inclusion
- Cancellation support

### InMemorySessionStoreTests (6 tests)
- CRUD operations
- Session existence checks
- Count tracking
- Clear operation
- Timestamp updates

## Troubleshooting

### No Results from Retrieval

**Problem**: Search returns empty results

**Solutions**:
1. Verify documents are indexed: check index has content
2. Try simpler queries (single keywords first)
3. Check tokenization: ensure query terms match document terms
4. Review MinChunkChars: may be filtering out all chunks

### Poor Retrieval Quality

**Problem**: Irrelevant chunks retrieved

**Solutions**:
1. Increase TopK to get more candidates
2. Adjust chunking (smaller chunks for precision, larger for recall)
3. Add more documents to improve coverage
4. Consider query reformulation or expansion

### Out of Memory

**Problem**: Memory usage grows too large

**Solutions**:
1. Reduce MaxChunksPerDocument
2. Increase MinChunkChars to create fewer, larger chunks
3. Implement document pagination for large corpora
4. Use database-backed session store instead of in-memory

### Inconsistent Results

**Problem**: Same query returns different results

**Solutions**:
1. Enable deterministic mode: `Deterministic = true`
2. Set fixed seed: `Seed = 42`
3. Ensure no concurrent index updates during search
4. Verify temperature is low (< 0.3) for consistent generation

## Future Enhancements

Potential areas for extension:

1. **Semantic Embeddings**: Plug in custom embedding providers for vector search
2. **Hybrid Retrieval**: Combine BM25 with vector similarity
3. **Reranking**: Add cross-encoder reranking for improved quality
4. **Query Expansion**: Automatic query reformulation
5. **Persistent Storage**: File-based or database-backed index and sessions
6. **Async Indexing**: Background document processing
7. **Incremental Updates**: Update index without full rebuild
8. **Faceted Search**: Filter by tags, metadata, date ranges

## API Reference

See XML documentation in source files for detailed API reference:

- `SmallMind.Retrieval.*`: Document, chunk, and retrieval APIs
- `SmallMind.Chat.*`: Session and chat orchestration APIs

## Example Applications

See `samples/RagChatExample.cs` for a complete working example demonstrating:
- Document loading and indexing
- Retrieval with BM25
- RAG prompt building
- Multi-turn chat sessions
- Citation tracking

Run the example:
```bash
dotnet run --project samples/RagChatExample.cs
```

## Support

For issues, questions, or contributions, please refer to the main SmallMind repository.

# SmallMind Thread-Safety and Concurrency Model

## Overview

SmallMind provides explicit thread-safety guarantees for production use. Understanding the concurrency model is critical for building safe, high-performance applications.

## Thread-Safety Guarantees

### Engine Level (`ISmallMindEngine`)

✅ **Thread-Safe**: The engine can be safely used from multiple threads concurrently.
- Multiple threads can call `CreateTextGenerationSession()`, `CreateChatClient()`, or `CreateEmbeddingSession()` simultaneously
- Model weights are immutable after loading and safely shared across all sessions
- Engine resources are thread-safe and managed internally

```csharp
// SAFE: Multiple threads can create sessions concurrently
var engine = SmallMindFactory.Create(options);

// Thread 1
var session1 = engine.CreateTextGenerationSession(options);

// Thread 2 (concurrent with thread 1)
var session2 = engine.CreateTextGenerationSession(options);
```

### Session Level (`ITextGenerationSession`, `IChatClient`)

❌ **NOT Thread-Safe**: Sessions must be used from a single thread or explicitly synchronized.
- Each session maintains mutable state (conversation history, KV cache, RNG state)
- Concurrent calls to `Generate()` or `GenerateStreaming()` on the same session are not supported
- Sessions should be considered "owned" by a single thread or task

```csharp
// ❌ UNSAFE: Concurrent calls to the same session
var session = engine.CreateTextGenerationSession(options);

// Thread 1
var result1 = session.Generate(request1); // Concurrent with thread 2

// Thread 2
var result2 = session.Generate(request2); // ❌ Race condition!

// ✅ SAFE: Use separate sessions for concurrent requests
var session1 = engine.CreateTextGenerationSession(options);
var session2 = engine.CreateTextGenerationSession(options);

// Thread 1
var result1 = session1.Generate(request1);

// Thread 2
var result2 = session2.Generate(request2); // ✅ Safe - separate session
```

### Shared vs Per-Session Resources

**Shared Across Sessions (Thread-Safe):**
- Model weights (read-only after loading)
- Tokenizer (immutable vocabulary and merges)
- Memory-mapped GGUF files (read-only)

**Per-Session (Requires Synchronization):**
- KV cache state
- Conversation history
- Random number generator (RNG) state
- Sampling temperature/top-p state
- Token generation counters

## Correlation IDs for Multi-Session Scenarios

SmallMind supports correlation IDs to track requests across sessions in multi-threaded or multi-user scenarios:

```csharp
// Enable logging with correlation
var options = new SmallMindOptions
{
    ModelPath = "model.gguf",
    Logger = new ConsoleRuntimeLogger(),
    Metrics = new InMemoryRuntimeMetrics()
};

var engine = SmallMindFactory.Create(options);

// Each session can have its own correlation ID
var chatOptions = new ChatClientOptions
{
    SessionId = "user-123-session-456" // Unique per user/session
};

var chatClient = engine.CreateChatClient(model, chatOptions);
```

All logs and metrics from this session will include the correlation ID, making it easy to track activity in production environments.

## Cancellation Token Propagation

All SmallMind API calls that may run for extended periods support `CancellationToken`:

```csharp
// Create a cancellation token source
using var cts = new CancellationTokenSource();
cts.CancelAfter(TimeSpan.FromSeconds(30)); // Timeout after 30 seconds

// Pass the token to generation methods
var result = await session.Generate(request, cts.Token);

// Streaming also supports cancellation
await foreach (var token in session.GenerateStreaming(request, cts.Token))
{
    Console.Write(token.TokenText);
    
    // Can cancel mid-stream
    if (someCondition)
        cts.Cancel();
}
```

Cancellation propagates to:
- Token generation loops
- GGUF file I/O
- RAG retrieval operations
- Streaming callbacks

## Best Practices

### ✅ DO:
- Create one engine instance and share it across your application
- Create separate sessions for concurrent users or requests
- Use correlation IDs for tracking in multi-session scenarios
- Always pass `CancellationToken` for long-running operations
- Dispose sessions when done to release resources

### ❌ DON'T:
- Share session instances across threads without explicit synchronization
- Create multiple engine instances (wastes memory - share one engine)
- Assume sessions are thread-safe (they are not)
- Forget to dispose sessions and engines

## Performance Considerations

- **Session Creation**: Lightweight (~1ms overhead)
- **Shared Weights**: Single copy in memory, shared read-only across all sessions
- **KV Cache**: Per-session, configurable size, can be limited via `MaxKvCacheTokens`
- **Memory Budget**: Use `MaxTensorBytes` and `MemoryBudgetMode` to control memory usage

## Example: Multi-User Chat Server

```csharp
public class ChatServer
{
    private readonly ISmallMindEngine _engine; // Shared engine (thread-safe)
    private readonly ConcurrentDictionary<string, IChatClient> _sessions = new();
    
    public ChatServer(SmallMindOptions options)
    {
        _engine = SmallMindFactory.Create(options);
    }
    
    public async Task<string> HandleUserMessage(string userId, string message, CancellationToken ct)
    {
        // Get or create user-specific session
        var session = _sessions.GetOrAdd(userId, _ =>
        {
            var chatOptions = new ChatClientOptions
            {
                SessionId = userId,
                EnableKvCache = true,
                MaxKvCacheTokens = 2048
            };
            
            // Load model (thread-safe)
            var model = await _engine.LoadModelAsync(new ModelLoadRequest
            {
                Path = "model.gguf"
            }, ct);
            
            return _engine.CreateChatClient(model, chatOptions);
        });
        
        // Use session (must be synchronized per-user)
        // Each user has their own session, so no cross-user contention
        var request = new ChatRequest
        {
            Messages = new[] { new ChatMessage { Role = "user", Content = message } }
        };
        
        var response = await session.SendChat(request, ct);
        return response.Content;
    }
    
    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
        }
        _engine.Dispose();
    }
}
```

## Additional Resources

- [Public API Documentation](docs/PublicApi.md)
- [Performance Optimization Guide](docs/PERFORMANCE.md)
- [Memory Management](docs/MEMORY.md)

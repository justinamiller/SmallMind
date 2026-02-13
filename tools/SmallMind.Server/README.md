# SmallMind.Server

OpenAI-compatible HTTP API server for SmallMind inference engine.

## Features

- **OpenAI API Compatible**: Drop-in replacement for OpenAI API clients
- **Streaming Support**: Server-Sent Events (SSE) for real-time token generation
- **Request Queueing**: Bounded queue with backpressure (HTTP 429 when full)
- **Observability**: Built-in metrics via System.Diagnostics.Metrics and ActivitySource
- **Production Ready**: Zero third-party dependencies, pure .NET 10

## Quick Start

### Prerequisites

- .NET 10 SDK
- A trained SmallMind model (.smq file)

### Running

```bash
# Build
dotnet build

# Run with direct model path
dotnet run -- --ServerOptions:ModelPath=../../benchmark-model.smq

# Run on all interfaces
dotnet run -- --ServerOptions:Host=0.0.0.0 --ServerOptions:Port=8080
```

## API Endpoints

- `GET /healthz` - Health check
- `GET /readyz` - Readiness check
- `GET /v1/models` - List models
- `POST /v1/chat/completions` - Chat completions (streaming & non-streaming)
- `POST /v1/completions` - Text completions
- `POST /v1/embeddings` - Embeddings

## Examples

See `example-client.sh` for complete examples.

### Chat Completion

```bash
curl http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "smallmind",
    "messages": [{"role": "user", "content": "Hello!"}],
    "max_tokens": 50,
    "stream": true
  }'
```

### Using with OpenAI Python Client

```python
from openai import OpenAI

client = OpenAI(
    base_url="http://localhost:8080/v1",
    api_key="not-needed"
)

response = client.chat.completions.create(
    model="smallmind",
    messages=[{"role": "user", "content": "Hello!"}]
)
print(response.choices[0].message.content)
```

## Configuration

Edit `appsettings.json` or use CLI args:

```json
{
  "ServerOptions": {
    "ModelPath": "/path/to/model.smq",
    "Host": "localhost",
    "Port": 8080,
    "MaxConcurrentRequests": 4,
    "MaxQueueDepth": 32
  }
}
```

### Production Hardening Options

| Option | Default | Description |
|--------|---------|-------------|
| `MaxConcurrentRequests` | 4 | Maximum in-flight inference requests |
| `MaxQueueDepth` | 32 | Maximum queued requests before 429 |
| `RequestTimeoutMs` | 300000 | Whole-request timeout (ms) |
| `MaxCompletionTokens` | 2048 | Max output tokens per request |
| `MaxPromptTokens` | 8192 | Max prompt tokens per request |
| `MaxContextTokens` | 4096 | Max total tokens (prompt + completion) |
| `PerTokenTimeoutMs` | 5000 | Timeout per generated token (streaming) |
| `MaxRequestBodySizeBytes` | 1048576 | Max request body size (1 MB) |
| `StrictLimits` | true | Reject requests exceeding any limit |
| `EnableConsoleLogging` | false | Enable console logging (default: silent) |

## Testing

Run the test script:

```bash
./test-server.sh
```

# SmallMind.Server

OpenAI-compatible HTTP API server for SmallMind inference engine.

## Features

- **OpenAI API Compatible**: Drop-in replacement for OpenAI API clients
- **Streaming Support**: Server-Sent Events (SSE) for real-time token generation
- **Request Queueing**: Bounded queue with backpressure (429 when full)
- **Observability**: Built-in metrics via System.Diagnostics.Metrics and ActivitySource
- **Production Ready**: Zero third-party dependencies, pure .NET 10

## Quick Start

### Configuration

Configure via `appsettings.json`, environment variables, or CLI arguments:

```json
{
  "ServerOptions": {
    "ModelId": "my-model",
    "ModelPath": "/path/to/model.smq",
    "Host": "localhost",
    "Port": 8080,
    "MaxConcurrentRequests": 4
  }
}
```

### Running the Server

```bash
dotnet run -- --ServerOptions:ModelPath=/path/to/model.smq
```

## API Endpoints

- `GET /healthz` - Health check
- `GET /readyz` - Readiness check
- `GET /v1/models` - List models
- `POST /v1/chat/completions` - Chat completions
- `POST /v1/completions` - Text completions
- `POST /v1/embeddings` - Embeddings

## Example

```bash
curl http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "smallmind",
    "messages": [{"role": "user", "content": "Hello!"}],
    "stream": true
  }'
```

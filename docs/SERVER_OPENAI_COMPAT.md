# SmallMind OpenAI-Compatible Server

SmallMind Server provides an OpenAI-compatible HTTP API for the SmallMind inference engine. It allows you to use SmallMind models with any OpenAI-compatible client library.

## Features

- **OpenAI-Compatible API**: Drop-in replacement for OpenAI API endpoints
- **Model Registry**: Manage models with manifest, caching, and verification
- **Streaming Support**: Server-Sent Events (SSE) for real-time generation
- **Concurrency Control**: Request queue with backpressure and configurable limits
- **Health Checks**: Liveness and readiness probes for orchestration
- **Observability**: Built-in metrics and tracing using .NET BCL (no third-party dependencies)
- **Zero Dependencies**: Pure .NET implementation with no external NuGet packages

## Quick Start

### 1. Add a Model to the Registry

```bash
# Add a local model
dotnet run --project src/SmallMind.Console -- model add ./my-model.smq --id my-model

# Add a model from URL
dotnet run --project src/SmallMind.Console -- model add https://example.com/model.gguf --id remote-model

# List registered models
dotnet run --project src/SmallMind.Console -- model list

# Verify a model
dotnet run --project src/SmallMind.Console -- model verify my-model
```

### 2. Start the Server

Using a registered model:
```bash
cd tools/SmallMind.Server
dotnet run -- --ServerOptions:ModelId=my-model
```

Using a direct file path:
```bash
cd tools/SmallMind.Server
dotnet run -- --ServerOptions:ModelPath=../../benchmark-model.smq
```

### 3. Test the API

```bash
# Health check
curl http://localhost:8080/healthz

# List models
curl http://localhost:8080/v1/models

# Chat completion (non-streaming)
curl http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "smallmind",
    "messages": [
      {"role": "system", "content": "You are a helpful assistant."},
      {"role": "user", "content": "Hello!"}
    ],
    "max_tokens": 50,
    "temperature": 0.7
  }'

# Chat completion (streaming)
curl http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "smallmind",
    "messages": [{"role": "user", "content": "Count to 10"}],
    "max_tokens": 100,
    "stream": true
  }'
```

## API Endpoints

### GET /healthz

Liveness probe - returns 200 if the server is alive.

**Response:**
```json
{"status": "healthy"}
```

### GET /readyz

Readiness probe - returns 200 if the server is ready to accept requests (model loaded and functional).

**Response (ready):**
```json
{"status": "ready", "capabilities": {...}}
```

**Response (not ready):**
```json
{"status": "not ready", "reason": "..."}
```

### GET /v1/models

List available models.

**Response:**
```json
{
  "object": "list",
  "data": [
    {
      "id": "smallmind",
      "object": "model",
      "created": 1234567890,
      "owned_by": "smallmind"
    }
  ]
}
```

### POST /v1/chat/completions

Generate chat completions from messages.

**Request:**
```json
{
  "model": "smallmind",
  "messages": [
    {"role": "system", "content": "You are a helpful assistant."},
    {"role": "user", "content": "What is the capital of France?"}
  ],
  "max_tokens": 100,
  "temperature": 0.7,
  "top_p": 0.9,
  "top_k": 40,
  "stream": false,
  "seed": 42
}
```

**Response (non-streaming):**
```json
{
  "id": "chatcmpl-123",
  "object": "chat.completion",
  "created": 1234567890,
  "model": "smallmind",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "The capital of France is Paris."
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 15,
    "completion_tokens": 8,
    "total_tokens": 23
  }
}
```

**Response (streaming):**

When `stream: true`, the response is a series of Server-Sent Events:

```
data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1234567890,"model":"smallmind","choices":[{"index":0,"delta":{"role":"assistant","content":"The"},"finish_reason":null}]}

data: {"id":"chatcmpl-123","object":"chat.completion.chunk","created":1234567890,"model":"smallmind","choices":[{"index":0,"delta":{"content":" capital"},"finish_reason":null}]}

...

data: [DONE]
```

### POST /v1/completions

Generate text completions from a prompt.

**Request:**
```json
{
  "model": "smallmind",
  "prompt": "Once upon a time",
  "max_tokens": 50,
  "temperature": 0.8,
  "top_p": 0.95,
  "stream": false
}
```

**Response:**
```json
{
  "id": "cmpl-123",
  "object": "text_completion",
  "created": 1234567890,
  "model": "smallmind",
  "choices": [
    {
      "text": " there was a brave knight...",
      "index": 0,
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 4,
    "completion_tokens": 10,
    "total_tokens": 14
  }
}
```

### POST /v1/embeddings

Generate embeddings from input text.

**Request:**
```json
{
  "model": "smallmind",
  "input": "The quick brown fox jumps over the lazy dog"
}
```

**Response:**
```json
{
  "object": "list",
  "data": [
    {
      "object": "embedding",
      "embedding": [0.123, -0.456, 0.789, ...],
      "index": 0
    }
  ],
  "model": "smallmind",
  "usage": {
    "prompt_tokens": 10,
    "total_tokens": 10
  }
}
```

**Note:** If embeddings are not supported by the current model, returns HTTP 400:
```json
{
  "error": {
    "message": "Embeddings are not supported by this model",
    "type": "invalid_request_error",
    "code": "embeddings_not_supported"
  }
}
```

## Configuration

Configuration can be provided via:
1. `appsettings.json`
2. Environment variables (prefix with `ServerOptions__`)
3. Command-line arguments (format: `--ServerOptions:Key=Value`)

### Configuration Options

| Option | Type | Default | Description |
|--------|------|---------|-------------|
| `ModelId` | string | - | Model ID from registry |
| `ModelPath` | string | - | Direct path to model file |
| `CacheDir` | string | (platform default) | Model cache directory |
| `Host` | string | `localhost` | Server host address |
| `Port` | int | `8080` | Server port |
| `MaxConcurrentRequests` | int | `4` | Maximum concurrent inference requests |
| `MaxQueueDepth` | int | `32` | Maximum queued requests (returns 429 when full) |
| `RequestTimeoutMs` | int | `300000` | Request timeout in milliseconds |
| `MaxContextTokens` | int | `4096` | Maximum context tokens |
| `DefaultMaxTokens` | int | `100` | Default max tokens for generation |
| `DefaultTemperature` | double | `0.8` | Default temperature |
| `DefaultTopP` | double | `0.95` | Default top-p value |
| `DefaultTopK` | int | `40` | Default top-k value |

### Example Configuration

**appsettings.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ServerOptions": {
    "ModelId": "my-model",
    "Host": "0.0.0.0",
    "Port": 8080,
    "MaxConcurrentRequests": 8,
    "MaxQueueDepth": 64,
    "RequestTimeoutMs": 300000,
    "MaxContextTokens": 2048
  }
}
```

**Environment Variables:**
```bash
export ServerOptions__ModelPath=/path/to/model.smq
export ServerOptions__Port=9000
export ServerOptions__MaxConcurrentRequests=16
```

**Command-line Arguments:**
```bash
dotnet run -- \
  --ServerOptions:ModelPath=./model.smq \
  --ServerOptions:Port=9000 \
  --ServerOptions:MaxConcurrentRequests=16
```

## Docker Deployment

### Build the Image

```bash
docker build -t smallmind-server .
```

### Run the Container

With a model in the registry:
```bash
docker run -p 8080:8080 \
  -e ServerOptions__ModelId=my-model \
  -v ~/.cache/smallmind/models:/root/.cache/smallmind/models:ro \
  smallmind-server
```

With a direct model file:
```bash
docker run -p 8080:8080 \
  -e ServerOptions__ModelPath=/app/models/model.smq \
  -v /path/to/models:/app/models:ro \
  smallmind-server
```

### Docker Compose

```yaml
version: '3.8'
services:
  smallmind:
    image: smallmind-server
    ports:
      - "8080:8080"
    environment:
      - ServerOptions__ModelPath=/app/models/benchmark-model.smq
      - ServerOptions__MaxConcurrentRequests=8
    volumes:
      - ./models:/app/models:ro
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/healthz"]
      interval: 30s
      timeout: 3s
      retries: 3
```

## Using with OpenAI Client Libraries

### Python

```python
from openai import OpenAI

client = OpenAI(
    base_url="http://localhost:8080/v1",
    api_key="not-needed"  # API key not required but must be set
)

response = client.chat.completions.create(
    model="smallmind",
    messages=[
        {"role": "system", "content": "You are a helpful assistant."},
        {"role": "user", "content": "What is the capital of France?"}
    ],
    max_tokens=100,
    temperature=0.7
)

print(response.choices[0].message.content)
```

### Node.js

```javascript
import OpenAI from 'openai';

const client = new OpenAI({
  baseURL: 'http://localhost:8080/v1',
  apiKey: 'not-needed'
});

const response = await client.chat.completions.create({
  model: 'smallmind',
  messages: [
    { role: 'system', content: 'You are a helpful assistant.' },
    { role: 'user', content: 'What is the capital of France?' }
  ],
  max_tokens: 100,
  temperature: 0.7
});

console.log(response.choices[0].message.content);
```

### cURL

```bash
curl http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "smallmind",
    "messages": [
      {"role": "user", "content": "Hello!"}
    ],
    "max_tokens": 50
  }'
```

## Observability

SmallMind Server includes built-in observability using .NET BCL primitives (no third-party dependencies).

### Metrics

The server exposes metrics via `System.Diagnostics.Metrics`:

**Meter Name:** `SmallMind.Server`

**Metrics:**
- `requests_total` (Counter): Total number of requests
- `requests_inflight` (UpDownCounter): Current number of in-flight requests
- `request_duration_ms` (Histogram): Request duration in milliseconds
- `tokens_generated_total` (Counter): Total tokens generated

### Distributed Tracing

The server creates activities via `System.Diagnostics.ActivitySource`:

**ActivitySource Name:** `SmallMind.Server`

**Activities:**
- `request`: HTTP request processing
- `generation`: Text generation

### Consuming Metrics

You can consume metrics using:
- **dotnet-counters**: `dotnet-counters monitor --process-id <pid> SmallMind.Server`
- **OpenTelemetry Collector**: Configure OTLP exporter
- **Prometheus**: Use .NET metrics exporter

Example with dotnet-counters:
```bash
dotnet-counters monitor --process-id $(pgrep -f SmallMind.Server) SmallMind.Server
```

## Compatibility Notes

### Supported Features

- ✅ Chat completions (non-streaming)
- ✅ Chat completions (streaming via SSE)
- ✅ Text completions (non-streaming)
- ✅ Text completions (streaming via SSE)
- ✅ Embeddings (if supported by model)
- ✅ Temperature, top_p, top_k sampling
- ✅ Max tokens limit
- ✅ Stop sequences
- ✅ Deterministic generation (seed)
- ✅ Request cancellation

### Not Supported (OpenAI features)

- ❌ `response_format` (structured output)
- ❌ `functions` / `tools` (function calling)
- ❌ `logprobs` (log probabilities)
- ❌ `n` parameter (multiple completions)
- ❌ `presence_penalty` / `frequency_penalty`
- ❌ `logit_bias`

### Limitations

1. **Model Selection**: The `model` parameter in requests is ignored; the server uses the configured model
2. **Context Length**: Limited by the model's maximum context size
3. **Concurrency**: Controlled by `MaxConcurrentRequests`; additional requests are queued or rejected (429)
4. **Performance**: CPU-only inference; no GPU acceleration

## Error Handling

All errors return OpenAI-compatible error responses:

```json
{
  "error": {
    "message": "Descriptive error message",
    "type": "invalid_request_error",
    "code": "specific_error_code",
    "param": "parameter_name"
  }
}
```

**Common Error Codes:**

| HTTP Status | Error Type | Description |
|-------------|------------|-------------|
| 400 | `invalid_request_error` | Malformed request |
| 429 | `rate_limit_exceeded` | Request queue full |
| 500 | `internal_error` | Server error |
| 503 | `service_unavailable` | Model not ready |

## Performance Tuning

### Concurrency

Adjust `MaxConcurrentRequests` based on your CPU cores and memory:
- **Low-end**: 1-2 concurrent requests
- **Mid-range**: 4-8 concurrent requests
- **High-end**: 8-16 concurrent requests

### Queue Depth

Set `MaxQueueDepth` to handle bursts:
- **Conservative**: 16-32 (fail fast)
- **Moderate**: 32-64 (balanced)
- **Aggressive**: 64-128 (absorb spikes)

### Context Length

Reduce `MaxContextTokens` for faster inference:
- **Short conversations**: 512-1024 tokens
- **Medium conversations**: 1024-2048 tokens
- **Long conversations**: 2048-4096 tokens

## Troubleshooting

### Server won't start

**Check model path/ID:**
```bash
# Verify model exists
dotnet run --project src/SmallMind.Console -- model list

# Verify model integrity
dotnet run --project src/SmallMind.Console -- model verify <model-id>
```

### Requests return 429 (queue full)

**Increase queue depth or concurrent requests:**
```bash
dotnet run -- \
  --ServerOptions:MaxQueueDepth=64 \
  --ServerOptions:MaxConcurrentRequests=8
```

### Slow inference

**Reduce context length or max tokens:**
```bash
dotnet run -- \
  --ServerOptions:MaxContextTokens=1024 \
  --ServerOptions:DefaultMaxTokens=256
```

### Out of memory

**Reduce concurrent requests:**
```bash
dotnet run -- --ServerOptions:MaxConcurrentRequests=2
```

## Development

### Building from Source

```bash
# Build server
dotnet build tools/SmallMind.Server/SmallMind.Server.csproj -c Release

# Run server
cd tools/SmallMind.Server
dotnet run -c Release
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run model registry tests
dotnet test tests/SmallMind.ModelRegistry.Tests/
```

### Development Mode

Run with detailed logging:
```bash
cd tools/SmallMind.Server
ASPNETCORE_ENVIRONMENT=Development dotnet run
```

## License

SmallMind is licensed under the MIT License. See LICENSE file for details.

## Support

For issues, questions, or contributions:
- GitHub Issues: https://github.com/justinamiller/SmallMind/issues
- Documentation: https://github.com/justinamiller/SmallMind

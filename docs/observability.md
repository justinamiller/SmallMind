# Observability Guide

This guide covers logging, metrics, and health checks for SmallMind in production environments.

## Table of Contents

- [Logging](#logging)
- [Metrics](#metrics)
- [Health Checks](#health-checks)
- [Distributed Tracing](#distributed-tracing)
- [Monitoring Best Practices](#monitoring-best-practices)

## Logging

SmallMind uses structured logging via `Microsoft.Extensions.Logging` with high-performance `LoggerMessage` source generators.

### Log Categories

| Category | Description | Typical Level |
|----------|-------------|---------------|
| `SmallMind.Core.Training` | Training progress and metrics | Information |
| `SmallMind.Core.Transformer` | Model inference | Debug |
| `SmallMind.Text.Sampling` | Text generation | Debug |
| `SmallMind.Telemetry` | Metrics and diagnostics | Debug |
| `SmallMind.Health` | Health check results | Information |

### Key Log Messages

#### Training Logs

```csharp
// Training step (every logEvery steps)
[INF] Training step 100/1000, Loss: 2.34, Tokens/sec: 1234.56, Elapsed: 5.23s

// Validation
[INF] Validation loss: 2.45

// Checkpoint saved
[INF] Checkpoint saved to: ./checkpoints/model_step_1000.json

// Training completed
[INF] Training completed in 125.34 seconds. Total tokens: 1234567, Tokens/sec: 9876.54

// Training cancelled
[WRN] Training cancelled at step 543. Checkpoint saved to: ./checkpoints/model_cancelled.json
```

#### Diagnostic Logs

```csharp
// Gradient health (when diagnostics enabled)
[DBG] Gradient health check: step 100, NaN count: 0, max gradient: 1.234

// Numerical instability
[WRN] Numerical instability detected: Loss is NaN or Infinity at step 234
```

### Configuration Example

```csharp
builder.Logging.AddConsole(options =>
{
    options.FormatterName = "json";  // Structured JSON logging
});

builder.Logging.AddFilter("SmallMind.Core.Training", LogLevel.Information);
builder.Logging.AddFilter("SmallMind.Telemetry", LogLevel.Debug);
```

### Structured Logging Best Practices

1. **Use log levels appropriately**:
   - Information: Key business events (training completed, checkpoint saved)
   - Debug: Detailed diagnostics (gradient health, performance metrics)
   - Warning: Recoverable issues (numerical instability, validation plateaus)
   - Error: Training failures, checkpoint errors

2. **Include context** in log messages:
   - Step number
   - Loss values
   - Timing information
   - File paths

3. **Use scoped logging** for correlation:
   ```csharp
   using (logger.BeginScope("TrainingSession={SessionId}", sessionId))
   {
       // All logs in this scope include SessionId
   }
   ```

## Metrics

SmallMind uses `System.Diagnostics.Metrics` for instrumentation compatible with OpenTelemetry.

### Meter Configuration

**Meter Name**: `SmallMind`

All SmallMind metrics are published under this meter name.

### Available Metrics

#### Training Metrics

| Metric Name | Type | Unit | Description |
|-------------|------|------|-------------|
| `smallmind.training.step` | Counter | steps | Training steps completed |
| `smallmind.training.loss` | Histogram | loss | Training loss distribution |
| `smallmind.training.tokens_per_second` | Histogram | tokens/s | Training throughput |
| `smallmind.training.duration` | Histogram | seconds | Training session duration |

#### Inference Metrics

| Metric Name | Type | Counter | Description |
|-------------|------|---------|-------------|
| `smallmind.inference.generation` | Counter | generations | Text generation requests |
| `smallmind.inference.tokens_generated` | Counter | tokens | Tokens generated |
| `smallmind.inference.duration` | Histogram | milliseconds | Generation latency |

#### Checkpoint Metrics

| Metric Name | Type | Unit | Description |
|-------------|------|------|-------------|
| `smallmind.checkpoint.save` | Counter | checkpoints | Checkpoints saved |
| `smallmind.checkpoint.load` | Counter | checkpoints | Checkpoints loaded |
| `smallmind.checkpoint.size` | Histogram | bytes | Checkpoint file size |

### Enabling Metrics

```csharp
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("SmallMind");
        metrics.AddPrometheusExporter();  // Or your preferred exporter
    });
```

### Prometheus Example

```csharp
// Add Prometheus scraping endpoint
app.MapPrometheusScrapingEndpoint();

// Metrics available at /metrics
```

Example Prometheus metrics output:
```
# HELP smallmind_training_step_total Training steps completed
# TYPE smallmind_training_step_total counter
smallmind_training_step_total 1000

# HELP smallmind_training_loss Training loss distribution
# TYPE smallmind_training_loss histogram
smallmind_training_loss_bucket{le="1.0"} 0
smallmind_training_loss_bucket{le="2.0"} 234
smallmind_training_loss_bucket{le="3.0"} 567
```

### Custom Metrics

To add custom metrics:

```csharp
using System.Diagnostics.Metrics;

public class CustomMetrics
{
    private static readonly Meter _meter = new("SmallMind");
    private static readonly Counter<long> _customCounter = 
        _meter.CreateCounter<long>("smallmind.custom.events");

    public void RecordEvent()
    {
        _customCounter.Add(1, new KeyValuePair<string, object?>("type", "custom"));
    }
}
```

## Health Checks

SmallMind provides health check endpoints for Kubernetes readiness/liveness probes.

### Available Health Checks

| Check Name | Purpose | Failure Condition |
|------------|---------|-------------------|
| `SmallMindModelHealth` | Verifies model is loaded and functional | Model null or corrupt |
| `SmallMindMemoryHealth` | Checks available memory | Available memory < threshold |
| `SmallMindInferenceHealth` | Tests inference pipeline | Inference fails or times out |

### Configuration

```csharp
builder.Services.AddHealthChecks()
    .AddSmallMindHealthChecks(options =>
    {
        options.IncludeModelCheck = true;
        options.IncludeMemoryCheck = true;
        options.MemoryThresholdMB = 100;  // Fail if < 100MB available
        options.IncludeInferenceCheck = true;
        options.InferenceTimeoutMs = 1000;  // Fail if inference > 1s
    });
```

### Health Check Endpoints

```csharp
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live")
});
```

### Kubernetes Integration

```yaml
apiVersion: v1
kind: Pod
metadata:
  name: smallmind-api
spec:
  containers:
  - name: api
    image: smallmind:latest
    livenessProbe:
      httpGet:
        path: /health/live
        port: 8080
      initialDelaySeconds: 30
      periodSeconds: 10
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 8080
      initialDelaySeconds: 10
      periodSeconds: 5
```

### Health Check Response Example

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "SmallMindModelHealth": {
      "status": "Healthy",
      "duration": "00:00:00.0012345",
      "data": {
        "modelLoaded": true,
        "vocabSize": 256,
        "layers": 4
      }
    },
    "SmallMindMemoryHealth": {
      "status": "Healthy",
      "duration": "00:00:00.0001234",
      "data": {
        "availableMemoryMB": 2048,
        "thresholdMB": 100
      }
    }
  }
}
```

## Distributed Tracing

SmallMind supports distributed tracing via OpenTelemetry.

### Configuration

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddSource("SmallMind");
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddJaegerExporter();  // Or your preferred exporter
    });
```

### Key Spans

| Span Name | Description | Attributes |
|-----------|-------------|------------|
| `Training.Train` | Training session | steps, learningRate, batchSize |
| `Training.Step` | Single training step | stepNumber, loss |
| `Inference.Generate` | Text generation | prompt, maxTokens, temperature |
| `Checkpoint.Save` | Checkpoint save operation | path, sizeBytes |
| `Checkpoint.Load` | Checkpoint load operation | path |

### Example Trace

```
Training.Train (5m 23s)
├── Checkpoint.Load (123ms)
├── Training.Step 1 (52ms)
│   ├── Forward (23ms)
│   └── Backward (29ms)
├── Training.Step 2 (51ms)
├── ...
├── Training.Step 1000 (48ms)
└── Checkpoint.Save (234ms)
```

## Monitoring Best Practices

### 1. Set Up Alerts

Monitor these key metrics:

- **Training Loss**: Alert if loss is NaN, Infinity, or doesn't decrease over N steps
- **Memory Usage**: Alert if available memory < threshold
- **Checkpoint Failures**: Alert on any checkpoint save/load errors
- **Inference Latency**: Alert if p99 > threshold

### 2. Dashboard Recommendations

Create dashboards with:

- **Training Progress**: Loss over time, tokens/sec
- **System Health**: Memory usage, CPU usage, thread pool status
- **Inference Performance**: Latency (p50, p95, p99), throughput
- **Error Rates**: Training failures, checkpoint errors, validation errors

### 3. Log Aggregation

Use a centralized logging solution:

- **ELK Stack** (Elasticsearch, Logstash, Kibana)
- **Grafana Loki**
- **Azure Application Insights**
- **AWS CloudWatch**

Query examples:
```
# All training sessions with high loss
Category:"SmallMind.Core.Training" AND Loss > 10

# Numerical instability warnings
Level:Warning AND Message:*"instability"*

# Cancelled training sessions
Message:*"cancelled"* AND Category:*Training*
```

### 4. Correlation IDs

Use correlation IDs for request tracking:

```csharp
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? Guid.NewGuid().ToString();
    
    using (logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId
    }))
    {
        await next();
    }
});
```

### 5. SLIs and SLOs

Define Service Level Indicators (SLIs) and Objectives (SLOs):

| SLI | SLO | Measurement |
|-----|-----|-------------|
| Inference Latency | p99 < 500ms | `smallmind.inference.duration` |
| Inference Availability | 99.9% success | `smallmind.inference.generation` error rate |
| Training Success Rate | 95% complete without errors | Training completion vs. failure logs |
| Checkpoint Reliability | 99.9% successful saves | `smallmind.checkpoint.save` error rate |

## Example: Full Observability Setup

```csharp
var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.AddConsole(options => options.FormatterName = "json");
builder.Logging.AddFilter("SmallMind", LogLevel.Information);

// Metrics
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("SmallMind");
        metrics.AddPrometheusExporter();
    })
    .WithTracing(tracing =>
    {
        tracing.AddSource("SmallMind");
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddJaegerExporter(options =>
        {
            options.AgentHost = "jaeger";
            options.AgentPort = 6831;
        });
    });

// Health Checks
builder.Services.AddHealthChecks()
    .AddSmallMindHealthChecks();

var app = builder.Build();

// Endpoints
app.MapHealthChecks("/health");
app.MapPrometheusScrapingEndpoint();

app.Run();
```

## Troubleshooting

See [docs/troubleshooting.md](troubleshooting.md) for observability-related issues.

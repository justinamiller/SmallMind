# SmallMind Showcase

A production-quality web application demonstrating SmallMind's capabilities.

## Quick Start

```bash
# 1. Add model files to src/SmallMind.Showcase.Web/models/
# 2. Run the application
cd src/SmallMind.Showcase.Web
dotnet run

# 3. Open browser to https://localhost:5001
```

## Features

✅ **Chat Interface** - ChatGPT-like UI with streaming responses  
✅ **Session Management** - Create/switch multiple chat sessions  
✅ **Model Registry** - Discover and load local GGUF/SMQ models  
✅ **Real-Time Metrics** - TTFT, tok/s, latency, GC stats, memory  
✅ **Responsive Design** - Modern 3-column layout  
✅ **Zero Dependencies** - Built with ASP.NET Core + Blazor Server only  

## Documentation

See [docs/showcase.md](docs/showcase.md) for full documentation.

## Architecture

- **Frontend**: Blazor Server (real-time updates via SignalR)
- **Backend**: SmallMind public API
- **Storage**: JSON file-based (no database required)
- **Metrics**: Custom MetricsCollector with percentile tracking

## Requirements

- .NET 10 SDK
- At least one compatible model file (.gguf or .smq)
- 4GB+ RAM (depending on model size)

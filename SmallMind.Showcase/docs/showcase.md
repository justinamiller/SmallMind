# SmallMind Showcase - User Guide

## Overview

SmallMind Showcase is a demonstration web application that showcases the capabilities of the SmallMind pure C# language model framework. It provides a ChatGPT-like interface for interacting with local language models, complete with real-time metrics and performance monitoring.

## Features

- 🤖 **Chat Interface**: Interactive chat with streaming responses
- 📊 **Real-Time Metrics**: TTFT, tok/s, latency percentiles, GC stats, and memory usage
- 💬 **Session Management**: Create, switch, and manage multiple chat sessions
- 🔧 **Model Management**: Load and switch between different local models (GGUF/SMQ)
- ⚙️ **Configurable Parameters**: Adjust temperature, top-p, top-k, max tokens, and more
- 📁 **Persistent Storage**: Sessions saved as JSON files for later access

## Getting Started

### Prerequisites

- .NET 10 SDK or later
- At least one compatible language model file (.gguf or .smq format)

### Running the Application

1. **Clone and build the repository:**
   ```bash
   cd SmallMind.Showcase
   dotnet restore
   dotnet build
   ```

2. **Add your models:**
   - Create a `models` directory in `SmallMind.Showcase/src/SmallMind.Showcase.Web/`
   - Copy your .gguf or .smq model files into this directory
   
   Example models (compatible with SmallMind):
   - SmolLM2-135M-Instruct Q4 quantized
   - Llama models in GGUF format
   - Phi models in GGUF format
   - Mistral models in GGUF format

3. **Run the application:**
   ```bash
   cd src/SmallMind.Showcase.Web
   dotnet run
   ```

4. **Open your browser:**
   - Navigate to `https://localhost:5001` or `http://localhost:5000`
   - The showcase interface will appear

### Configuration

Edit `appsettings.json` to customize paths:

```json
{
  "SmallMind": {
    "ModelsPath": "./models",     // Where to find model files
    "DataPath": "./.data"          // Where to store session data
  }
}
```

## Using the Application

### 1. Load a Model

When you first open the application, you'll see the model selection screen:

1. Browse available models in the models directory
2. Click "Load Model" on the model you want to use
3. Wait for the model to load (this may take a few seconds depending on model size)
4. Once loaded, the active model will be shown in the header

### 2. Create a Chat Session

1. Click the "+ New" button in the left sidebar
2. A new session will be created and selected
3. Type your message in the input box at the bottom
4. Press Enter to send (Shift+Enter for newline)

### 3. Monitor Metrics

The right sidebar shows real-time metrics:

- **TTFT**: Time to First Token (ms)
- **Prefill**: Prefill tokens per second
- **Decode**: Decode tokens per second
- **Per-token**: Average latency per token (ms)
- **Token counts**: Prompt, generated, and total tokens
- **Memory**: Managed heap size and GC collection counts
- **Percentiles**: P50, P95, P99 latency distribution

### 4. Manage Sessions

- **Switch sessions**: Click on any session in the left sidebar
- **Create new**: Click the "+ New" button
- Sessions are automatically saved to disk

### 5. Control Generation

- **Stop**: Click the "Stop" button to cancel generation mid-stream
- **Clear**: Clear the current conversation (future feature)
- **Regenerate**: Regenerate the last response (future feature)

## Advanced Configuration

### Generation Parameters

Modify these in code (future UI feature) via `GenerationConfig`:

- **Temperature** (0.0-2.0): Controls randomness. Lower = more deterministic, higher = more creative
- **TopP** (0.0-1.0): Nucleus sampling threshold. Default 0.9
- **TopK** (int): Top-k sampling. Default 40
- **MaxTokens** (int): Maximum tokens to generate. Default 512
- **Seed** (int?): Random seed for deterministic generation

### Model Format Support

SmallMind Showcase supports:

- **.gguf files**: GGUF format models (set `AllowGgufImport` in code)
- **.smq files**: SmallMind native quantized format

Quantization formats supported:
- Q4_0, Q4_1 (4-bit quantization)
- Q5_0, Q5_1 (5-bit quantization)
- Q8_0 (8-bit quantization)
- F16, F32 (half/full precision)

## Troubleshooting

### No models appear

**Problem**: The models list is empty

**Solution**: 
- Ensure models are placed in the configured `ModelsPath` directory
- Verify files have .gguf or .smq extensions
- Check file permissions

### Model fails to load

**Problem**: "Failed to load model" error appears

**Solution**:
- Verify the model file is not corrupted
- Check that you have enough RAM (models can be several GB)
- For GGUF files, ensure `AllowGgufImport = true` in SmallMindOptions
- Check the browser console and application logs for detailed error messages

### Generation is slow

**Problem**: Token generation is very slow

**Solution**:
- This is expected for CPU-only inference
- SmallMind is optimized but runs on CPU without GPU acceleration
- Smaller models (e.g., 135M parameters) will be faster than larger ones
- Quantized models (Q4) are faster than full precision (F32)

### Session data lost

**Problem**: Sessions disappear after restart

**Solution**:
- Check that the `DataPath` directory exists and is writable
- Session files are JSON files in `DataPath` with names like `session_*.json`
- Verify no file permission issues

## Performance Metrics Explained

### Time to First Token (TTFT)
- Time from sending the request to receiving the first token
- Includes prompt processing (prefill) time
- Lower is better (aim for < 1000ms)

### Prefill vs Decode
- **Prefill**: Processing the input prompt to build KV cache
- **Decode**: Generating output tokens one at a time
- Prefill is typically much faster (processes all tokens in parallel)

### Tokens per Second (tok/s)
- **Prefill tok/s**: How fast the prompt is processed
- **Decode tok/s**: How fast output tokens are generated
- Higher is better (10+ tok/s is good for CPU)

### Latency Percentiles
- **P50 (median)**: 50% of requests complete faster than this
- **P95**: 95% of requests complete faster than this
- **P99**: 99% of requests complete faster than this
- Useful for understanding consistency and tail latencies

### Memory & GC
- **Managed Heap**: C# managed memory size
- **GC Gen0/Gen1/Gen2**: Garbage collection counts
- Lower GC counts during generation = better performance

## Architecture

SmallMind Showcase is built with:

- **Frontend**: Blazor Server (ASP.NET Core)
- **Backend**: SmallMind pure C# LLM engine
- **Storage**: JSON file-based persistence
- **Styling**: Bootstrap 5 + custom CSS

### Project Structure

```
SmallMind.Showcase/
├── src/
│   ├── SmallMind.Showcase.Core/       # Business logic & services
│   │   ├── Models/                     # Domain models
│   │   ├── Interfaces/                 # Service interfaces
│   │   └── Services/                   # Service implementations
│   └── SmallMind.Showcase.Web/         # Blazor web application
│       ├── Components/                 # Razor components
│       └── wwwroot/                    # Static assets
├── docs/
│   └── showcase.md                     # This file
└── .data/                              # Session storage (gitignored)
```

## Contributing

This showcase is part of the SmallMind project. To contribute:

1. Follow the minimal-change principle
2. Test your changes thoroughly
3. Ensure no new third-party dependencies are added to core SmallMind
4. Web app may use built-in ASP.NET Core features

## License

Same license as the parent SmallMind repository.

## Support

For issues and questions:
- Check the SmallMind repository README
- Review the SmallMind documentation
- Open an issue on GitHub with the `showcase` label

---

**Built with ❤️ using SmallMind - A pure C# language model framework**

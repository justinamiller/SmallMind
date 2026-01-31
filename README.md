# SmallMind - Tiny Educational LLM in Pure C#

SmallMind is a deliberately tiny, educational language model built entirely in C# (.NET 8) from scratch **with NO 3rd party dependencies**. It demonstrates the core concepts of decoder-only Transformers (GPT-style) using only native C# classes.

## Features

- **Pure C# implementation** - No TorchSharp, no ONNX, no external ML libraries
- **Custom automatic differentiation** - Tensor class with backpropagation
- **Decoder-only Transformer architecture** with:
  - Token and positional embeddings
  - Masked multi-head self-attention
  - Feed-forward MLP with GELU activation
  - LayerNorm and residual connections
  - Final linear head to vocabulary
- **Character-level tokenizer** for simplicity
- **Training from scratch** on CPU with next-token prediction
- **Enhanced training mode** with:
  - Gradient accumulation for larger effective batch sizes
  - Learning rate warmup and cosine annealing
  - Validation loss tracking with best model saving
- **Text generation** with temperature sampling and top-k filtering
- **Question-answering capability** - Answer questions based on training data
- **Interactive conversation mode** - Multi-turn conversations with session context
- **Session context management** - Maintain conversation history across turns
- **Checkpointing** to save and load model weights (JSON format)
- **Multiple data loading formats** - Load training data from JSON, XML, CSV, text files, or directories
- **Fully self-contained** - only uses System.* namespaces

## Data Loading

SmallMind now supports loading training data from multiple sources using the `DataLoader` class:

### Supported Formats

1. **Text Files** - `FromTextFile()` - Plain text, one sentence per line
2. **JSON Files** - `FromJsonFile()` - Expected format: `{ "sentences": [...] }`
3. **XML Files** - `FromXmlFile()` - Extracts text from specified XML elements
4. **CSV Files** - `FromCsvFile()` - Column-based extraction with header handling
5. **Directories** - `FromDirectory()` - Batch load from multiple files
6. **Custom Delimiters** - `FromTextWithDelimiters()` - Split text by custom delimiters

### Quick Example

```csharp
using TinyLLM;

// Load from JSON file
var trainingText = DataLoader.FromJsonFile("data.json");

// Load from multiple files in a directory
var trainingText = DataLoader.FromDirectory("training_data/");

// Load from XML with custom element
var trainingText = DataLoader.FromXmlFile("data.xml", elementName: "sentence");
```

See the `examples/` directory for comprehensive usage examples and the `sample_data/` directory for sample files in all supported formats.

### Sample Data

The repository includes sample data files in the `sample_data/` directory:
- `sample.txt` - Plain text format
- `sample.json` - JSON format
- `sample.xml` - XML format  
- `sample.csv` - CSV format

All files contain identical content to demonstrate format equivalence.

## Requirements

- .NET 8 SDK
- **No other dependencies!**
- No GPU required (CPU-only, training will be slow)

## Quick Start

### 1. Build the Project

```bash
# Clone the repository (or create a new directory)
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind

# Build the project
dotnet build

# OR build in release mode for better performance
dotnet build -c Release
```

### 2. Train the Model

Train on the default `data.txt` file (created automatically if missing):

```bash
dotnet run

# OR for better performance
dotnet run -c Release
```

This will:
- Load or create `data.txt` with sample text
- Build a character-level vocabulary
- Train a tiny Transformer for 2000 steps (takes ~10-30 minutes on CPU)
- Save checkpoints to `checkpoints/model.json` every 500 steps
- Generate sample text after training

### 3. Generate Text from a Checkpoint

Skip training and generate text from an existing checkpoint:

```bash
dotnet run -- --no-train --load --prompt "Once upon a time" --steps 200

# OR with release build
dotnet run -c Release -- --no-train --load --prompt "Once upon a time" --steps 200
```

### 4. Custom Training Data

Replace `data.txt` with your own text file (at least 1000 characters recommended):

```bash
# Create your own data.txt
echo "Your custom training text here..." > data.txt

# Train on your data
dotnet run
```

## Command-Line Arguments

| Argument | Default | Description |
|----------|---------|-------------|
| `--model-preset NAME` | "default" | Choose a model architecture preset (default, tiny, mistral-medium, mistral-7b, deepseek) |
| `--list-presets` | - | List all available model presets with their configurations |
| `--no-train` | (train enabled) | Skip training, only generate |
| `--load` | (auto-detect) | Force load checkpoint before generation |
| `--prompt "text"` | "Once upon a time" | Starting text for generation |
| `--steps N` | 200 | Number of tokens to generate |
| `--temperature T` | 1.0 | Sampling temperature (0.1-2.0, lower=more conservative) |
| `--top-k K` | 0 | Top-k filtering (0=disabled, 40 is typical) |
| `--perf` | (disabled) | Show performance metrics with detailed timing and throughput statistics |
| `--perf-json` | (disabled) | Output performance metrics as JSON (machine-readable format) |
| `--bench` | (disabled) | Run benchmark mode with sweeps over different max_tokens configurations |
| `--block-size N` | (preset default) | Context window size (max: 32768, overrides preset) |
| `--max-block-size N` | 32768 | Override maximum block size limit for extremely large contexts |
| `--batch-size N` | (preset default) | Batch size for training (overrides preset, higher = better throughput, more memory) |
| `--auto-config` | (disabled) | Auto-configure block size and batch size based on system RAM and CPU |
| `--enhanced-training` | (disabled) | Use enhanced training with gradient accumulation and LR scheduling |
| `--grad-accum N` | 1 | Gradient accumulation steps (effective batch = batch-size × grad-accum) |
| `--warmup N` | 100 | Learning rate warmup steps |
| `--qa` | (disabled) | Question-answering mode - answer a question based on training data |
| `--interactive` | (disabled) | Interactive conversation mode with session context |

## Examples

```bash
# List all available model presets
dotnet run -- --list-presets

# Train with a specific model preset
dotnet run -- --model-preset mistral-medium

# Train with Mistral 7B inspired architecture
dotnet run -- --model-preset mistral-7b --enhanced-training

# Train with DeepSeek inspired architecture (larger model)
dotnet run -- --model-preset deepseek --enhanced-training --perf

# Use tiny preset for fast testing
dotnet run -- --model-preset tiny

# Generate with a specific preset without training
dotnet run -- --model-preset mistral-medium --no-train --prompt "Knowledge is" --steps 150

# Train and generate with default settings
dotnet run

# Generate with custom prompt and temperature
dotnet run -- --no-train --prompt "The wise owl" --steps 300 --temperature 0.8

# Generate with top-k sampling for more focused output
dotnet run -- --no-train --prompt "Knowledge is" --steps 150 --top-k 40 --temperature 1.2

# Train with real-time performance metrics
dotnet run -- --perf

# Generate with performance tracking
dotnet run -- --no-train --prompt "Once upon a time" --steps 200 --perf

# Use auto-configuration to determine optimal block size based on system resources
dotnet run -- --auto-config

# Use a custom block size (larger context window)
dotnet run -- --block-size 1024

# Use maximum block size with performance tracking
dotnet run -- --block-size 32768 --perf --no-train --prompt "Test" --steps 50

# Use extremely large block size with override (requires significant RAM, 128GB+)
dotnet run -- --block-size 65536 --max-block-size 65536 --batch-size 2 --perf

# Use custom batch size for better throughput (requires more memory)
dotnet run -- --batch-size 32 --block-size 512

# Enhanced training with gradient accumulation and learning rate scheduling
dotnet run -- --enhanced-training --grad-accum 4 --warmup 200 --perf

# Question-answering mode - ask a question based on training data
dotnet run -- --no-train --qa --prompt "What is knowledge?"

# Interactive conversation mode with session context
dotnet run -- --no-train --interactive

# Performance tracking with detailed metrics
dotnet run -- --no-train --prompt "Once upon a time" --steps 200 --perf

# JSON performance output for automated testing
dotnet run -- --no-train --prompt "Test" --steps 100 --perf-json

# Benchmark mode to test different configurations
dotnet run -- --no-train --bench --prompt "Knowledge is"
```

## Model Presets

SmallMind now supports multiple model architecture presets inspired by popular LLM approaches. Choose different presets to experiment with various model sizes and configurations:

### Available Presets

1. **default** - Original tiny model for educational purposes
   - 128 embedding dimensions, 4 layers, 4 attention heads
   - 512 token context window, batch size 16
   - Fast training on CPU, good for learning

2. **tiny** - Very small model for quick testing
   - 64 embedding dimensions, 2 layers, 2 attention heads
   - 256 token context window, batch size 32
   - Fastest training, ideal for prototyping

3. **mistral-medium** - Medium-sized balanced configuration
   - 192 embedding dimensions, 6 layers, 6 attention heads
   - 1024 token context window, batch size 12
   - Inspired by Mistral architecture, balanced performance

4. **mistral-7b** - Larger model with more capacity
   - 256 embedding dimensions, 8 layers, 8 attention heads
   - 2048 token context window, batch size 8
   - Inspired by Mistral 7B architecture, better quality but slower

5. **deepseek** - Large model optimized for reasoning
   - 320 embedding dimensions, 10 layers, 8 attention heads
   - 4096 token context window, batch size 4
   - Inspired by DeepSeek architecture, best for complex tasks

### Using Model Presets

```bash
# List all available presets with detailed information
dotnet run -- --list-presets

# Train with a specific preset
dotnet run -- --model-preset mistral-medium

# Override preset settings with custom values
dotnet run -- --model-preset tiny --block-size 512 --batch-size 16
```

**Note:** Larger presets (mistral-7b, deepseek) require more memory and train significantly slower on CPU. Start with smaller presets (tiny, default) for testing, then scale up as needed.

## Performance Feedback Mode

SmallMind now includes comprehensive performance tracking and benchmarking capabilities inspired by llama.cpp. These features help analyze both capacity (throughput) and user experience (latency) metrics.

### Basic Performance Tracking

Use the `--perf` flag to get detailed performance metrics after generation:

```bash
dotnet run -- --no-train --prompt "Once upon a time" --steps 200 --perf
```

Example output:
```
=== Performance Summary ===
Concurrency: 1
Max tokens: 200
Duration: 45.32s
Requests: total=1 completed=1 failed=0
Throughput: 4.41 tok/s, 0.02 req/s
Tokens: input=4 output=200

Latency (ms):
  TTFT:  p50=892.3, p95=892.3, p99=892.3, mean=892.3
  E2E:   p50=45324.1, p95=45324.1, p99=45324.1, mean=45324.1

Tokens/Request: p50=200.0, p95=200.0, p99=200.0, mean=200.0
```

### JSON Output

For machine-readable metrics, use `--perf-json`:

```bash
dotnet run -- --no-train --prompt "Test" --steps 100 --perf-json
```

Example output:
```json
{
  "concurrency": 1,
  "maxTokensRequested": 100,
  "totalRequests": 1,
  "completedRequests": 1,
  "failedRequests": 0,
  "durationSeconds": 22.45,
  "totalInputTokens": 1,
  "totalOutputTokens": 100,
  "tokensPerSecond": 4.45,
  "requestsPerSecond": 0.045,
  "ttft": {
    "min": 445.2,
    "mean": 445.2,
    "p50": 445.2,
    "p95": 445.2,
    "p99": 445.2,
    "max": 445.2
  },
  "e2eLatency": {
    "min": 22451.3,
    "mean": 22451.3,
    "p50": 22451.3,
    "p95": 22451.3,
    "p99": 22451.3,
    "max": 22451.3
  },
  "tokensPerRequest": {
    "min": 100.0,
    "mean": 100.0,
    "p50": 100.0,
    "p95": 100.0,
    "p99": 100.0,
    "max": 100.0
  }
}
```

### Benchmark Mode

Run sweeps over different `max_tokens` configurations to find optimal settings:

```bash
dotnet run -- --no-train --bench --prompt "Knowledge is"
```

Example output:
```
=== Benchmark Mode ===
Running performance sweeps over different configurations...

Running: concurrency=1, max_tokens=64
  Completed: 4.52 tok/s

Running: concurrency=1, max_tokens=128
  Completed: 4.48 tok/s

Running: concurrency=1, max_tokens=256
  Completed: 4.41 tok/s

Best throughput: 4.52 tok/s (concurrency=1, max_tokens=64)

=== Detailed Results (sorted by throughput) ===

Rank   | Concurrency  | Max Tokens  | Tok/s      | Req/s      | Duration   | Requests   | TTFT p95   | E2E p95   
--------------------------------------------------------------------------------------------------------------------------
1      | 1            | 64          | 4.52       | 0.02       | 14.16s     | 1          | 892.3ms    | 14159.2ms 
2      | 1            | 128         | 4.48       | 0.01       | 28.57s     | 1          | 889.1ms    | 28571.4ms 
3      | 1            | 256         | 4.41       | 0.00       | 58.05s     | 1          | 895.7ms    | 58046.8ms 
```

### Metrics Explained

**Throughput/Capacity Metrics:**
- `concurrency`: Number of in-flight requests (max and average)
- `max_tokens`: Requested maximum output tokens
- `total_requests`: Total requests started
- `completed_requests`: Successfully completed requests
- `failed_requests`: Requests that failed or timed out
- `duration_seconds`: Wall clock time for the run
- `total_output_tokens`: Sum of all generated tokens
- `total_input_tokens`: Sum of all prompt tokens
- `tok_per_sec`: Aggregate throughput (output tokens / duration)
- `req_per_sec`: Request throughput (completed requests / duration)

**Latency/UX Metrics:**
- `TTFT` (Time To First Token): Time from request start to first token emission
- `E2E` (End-to-End): Total time from request start to completion
- `Tokens/Request`: Distribution of output tokens per request
- All latency metrics include: min, mean, p50, p95, p99, max

### Notes

- Performance tracking has minimal overhead when `--perf` is not enabled
- CPU-only execution means true concurrency testing isn't applicable
- Benchmark mode tests different token lengths to find sweet spots
- JSON output is suitable for automated performance testing pipelines

## New Features

### Enhanced Training

SmallMind now supports enhanced training with:
- **Gradient Accumulation**: Simulate larger batch sizes without additional memory
- **Learning Rate Scheduling**: Cosine annealing with warmup for better convergence
- **Validation Loss Tracking**: Monitor overfitting and save best model
- **Best Model Saving**: Automatically save the model with lowest validation loss

Use `--enhanced-training` to enable these features:
```bash
dotnet run -- --enhanced-training --grad-accum 4 --warmup 200 --perf
```

### Question-Answering Mode

Ask questions based on the model's training data using the `--qa` flag:
```bash
dotnet run -- --no-train --qa --prompt "What is the quick brown fox?"
```

The Q&A engine:
- Extracts relevant context from training corpus using keyword matching
- Formats the question with context in a Q&A template
- Generates focused answers using lower temperature sampling
- Cleans up the response to extract just the answer

### Interactive Conversation Mode

Have multi-turn conversations with session context using `--interactive`:
```bash
dotnet run -- --no-train --interactive
```

Features:
- **Persistent Context**: Maintains conversation history across turns
- **Intelligent Truncation**: Automatically manages context window limits
- **Session Commands**:
  - `exit` - Exit the conversation
  - `clear` - Clear conversation history
  - `save` - Save session to JSON file
  - `history` - Show full conversation history
- **Session Persistence**: Save and load conversations for later

Example interaction:
```
You: What is the quick brown fox?
Assistant: The quick brown fox jumps over the lazy dog.
You: What else do you know?
Assistant: [Continues conversation with full context...]
```

## Model Architecture

**SmallMind supports multiple model architectures through presets.** Use `--model-preset` to choose between different configurations inspired by popular LLM approaches (default, tiny, mistral-medium, mistral-7b, deepseek). See the "Model Presets" section above for details.

**Default hyperparameters** (small for CPU training):

- Context length (block size): 512 tokens (configurable, max: 32768, can be overridden further)
- Embedding dimension: 128
- Number of layers: 4
- Number of attention heads: 4
- Dropout: 0.1 (training only)
- Batch size: 16 (configurable, auto-scales with block size)
- Learning rate: 3e-4 (AdamW optimizer)
- Training steps: 2000
- Vocabulary: Character-level (built from data.txt)

**Block Size Configuration:**

The context window (block size) can be configured in three ways:
1. **Default**: 512 tokens - good balance for CPU training
2. **Manual**: Use `--block-size N` to specify any size up to 32768
3. **Auto-configured**: Use `--auto-config` to automatically determine optimal size based on:
   - Available system RAM (primary factor)
   - CPU cores
   - Memory usage estimates for the model architecture

Auto-configuration algorithm:
- 128GB+ available RAM → 32768 tokens (maximum)
- 64GB+ available RAM → 16384 tokens
- 32GB+ available RAM → 8192 tokens
- 16GB+ available RAM → 6144 tokens
- 8GB+ available RAM → 4096 tokens
- 4-8GB available RAM → 2048 tokens
- 2-4GB available RAM → 1024 tokens
- 1-2GB available RAM → 512 tokens (default)
- <1GB available RAM → 256 tokens

**Maximum Block Size Override:**

For users with very high RAM (128GB+), you can override the maximum block size limit:
- Use `--max-block-size N` to set a higher limit (e.g., 32768, 65536)
- Note: Extremely large block sizes require proportionally more memory
- Memory usage grows with O(blockSize²) due to attention mechanism

**Batch Size Configuration:**

The batch size controls how many sequences are processed in parallel:
1. **Default**: 16 - good balance for most systems
2. **Manual**: Use `--batch-size N` to specify custom batch size
3. **Auto-configured**: Use `--auto-config` to automatically scale batch size inversely with block size

Auto-configuration scales batch size based on block size and available memory:
- Larger block sizes → smaller batches (to fit in memory)
- Smaller block sizes → larger batches (for better throughput)

## Project Structure

```
SmallMind/
├── TinyLLM.csproj         # .NET 8 project file (NO dependencies!)
├── Program.cs             # CLI entry point and argument parsing
├── Tokenizer.cs           # Character-level tokenization
├── DataLoader.cs          # Load training data from JSON, XML, CSV, text files
├── Tensor.cs              # Custom tensor with automatic differentiation
├── NeuralNet.cs           # Neural network layers (Linear, Embedding, LayerNorm, etc.)
├── Transformer.cs         # Transformer model (attention, MLP, blocks)
├── Training.cs            # Dataset batching and training loop (with enhanced training)
├── Sampling.cs            # Text generation with temperature and top-k
├── Optimizer.cs           # AdamW optimizer with learning rate scheduling
├── QuestionAnsweringEngine.cs  # Q&A engine for answering questions
├── ConversationSession.cs # Session context management for conversations
├── data.txt               # Training corpus (auto-generated if missing)
├── sample_data/           # Sample data files in multiple formats
│   ├── sample.txt         # Plain text format
│   ├── sample.json        # JSON format
│   ├── sample.xml         # XML format
│   └── sample.csv         # CSV format
├── examples/              # Example code and documentation
│   ├── DataLoaderExample.cs  # Comprehensive data loading examples
│   └── README.md          # Examples documentation
├── Tests/                 # Unit tests
│   ├── TinyLLM.Tests.csproj  # Test project file
│   └── DataLoaderTests.cs    # DataLoader unit tests
├── checkpoints/           # Model checkpoints (created during training)
│   ├── model.json         # Saved model weights
│   └── model_best.json    # Best model (lowest validation loss)
├── sessions/              # Conversation sessions (created when using --interactive)
└── README.md              # This file
```

## Implementation Details

### Pure C# - No Dependencies

This project implements everything from scratch using only C# standard library:

- **Tensor.cs**: Custom tensor class with:
  - N-dimensional array storage
  - Automatic differentiation (gradients)
  - Matrix multiplication
  - Element-wise operations
  - Broadcasting support
  
- **NeuralNet.cs**: Neural network building blocks:
  - Linear (fully connected) layers
  - Embedding layers
  - LayerNorm (layer normalization)
  - Dropout (regularization)
  - Activation functions (GELU, ReLU, Tanh)
  
- **Transformer.cs**: Complete Transformer implementation:
  - Multi-head attention with causal masking
  - Feed-forward MLP
  - Transformer blocks
  - Model forward pass
  
- **Optimizer.cs**: AdamW optimizer with:
  - First moment estimation
  - Second moment estimation
  - Bias correction
  - Weight decay
  - Learning rate scheduling support

- **Training.cs**: Training infrastructure:
  - Mini-batch generation
  - Cross-entropy loss computation
  - Gradient computation
  - Enhanced training with gradient accumulation
  - Learning rate warmup and cosine annealing
  - Validation loss tracking
  - Checkpoint save/load (JSON format)

- **QuestionAnsweringEngine.cs**: Q&A capabilities:
  - Question answering based on training data
  - Keyword-based context retrieval
  - Q&A prompt templates
  - Answer extraction and cleaning

- **ConversationSession.cs**: Session management:
  - Conversation history tracking
  - Context window management
  - Intelligent truncation
  - Session persistence (save/load)

### Performance Notes

Since this is pure C# without optimized linear algebra libraries, performance is **very limited**:
- Training is **extremely slow** (~1-2 hours or more for 2000 steps on modern CPU)
- No GPU acceleration available
- Matrix operations are not vectorized/optimized (but use parallel processing where beneficial)
- Batch size and model size severely impact speed

**Performance Optimizations Added:**
- Parallel processing for matrix multiplication in Tensor operations (for matrices with M >= 4)
- Parallel processing for attention score computation (for batch * heads >= 4)
- Parallel processing for softmax and attention application
- Auto-scaling batch size based on block size and available memory

**This is intentional for educational purposes!** The goal is to understand how everything works, not to train production models.

**For Testing:**
- Reduce `TRAIN_STEPS` to 100-200 for quick tests
- Reduce `BATCH_SIZE` to 4
- Reduce `BLOCK_SIZE` to 32
- Reduce `N_EMBD` to 64
- Reduce `N_LAYER` to 2

**Expected Training Times (approximate):**
- 100 steps with small model: ~2-5 minutes
- 500 steps with small model: ~10-25 minutes
- 2000 steps with default model: ~1-3 hours
- Times vary significantly based on CPU and number of cores

For production use, consider:
- Using TorchSharp or ML.NET with GPU support
- Optimizing matrix operations with SIMD
- Using native libraries like MKL or OpenBLAS

## Troubleshooting

### Training is Too Slow

Training on CPU with pure C# is inherently slow. To speed up:
- Build in Release mode: `dotnet run -c Release`
- Reduce `TRAIN_STEPS` in Program.cs (try 500-1000)
- Reduce `BATCH_SIZE` (try 8 or 4)
- Reduce `BLOCK_SIZE` (try 64)
- Reduce `N_EMBD` (try 64)

### Out of Memory

If training runs out of memory:
- Reduce `BATCH_SIZE` in Program.cs (try 8 or 4)
- Reduce `BLOCK_SIZE` (try 64)
- Reduce `N_EMBD` (try 64)
- Use smaller training data

### Generated Text is Gibberish

This is expected with:
- Very short training (< 500 steps)
- Very small training data (< 500 characters)
- Model hasn't converged yet

Try:
- Training for more steps (5000+)
- Using more training data (10KB+ text)
- Lowering temperature for more conservative generation
- Waiting longer - the model learns slowly without GPU

### Checkpoint Loading Fails

- Checkpoints are saved as JSON in `checkpoints/model.json`
- Make sure the checkpoint was created successfully
- Check that the model architecture hasn't changed
- If needed, delete old checkpoints and retrain

## Educational Notes

### Why Pure C#?

This implementation deliberately avoids external libraries to demonstrate:
1. How automatic differentiation works
2. How backpropagation flows through neural networks
3. How matrix operations compose to form complex models
4. The mathematical foundations of Transformers

It's a learning tool, not a production framework!

### Causal Masking

The attention mechanism uses a causal mask (lower triangular) to prevent tokens from attending to future positions. This ensures the model learns to predict the next token based only on previous context.

### Character-Level Tokenization

Unlike modern LLMs that use subword tokenization (BPE, WordPiece), this implementation uses character-level tokens for simplicity. This means:
- Vocabulary size is very small (~50-100 characters)
- Model must learn to compose characters into words
- Generation is slower (more tokens per word)
- Good for educational purposes and small datasets

### Mini-Batching

Training uses random batches of sequences sampled from the training data. Each sequence is `BLOCK_SIZE` tokens long, and the model learns to predict the next token at each position.

### Temperature Sampling

- Temperature = 1.0: Use raw model probabilities
- Temperature < 1.0: More conservative (sharpen distribution)
- Temperature > 1.0: More random (flatten distribution)

### Top-K Filtering

Only sample from the K most likely tokens. Helps prevent the model from generating very unlikely tokens.

## Extending the Project

### Increase Model Size

Edit `Program.cs` constants:
```csharp
private const int N_EMBD = 256;      // Increase embedding dimension
private const int N_LAYER = 8;       // More layers
private const int N_HEAD = 8;        // More attention heads
private const int BLOCK_SIZE = 256;  // Longer context
```

Note: Larger models train much slower without GPU!

### Optimize Performance

- Use `Span<T>` and `Memory<T>` for better memory efficiency
- Implement SIMD vectorization for matrix operations
- Add parallel processing for batch operations
- Consider using `System.Numerics.Tensors` in .NET 9+

### Add Features

- Validation loss tracking
- Learning rate scheduling
- Gradient clipping
- Early stopping
- BPE tokenization
- Model architecture search

## Comparison: TorchSharp vs Pure C#

| Feature | TorchSharp Version | Pure C# Version |
|---------|-------------------|-----------------|
| Dependencies | TorchSharp NuGet | None |
| Training Speed | Fast (optimized) | Slow (unoptimized) |
| GPU Support | Yes | No |
| Code Complexity | Lower (uses library) | Higher (implements everything) |
| Educational Value | Medium | Very High |
| Production Ready | Yes | No |
| Binary Size | Large (~500MB+) | Small (~200KB) |

## License

This is an educational project. Feel free to use and modify for learning purposes.

## References

- Attention Is All You Need (Vaswani et al., 2017)
- Language Models are Unsupervised Multitask Learners (Radford et al., 2019)
- The Annotated Transformer: http://nlp.seas.harvard.edu/annotated-transformer/
- Understanding Automatic Differentiation in 30 lines of Python

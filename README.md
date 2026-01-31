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
- **Text generation** with temperature sampling and top-k filtering
- **Checkpointing** to save and load model weights (JSON format)
- **Fully self-contained** - only uses System.* namespaces

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
| `--no-train` | (train enabled) | Skip training, only generate |
| `--load` | (auto-detect) | Force load checkpoint before generation |
| `--prompt "text"` | "Once upon a time" | Starting text for generation |
| `--steps N` | 200 | Number of tokens to generate |
| `--temperature T` | 1.0 | Sampling temperature (0.1-2.0, lower=more conservative) |
| `--top-k K` | 0 | Top-k filtering (0=disabled, 40 is typical) |

## Examples

```bash
# Train and generate with default settings
dotnet run

# Generate with custom prompt and temperature
dotnet run -- --no-train --prompt "The wise owl" --steps 300 --temperature 0.8

# Generate with top-k sampling for more focused output
dotnet run -- --no-train --prompt "Knowledge is" --steps 150 --top-k 40 --temperature 1.2
```

## Model Architecture

**Default hyperparameters** (small for CPU training):

- Context length (block size): 128 tokens
- Embedding dimension: 128
- Number of layers: 4
- Number of attention heads: 4
- Dropout: 0.1 (training only)
- Batch size: 16
- Learning rate: 3e-4 (AdamW optimizer)
- Training steps: 2000
- Vocabulary: Character-level (built from data.txt)

## Project Structure

```
SmallMind/
├── TinyLLM.csproj         # .NET 8 project file (NO dependencies!)
├── Program.cs             # CLI entry point and argument parsing
├── Tokenizer.cs           # Character-level tokenization
├── Tensor.cs              # Custom tensor with automatic differentiation
├── NeuralNet.cs           # Neural network layers (Linear, Embedding, LayerNorm, etc.)
├── Transformer.cs         # Transformer model (attention, MLP, blocks)
├── Training.cs            # Dataset batching and training loop
├── Sampling.cs            # Text generation with temperature and top-k
├── Optimizer.cs           # AdamW optimizer
├── data.txt               # Training corpus (auto-generated if missing)
├── checkpoints/           # Model checkpoints (created during training)
│   └── model.json         # Saved model weights
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

- **Training.cs**: Training infrastructure:
  - Mini-batch generation
  - Cross-entropy loss computation
  - Gradient computation
  - Checkpoint save/load (JSON format)

### Performance Notes

Since this is pure C# without optimized linear algebra libraries, performance is **very limited**:
- Training is **extremely slow** (~1-2 hours or more for 2000 steps on modern CPU)
- No GPU acceleration available
- Matrix operations are not vectorized/optimized
- Batch size and model size severely impact speed

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
- Times vary significantly based on CPU

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

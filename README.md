# SmallMind - Tiny Educational LLM in C#

SmallMind is a deliberately tiny, educational language model built in C# (.NET 8) from scratch. It demonstrates the core concepts of decoder-only Transformers (GPT-style) without any LLM runtime libraries.

## Features

- **Decoder-only Transformer architecture** with:
  - Token and positional embeddings
  - Masked multi-head self-attention
  - Feed-forward MLP with GELU activation
  - LayerNorm and residual connections
  - Final linear head to vocabulary
- **Character-level tokenizer** for simplicity
- **Training from scratch** on CPU with next-token prediction
- **Text generation** with temperature sampling and top-k filtering
- **Checkpointing** to save and load model weights
- **Fully self-contained** - only requires TorchSharp NuGet package

## Requirements

- .NET 8 SDK
- TorchSharp (CPU version) - installed via NuGet
- No GPU required (CPU-only, training will be slow)

## Quick Start

### 1. Create Project and Install Dependencies

```bash
# Clone the repository (or create a new directory)
git clone https://github.com/justinamiller/SmallMind.git
cd SmallMind

# Restore NuGet packages
dotnet restore

# Build the project
dotnet build
```

### 2. Train the Model

Train on the default `data.txt` file (created automatically if missing):

```bash
dotnet run
```

This will:
- Load or create `data.txt` with sample text
- Build a character-level vocabulary
- Train a tiny Transformer for 2000 steps (takes ~5-15 minutes on CPU)
- Save checkpoints to `checkpoints/model.pt` every 500 steps
- Generate sample text after training

### 3. Generate Text from a Checkpoint

Skip training and generate text from an existing checkpoint:

```bash
dotnet run -- --no-train --load --prompt "Once upon a time" --steps 200
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

# Train for longer (modify TRAIN_STEPS in Program.cs)
# Then generate
dotnet run
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
├── TinyLLM.csproj         # .NET 8 project file
├── Program.cs             # CLI entry point and argument parsing
├── Tokenizer.cs           # Character-level tokenization
├── Transformer.cs         # Transformer model (attention, MLP, blocks)
├── Training.cs            # Dataset batching and training loop
├── Sampling.cs            # Text generation with temperature and top-k
├── data.txt               # Training corpus (auto-generated if missing)
├── checkpoints/           # Model checkpoints (created during training)
│   └── model.pt
└── README.md              # This file
```

## Troubleshooting

### TorchSharp Native Library Issues

If you see errors about missing native libraries:

**Windows:**
```
Install Visual C++ Redistributable:
https://aka.ms/vs/17/release/vc_redist.x64.exe
```

**Linux:**
```bash
# Install required dependencies
sudo apt-get update
sudo apt-get install -y libomp5
```

**macOS:**
```bash
# TorchSharp CPU should work out of the box on macOS
# If you encounter issues, ensure you have the latest .NET SDK
brew install libomp
```

### Out of Memory

If training runs out of memory:
- Reduce `BATCH_SIZE` in Program.cs (try 8 or 4)
- Reduce `BLOCK_SIZE` (try 64)
- Reduce `N_EMBD` (try 64)

### Training is Too Slow

Training on CPU is inherently slow. To speed up:
- Reduce `TRAIN_STEPS` (try 500-1000)
- Reduce `BATCH_SIZE` if you want faster iterations (but slower convergence)
- Use smaller `data.txt` (but results will be worse)
- For production use, consider using GPU version of TorchSharp (`TorchSharp-cuda-windows` or similar)

### Generated Text is Gibberish

This is expected with:
- Very short training (< 500 steps)
- Very small training data (< 500 characters)
- Model hasn't converged yet

Try:
- Training for more steps (5000+)
- Using more training data (10KB+ text)
- Lowering temperature for more conservative generation

## Educational Notes

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

### Add Validation Loss
Track loss on a held-out validation set to monitor overfitting.

### Implement BPE Tokenization
Replace character-level tokenizer with byte-pair encoding for better efficiency.

### Add Gradient Clipping
Prevent exploding gradients during training.

### Multi-GPU Training
Switch to `TorchSharp-cuda` package and implement data parallelism.

## License

This is an educational project. Feel free to use and modify for learning purposes.

## References

- Attention Is All You Need (Vaswani et al., 2017)
- Language Models are Unsupervised Multitask Learners (Radford et al., 2019)
- TorchSharp: https://github.com/dotnet/TorchSharp

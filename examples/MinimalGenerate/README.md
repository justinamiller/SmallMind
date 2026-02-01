# Minimal Generate Example

Demonstrates using SmallMind as a library for text generation.

## Usage

### With Pre-Trained Checkpoint
```bash
dotnet run <path-to-checkpoint> "Your prompt"
```

### Demo Mode (Creates Tiny Model)
```bash
dotnet run
```

## Code Example

```csharp
// Load checkpoint
ICheckpointStore store = new BinaryCheckpointStore();
var checkpoint = await store.LoadAsync("model.smnd");
var model = CheckpointExtensions.FromCheckpoint(checkpoint);

// Generate text
var tokenizer = new CharTokenizer();
var generator = new Sampling(model, tokenizer, model.BlockSize);
var text = generator.Generate("Hello", maxNewTokens: 50, temperature: 0.8);
```

## Training a Model

Use SmallMind.Console to train:
```bash
cd ../../src/SmallMind.Console
dotnet run -- train --data ../../data.txt --steps 1000
```

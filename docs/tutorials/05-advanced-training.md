# Tutorial 5: Advanced Training - Optimization Techniques

Learn advanced training techniques including learning rate schedules, gradient clipping, and optimization strategies for SmallMind models.

## Overview

This tutorial covers:
- Learning rate scheduling strategies
- Gradient clipping for training stability
- Advanced optimizer configuration
- Training best practices

## Learning Rate Schedules

Learning rate schedules help improve training convergence and final model quality.

### Constant Learning Rate

Simplest approach - useful for baseline:

```csharp
using SmallMind.Core.Core;

var scheduler = new ConstantLR(lr: 0.001f);

// During training
for (int step = 0; step < totalSteps; step++)
{
    float lr = scheduler.GetLearningRate(step);
    optimizer.SetLearningRate(lr);
    
    // Training step...
    optimizer.Step();
}
```

### Warmup Learning Rate

Gradually increase LR from 0 to target - prevents early training instability:

```csharp
var scheduler = new WarmupLR(
    baseLr: 0.001f,
    warmupSteps: 1000  // Warmup for first 1000 steps
);

// LR increases linearly: 0 → 0.001 over 1000 steps
// Then stays at 0.001
```

### Cosine Annealing (Recommended)

Smoothly decay learning rate following a cosine curve - excellent for most use cases:

```csharp
var scheduler = new CosineAnnealingLR(
    baseLr: 0.001f,        // Starting LR after warmup
    minLr: 0.00001f,       // Minimum LR at end
    totalSteps: 10000,     // Total training steps
    warmupSteps: 1000      // Optional warmup period
);

// LR follows: warmup → cosine decay from 0.001 to 0.00001
```

### Step Decay

Reduce LR by a factor at regular intervals:

```csharp
var scheduler = new StepDecayLR(
    baseLr: 0.001f,
    decayFactor: 0.5f,     // Halve LR each decay
    decaySteps: 2000,      // Decay every 2000 steps
    warmupSteps: 500
);

// LR: warmup → 0.001 → 0.0005 (step 2000) → 0.00025 (step 4000) → ...
```

### Exponential Decay

Smooth exponential decrease:

```csharp
var scheduler = new ExponentialDecayLR(
    baseLr: 0.001f,
    decayRate: 0.96f,      // 4% decay per step
    warmupSteps: 500
);

// LR decays exponentially after warmup
```

### One-Cycle Policy

Increase then decrease - popular for fast convergence:

```csharp
var scheduler = new OneCycleLR(
    maxLr: 0.003f,         // Peak LR
    minLr: 0.0001f,        // Starting and ending LR
    totalSteps: 10000,
    pctStart: 0.3f         // 30% of steps increasing, 70% decreasing
);

// LR: 0.0001 → 0.003 (30% of steps) → 0.0001 (70% of steps)
```

## Gradient Clipping

Prevents exploding gradients during training.

### Clip by Value

Limit individual gradient values:

```csharp
var optimizer = new AdamW(
    parameters: model.Parameters,
    lr: 0.001f,
    gradClipValue: 1.0f  // Clip gradients to [-1.0, 1.0]
);

// Gradients are automatically clipped during optimizer.Step()
```

### Clip by Norm

Limit global gradient norm (recommended for transformers):

```csharp
var optimizer = new AdamW(
    parameters: model.Parameters,
    lr: 0.001f
);

// Manually clip by norm before optimizer step
optimizer.ClipGradientsByNorm(maxNorm: 1.0f);
optimizer.Step();
```

## Complete Training Example

### Basic Training with Schedules

```csharp
using SmallMind.Core;
using SmallMind.Core.Core;
using SmallMind.Transformers;
using SmallMind.Tokenizers;
using SmallMind.Runtime;
using System;
using System.Diagnostics;

class AdvancedTrainingExample
{
    static async Task Main()
    {
        // Load training data
        string trainingText = File.ReadAllText("data.txt");
        
        // Create tokenizer
        var tokenizer = new CharTokenizer(trainingText);
        var data = tokenizer.Encode(trainingText);
        
        // Build model using builder pattern
        var model = TransformerModelBuilder.Create()
            .UseSmallConfig(vocabSize: tokenizer.VocabSize)
            .WithBlockSize(128)
            .WithDropout(0.1)
            .Build();
        
        // Training configuration
        int totalSteps = 10000;
        int warmupSteps = 1000;
        float baseLearningRate = 0.001f;
        float minLearningRate = 0.00001f;
        
        // Create learning rate scheduler
        var scheduler = new CosineAnnealingLR(
            baseLr: baseLearningRate,
            minLr: minLearningRate,
            totalSteps: totalSteps,
            warmupSteps: warmupSteps
        );
        
        // Create optimizer with gradient clipping
        var optimizer = new AdamW(
            parameters: model.Parameters,
            lr: baseLearningRate,
            beta1: 0.9f,
            beta2: 0.999f,
            weightDecay: 0.01f,
            gradClipValue: 1.0f  // Enable gradient clipping
        );
        
        // Training loop
        int batchSize = 32;
        int blockSize = model.BlockSize;
        var random = new Random(42);
        
        Console.WriteLine("Training with advanced optimization:");
        Console.WriteLine($"  Total steps: {totalSteps}");
        Console.WriteLine($"  Warmup steps: {warmupSteps}");
        Console.WriteLine($"  Base LR: {baseLearningRate}");
        Console.WriteLine($"  Min LR: {minLearningRate}");
        Console.WriteLine($"  Gradient clipping: 1.0");
        Console.WriteLine();
        
        var stopwatch = Stopwatch.StartNew();
        
        for (int step = 0; step < totalSteps; step++)
        {
            // Update learning rate
            float currentLr = scheduler.GetLearningRate(step);
            optimizer.SetLearningRate(currentLr);
            
            // Get batch
            int startIdx = random.Next(0, data.Count - blockSize - 1);
            var inputs = data.GetRange(startIdx, blockSize);
            var targets = data.GetRange(startIdx + 1, blockSize);
            
            // Forward pass
            model.Train();
            var logits = model.Forward(inputs);
            
            // Compute loss
            float loss = ComputeCrossEntropyLoss(logits, targets);
            
            // Backward pass
            model.Backward(logits, targets);
            
            // Optional: Additional gradient clipping by norm
            // optimizer.ClipGradientsByNorm(1.0f);
            
            // Update weights
            optimizer.Step();
            optimizer.ZeroGrad();
            
            // Log progress
            if ((step + 1) % 100 == 0)
            {
                Console.WriteLine($"Step {step + 1}/{totalSteps} | " +
                                $"Loss: {loss:F4} | " +
                                $"LR: {currentLr:F6} | " +
                                $"Time: {stopwatch.Elapsed.TotalSeconds:F1}s");
            }
            
            // Save checkpoint periodically
            if ((step + 1) % 1000 == 0)
            {
                await SaveCheckpoint(model, tokenizer, $"checkpoint_step_{step + 1}.smnd");
            }
        }
        
        stopwatch.Stop();
        Console.WriteLine($"\nTraining completed in {stopwatch.Elapsed.TotalMinutes:F1} minutes");
        
        // Save final model
        await SaveCheckpoint(model, tokenizer, "final_model.smnd");
        
        // Test generation
        TestGeneration(model, tokenizer);
    }
    
    static float ComputeCrossEntropyLoss(Tensor logits, List<int> targets)
    {
        int seqLen = targets.Count;
        int vocabSize = logits.Shape[2];
        float totalLoss = 0.0f;
        
        for (int t = 0; t < seqLen; t++)
        {
            float maxLogit = float.NegativeInfinity;
            for (int v = 0; v < vocabSize; v++)
            {
                float val = logits.Data[t * vocabSize + v];
                if (val > maxLogit) maxLogit = val;
            }
            
            float sumExp = 0.0f;
            for (int v = 0; v < vocabSize; v++)
            {
                sumExp += MathF.Exp(logits.Data[t * vocabSize + v] - maxLogit);
            }
            
            int targetClass = targets[t];
            float logProb = logits.Data[t * vocabSize + targetClass] - maxLogit - MathF.Log(sumExp);
            totalLoss -= logProb;
        }
        
        return totalLoss / seqLen;
    }
    
    static async Task SaveCheckpoint(TransformerModel model, ITokenizer tokenizer, string path)
    {
        var checkpoint = CheckpointExtensions.ToCheckpoint(model);
        var store = new BinaryCheckpointStore();
        await store.SaveAsync(checkpoint, path);
        Console.WriteLine($"Saved checkpoint: {path}");
    }
    
    static void TestGeneration(TransformerModel model, ITokenizer tokenizer)
    {
        model.Eval();
        var generator = new Sampling(model, tokenizer, model.BlockSize);
        
        var text = generator.Generate(
            prompt: "The",
            maxNewTokens: 100,
            temperature: 0.8,
            topK: 40,
            seed: 42
        );
        
        Console.WriteLine("\n=== Sample Generation ===");
        Console.WriteLine(text);
    }
}
```

## Training with Validation

Track validation loss to prevent overfitting:

```csharp
public class TrainerWithValidation
{
    private readonly TransformerModel _model;
    private readonly AdamW _optimizer;
    private readonly ILearningRateScheduler _scheduler;
    private readonly List<int> _trainData;
    private readonly List<int> _valData;
    
    private float _bestValLoss = float.MaxValue;
    
    public async Task Train(int totalSteps, string checkpointPath)
    {
        for (int step = 0; step < totalSteps; step++)
        {
            // Training step
            float trainLoss = TrainStep(step);
            
            // Validate every N steps
            if ((step + 1) % 100 == 0)
            {
                float valLoss = ValidateStep();
                Console.WriteLine($"Step {step + 1} | Train: {trainLoss:F4} | Val: {valLoss:F4}");
                
                // Save best model
                if (valLoss < _bestValLoss)
                {
                    _bestValLoss = valLoss;
                    await SaveCheckpoint($"{checkpointPath}.best");
                    Console.WriteLine($"  New best validation loss: {valLoss:F4}");
                }
            }
        }
    }
    
    private float TrainStep(int step)
    {
        _model.Train();
        
        // Update learning rate
        float lr = _scheduler.GetLearningRate(step);
        _optimizer.SetLearningRate(lr);
        
        // Get batch, forward, backward, optimize
        // ... (training logic)
        
        return loss;
    }
    
    private float ValidateStep()
    {
        _model.Eval();
        
        // Compute validation loss without gradients
        // ... (validation logic)
        
        return loss;
    }
}
```

## Optimizer Configuration

### Conservative (Stable Training)

```csharp
var optimizer = new AdamW(
    parameters: model.Parameters,
    lr: 0.0003f,          // Lower LR
    beta1: 0.9f,
    beta2: 0.999f,
    weightDecay: 0.1f,    // Higher weight decay
    gradClipValue: 0.5f   // Aggressive clipping
);
```

### Aggressive (Fast Convergence)

```csharp
var optimizer = new AdamW(
    parameters: model.Parameters,
    lr: 0.003f,           // Higher LR
    beta1: 0.9f,
    beta2: 0.98f,         // Lower beta2
    weightDecay: 0.01f,   // Lower weight decay
    gradClipValue: 2.0f   // Gentler clipping
);

var scheduler = new OneCycleLR(
    maxLr: 0.01f,         // High peak LR
    minLr: 0.0001f,
    totalSteps: totalSteps
);
```

## Best Practices

### 1. Start with Warmup

Always use warmup for stable training:
```csharp
var scheduler = new CosineAnnealingLR(
    baseLr: 0.001f,
    minLr: 0.00001f,
    totalSteps: totalSteps,
    warmupSteps: totalSteps / 10  // 10% warmup is typical
);
```

### 2. Monitor Gradients

Check for exploding/vanishing gradients:
```csharp
float maxGrad = 0.0f;
foreach (var param in model.Parameters)
{
    if (param.Grad != null)
    {
        for (int i = 0; i < param.Size; i++)
        {
            maxGrad = Math.Max(maxGrad, Math.Abs(param.Grad[i]));
        }
    }
}

if (maxGrad > 10.0f)
{
    Console.WriteLine($"WARNING: Large gradients detected: {maxGrad}");
}
```

### 3. Use Validation Loss

Save model based on validation performance:
```csharp
if (valLoss < bestValLoss)
{
    bestValLoss = valLoss;
    await SaveCheckpoint("model.best.smnd");
}
```

### 4. Experiment with Schedules

Try different schedules for your task:
- **Cosine Annealing**: General purpose, works well for most cases
- **One-Cycle**: Fast convergence for time-limited training
- **Step Decay**: When you know roughly when to reduce LR
- **Warmup**: Always use for initial stability

## Debugging Training Issues

### Loss Not Decreasing

1. Check learning rate is not too small
2. Verify gradients are non-zero
3. Try removing weight decay temporarily
4. Increase model capacity

### Loss Exploding

1. Reduce learning rate
2. Enable/increase gradient clipping
3. Reduce batch size
4. Check for NaN values in data

### Unstable Training

1. Add/increase warmup steps
2. Use gradient clipping
3. Reduce learning rate
4. Increase weight decay

## Next Steps

- See [examples/MinimalGenerate](../../examples/MinimalGenerate/) for usage examples
- Check [samples/](../../samples/) for complete applications
- Read [PERFORMANCE_OPTIMIZATIONS.md](../PERFORMANCE_OPTIMIZATIONS.md) for CPU optimization tips

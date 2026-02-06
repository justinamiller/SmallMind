# Training Metrics Guide

## Overview

SmallMind now includes comprehensive training metrics to help you monitor and improve model prediction quality. This guide explains how to use these metrics and what they tell you about your model's performance.

## Available Metrics

### 1. **Training Loss**
- **What it measures**: How well the model predicts the training data
- **Range**: 0 to ∞ (lower is better)
- **Interpretation**: 
  - Decreasing loss = model is learning
  - Stable loss = model has converged
  - Increasing loss = training instability or overfitting
- **Example**: Loss of 2.5 means on average, the model assigns ~exp(-2.5) ≈ 8% probability to correct tokens

### 2. **Validation Loss**
- **What it measures**: How well the model predicts held-out validation data
- **Range**: 0 to ∞ (lower is better)
- **Interpretation**:
  - Lower than training loss = underfitting (model can learn more)
  - Similar to training loss = good generalization
  - Higher than training loss = overfitting (model memorizing training data)
- **Use case**: Track validation loss to prevent overfitting and select best checkpoints

### 3. **Perplexity**
- **What it measures**: How "surprised" the model is by the next token
- **Formula**: `perplexity = exp(loss)`
- **Range**: 1 to ∞ (lower is better)
- **Interpretation**:
  - Perplexity of 1 = perfect prediction
  - Perplexity of 100 = model is choosing among ~100 equally likely tokens
  - Perplexity of 1000 = model is very uncertain
- **Industry benchmarks**:
  - State-of-the-art LLMs: 10-30 perplexity on common benchmarks
  - Decent small models: 50-200 perplexity
  - Random baseline: ~vocabulary_size perplexity

### 4. **Token Accuracy**
- **What it measures**: Percentage of tokens where the model's top-1 prediction is correct
- **Range**: 0 to 1 (higher is better)
- **Interpretation**:
  - 0.5 = model correctly predicts 50% of tokens
  - Higher accuracy = better predictions
  - Note: This is stricter than perplexity (requires exact match, not just probability)
- **Example**: 65% accuracy means the model's most confident prediction is correct 65% of the time

### 5. **Gradient Health**
Monitors gradient flow during backpropagation to detect training issues:

#### Mean Gradient Norm
- **What it measures**: Average magnitude of gradients across all parameters
- **Healthy range**: 0.001 to 1.0 (depends on model architecture)
- **Interpretation**:
  - Too small (< 1e-8) = vanishing gradients, model won't learn
  - Too large (> 100) = exploding gradients, training unstable
  - Stable values = healthy gradient flow

#### NaN/Inf Count
- **What it measures**: Number of corrupted gradient values
- **Healthy value**: 0
- **Interpretation**:
  - Any NaN/Inf = critical issue, training will fail
  - Causes: numerical overflow, division by zero, or unstable operations
  - Action: Reduce learning rate, use gradient clipping, or check model architecture

## How to Use

### Basic Usage (Automatic)

Metrics are automatically tracked when using `TrainEnhanced()`:

```csharp
var training = new Training(model, tokenizer, trainingText, 
    blockSize: 32, batchSize: 4, seed: 42);

// Metrics are automatically tracked during training
training.TrainEnhanced(
    steps: 1000,
    learningRate: 0.001,
    logEvery: 100,
    saveEvery: 500,
    checkpointDir: "./checkpoints",
    showPerf: true,
    valEvery: 100,  // Validate every 100 steps
    valBatches: 10  // Use 10 batches for validation
);

// After training, view comprehensive metrics report
Console.WriteLine(training.Metrics.GetReport());
```

### Accessing Metrics Programmatically

```csharp
// Get current values
float? currentLoss = training.Metrics.GetCurrentTrainingLoss();
float? currentPerplexity = training.Metrics.GetCurrentPerplexity();
float? currentAccuracy = training.Metrics.GetCurrentTokenAccuracy();

// Get best values
float? bestValLoss = training.Metrics.GetBestValidationLoss();
float? bestPerplexity = training.Metrics.GetBestPerplexity();

// Check if training is progressing
bool progressing = training.Metrics.IsTrainingProgressing(lookbackSteps: 10);
if (!progressing)
{
    Console.WriteLine("Warning: Training may have stalled!");
}

// Get full statistics summary
var summary = training.Metrics.GetSummary();
Console.WriteLine($"Average training loss: {summary.TrainingLossStats?.Mean:F4}");
Console.WriteLine($"Best perplexity: {summary.PerplexityStats?.Min:F2}");
Console.WriteLine($"Token accuracy: {summary.TokenAccuracyStats?.Mean * 100:F2}%");
```

## Output Example

During training, you'll see output like:

```
Starting enhanced training for 1000 steps...
Batch size: 4, Block size: 32, Base learning rate: 0.001
Validation every 100 steps with 10 batches
Model quality metrics tracking: ENABLED

Step 100/1000, Loss: 3.2156, LR: 0.000950
Validation - Loss: 3.3421, Perplexity: 28.29, Accuracy: 42.50%

Step 200/1000, Loss: 2.8934, LR: 0.000900
Validation - Loss: 2.9512, Perplexity: 19.12, Accuracy: 48.75%

...

Training completed.

=== Training Metrics Report ===

Training Loss:
  Current: 2.1234
  Average: 2.8567
  Best:    1.9876

Validation Loss:
  Current: 2.2341
  Average: 2.9123
  Best:    2.1890

Perplexity (lower is better):
  Current: 9.34
  Average: 18.47
  Best:    8.92

Token Prediction Accuracy:
  Current: 58.25%
  Average: 51.33%
  Best:    60.12%

Gradient Health:
  Average Norm: 0.012345
  Max Norm:     0.234567
  Issues:       0 NaN, 0 Inf

Total Steps: 1000 training, 10 validation
```

## Interpreting Results

### Good Training Progress
- Training loss steadily decreasing
- Validation loss tracking training loss (not increasing)
- Perplexity decreasing
- Token accuracy increasing
- No gradient issues (0 NaN/Inf, stable norms)

### Warning Signs
- **Overfitting**: Validation loss increasing while training loss decreases
- **Underfitting**: Both losses plateau at high values
- **Vanishing gradients**: Mean gradient norm < 1e-8
- **Exploding gradients**: Max gradient norm > 100 or NaN/Inf present
- **Stalled training**: Loss not decreasing for many steps

## Recommendations for Improving Predictions

Based on your metrics, here are actions you can take:

### High Perplexity / Low Accuracy
- **Increase model capacity**: More layers, larger embedding dimension, more heads
- **Train longer**: More steps until loss plateaus
- **Better data**: More diverse, higher-quality training text
- **Tune hyperparameters**: Adjust learning rate, batch size

### Overfitting (Val Loss > Train Loss)
- **Add regularization**: Increase dropout rate
- **More training data**: Collect additional examples
- **Early stopping**: Use best validation checkpoint
- **Data augmentation**: If applicable to your domain

### Gradient Issues
- **Vanishing gradients**: 
  - Increase learning rate
  - Use residual connections (if customizing architecture)
  - Check activation functions
- **Exploding gradients**:
  - Decrease learning rate
  - Add gradient clipping (set max norm)
  - Reduce batch size

### Slow Convergence
- **Adjust learning rate**: Try warmup + cosine annealing
- **Batch size**: Experiment with larger batches (with learning rate scaling)
- **Optimizer**: AdamW generally works well, already used in SmallMind
- **Gradient accumulation**: Effective batch size without memory overhead

## Advanced: Custom Metrics

You can compute metrics manually for specific use cases:

```csharp
using SmallMind.Runtime.Metrics;

// Compute token accuracy for a specific batch
var logits = model.Forward(inputBatch);
float accuracy = MetricsComputer.ComputeTokenAccuracy(logits, targets);

// Check gradient health
var (meanNorm, maxNorm, minNorm, nanCount, infCount) = 
    MetricsComputer.ComputeGradientStats(model.Parameters);

bool healthy = MetricsComputer.AreGradientsHealthy(
    meanNorm, maxNorm, nanCount, infCount, 
    maxAllowedNorm: 100f);

if (!healthy)
{
    Console.WriteLine("Unhealthy gradients detected!");
}

// Compute perplexity from any loss value
float perplexity = MetricsComputer.ComputePerplexity(lossValue);
```

## Comparison with Other Models

To compare your SmallMind model with others:

1. **Track perplexity** on a standard test set
2. **Measure token accuracy** on held-out data
3. **Compare training efficiency**: Steps to reach target perplexity
4. **Evaluate generation quality**: Subjectively assess generated text

Example comparison:
```
Model A: Perplexity 45, Accuracy 52%, 500 steps to converge
Model B: Perplexity 38, Accuracy 58%, 800 steps to converge
→ Model B has better predictions but takes longer to train
```

## Summary

The metrics system provides:
- ✅ **Quantitative measures** of prediction quality (perplexity, accuracy)
- ✅ **Training health monitoring** (gradient norms, NaN/Inf detection)
- ✅ **Progress tracking** to know when to stop training
- ✅ **Debugging tools** to diagnose training issues
- ✅ **Comparison baseline** to measure improvements

Use these metrics to:
1. Monitor training in real-time
2. Detect and fix issues early
3. Select the best model checkpoint
4. Compare different hyperparameters or architectures
5. Demonstrate improvements objectively

For questions or issues, see the [SmallMind documentation](../README.md) or file an issue on GitHub.

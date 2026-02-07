# Training Metrics Example

This example demonstrates the comprehensive training metrics system added to SmallMind. It shows how to:

1. **Track prediction quality** with perplexity and token accuracy
2. **Monitor gradient health** to prevent training failures
3. **Analyze training progress** programmatically
4. **Get actionable recommendations** for improving model performance

## What You'll Learn

- How perplexity measures model uncertainty (lower = better)
- How token accuracy shows prediction correctness
- How to detect gradient issues (vanishing, exploding, NaN/Inf)
- How to identify overfitting vs underfitting
- How to measure if changes actually improve your model

## Running the Example

```bash
cd examples/MetricsExample
dotnet run
```

## Expected Output

The example trains a small transformer on Shakespeare text and displays:

1. **Real-time metrics** during training:
   - Training loss
   - Validation loss and perplexity
   - Token prediction accuracy

2. **Comprehensive report** after training:
   - Statistical summaries (mean, min, max, stddev)
   - Best observed values
   - Gradient health status

3. **Automated analysis**:
   - Is training progressing?
   - Are gradients healthy?
   - Overfitting or underfitting detection

4. **Actionable recommendations**:
   - How to improve model performance
   - Hyperparameter suggestions
   - Architecture guidance

## Key Metrics Explained

### Perplexity
- **What**: exp(loss) - measures model "surprise"
- **Range**: 1 to ∞ (lower is better)
- **Example**: Perplexity of 50 means model is choosing among ~50 equally likely tokens

### Token Accuracy
- **What**: % of tokens where top-1 prediction is correct
- **Range**: 0 to 1 (higher is better)
- **Example**: 65% accuracy = model's most confident prediction is right 65% of the time

### Gradient Health
- **What**: Statistics on gradient magnitudes
- **Checks**: NaN/Inf detection, vanishing (<1e-8), exploding (>100)
- **Why**: Prevents training failures and diagnoses learning issues

## Measuring Improvements

To objectively measure if changes improve prediction:

```csharp
// Baseline
training.TrainEnhanced(...);
var baselinePerplexity = training.Metrics.GetBestPerplexity();
// e.g., 82.5

// After change (e.g., increasing model size)
training2.TrainEnhanced(...);
var newPerplexity = training2.Metrics.GetBestPerplexity();
// e.g., 65.3

// Improvement: 82.5 → 65.3 (20.8% reduction)
```

Track multiple metrics:
- **Perplexity**: Overall prediction quality
- **Token Accuracy**: Exact prediction correctness
- **Training Speed**: Steps to reach target perplexity
- **Overfitting Gap**: Validation loss - training loss

## See Also

- [Training Metrics Guide](../../docs/TRAINING_METRICS_GUIDE.md) - Detailed documentation
- [GoldenPath Example](../GoldenPath/) - Basic training example
- [Main README](../../README.md) - SmallMind overview

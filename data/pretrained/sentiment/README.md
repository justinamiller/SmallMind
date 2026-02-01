# Sentiment Analysis Pack

## Overview

**Pack ID**: `sm.pretrained.sentiment.v1`  
**Domain**: General Sentiment Analysis  
**Task Type**: Classification (Positive, Negative, Neutral)  
**Status**: Stable  
**License**: MIT

This pack provides production-ready sentiment analysis data for validating SmallMind's classification capabilities on general-purpose product reviews and user feedback.

## Intended Use

- **Classification**: Train or fine-tune sentiment analysis models
- **Evaluation**: Benchmark model accuracy on sentiment tasks
- **Regression Testing**: Ensure model behavior remains consistent across versions
- **Performance Benchmarking**: Measure inference throughput and latency

## Not Intended For

This is **not** your proprietary training data. It provides:
- A standardized format for your own sentiment data
- A validation harness for testing pipelines
- Baseline performance metrics

For production sentiment analysis, collect domain-specific labeled data relevant to your use case.

## Data Statistics

- **Total Samples**: 30
- **Label Distribution**:
  - Positive: 11 samples (37%)
  - Negative: 10 samples (33%)
  - Neutral: 9 samples (30%)
- **Average Text Length**: ~60 characters
- **Domain**: Product reviews, customer feedback

## Pack Structure

```
sentiment/
├── manifest.json          # Pack metadata and configuration
├── README.md             # This file
├── task/
│   └── inputs.jsonl      # Input texts with ground truth labels
└── eval/
    ├── labels.jsonl      # Ground truth for evaluation
    └── scoring.md        # Evaluation methodology and thresholds
```

## Data Format

### Task Inputs (`task/inputs.jsonl`)

Each line is a JSON object:
```json
{
  "id": "sentiment_001",
  "task": "sentiment",
  "text": "This product is amazing! I absolutely love it.",
  "label": "positive"
}
```

**Fields**:
- `id`: Unique identifier for the sample
- `task`: Task type (`sentiment`)
- `text`: Input text to classify
- `label`: Ground truth sentiment (`positive`, `negative`, `neutral`)

### Evaluation Labels (`eval/labels.jsonl`)

Same format as task inputs. Used for computing accuracy metrics.

## Usage Examples

### CLI Usage

```bash
# Run sentiment analysis on the pack (deterministic mode)
smallmind pack run sentiment --deterministic --out artifacts/sentiment.md

# Score results against ground truth
smallmind pack score sentiment --run artifacts/sentiment.run.json
```

### API Usage (C#)

```csharp
using SmallMind.Runtime.PretrainedModels;

// Load the pack
var packPath = "data/pretrained/sentiment";
var pack = PretrainedPack.Load(packPath);

Console.WriteLine($"Loaded pack: {pack.Manifest.Id}");
Console.WriteLine($"Total samples: {pack.Samples.Count}");

// Run inference on all samples
foreach (var sample in pack.Samples)
{
    var prediction = model.AnalyzeSentiment(sample.Text);
    Console.WriteLine($"[{sample.Id}] True: {sample.Label}, Predicted: {prediction}");
}

// Compute accuracy
var scorer = new PackScorer();
var results = scorer.Score(pack, predictions);
Console.WriteLine($"Accuracy: {results.Accuracy:F2}%");
```

### Loading for Training

```csharp
using SmallMind.Runtime.PretrainedModels;

// Load samples from pack
var packPath = "data/pretrained/sentiment";
var samples = DatasetLoader.LoadFromJsonl(
    Path.Combine(packPath, "task/inputs.jsonl")
);

// Split for training/validation
var (train, val) = DatasetLoader.SplitDataset(
    samples, 
    trainRatio: 0.8, 
    seed: 42
);

Console.WriteLine($"Training samples: {train.Count}");
Console.WriteLine($"Validation samples: {val.Count}");

// Use with your training pipeline
// ...
```

## Evaluation Metrics

See `eval/scoring.md` for detailed evaluation methodology.

**Key Metrics**:
- **Accuracy**: Percentage of correct predictions
- **Per-Class Precision/Recall**: Performance on each sentiment
- **Confusion Matrix**: Common misclassification patterns

**Acceptance Thresholds**:
- Untrained baseline: ~33% (random chance)
- Minimum viable: ≥60%
- Production ready: ≥75%
- High quality: ≥85%

## Deterministic Execution

To ensure reproducible results:
- Set `deterministic: true` in pack manifest
- Use temperature = 0.0
- Fix random seeds
- Disable sampling randomness

Deterministic mode guarantees:
- Same input → same output
- Reproducible across runs
- Reproducible across machines

## Swapping In Your Data

To use your own sentiment data with this pack structure:

1. **Convert to JSONL**: Transform your data to match the format
2. **Update Manifest**: Modify `manifest.json` with your metadata
3. **Add Evaluation Data**: Include ground truth in `eval/labels.jsonl`
4. **Update Scoring**: Adjust thresholds in `eval/scoring.md` for your domain
5. **Maintain Structure**: Keep the folder hierarchy

This ensures your custom data works with existing SmallMind tools and workflows.

## Source and Licensing

- **Origin**: Synthetic data created for demonstration
- **License**: MIT
- **Redistributable**: Yes
- **Attribution**: None required

This pack is provided as a starter asset. It is not a replacement for real-world, domain-specific sentiment data.

## Related Packs

- **`finance`**: Finance-specific sentiment analysis with domain terminology
- **`classification`**: Multi-class topic classification

## Support

This is an open-source pack with no paid support. For issues or contributions, see the main SmallMind repository.

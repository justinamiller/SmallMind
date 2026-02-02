# Topic Classification Pack

## Overview

**Pack ID**: `sm.pretrained.classification.v1`  
**Domain**: Topic Classification  
**Task Type**: Multi-class Classification (Technology, Sports, Politics, Entertainment)  
**Status**: Stable  
**License**: MIT

This pack provides production-ready topic classification data for validating SmallMind's text classification capabilities on news content and article categorization.

## Intended Use

- **Classification**: Train or fine-tune topic classification models
- **Evaluation**: Benchmark model accuracy on multi-class tasks
- **Regression Testing**: Validate consistent behavior across model versions
- **Performance Benchmarking**: Measure throughput and latency

## Not Intended For

This is **not** your proprietary categorization data. It provides:
- A standardized format for your own classification tasks
- A validation harness for testing pipelines
- Baseline performance metrics

For production classification, collect domain-specific labeled data with your actual categories and content.

## Data Statistics

- **Total Samples**: 30
- **Categories**: 4 (Technology, Sports, Politics, Entertainment)
- **Label Distribution**:
  - Technology: 9 samples (30%)
  - Sports: 7 samples (23%)
  - Politics: 7 samples (23%)
  - Entertainment: 7 samples (23%)
- **Average Text Length**: ~70 characters
- **Domain**: News articles, content categorization

## Pack Structure

```
classification/
├── manifest.json          # Pack metadata and configuration
├── README.md             # This file
├── task/
│   ├── inputs.jsonl      # Input texts with ground truth categories
│   └── categories.json   # Category definitions and examples
└── eval/
    ├── labels.jsonl      # Ground truth for evaluation
    └── scoring.md        # Evaluation methodology and thresholds
```

## Data Format

### Task Inputs (`task/inputs.jsonl`)

Each line is a JSON object:
```json
{
  "id": "classification_001",
  "task": "classification",
  "text": "New smartphone features groundbreaking AI capabilities and 5G connectivity.",
  "label": "Technology"
}
```

**Fields**:
- `id`: Unique identifier for the sample
- `task`: Task type (`classification`)
- `text`: Input text to classify
- `label`: Ground truth category

### Categories (`task/categories.json`)

Defines all categories with descriptions and examples:
```json
{
  "categories": [
    {
      "id": "tech",
      "name": "Technology",
      "description": "Technology news, software, hardware, AI, cybersecurity",
      "examples": ["New smartphone...", "Software update..."]
    }
  ]
}
```

## Usage Examples

### CLI Usage

```bash
# Run classification on the pack (deterministic mode)
smallmind pack run classification --deterministic --out artifacts/classification.md

# Score results against ground truth
smallmind pack score classification --run artifacts/classification.run.json
```

### API Usage (C#)

```csharp
using SmallMind.Runtime.PretrainedModels;

// Load the pack
var packPath = "data/pretrained/classification";
var pack = PretrainedPack.Load(packPath);

Console.WriteLine($"Loaded pack: {pack.Manifest.Id}");
Console.WriteLine($"Categories: {string.Join(", ", pack.Categories)}");

// Run inference on all samples
foreach (var sample in pack.Samples)
{
    var prediction = model.Classify(sample.Text);
    var match = prediction.Equals(sample.Label, 
        StringComparison.OrdinalIgnoreCase);
    Console.WriteLine($"[{sample.Id}] True: {sample.Label}, " +
                     $"Predicted: {prediction} {(match ? "✓" : "✗")}");
}

// Compute metrics
var scorer = new PackScorer();
var results = scorer.ScoreClassification(pack, predictions);
Console.WriteLine($"Accuracy: {results.Accuracy:F2}%");
Console.WriteLine($"Macro F1: {results.MacroF1:F2}");

// Per-category metrics
foreach (var category in pack.Categories)
{
    Console.WriteLine($"{category}:");
    Console.WriteLine($"  Precision: {results.Precision[category]:F2}%");
    Console.WriteLine($"  Recall: {results.Recall[category]:F2}%");
}
```

### Loading Categories

```csharp
using System.Text.Json;

var categoriesPath = "data/pretrained/classification/task/categories.json";
var json = await File.ReadAllTextAsync(categoriesPath);
var categories = JsonSerializer.Deserialize<CategoryDefinitions>(json);

foreach (var category in categories.Categories)
{
    Console.WriteLine($"{category.Name}: {category.Description}");
    Console.WriteLine($"  Examples: {string.Join("; ", category.Examples)}");
}
```

### Loading for Training

```csharp
using SmallMind.Runtime.PretrainedModels;

// Load samples
var packPath = "data/pretrained/classification";
var samples = DatasetLoader.LoadFromJsonl(
    Path.Combine(packPath, "task/inputs.jsonl")
);

// Get unique categories
var categories = DatasetLoader.GetUniqueLabels(samples);
Console.WriteLine($"Categories: {string.Join(", ", categories)}");

// Split for training/validation
var (train, val) = DatasetLoader.SplitDataset(
    samples, 
    trainRatio: 0.8, 
    seed: 42
);

// Use with your training pipeline
// var model = PretrainedModelFactory.CreateClassificationModel(
//     vocabSize: tokenizer.VocabSize,
//     labels: categories,
//     ...
// );
```

## Evaluation Metrics

See `eval/scoring.md` for detailed evaluation methodology.

**Key Metrics**:
- **Accuracy**: Percentage of correct predictions
- **Per-Category Precision**: What % of predicted X are actually X
- **Per-Category Recall**: What % of actual X are predicted as X
- **F1 Score**: Harmonic mean of precision and recall
- **Confusion Matrix**: Which categories are confused

**Acceptance Thresholds**:
- Untrained baseline: ~25% (random chance for 4 classes)
- Minimum viable: ≥55%
- Production ready: ≥70%
- High quality: ≥85%

## Deterministic Execution

To ensure reproducible results:
- Set `deterministic: true` in manifest
- Use temperature = 0.0
- Fix random seeds
- No sampling randomness

Guarantees:
- Same input → same output
- Reproducible across runs and machines

## Swapping In Your Data

To use your own classification data:

1. **Define Categories**: Update `task/categories.json` with your labels
2. **Convert to JSONL**: Transform your data to match the format
3. **Update Manifest**: Modify `manifest.json` with your metadata
4. **Add Evaluation Data**: Include ground truth in `eval/labels.jsonl`
5. **Adjust Thresholds**: Update `eval/scoring.md` for your domain
6. **Maintain Structure**: Keep the folder hierarchy

Example categories for different domains:
- **Customer Support**: Billing, Technical, Shipping, Returns
- **Document Classification**: Invoice, Contract, Receipt, Report
- **Email Routing**: Sales, Support, HR, IT
- **Content Moderation**: Safe, Questionable, Unsafe

## Category Design Guidelines

For effective classification:

1. **Mutually Exclusive**: Categories shouldn't overlap
2. **Collectively Exhaustive**: Cover all expected inputs
3. **Balanced Distribution**: Aim for similar sample counts per category
4. **Clear Boundaries**: Unambiguous category definitions
5. **Consistent Granularity**: Similar level of specificity

## Source and Licensing

- **Origin**: Synthetic data created for demonstration
- **License**: MIT
- **Redistributable**: Yes
- **Attribution**: None required

This pack is provided as a starter asset, not a replacement for real-world classification data.

## Related Packs

- **`sentiment`**: Binary/ternary sentiment classification
- **`finance`**: Domain-specific financial sentiment

## Support

This is an open-source pack with no paid support. For issues or contributions, see the main SmallMind repository.

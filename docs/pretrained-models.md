# Pre-Trained Models Guide

SmallMind now supports pre-trained models for common NLP tasks including sentiment analysis, text classification, summarization, and question answering. This guide explains how to create, train, deploy, and use these models.

## Table of Contents

1. [Overview](#overview)
2. [Supported Tasks](#supported-tasks)
3. [Quick Start](#quick-start)
4. [Creating Models](#creating-models)
5. [Loading and Saving Models](#loading-and-saving-models)
6. [Working with Datasets](#working-with-datasets)
7. [Fine-Tuning Models](#fine-tuning-models)
8. [Domain-Specific Models](#domain-specific-models)
9. [Production Deployment](#production-deployment)
10. [API Reference](#api-reference)

## Overview

Pre-trained models in SmallMind are built on the same Transformer architecture used for text generation but are specialized for specific tasks through:

- Task-specific output interpretations
- Domain-specific training data
- Optimized inference patterns
- Metadata-driven configuration

All models are saved in the `.smnd` (SmallMind) binary format for efficient storage and loading.

## Supported Tasks

### 1. Sentiment Analysis
Classify text into sentiment categories: Positive, Negative, or Neutral.

**Use Cases:**
- Product review analysis
- Social media monitoring
- Customer feedback processing
- Brand sentiment tracking

### 2. Text Classification
Categorize text into predefined labels.

**Use Cases:**
- Topic classification
- Spam detection
- Intent recognition
- Content categorization

### 3. Summarization
Generate concise summaries of longer text (Coming Soon).

### 4. Question Answering
Answer questions based on provided context (Coming Soon).

## Quick Start

### Creating a Sentiment Analysis Model

```csharp
using SmallMind.Runtime.PretrainedModels;
using SmallMind.Tokenizers;

// Create tokenizer
const string vocab = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?;:'\"-\n";
var tokenizer = new CharTokenizer(vocab);

// Create sentiment model
var model = PretrainedModelFactory.CreateSentimentModel(
    vocabSize: tokenizer.VocabSize,
    blockSize: 128,
    domain: DomainType.General,
    embedDim: 128,
    numLayers: 4,
    numHeads: 4
);

// Save model
await PretrainedModelFactory.SaveAsync(model, "sentiment-model.smnd");
```

### Using a Sentiment Model

```csharp
// Analyze sentiment
var sentiment = model.AnalyzeSentiment("This product is amazing!");
// Returns: "Positive"

// Get detailed scores
var scores = model.AnalyzeSentimentWithScores("This product is amazing!");
// Returns: { "Positive": 0.85, "Negative": 0.05, "Neutral": 0.10 }
```

### Creating a Text Classification Model

```csharp
// Define categories
var labels = new[] { "Technology", "Sports", "Politics", "Entertainment" };

// Create model
var model = PretrainedModelFactory.CreateClassificationModel(
    vocabSize: tokenizer.VocabSize,
    labels: labels,
    blockSize: 128,
    domain: DomainType.General
);

// Classify text
var category = model.Classify("The team won the championship!");
// Returns: "Sports"

// Get probabilities
var probs = model.ClassifyWithProbabilities("The team won the championship!");
// Returns: { "Technology": 0.05, "Sports": 0.85, "Politics": 0.05, "Entertainment": 0.05 }
```

## Creating Models

### Model Configuration Parameters

```csharp
var model = PretrainedModelFactory.CreateSentimentModel(
    vocabSize: 100,          // Size of vocabulary (from tokenizer)
    blockSize: 128,          // Maximum sequence length
    domain: DomainType.General,  // Domain specialization
    embedDim: 128,           // Embedding dimension
    numLayers: 4,            // Number of transformer layers
    numHeads: 4,             // Number of attention heads
    dropout: 0.1,            // Dropout rate
    seed: 42                 // Random seed
);
```

**Recommended Configurations:**

**Tiny (Fast, Low Memory):**
- embedDim: 64
- numLayers: 2
- numHeads: 2
- blockSize: 64

**Small (Balanced):**
- embedDim: 128
- numLayers: 4
- numHeads: 4
- blockSize: 128

**Medium (Better Quality):**
- embedDim: 256
- numLayers: 6
- numHeads: 8
- blockSize: 256

## Loading and Saving Models

### Saving Models

```csharp
await PretrainedModelFactory.SaveAsync(model, "my-model.smnd");
```

The saved checkpoint includes:
- Model architecture parameters
- All trained weights
- Task and domain metadata
- Model name and description
- Classification labels (for classification models)

### Loading Models

```csharp
var tokenizer = new CharTokenizer(vocab);
var model = await PretrainedModelFactory.LoadAsync("my-model.smnd", tokenizer);

Console.WriteLine($"Loaded: {model.Name}");
Console.WriteLine($"Task: {model.Task}");
Console.WriteLine($"Domain: {model.Domain}");
```

The factory automatically creates the correct model type based on saved metadata.

## Working with Datasets

### Dataset Format

SmallMind uses a simple pipe-delimited format for labeled datasets:

```
label|text content here
```

### Loading Datasets

```csharp
using SmallMind.Runtime.PretrainedModels;

// Load sentiment data
var samples = DatasetLoader.LoadSentimentData("data/sentiment-train.txt");

// Load classification data with label validation
var samples = DatasetLoader.LoadClassificationData(
    "data/topics.txt",
    expectedLabels: new[] { "Tech", "Sports", "Politics" }
);
```

### Splitting Data

```csharp
var (train, validation) = DatasetLoader.SplitDataset(
    samples,
    trainRatio: 0.8,  // 80% training, 20% validation
    seed: 42
);

Console.WriteLine($"Training samples: {train.Count}");
Console.WriteLine($"Validation samples: {validation.Count}");
```

### Dataset Statistics

```csharp
DatasetLoader.PrintStatistics(samples, "My Dataset");
// Output:
// My Dataset Statistics:
// Total samples: 100
//
// Label distribution:
//   positive: 35 (35.0%)
//   negative: 30 (30.0%)
//   neutral: 35 (35.0%)
//
// Text length statistics:
//   Min: 15
//   Max: 250
//   Average: 85.3
```

### Sample Datasets

SmallMind includes sample datasets in `data/pretrained/`:

- `sentiment/sample-sentiment.txt` - General sentiment data
- `finance/finance-sentiment.txt` - Financial sentiment data
- `classification/topic-classification.txt` - Topic classification data

## Fine-Tuning Models

### Basic Training Loop

```csharp
using SmallMind.Core.Core;
using SmallMind.Runtime;

// Load data
var samples = DatasetLoader.LoadSentimentData("data/sentiment-train.txt");
var (train, val) = DatasetLoader.SplitDataset(samples, 0.8);

// Create model
var model = PretrainedModelFactory.CreateSentimentModel(
    vocabSize: tokenizer.VocabSize,
    domain: DomainType.General
);

// Use SmallMind's training infrastructure
var trainer = new Training(
    model.Model,
    tokenizer,
    learningRate: 0.001,
    batchSize: 32
);

// Training loop (simplified)
for (int epoch = 0; epoch < 10; epoch++)
{
    // Train on batches
    // Evaluate on validation set
    // Save checkpoints
}
```

### Transfer Learning

Start from a pre-trained model and fine-tune for your specific domain:

```csharp
// Load general pre-trained model
var baseModel = await PretrainedModelFactory.LoadAsync("base-sentiment.smnd", tokenizer);

// Fine-tune on domain-specific data
var financeData = DatasetLoader.LoadSentimentData("data/finance-sentiment.txt");
// ... continue training ...

// Save fine-tuned model
await PretrainedModelFactory.SaveAsync(baseModel, "finance-sentiment.smnd");
```

## Domain-Specific Models

SmallMind supports domain specialization through the `DomainType` enum:

### Available Domains

- **General** - All-purpose models
- **Finance** - Financial news, market analysis
- **Healthcare** - Medical records, health articles
- **Legal** - Contracts, legal documents
- **ECommerce** - Product reviews, shopping trends

### Creating Domain-Specific Models

```csharp
// Finance sentiment model
var financeModel = PretrainedModelFactory.CreateSentimentModel(
    vocabSize: tokenizer.VocabSize,
    domain: DomainType.Finance
);

// Legal classification model
var legalModel = PretrainedModelFactory.CreateClassificationModel(
    vocabSize: tokenizer.VocabSize,
    labels: new[] { "Contract", "NDA", "Agreement", "Policy" },
    domain: DomainType.Legal
);
```

Domain-specific models can be trained on specialized datasets to improve accuracy in their target domain.

## Production Deployment

### Model Serving

```csharp
public class SentimentService
{
    private readonly ISentimentAnalysisModel _model;
    
    public async Task InitializeAsync(string modelPath)
    {
        var tokenizer = CreateTokenizer();
        _model = (ISentimentAnalysisModel)await PretrainedModelFactory.LoadAsync(
            modelPath, 
            tokenizer
        );
    }
    
    public string AnalyzeSentiment(string text)
    {
        return _model.AnalyzeSentiment(text);
    }
}
```

### Performance Optimization

SmallMind models are optimized for CPU inference through:

1. **SIMD Acceleration** - Leverages hardware vector instructions
2. **Memory Pooling** - Reduces GC pressure
3. **Efficient Tensors** - Contiguous memory layouts

### Quantization (Coming Soon)

Reduce model size and improve inference speed:

```csharp
// Future API
var quantizedModel = ModelQuantizer.Quantize(model, precision: Precision.Int8);
await PretrainedModelFactory.SaveAsync(quantizedModel, "model-quantized.smnd");
```

### Deployment Checklist

- ✅ Test model on validation set
- ✅ Measure inference latency
- ✅ Estimate memory requirements
- ✅ Set up monitoring and logging
- ✅ Implement error handling
- ✅ Create model versioning strategy
- ✅ Document model capabilities and limitations

## API Reference

### Interfaces

#### `IPretrainedModel`
Base interface for all pre-trained models.

Properties:
- `TaskType Task` - The task type
- `DomainType Domain` - The domain specialization
- `TransformerModel Model` - Underlying Transformer
- `string Name` - Model name
- `string Description` - Model description

#### `ISentimentAnalysisModel`
Sentiment analysis-specific interface.

Methods:
- `string AnalyzeSentiment(string text)` - Get sentiment label
- `Dictionary<string, float> AnalyzeSentimentWithScores(string text)` - Get scores

#### `ITextClassificationModel`
Text classification-specific interface.

Properties:
- `IReadOnlyList<string> Labels` - Available categories

Methods:
- `string Classify(string text)` - Get category
- `Dictionary<string, float> ClassifyWithProbabilities(string text)` - Get probabilities

### Factory Methods

#### `PretrainedModelFactory`

**Create Models:**
- `CreateSentimentModel(...)` - Create sentiment analysis model
- `CreateClassificationModel(...)` - Create text classification model

**Load/Save:**
- `LoadAsync(string path, ITokenizer tokenizer)` - Load from checkpoint
- `SaveAsync(IPretrainedModel model, string path)` - Save to checkpoint

### Dataset Utilities

#### `DatasetLoader`

**Load Data:**
- `LoadSentimentData(string path)` - Load sentiment dataset
- `LoadClassificationData(string path, string[]? labels)` - Load classification dataset
- `LoadLabeledData(string path, string[]? labels)` - Load generic labeled data

**Process Data:**
- `SplitDataset(samples, ratio, seed)` - Split train/validation
- `GetUniqueLabels(samples)` - Extract unique labels
- `GetLabelDistribution(samples)` - Get label counts
- `PrintStatistics(samples, name)` - Print dataset info

## Best Practices

1. **Start Small** - Begin with tiny models for prototyping
2. **Validate Data** - Ensure dataset quality and balance
3. **Monitor Performance** - Track metrics during training
4. **Version Models** - Save checkpoints at milestones
5. **Test Thoroughly** - Validate on unseen data
6. **Document Decisions** - Record hyperparameters and results

## Examples

See `examples/PretrainedModels/` for complete working examples:
- Sentiment analysis with general and domain-specific models
- Text classification with custom categories
- Model save/load workflows
- Dataset loading and processing

## Why SmallMind?

Advantages over other solutions:

✅ **Pure C#** - No Python dependencies  
✅ **CPU-Optimized** - SIMD acceleration for fast inference  
✅ **Lightweight** - Small models, minimal dependencies  
✅ **Educational** - Transparent, understandable code  
✅ **Modular** - Easy to extend and customize  
✅ **Production-Ready** - Built-in checkpointing and deployment support

## Troubleshooting

**Model predictions are random:**
- Models need training on labeled data
- Untrained models provide random/heuristic outputs
- See training examples for guidance

**Out of memory errors:**
- Reduce `blockSize` or `embedDim`
- Use smaller batch sizes
- Consider model quantization

**Slow inference:**
- Ensure SIMD is enabled (automatic on supported CPUs)
- Reduce model size
- Profile with `MemoryEstimator`

## Next Steps

- Review the complete example in `examples/PretrainedModels/`
- Explore sample datasets in `data/pretrained/`
- Read about training infrastructure in `docs/LIBRARY_USAGE.md`
- Check performance tips in `docs/PERFORMANCE_OPTIMIZATIONS.md`

---

For more information, see the main [SmallMind README](../README.md).

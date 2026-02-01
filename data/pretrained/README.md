# Sample Datasets for Pre-Trained Models

This directory contains sample training datasets for different pre-trained model tasks and domains.

## Directory Structure

```
pretrained/
├── sentiment/          # General sentiment analysis datasets
├── classification/     # Text classification datasets
├── finance/           # Finance domain datasets
├── healthcare/        # Healthcare domain datasets (to be added)
├── legal/            # Legal domain datasets (to be added)
└── ecommerce/        # E-commerce domain datasets (to be added)
```

## Dataset Format

### Sentiment Analysis
Format: `label|text`

Labels: `positive`, `negative`, `neutral`

Example:
```
positive|This product is amazing! I absolutely love it.
negative|Terrible quality. Broke after one day.
neutral|It's okay. Nothing special but gets the job done.
```

### Text Classification
Format: `category|text`

Example with topic classification:
```
Technology|New smartphone features groundbreaking AI capabilities.
Sports|Championship team celebrates historic victory.
Politics|Congress passes landmark legislation.
Entertainment|Award-winning film breaks box office records.
```

## Using These Datasets

### Loading Data
```csharp
using SmallMind.Runtime.PretrainedModels;

// Load sentiment data
var sentimentData = DatasetLoader.LoadSentimentData("data/pretrained/sentiment/sample-sentiment.txt");

// Load classification data
var classificationData = DatasetLoader.LoadClassificationData(
    "data/pretrained/classification/topic-classification.txt",
    new[] { "Technology", "Sports", "Politics", "Entertainment" }
);
```

### Training Models
```csharp
// Create model
var model = PretrainedModelFactory.CreateSentimentModel(
    vocabSize: tokenizer.VocabSize,
    domain: DomainType.General
);

// Train using SmallMind's training infrastructure
// (See training examples in examples/ directory)
```

## Dataset Sources

These datasets are **synthetic examples** created for demonstration purposes. For production use, you should:

1. Collect real labeled data from your specific domain
2. Ensure data quality and diversity
3. Follow proper data licensing and privacy requirements
4. Validate and test with held-out datasets

## License

These sample datasets are provided under the MIT License for educational and demonstration purposes.

## Contributing

To add new datasets:
1. Follow the format conventions above
2. Create appropriately named files in the correct subdirectory
3. Update this README with dataset descriptions
4. Ensure data quality and proper labeling

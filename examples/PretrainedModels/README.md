# Pre-Trained Models Example

This example demonstrates how to use SmallMind's pre-trained model infrastructure for various NLP tasks.

## Features Demonstrated

1. **Sentiment Analysis**: Create and use models for analyzing text sentiment (Positive, Negative, Neutral)
2. **Text Classification**: Build models for categorizing text into predefined labels
3. **Domain-Specific Models**: Create specialized models for different domains (Finance, Healthcare, Legal, E-commerce)

## Running the Example

```bash
cd examples/PretrainedModels
dotnet run
```

## What This Example Shows

### 1. Sentiment Analysis
- Creates a general-purpose sentiment analysis model
- Tests sentiment on sample texts
- Saves the model to a `.smnd` checkpoint file
- Loads the model back from the checkpoint

### 2. Text Classification
- Creates a text classification model with custom labels
- Demonstrates topic classification (Technology, Sports, Politics, Entertainment)
- Shows probability distributions for all categories

### 3. Domain-Specific Models
- Creates a Finance-specific sentiment analysis model
- Demonstrates how to specialize models for different industries

## Important Notes

⚠️ **These are UNTRAINED models** - The example creates model architectures but doesn't train them. The predictions shown are based on random/heuristic initialization and are for demonstration purposes only.

In a real-world scenario, you would:
1. Prepare labeled training data for your specific task and domain
2. Train the model using SmallMind's training infrastructure
3. Save the trained model as a `.smnd` checkpoint
4. Deploy the trained model for production inference

## Model Architecture

The example creates small models optimized for demonstration:
- **Vocabulary Size**: Based on character-level tokenization
- **Block Size**: 64 tokens (max sequence length)
- **Embedding Dimension**: 64
- **Layers**: 2 transformer layers
- **Attention Heads**: 2 heads per layer

These are intentionally small for quick experimentation. Production models would typically be larger.

## Next Steps

To use these models in production:
1. See `docs/pretrained-models.md` for training guidance
2. Check `data/pretrained/` for sample training datasets
3. Review fine-tuning examples for domain-specific training
4. Use the `BinaryCheckpointStore` for efficient model deployment

# SmallMind Examples

This directory contains example code and sample data files demonstrating how to use SmallMind with different data formats.

## Sample Data Files

The `data/` directory contains the same training data in multiple formats:

- **training.txt** - Plain text file, one sentence per line
- **training.json** - JSON format with sentences array
- **training.xml** - XML format with sentence elements

All three files contain identical data (20 training sentences).

## Example Code Files

### SimpleExample.cs
Demonstrates the simplest way to load and train a model using the `DataLoader` helper class.

### DataLoadingExample.cs
Shows how to load data from all three formats (text, JSON, XML) and verify they contain the same data.

## Running the Examples

These are standalone code examples for reference. To use them in your own project:

1. **Copy the relevant code** into your own C# application
2. **Install SmallMind** as a reference or copy the source files
3. **Update file paths** to point to your data files

## Quick Start

```csharp
using SmallMind;

// Load training data from any format
string[] trainingData = DataLoader.FromTextFile("data/training.txt");
// OR: DataLoader.FromJsonFile("data/training.json");
// OR: DataLoader.FromXmlFile("data/training.xml");

// Create and train the model
var model = new LanguageModel(32, 64, 100, 0.01f);
for (int epoch = 0; epoch < 50; epoch++)
{
    float loss = model.Train(trainingData);
    Console.WriteLine($"Epoch {epoch}: Loss = {loss:F4}");
}

// Generate predictions
string prediction = model.Predict("The cat", maxTokens: 5);
Console.WriteLine($"Prediction: {prediction}");
```

## See Also

- [DATA_FORMATS.md](../DATA_FORMATS.md) - Complete guide to loading data from various sources
- [README.md](../README.md) - Main project documentation
- [PERFORMANCE.md](../PERFORMANCE.md) - Performance optimization guide

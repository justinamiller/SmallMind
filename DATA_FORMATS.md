# Data Input Formats Guide

## Overview

SmallMind accepts training data as a **plain text string array** (`string[]`). This simple format makes it easy to work with and allows you to load data from any source (JSON, XML, CSV, databases, etc.) by converting it to an array of strings.

## Input Format

### Required Format
```csharp
string[] trainingData = 
{
    "The cat sat on the mat",
    "The dog sat on the log",
    "Birds can fly high"
};

model.Train(trainingData);
```

Each string represents one training example (sentence or text snippet). The model:
1. Tokenizes each string into words
2. Learns to predict the next word given previous words
3. Uses parallel processing to train on multiple sentences simultaneously

## Loading Data from Different Sources

### 1. Plain Text Files

#### Single File - One Sentence Per Line
```csharp
// Load from a .txt file where each line is a sentence
string[] trainingData = File.ReadAllLines("data/training.txt");
model.Train(trainingData);
```

**Example file (training.txt):**
```text
The cat sat on the mat
The dog sat on the log
Birds can fly high
Fish live in water
```

#### Single File - Split by Delimiter
```csharp
// Load from a file with custom delimiter
string content = File.ReadAllText("data/corpus.txt");
string[] trainingData = content.Split(new[] { '.', '!', '?' }, 
    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
model.Train(trainingData);
```

#### Multiple Files
```csharp
// Load from multiple text files
var trainingData = Directory.GetFiles("data/texts/", "*.txt")
    .SelectMany(file => File.ReadAllLines(file))
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .ToArray();
model.Train(trainingData);
```

### 2. JSON Files

#### Simple Array Format
```csharp
using System.Text.Json;

// JSON file format: { "sentences": ["sentence1", "sentence2", ...] }
string json = File.ReadAllText("data/training.json");
var data = JsonSerializer.Deserialize<TrainingData>(json);
model.Train(data.Sentences);

public class TrainingData
{
    public string[] Sentences { get; set; } = Array.Empty<string>();
}
```

**Example file (training.json):**
```json
{
  "sentences": [
    "The cat sat on the mat",
    "The dog sat on the log",
    "Birds can fly high",
    "Fish live in water"
  ]
}
```

#### Complex JSON with Metadata
```csharp
using System.Text.Json;

// JSON with metadata
string json = File.ReadAllText("data/corpus.json");
var corpus = JsonSerializer.Deserialize<Corpus>(json);
var trainingData = corpus.Documents
    .SelectMany(doc => doc.Sentences)
    .ToArray();
model.Train(trainingData);

public class Corpus
{
    public Document[] Documents { get; set; } = Array.Empty<Document>();
}

public class Document
{
    public string Title { get; set; } = "";
    public string[] Sentences { get; set; } = Array.Empty<string>();
}
```

**Example file (corpus.json):**
```json
{
  "documents": [
    {
      "title": "Animals",
      "sentences": [
        "The cat sat on the mat",
        "The dog sat on the log"
      ]
    },
    {
      "title": "Nature",
      "sentences": [
        "Birds can fly high",
        "Fish live in water"
      ]
    }
  ]
}
```

### 3. XML Files

#### Simple XML Format
```csharp
using System.Xml.Linq;

// Load from XML
var xml = XDocument.Load("data/training.xml");
var trainingData = xml.Descendants("sentence")
    .Select(e => e.Value)
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .ToArray();
model.Train(trainingData);
```

**Example file (training.xml):**
```xml
<?xml version="1.0" encoding="utf-8"?>
<training>
  <sentence>The cat sat on the mat</sentence>
  <sentence>The dog sat on the log</sentence>
  <sentence>Birds can fly high</sentence>
  <sentence>Fish live in water</sentence>
</training>
```

#### Complex XML with Attributes
```csharp
using System.Xml.Linq;

// Load from XML with metadata
var xml = XDocument.Load("data/corpus.xml");
var trainingData = xml.Descendants("document")
    .SelectMany(doc => doc.Descendants("sentence"))
    .Select(e => e.Value)
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .ToArray();
model.Train(trainingData);
```

**Example file (corpus.xml):**
```xml
<?xml version="1.0" encoding="utf-8"?>
<corpus>
  <document category="animals">
    <sentence>The cat sat on the mat</sentence>
    <sentence>The dog sat on the log</sentence>
  </document>
  <document category="nature">
    <sentence>Birds can fly high</sentence>
    <sentence>Fish live in water</sentence>
  </document>
</corpus>
```

### 4. CSV Files

```csharp
// Load from CSV (assuming sentences are in a specific column)
var trainingData = File.ReadAllLines("data/sentences.csv")
    .Skip(1) // Skip header
    .Select(line => line.Split(',')[0].Trim('"')) // Get first column
    .Where(s => !string.IsNullOrWhiteSpace(s))
    .ToArray();
model.Train(trainingData);
```

**Example file (sentences.csv):**
```csv
sentence,category,language
"The cat sat on the mat",animals,en
"The dog sat on the log",animals,en
"Birds can fly high",nature,en
```

### 5. Database

```csharp
// Pseudo-code for loading from database
using (var connection = new SqlConnection(connectionString))
{
    var command = new SqlCommand("SELECT Text FROM Sentences WHERE Language = 'en'", connection);
    connection.Open();
    
    var sentences = new List<string>();
    using (var reader = command.ExecuteReader())
    {
        while (reader.Read())
        {
            sentences.Add(reader.GetString(0));
        }
    }
    
    model.Train(sentences.ToArray());
}
```

### 6. Web API / REST Endpoint

```csharp
using System.Net.Http;
using System.Text.Json;

// Load from web API
using var client = new HttpClient();
var response = await client.GetStringAsync("https://api.example.com/training-data");
var data = JsonSerializer.Deserialize<TrainingData>(response);
model.Train(data.Sentences);
```

## Data Preprocessing

### Cleaning and Filtering
```csharp
var trainingData = File.ReadAllLines("data/raw.txt")
    .Where(line => !string.IsNullOrWhiteSpace(line))  // Remove empty lines
    .Where(line => line.Length >= 10)                  // Minimum length
    .Where(line => line.Length <= 200)                 // Maximum length
    .Select(line => line.Trim())                       // Trim whitespace
    .Select(line => CleanText(line))                   // Custom cleaning
    .ToArray();

string CleanText(string text)
{
    // Remove extra spaces
    text = Regex.Replace(text, @"\s+", " ");
    // Remove special characters if needed
    // text = Regex.Replace(text, @"[^\w\s]", "");
    return text;
}
```

### Augmentation
```csharp
var baseData = File.ReadAllLines("data/training.txt");
var augmentedData = new List<string>(baseData);

// Add variations, transformations, etc.
foreach (var sentence in baseData)
{
    // Example: Add lowercase version
    augmentedData.Add(sentence.ToLowerInvariant());
}

model.Train(augmentedData.ToArray());
```

## Best Practices

### 1. Data Size
- **Minimum**: 10-100 sentences for basic learning
- **Recommended**: 1,000-10,000 sentences for good performance
- **Large-scale**: 100,000+ sentences for production use

### 2. Data Quality
- Use grammatically correct sentences
- Remove duplicate sentences
- Filter out very short or very long sentences
- Ensure consistent formatting

### 3. Memory Considerations
```csharp
// For very large datasets, process in batches
const int batchSize = 1000;
var allData = File.ReadAllLines("large-corpus.txt");

for (int i = 0; i < allData.Length; i += batchSize)
{
    var batch = allData.Skip(i).Take(batchSize).ToArray();
    model.Train(batch);
}
```

### 4. Validation Split
```csharp
// Split data into training and validation sets
var allData = File.ReadAllLines("data.txt");
var shuffled = allData.OrderBy(x => Random.Shared.Next()).ToArray();

int splitIndex = (int)(allData.Length * 0.8); // 80% train, 20% validation
var trainingData = shuffled.Take(splitIndex).ToArray();
var validationData = shuffled.Skip(splitIndex).ToArray();

// Train on training set
for (int epoch = 0; epoch < 50; epoch++)
{
    model.Train(trainingData);
}

// Validate on validation set
float validationLoss = model.Train(validationData);
Console.WriteLine($"Validation loss: {validationLoss}");
```

## Summary

**Input format**: `string[]` - Array of plain text strings

**Why this format?**
- ✅ Simple and flexible
- ✅ Easy to load from any source
- ✅ No forced dependency on specific file formats
- ✅ Works with streaming, databases, APIs, etc.
- ✅ Minimal memory overhead

**Convert from any format**: Use C# standard libraries (`System.Text.Json`, `System.Xml.Linq`, `File`, etc.) to convert your data source to `string[]` before passing to the model.

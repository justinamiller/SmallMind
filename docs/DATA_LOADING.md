# TinyLLM Data Loading Guide

## Overview

TinyLLM uses **character-level tokenization** and expects training data as a single text string. This guide shows how to load data from various sources.

## Basic Input Format

```csharp
// Load from a single text file
string trainingText = File.ReadAllText("data.txt");
```

TinyLLM will:
1. Build a vocabulary from all unique characters in the text
2. Convert each character to a token ID
3. Use sliding windows to create training examples

## Loading from Multiple Sources

### Combining Multiple Text Files

```csharp
// Load and combine multiple files
var files = Directory.GetFiles("data/", "*.txt");
var allText = string.Join("\n\n", files.Select(File.ReadAllText));
File.WriteAllText("combined_data.txt", allText);
```

### Loading from Structured Formats

While TinyLLM needs plain text, you can extract text from structured formats:

#### From JSON

```csharp
using System.Text.Json;

// Example: { "documents": [{"text": "..."}, {"text": "..."}] }
var json = File.ReadAllText("data.json");
var doc = JsonDocument.Parse(json);
var texts = doc.RootElement
    .GetProperty("documents")
    .EnumerateArray()
    .Select(d => d.GetProperty("text").GetString())
    .Where(t => t != null);

string trainingText = string.Join("\n\n", texts);
File.WriteAllText("data.txt", trainingText);
```

#### From CSV

```csharp
// Assuming CSV with a "text" column
var lines = File.ReadAllLines("data.csv");
var texts = lines.Skip(1) // Skip header
    .Select(line => line.Split(','))
    .Select(fields => fields[0]) // First column
    .Where(text => !string.IsNullOrWhiteSpace(text));

string trainingText = string.Join("\n", texts);
File.WriteAllText("data.txt", trainingText);
```

#### From XML

```csharp
using System.Xml.Linq;

var doc = XDocument.Load("data.xml");
var texts = doc.Descendants("text")
    .Select(e => e.Value)
    .Where(t => !string.IsNullOrWhiteSpace(t));

string trainingText = string.Join("\n\n", texts);
File.WriteAllText("data.txt", trainingText);
```

### Loading from Web APIs

```csharp
using System.Net.Http;

var client = new HttpClient();
var response = await client.GetStringAsync("https://api.example.com/texts");
File.WriteAllText("data.txt", response);
```

## Data Preprocessing

### Cleaning Text

```csharp
string text = File.ReadAllText("raw_data.txt");

// Remove excessive whitespace
text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");

// Normalize line endings
text = text.Replace("\r\n", "\n");

// Remove special characters if desired
text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\w\s\.,!?;:]", "");

File.WriteAllText("data.txt", text);
```

### Sentence Splitting

```csharp
// If you have paragraphs and want to split into sentences
string text = File.ReadAllText("raw_data.txt");
var sentences = System.Text.RegularExpressions.Regex.Split(text, @"(?<=[.!?])\s+");
string formatted = string.Join("\n", sentences);
File.WriteAllText("data.txt", formatted);
```

## Best Practices

1. **File Size**: Larger training files generally lead to better models. Aim for at least 10KB of text.

2. **Character Diversity**: Ensure your data contains the characters you want the model to learn (letters, numbers, punctuation).

3. **Data Quality**: Clean, well-formatted text works better than noisy data.

4. **Encoding**: Use UTF-8 encoding for text files to support international characters.

5. **Combining Sources**: When combining multiple sources, add separators (e.g., `\n\n`) to maintain document boundaries.

## Example: Complete Data Pipeline

```csharp
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

public static class DataPipeline
{
    public static void PrepareTrainingData(string outputFile)
    {
        var allTexts = new List<string>();

        // Load from text files
        var txtFiles = Directory.GetFiles("sources/txt/", "*.txt");
        allTexts.AddRange(txtFiles.Select(File.ReadAllText));

        // Load from JSON files
        var jsonFiles = Directory.GetFiles("sources/json/", "*.json");
        foreach (var jsonFile in jsonFiles)
        {
            var json = File.ReadAllText(jsonFile);
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var textProp))
            {
                allTexts.Add(textProp.GetString() ?? "");
            }
        }

        // Load from XML files
        var xmlFiles = Directory.GetFiles("sources/xml/", "*.xml");
        foreach (var xmlFile in xmlFiles)
        {
            var doc = XDocument.Load(xmlFile);
            var texts = doc.Descendants("content")
                .Select(e => e.Value)
                .Where(t => !string.IsNullOrWhiteSpace(t));
            allTexts.AddRange(texts);
        }

        // Combine and clean
        string combined = string.Join("\n\n", allTexts);
        combined = System.Text.RegularExpressions.Regex.Replace(combined, @"\s+", " ");
        combined = combined.Trim();

        // Write output
        File.WriteAllText(outputFile, combined);
        Console.WriteLine($"Created training data: {combined.Length} characters");
    }
}

// Usage:
DataPipeline.PrepareTrainingData("data.txt");
```

## Advanced: Streaming Large Files

For very large datasets, you can use streaming:

```csharp
using (var writer = new StreamWriter("data.txt"))
{
    foreach (var file in Directory.GetFiles("large_corpus/", "*.txt"))
    {
        using (var reader = new StreamReader(file))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                writer.WriteLine(line);
            }
        }
        writer.WriteLine(); // Separator between files
    }
}
```

## Command Line Usage

Once you have prepared your `data.txt` file:

```bash
# Train with default settings
dotnet run

# Train with custom block size
dotnet run -- --block-size 512

# Train with auto-configuration based on system resources
dotnet run -- --auto-config

# Load existing model and generate text
dotnet run -- --load --no-train --prompt "Your prompt here"
```

## Troubleshooting

**Error: "Training data too short"**
- Ensure your data.txt file has more characters than the block size
- Default block size is 512, so you need at least 513 characters
- Use `--block-size 128` for smaller datasets

**Error: "Vocabulary built: 0 unique characters"**
- Check that your data.txt file is not empty
- Verify the file encoding is UTF-8
- Ensure the file path is correct

**Poor Model Performance**
- Increase training data size (aim for >100KB)
- Increase training steps (default is 2000)
- Ensure data quality is good (well-formed text)

# DataLoader Examples

This directory contains example code demonstrating how to use the `DataLoader` class to load training data from various sources.

## DataLoaderExample.cs

A comprehensive example program showing all data loading methods:

1. **FromTextFile()** - Load plain text, one sentence per line
2. **FromJsonFile()** - Load from JSON with `{ "sentences": [...] }` format
3. **FromXmlFile()** - Extract text from specified XML elements
4. **FromCsvFile()** - Load from CSV with column-based extraction
5. **FromDirectory()** - Batch load from multiple files
6. **FromTextWithDelimiters()** - Split text by custom delimiters
7. **Format equivalence verification** - Confirm all formats produce identical output
8. **Integration with Tokenizer** - Use loaded data with the character-level tokenizer

## Running the Example

To run the example, compile it as a standalone program:

```bash
# Navigate to the project root
cd /home/runner/work/SmallMind/SmallMind

# Compile the example
csc -out:examples/DataLoaderExample.exe -reference:bin/Debug/net8.0/SmallMind.dll examples/DataLoaderExample.cs

# Or use dotnet to compile and run
dotnet build
dotnet run --project examples/DataLoaderExample.csproj
```

Alternatively, you can copy the relevant code snippets into your own program.

## Sample Data

The `sample_data/` directory contains example data files in three formats (text, JSON, XML, CSV) with identical content:

- `sample.txt` - Plain text format
- `sample.json` - JSON format with sentences array
- `sample.xml` - XML format with sentence elements
- `sample.csv` - CSV format with text column

All files contain the same five sentences to demonstrate format equivalence.

## Code Snippets

### Load from Text File

```csharp
using SmallMind;

var text = DataLoader.FromTextFile("data.txt");
Console.WriteLine($"Loaded {text.Length} characters");
```

### Load from JSON File

```csharp
// JSON format: { "sentences": ["sent1", "sent2", ...] }
var text = DataLoader.FromJsonFile("data.json");
```

### Load from XML File

```csharp
// Extract text from <sentence> elements
var text = DataLoader.FromXmlFile("data.xml", elementName: "sentence");
```

### Load from CSV File

```csharp
// Load from first column with header
var text = DataLoader.FromCsvFile("data.csv", columnIndex: 0, hasHeader: true);
```

### Load from Directory

```csharp
// Batch load all text files
var text = DataLoader.FromDirectory("data/", searchPattern: "*.txt");
```

### Split Text with Delimiters

```csharp
var rawText = "First. Second! Third? Fourth.";
var text = DataLoader.FromTextWithDelimiters(rawText, delimiters: new[] { ".", "!", "?" });
```

### Use with Tokenizer

```csharp
// Load data and create tokenizer
var trainingText = DataLoader.FromJsonFile("data.json");
var tokenizer = new Tokenizer(trainingText);

// Train your model
var model = new TransformerModel(...);
var trainer = new Training(model, tokenizer, trainingText, ...);
trainer.Train(...);
```

## Testing

Unit tests for the DataLoader are located in `Tests/DataLoaderTests.cs`. Run them with:

```bash
cd Tests
dotnet test
```

The tests verify:
- Each loading method works correctly
- All formats produce equivalent output
- Error handling for missing files/directories
- Delimiter-based splitting
- Integration with the tokenizer

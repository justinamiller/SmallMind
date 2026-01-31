using SmallMind;
using System.Text.Json;
using System.Xml.Linq;

Console.WriteLine("SmallMind - Data Loading Examples");
Console.WriteLine("==================================\n");

// Example 1: Load from plain text file
Console.WriteLine("1. Loading from plain text file...");
string[] textData = File.ReadAllLines("examples/data/training.txt");
Console.WriteLine($"   Loaded {textData.Length} sentences from training.txt\n");

// Example 2: Load from JSON file
Console.WriteLine("2. Loading from JSON file...");
string jsonContent = File.ReadAllText("examples/data/training.json");
var jsonData = JsonSerializer.Deserialize<JsonTrainingData>(jsonContent);
Console.WriteLine($"   Loaded {jsonData?.Sentences.Length ?? 0} sentences from training.json\n");

// Example 3: Load from XML file
Console.WriteLine("3. Loading from XML file...");
var xml = XDocument.Load("examples/data/training.xml");
var xmlData = xml.Descendants("sentence")
    .Select(e => e.Value)
    .ToArray();
Console.WriteLine($"   Loaded {xmlData.Length} sentences from training.xml\n");

// Train a model using the text file data
Console.WriteLine("Training model with plain text data...");
var model = new LanguageModel(
    embeddingDim: 32,
    hiddenDim: 64,
    vocabSize: 100,
    learningRate: 0.01f
);

for (int epoch = 1; epoch <= 20; epoch++)
{
    float loss = model.Train(textData);
    if (epoch % 5 == 0)
    {
        Console.WriteLine($"Epoch {epoch,2}/20: Loss = {loss:F4}");
    }
}

// Test predictions
Console.WriteLine("\nTesting predictions:");
string[] testInputs = { "The cat", "The dog", "Birds can" };
foreach (var input in testInputs)
{
    string prediction = model.Predict(input, maxTokens: 3);
    Console.WriteLine($"  '{input}' -> '{prediction}'");
}

Console.WriteLine("\n✓ All data formats loaded successfully!");

// Helper class for JSON deserialization
public class JsonTrainingData
{
    public string[] Sentences { get; set; } = Array.Empty<string>();
}

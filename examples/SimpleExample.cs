using SmallMind;

Console.WriteLine("SmallMind - Simple Data Loading Example");
Console.WriteLine("========================================\n");

// Using the DataLoader helper class makes it easy to load from different formats
var model = new LanguageModel(
    embeddingDim: 32,
    hiddenDim: 64,
    vocabSize: 100,
    learningRate: 0.01f
);

// Load from plain text file
Console.WriteLine("Loading from plain text file...");
string[] trainingData = DataLoader.FromTextFile("examples/data/training.txt");
Console.WriteLine($"Loaded {trainingData.Length} sentences\n");

// Train the model
Console.WriteLine("Training model...");
for (int epoch = 1; epoch <= 30; epoch++)
{
    float loss = model.Train(trainingData);
    if (epoch % 10 == 0)
    {
        Console.WriteLine($"Epoch {epoch}/30: Loss = {loss:F4}");
    }
}

// Test predictions
Console.WriteLine("\nPredictions:");
string[] testInputs = { "The cat", "Birds can", "The sun" };
foreach (var input in testInputs)
{
    string prediction = model.Predict(input, maxTokens: 4);
    Console.WriteLine($"  '{input}' -> '{prediction}'");
}

Console.WriteLine("\n✓ Done!");

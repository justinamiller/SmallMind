using System.Diagnostics;
using SmallMind;

Console.WriteLine("SmallMind - Tiny Educational Language Model");
Console.WriteLine("============================================\n");

// Sample training data
string[] trainingData = 
{
    "The cat sat on the mat",
    "The dog sat on the log",
    "The bird flew in the sky",
    "The fish swam in the sea",
    "Cats and dogs are pets",
    "Birds can fly high",
    "Fish live in water",
    "The sun is bright",
    "The moon is white",
    "Stars shine at night"
};

Console.WriteLine($"Training data: {trainingData.Length} sentences\n");

// Initialize model with optimizations
var model = new LanguageModel(
    embeddingDim: 32,
    hiddenDim: 64,
    vocabSize: 100,
    learningRate: 0.01f
);

Console.WriteLine("Model initialized with:");
Console.WriteLine($"- Embedding dimensions: 32");
Console.WriteLine($"- Hidden dimensions: 64");
Console.WriteLine($"- Learning rate: 0.01");
Console.WriteLine($"- Using SIMD vectorization: {System.Numerics.Vector.IsHardwareAccelerated}");
Console.WriteLine($"- Processor count: {Environment.ProcessorCount}\n");

// Train the model
var stopwatch = Stopwatch.StartNew();
int epochs = 50;
Console.WriteLine($"Training for {epochs} epochs...\n");

for (int epoch = 1; epoch <= epochs; epoch++)
{
    float loss = model.Train(trainingData);
    
    if (epoch % 10 == 0 || epoch == 1)
    {
        Console.WriteLine($"Epoch {epoch,3}/{epochs}: Loss = {loss:F4}");
    }
}

stopwatch.Stop();
Console.WriteLine($"\nTraining completed in {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Average time per epoch: {stopwatch.ElapsedMilliseconds / epochs:F2}ms");

// Test the model
Console.WriteLine("\n--- Testing Model ---");
string[] testSentences = 
{
    "The cat",
    "The dog",
    "The bird"
};

foreach (var prompt in testSentences)
{
    string prediction = model.Predict(prompt, maxTokens: 3);
    Console.WriteLine($"Input: '{prompt}' -> Prediction: '{prediction}'");
}

Console.WriteLine("\n--- Performance Summary ---");
Console.WriteLine($"Total training time: {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Sentences processed: {trainingData.Length * epochs}");
Console.WriteLine($"Throughput: {(trainingData.Length * epochs * 1000.0 / stopwatch.ElapsedMilliseconds):F2} sentences/second");

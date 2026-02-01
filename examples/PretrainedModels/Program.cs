using SmallMind.Core;
using SmallMind.Runtime.PretrainedModels;
using SmallMind.Tokenizers;
using System;
using System.Threading.Tasks;

namespace PretrainedModelsExample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("SmallMind Pre-Trained Models Example");
            Console.WriteLine("=====================================\n");

            // Example 1: Sentiment Analysis
            await SentimentAnalysisExample();

            Console.WriteLine("\n" + new string('-', 60) + "\n");

            // Example 2: Text Classification
            await TextClassificationExample();

            Console.WriteLine("\n" + new string('-', 60) + "\n");

            // Example 3: Domain-Specific Sentiment (Finance)
            await DomainSpecificExample();
        }

        static async Task SentimentAnalysisExample()
        {
            Console.WriteLine("Example 1: Sentiment Analysis");
            Console.WriteLine("==============================\n");

            // Create a simple tokenizer
            const string vocab = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?;:'\"-\n()[]{}@#$%&*+=/<>|\\~`";
            var tokenizer = new CharTokenizer(vocab);

            // Create a sentiment analysis model
            var sentimentModel = PretrainedModelFactory.CreateSentimentModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 64,
                domain: DomainType.General,
                embedDim: 64,
                numLayers: 2,
                numHeads: 2,
                dropout: 0.1,
                seed: 42
            );

            Console.WriteLine($"Created model: {sentimentModel.Name}");
            Console.WriteLine($"Description: {sentimentModel.Description}");
            Console.WriteLine($"Domain: {sentimentModel.Domain}");
            Console.WriteLine($"Task: {sentimentModel.Task}\n");

            // Save the model
            var modelPath = "sentiment-general.smnd";
            await PretrainedModelFactory.SaveAsync(sentimentModel, modelPath);
            Console.WriteLine($"✓ Saved model to {modelPath}\n");

            // Test sentiment analysis (Note: This is untrained, so results are random)
            Console.WriteLine("Testing sentiment analysis (untrained model - random results):");
            var testTexts = new[]
            {
                "This is a great product! I love it.",
                "Terrible experience, very disappointed.",
                "It's okay, nothing special."
            };

            foreach (var text in testTexts)
            {
                var sentiment = sentimentModel.AnalyzeSentiment(text);
                var scores = sentimentModel.AnalyzeSentimentWithScores(text);
                
                Console.WriteLine($"\nText: \"{text}\"");
                Console.WriteLine($"Predicted Sentiment: {sentiment}");
                Console.WriteLine($"  Positive: {scores["Positive"]:F3}");
                Console.WriteLine($"  Negative: {scores["Negative"]:F3}");
                Console.WriteLine($"  Neutral: {scores["Neutral"]:F3}");
            }

            // Load the model back
            Console.WriteLine("\n\nLoading model from checkpoint...");
            var loadedModel = await PretrainedModelFactory.LoadAsync(modelPath, tokenizer);
            Console.WriteLine($"✓ Loaded model: {loadedModel.Name}");
        }

        static async Task TextClassificationExample()
        {
            Console.WriteLine("Example 2: Text Classification");
            Console.WriteLine("===============================\n");

            // Create a simple tokenizer
            const string vocab = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?;:'\"-\n()[]{}@#$%&*+=/<>|\\~`";
            var tokenizer = new CharTokenizer(vocab);

            // Create a text classification model for topic classification
            var labels = new[] { "Technology", "Sports", "Politics", "Entertainment" };
            var classificationModel = PretrainedModelFactory.CreateClassificationModel(
                vocabSize: tokenizer.VocabSize,
                labels: labels,
                blockSize: 64,
                domain: DomainType.General,
                embedDim: 64,
                numLayers: 2,
                numHeads: 2,
                dropout: 0.1,
                seed: 42
            );

            Console.WriteLine($"Created model: {classificationModel.Name}");
            Console.WriteLine($"Description: {classificationModel.Description}");
            Console.WriteLine($"Labels: {string.Join(", ", classificationModel.Labels)}\n");

            // Save the model
            var modelPath = "classification-general.smnd";
            await PretrainedModelFactory.SaveAsync(classificationModel, modelPath);
            Console.WriteLine($"✓ Saved model to {modelPath}\n");

            // Test classification (Note: This is untrained, so results are random)
            Console.WriteLine("Testing text classification (untrained model - random results):");
            var testTexts = new[]
            {
                "The new smartphone features advanced AI capabilities.",
                "The team won the championship in overtime.",
                "Congress passed a new bill today.",
                "The movie won best picture at the awards ceremony."
            };

            foreach (var text in testTexts)
            {
                var label = classificationModel.Classify(text);
                var probs = classificationModel.ClassifyWithProbabilities(text);
                
                Console.WriteLine($"\nText: \"{text}\"");
                Console.WriteLine($"Predicted Category: {label}");
                foreach (var kvp in probs)
                {
                    Console.WriteLine($"  {kvp.Key}: {kvp.Value:F3}");
                }
            }

            // Load the model back
            Console.WriteLine("\n\nLoading model from checkpoint...");
            var loadedModel = await PretrainedModelFactory.LoadAsync(modelPath, tokenizer);
            Console.WriteLine($"✓ Loaded model: {loadedModel.Name}");
        }

        static async Task DomainSpecificExample()
        {
            Console.WriteLine("Example 3: Domain-Specific Sentiment Analysis (Finance)");
            Console.WriteLine("=========================================================\n");

            // Create a simple tokenizer
            const string vocab = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 .,!?;:'\"-\n()[]{}@#$%&*+=/<>|\\~`";
            var tokenizer = new CharTokenizer(vocab);

            // Create a finance-specific sentiment analysis model
            var financeModel = PretrainedModelFactory.CreateSentimentModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 64,
                domain: DomainType.Finance,
                embedDim: 64,
                numLayers: 2,
                numHeads: 2,
                dropout: 0.1,
                seed: 42
            );

            Console.WriteLine($"Created model: {financeModel.Name}");
            Console.WriteLine($"Description: {financeModel.Description}");
            Console.WriteLine($"Domain: {financeModel.Domain}\n");

            // Save the model
            var modelPath = "sentiment-finance.smnd";
            await PretrainedModelFactory.SaveAsync(financeModel, modelPath);
            Console.WriteLine($"✓ Saved model to {modelPath}\n");

            // Test with financial texts
            Console.WriteLine("Testing finance sentiment analysis (untrained - random results):");
            var financialTexts = new[]
            {
                "Stock prices surged after positive earnings report.",
                "Market crashed due to economic uncertainty.",
                "Investors remain cautious about future outlook."
            };

            foreach (var text in financialTexts)
            {
                var sentiment = financeModel.AnalyzeSentiment(text);
                var scores = financeModel.AnalyzeSentimentWithScores(text);
                
                Console.WriteLine($"\nText: \"{text}\"");
                Console.WriteLine($"Predicted Sentiment: {sentiment}");
                Console.WriteLine($"  Positive: {scores["Positive"]:F3}");
                Console.WriteLine($"  Negative: {scores["Negative"]:F3}");
                Console.WriteLine($"  Neutral: {scores["Neutral"]:F3}");
            }

            Console.WriteLine("\n\nNote: These models are UNTRAINED and produce random predictions.");
            Console.WriteLine("In a production scenario, you would train these models on labeled data");
            Console.WriteLine("before using them for real sentiment analysis or classification tasks.");
        }
    }
}

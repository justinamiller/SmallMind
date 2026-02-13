using SmallMind.Runtime.PretrainedModels;
using SmallMind.Tokenizers;

namespace SmallMind.Tests.PretrainedModels
{
    public class PretrainedModelFactoryTests
    {
        private const string TestVocab = "abcdefghijklmnopqrstuvwxyz ";

        [Fact]
        public void CreateSentimentModel_CreatesValidModel()
        {
            // Arrange
            var tokenizer = new CharTokenizer(TestVocab);

            // Act
            var model = PretrainedModelFactory.CreateSentimentModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 32,
                domain: DomainType.General,
                embedDim: 32,
                numLayers: 2,
                numHeads: 2,
                seed: 42
            );

            // Assert
            Assert.NotNull(model);
            Assert.Equal(TaskType.SentimentAnalysis, model.Task);
            Assert.Equal(DomainType.General, model.Domain);
            Assert.Contains("SentimentAnalysis", model.Name);
        }

        [Fact]
        public void CreateClassificationModel_CreatesValidModel()
        {
            // Arrange
            var tokenizer = new CharTokenizer(TestVocab);
            var labels = new[] { "Cat1", "Cat2", "Cat3" };

            // Act
            var model = PretrainedModelFactory.CreateClassificationModel(
                vocabSize: tokenizer.VocabSize,
                labels: labels,
                blockSize: 32,
                domain: DomainType.General,
                embedDim: 32,
                numLayers: 2,
                numHeads: 2,
                seed: 42
            );

            // Assert
            Assert.NotNull(model);
            Assert.Equal(TaskType.TextClassification, model.Task);
            Assert.Equal(DomainType.General, model.Domain);
            Assert.Equal(3, model.Labels.Count);
            Assert.Contains("Cat1", model.Labels);
        }

        [Fact]
        public async Task SaveAndLoad_SentimentModel_PreservesMetadata()
        {
            // Arrange
            var tokenizer = new CharTokenizer(TestVocab);
            var model = PretrainedModelFactory.CreateSentimentModel(
                vocabSize: tokenizer.VocabSize,
                domain: DomainType.Finance,
                blockSize: 32,
                embedDim: 32,
                numLayers: 2,
                numHeads: 2
            );

            var tempFile = Path.GetTempFileName();
            try
            {
                // Act - Save
                await PretrainedModelFactory.SaveAsync(model, tempFile);

                // Act - Load
                var loaded = await PretrainedModelFactory.LoadAsync(tempFile, tokenizer);

                // Assert
                Assert.NotNull(loaded);
                Assert.Equal(TaskType.SentimentAnalysis, loaded.Task);
                Assert.Equal(DomainType.Finance, loaded.Domain);
                Assert.Equal(model.Name, loaded.Name);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public async Task SaveAndLoad_ClassificationModel_PreservesLabels()
        {
            // Arrange
            var tokenizer = new CharTokenizer(TestVocab);
            var labels = new[] { "Label1", "Label2", "Label3" };
            var model = PretrainedModelFactory.CreateClassificationModel(
                vocabSize: tokenizer.VocabSize,
                labels: labels,
                domain: DomainType.Legal,
                blockSize: 32,
                embedDim: 32,
                numLayers: 2,
                numHeads: 2
            );

            var tempFile = Path.GetTempFileName();
            try
            {
                // Act - Save
                await PretrainedModelFactory.SaveAsync(model, tempFile);

                // Act - Load
                var loaded = await PretrainedModelFactory.LoadAsync(tempFile, tokenizer);

                // Assert
                Assert.NotNull(loaded);
                Assert.IsType<TextClassificationModel>(loaded);

                var classifier = (TextClassificationModel)loaded;
                Assert.Equal(TaskType.TextClassification, classifier.Task);
                Assert.Equal(DomainType.Legal, classifier.Domain);
                Assert.Equal(3, classifier.Labels.Count);
                Assert.Contains("Label1", classifier.Labels);
                Assert.Contains("Label2", classifier.Labels);
                Assert.Contains("Label3", classifier.Labels);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [Fact]
        public void SentimentModel_AnalyzeSentiment_ReturnsValidLabel()
        {
            // Arrange
            var tokenizer = new CharTokenizer(TestVocab);
            var model = PretrainedModelFactory.CreateSentimentModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 32,
                embedDim: 32,
                numLayers: 2,
                numHeads: 2
            );

            // Act
            var sentiment = model.AnalyzeSentiment("this is a test");

            // Assert
            Assert.NotNull(sentiment);
            Assert.Contains(sentiment, new[] { "Positive", "Negative", "Neutral" });
        }

        [Fact]
        public void SentimentModel_AnalyzeSentimentWithScores_ReturnsProbabilities()
        {
            // Arrange
            var tokenizer = new CharTokenizer(TestVocab);
            var model = PretrainedModelFactory.CreateSentimentModel(
                vocabSize: tokenizer.VocabSize,
                blockSize: 32,
                embedDim: 32,
                numLayers: 2,
                numHeads: 2
            );

            // Act
            var scores = model.AnalyzeSentimentWithScores("this is a test");

            // Assert
            Assert.NotNull(scores);
            Assert.Equal(3, scores.Count);
            Assert.True(scores.ContainsKey("Positive"));
            Assert.True(scores.ContainsKey("Negative"));
            Assert.True(scores.ContainsKey("Neutral"));

            // Probabilities should sum to approximately 1.0
            var sum = scores["Positive"] + scores["Negative"] + scores["Neutral"];
            Assert.InRange(sum, 0.99f, 1.01f);

            // All probabilities should be between 0 and 1
            Assert.InRange(scores["Positive"], 0f, 1f);
            Assert.InRange(scores["Negative"], 0f, 1f);
            Assert.InRange(scores["Neutral"], 0f, 1f);
        }

        [Fact]
        public void ClassificationModel_Classify_ReturnsValidLabel()
        {
            // Arrange
            var tokenizer = new CharTokenizer(TestVocab);
            var labels = new[] { "Cat1", "Cat2", "Cat3" };
            var model = PretrainedModelFactory.CreateClassificationModel(
                vocabSize: tokenizer.VocabSize,
                labels: labels,
                blockSize: 32,
                embedDim: 32,
                numLayers: 2,
                numHeads: 2
            );

            // Act
            var category = model.Classify("this is a test");

            // Assert
            Assert.NotNull(category);
            Assert.Contains(category, labels);
        }

        [Fact]
        public void ClassificationModel_ClassifyWithProbabilities_ReturnsProbabilities()
        {
            // Arrange
            var tokenizer = new CharTokenizer(TestVocab);
            var labels = new[] { "Cat1", "Cat2", "Cat3" };
            var model = PretrainedModelFactory.CreateClassificationModel(
                vocabSize: tokenizer.VocabSize,
                labels: labels,
                blockSize: 32,
                embedDim: 32,
                numLayers: 2,
                numHeads: 2
            );

            // Act
            var probs = model.ClassifyWithProbabilities("this is a test");

            // Assert
            Assert.NotNull(probs);
            Assert.Equal(labels.Length, probs.Count);

            // All labels should be present
            foreach (var label in labels)
            {
                Assert.True(probs.ContainsKey(label));
                Assert.InRange(probs[label], 0f, 1f);
            }

            // Probabilities should sum to approximately 1.0
            var sum = 0f;
            foreach (var prob in probs.Values)
            {
                sum += prob;
            }
            Assert.InRange(sum, 0.99f, 1.01f);
        }
    }
}

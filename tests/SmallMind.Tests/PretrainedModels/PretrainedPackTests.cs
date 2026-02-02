using System;
using System.IO;
using Xunit;
using SmallMind.Runtime.PretrainedModels;

namespace SmallMind.Tests.PretrainedModels
{
    public class PretrainedPackTests
    {
        private static string GetProjectRoot()
        {
            // Navigate up from bin/Debug/net10.0 to the project root
            var currentDir = Directory.GetCurrentDirectory();
            var dirInfo = new DirectoryInfo(currentDir);
            
            // Go up until we find the solution directory (contains data/ and src/)
            while (dirInfo != null && !Directory.Exists(Path.Combine(dirInfo.FullName, "data", "pretrained")))
            {
                dirInfo = dirInfo.Parent;
            }
            
            if (dirInfo == null)
            {
                throw new DirectoryNotFoundException("Could not find project root with data/pretrained directory");
            }
            
            return dirInfo.FullName;
        }

        private static string TestDataPath => Path.Combine(GetProjectRoot(), "data", "pretrained");

        [Fact]
        public void LoadRegistry_ValidPath_LoadsSuccessfully()
        {
            // Arrange
            var registryPath = Path.Combine(TestDataPath, "registry.json");
            
            // Act
            var registry = PretrainedRegistry.Load(registryPath);
            
            // Assert
            Assert.NotNull(registry);
            Assert.NotEmpty(registry.Packs);
            Assert.True(registry.Packs.Count >= 3, "Expected at least 3 packs");
        }

        [Fact]
        public void LoadRegistry_FindsPacks_ByIdCorrectly()
        {
            // Arrange
            var registryPath = Path.Combine(TestDataPath, "registry.json");
            var registry = PretrainedRegistry.Load(registryPath);
            
            // Act
            var sentimentPack = registry.FindPack("sm.pretrained.sentiment.v1");
            var classificationPack = registry.FindPack("sm.pretrained.classification.v1");
            var financePack = registry.FindPack("sm.pretrained.finance.v1");
            
            // Assert
            Assert.NotNull(sentimentPack);
            Assert.Equal("sentiment", sentimentPack.Domain);
            Assert.NotNull(classificationPack);
            Assert.Equal("classification", classificationPack.Domain);
            Assert.NotNull(financePack);
            Assert.Equal("finance", financePack.Domain);
            Assert.True(financePack.RagEnabled);
        }

        [Fact]
        public void LoadPack_SentimentPack_LoadsCorrectly()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "sentiment");
            
            // Act
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack);
            Assert.Equal("sm.pretrained.sentiment.v1", pack.Manifest.Id);
            Assert.Equal("sentiment", pack.Manifest.Domain);
            Assert.NotEmpty(pack.Samples);
            Assert.Equal(30, pack.Samples.Count);
            Assert.NotEmpty(pack.EvaluationLabels);
            Assert.Contains("positive", pack.Categories);
            Assert.Contains("negative", pack.Categories);
            Assert.Contains("neutral", pack.Categories);
        }

        [Fact]
        public void LoadPack_ClassificationPack_LoadsCorrectly()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "classification");
            
            // Act
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack);
            Assert.Equal("sm.pretrained.classification.v1", pack.Manifest.Id);
            Assert.Equal("classification", pack.Manifest.Domain);
            Assert.NotEmpty(pack.Samples);
            Assert.Equal(30, pack.Samples.Count);
            Assert.Equal(4, pack.Categories.Count);
            Assert.Contains("Technology", pack.Categories);
            Assert.Contains("Sports", pack.Categories);
            Assert.Contains("Politics", pack.Categories);
            Assert.Contains("Entertainment", pack.Categories);
        }

        [Fact]
        public void LoadPack_FinancePack_LoadsRagDocuments()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "finance");
            
            // Act
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack);
            Assert.Equal("sm.pretrained.finance.v1", pack.Manifest.Id);
            Assert.True(pack.Manifest.Rag?.Enabled);
            Assert.NotEmpty(pack.RagDocumentPaths);
            Assert.Equal(5, pack.RagDocumentPaths.Count);
            
            // Verify all RAG documents exist
            foreach (var docPath in pack.RagDocumentPaths)
            {
                Assert.True(File.Exists(docPath), $"RAG document not found: {docPath}");
            }
        }

        [Fact]
        public void LoadFromJsonl_ValidFile_LoadsSamples()
        {
            // Arrange
            var jsonlPath = Path.Combine(TestDataPath, "sentiment", "task", "inputs.jsonl");
            
            // Act
            var samples = DatasetLoader.LoadFromJsonl(jsonlPath);
            
            // Assert
            Assert.NotEmpty(samples);
            Assert.Equal(30, samples.Count);
            
            // Verify first sample has all fields
            var firstSample = samples[0];
            Assert.NotNull(firstSample.Id);
            Assert.NotEmpty(firstSample.Id);
            Assert.NotNull(firstSample.Task);
            Assert.NotEmpty(firstSample.Text);
            Assert.NotEmpty(firstSample.Label);
        }

        [Fact]
        public void LoadFromJsonl_WithExpectedLabels_ValidatesSamples()
        {
            // Arrange
            var jsonlPath = Path.Combine(TestDataPath, "sentiment", "task", "inputs.jsonl");
            var expectedLabels = new[] { "positive", "negative", "neutral" };
            
            // Act
            var samples = DatasetLoader.LoadFromJsonl(jsonlPath, expectedLabels);
            
            // Assert
            Assert.NotEmpty(samples);
            
            // Verify all samples have valid labels
            foreach (var sample in samples)
            {
                Assert.Contains(sample.Label, expectedLabels, StringComparer.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void LoadLabeledData_AutoDetectsJsonl_ByExtension()
        {
            // Arrange
            var jsonlPath = Path.Combine(TestDataPath, "sentiment", "task", "inputs.jsonl");
            
            // Act
            var samples = DatasetLoader.LoadLabeledData(jsonlPath);
            
            // Assert
            Assert.NotEmpty(samples);
            Assert.Equal(30, samples.Count);
        }

        [Fact]
        public void LoadLabeledData_LegacyFormat_StillWorks()
        {
            // Arrange
            var legacyPath = Path.Combine(TestDataPath, "sentiment", "sample-sentiment.txt");
            
            // Act
            var samples = DatasetLoader.LoadLabeledData(legacyPath);
            
            // Assert
            Assert.NotEmpty(samples);
            // Legacy files should still load
            Assert.True(samples.Count > 0);
        }

        [Fact]
        public void PackManifest_HasRequiredFields()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "sentiment");
            var pack = PretrainedPack.Load(packPath);
            
            // Assert
            Assert.NotNull(pack.Manifest.Id);
            Assert.NotEmpty(pack.Manifest.Id);
            Assert.NotNull(pack.Manifest.Domain);
            Assert.NotEmpty(pack.Manifest.Domain);
            Assert.NotNull(pack.Manifest.IntendedUse);
            Assert.NotEmpty(pack.Manifest.IntendedUse);
            Assert.NotNull(pack.Manifest.Source);
            Assert.NotEmpty(pack.Manifest.Source.License);
        }

        [Fact]
        public void GetSummary_ReturnsFormattedSummary()
        {
            // Arrange
            var packPath = Path.Combine(TestDataPath, "sentiment");
            var pack = PretrainedPack.Load(packPath);
            
            // Act
            var summary = pack.GetSummary();
            
            // Assert
            Assert.NotNull(summary);
            Assert.Contains(pack.Manifest.Id, summary);
            Assert.Contains(pack.Manifest.Domain, summary);
            Assert.Contains(pack.Samples.Count.ToString(), summary);
        }
    }
}

using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace SmallMind.ModelRegistry.Tests
{
    /// <summary>
    /// Tests for ModelRegistry.
    /// </summary>
    public class ModelRegistryTests : IDisposable
    {
        private readonly string _tempCacheDir;

        public ModelRegistryTests()
        {
            _tempCacheDir = Path.Combine(Path.GetTempPath(), "smallmind-test-" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_tempCacheDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempCacheDir))
            {
                Directory.Delete(_tempCacheDir, recursive: true);
            }
        }

        [Fact]
        public async Task AddModelAsync_FromLocalFile_CreatesManifestAndCopiesFile()
        {
            // Arrange
            string sourceFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(sourceFile, "test model content");
                var registry = new ModelRegistry(_tempCacheDir);

                // Act
                string modelId = await registry.AddModelAsync(sourceFile, modelId: "test-model");

                // Assert
                Assert.Equal("test-model", modelId);

                var manifest = registry.GetManifest(modelId);
                Assert.NotNull(manifest);
                Assert.Equal("test-model", manifest.ModelId);
                Assert.Single(manifest.Files);
                
                string? modelFilePath = registry.GetModelFilePath(modelId);
                Assert.NotNull(modelFilePath);
                Assert.True(File.Exists(modelFilePath));
            }
            finally
            {
                if (File.Exists(sourceFile))
                {
                    File.Delete(sourceFile);
                }
            }
        }

        [Fact]
        public async Task AddModelAsync_WithoutModelId_GeneratesId()
        {
            // Arrange
            string sourceFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(sourceFile, "test content");
                var registry = new ModelRegistry(_tempCacheDir);

                // Act
                string modelId = await registry.AddModelAsync(sourceFile);

                // Assert
                Assert.NotNull(modelId);
                Assert.NotEmpty(modelId);

                var manifest = registry.GetManifest(modelId);
                Assert.NotNull(manifest);
            }
            finally
            {
                if (File.Exists(sourceFile))
                {
                    File.Delete(sourceFile);
                }
            }
        }

        [Fact]
        public async Task VerifyModel_WithValidModel_ReturnsTrue()
        {
            // Arrange
            string sourceFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(sourceFile, "test content");
                var registry = new ModelRegistry(_tempCacheDir);
                string modelId = await registry.AddModelAsync(sourceFile, modelId: "verify-test");

                // Act
                var result = registry.VerifyModel(modelId);

                // Assert
                Assert.True(result.IsValid);
                Assert.Empty(result.Errors);
            }
            finally
            {
                if (File.Exists(sourceFile))
                {
                    File.Delete(sourceFile);
                }
            }
        }

        [Fact]
        public void VerifyModel_WithNonExistentModel_ReturnsFalse()
        {
            // Arrange
            var registry = new ModelRegistry(_tempCacheDir);

            // Act
            var result = registry.VerifyModel("non-existent-model");

            // Assert
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public async Task VerifyModel_WithCorruptedFile_ReturnsFalse()
        {
            // Arrange
            string sourceFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(sourceFile, "original content");
                var registry = new ModelRegistry(_tempCacheDir);
                string modelId = await registry.AddModelAsync(sourceFile, modelId: "corrupt-test");

                // Corrupt the file
                string modelFilePath = registry.GetModelFilePath(modelId)!;
                File.WriteAllText(modelFilePath, "corrupted content");

                // Act
                var result = registry.VerifyModel(modelId);

                // Assert
                Assert.False(result.IsValid);
                Assert.Contains(result.Errors, e => e.Contains("SHA256"));
            }
            finally
            {
                if (File.Exists(sourceFile))
                {
                    File.Delete(sourceFile);
                }
            }
        }

        [Fact]
        public async Task ListModels_WithMultipleModels_ReturnsAllModels()
        {
            // Arrange
            var registry = new ModelRegistry(_tempCacheDir);
            
            string file1 = Path.GetTempFileName();
            string file2 = Path.GetTempFileName();
            
            try
            {
                File.WriteAllText(file1, "model 1");
                File.WriteAllText(file2, "model 2");

                await registry.AddModelAsync(file1, modelId: "model-1");
                await registry.AddModelAsync(file2, modelId: "model-2");

                // Act
                var models = registry.ListModels();

                // Assert
                Assert.Equal(2, models.Count);
                Assert.Contains(models, m => m.ModelId == "model-1");
                Assert.Contains(models, m => m.ModelId == "model-2");
            }
            finally
            {
                if (File.Exists(file1)) File.Delete(file1);
                if (File.Exists(file2)) File.Delete(file2);
            }
        }

        [Fact]
        public void ListModels_WithEmptyCache_ReturnsEmptyList()
        {
            // Arrange
            var registry = new ModelRegistry(_tempCacheDir);

            // Act
            var models = registry.ListModels();

            // Assert
            Assert.Empty(models);
        }

        [Fact]
        public async Task ManifestSerialization_RoundTrip_PreservesData()
        {
            // Arrange
            string sourceFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(sourceFile, "test");
                var registry = new ModelRegistry(_tempCacheDir);
                
                // Act
                string modelId = await registry.AddModelAsync(
                    sourceFile, 
                    modelId: "roundtrip-test", 
                    displayName: "Roundtrip Test Model");

                var manifest = registry.GetManifest(modelId);

                // Assert
                Assert.NotNull(manifest);
                Assert.Equal("roundtrip-test", manifest.ModelId);
                Assert.Equal("Roundtrip Test Model", manifest.DisplayName);
                Assert.NotNull(manifest.CreatedUtc);
                Assert.Single(manifest.Files);
                Assert.NotEmpty(manifest.Files[0].Sha256);
            }
            finally
            {
                if (File.Exists(sourceFile))
                {
                    File.Delete(sourceFile);
                }
            }
        }
    }
}

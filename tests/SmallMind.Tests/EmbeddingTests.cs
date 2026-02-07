using System;
using System.Collections.Generic;
using Xunit;
using SmallMind.Rag.Retrieval;
using SmallMind.Rag.Indexing;
using SmallMind.Rag;

namespace SmallMind.Tests
{
    /// <summary>
    /// Tests for TF-IDF embedding provider and vector index.
    /// </summary>
    public class EmbeddingTests
    {
        [Fact]
        public void TfidfEmbedding_CreatesVectors()
        {
            // Arrange
            var documents = new List<string>
            {
                "The quick brown fox jumps over the lazy dog",
                "A fast brown animal leaps across a sleepy canine",
                "Machine learning is a subset of artificial intelligence"
            };

            var embedder = new TfidfEmbeddingProvider(maxFeatures: 128);
            embedder.Fit(documents);

            // Act
            var embedding1 = embedder.Embed("quick fox");
            var embedding2 = embedder.Embed("machine learning");

            // Assert
            Assert.NotNull(embedding1);
            Assert.NotNull(embedding2);
            Assert.Equal(128, embedding1.Length);
            Assert.Equal(128, embedding2.Length);
        }

        [Fact]
        public void VectorIndex_AddsAndSearches()
        {
            // Arrange
            var documents = new List<string>
            {
                "The quick brown fox jumps over the lazy dog",
                "A fast brown animal leaps across a sleepy canine",
                "Machine learning is a subset of artificial intelligence",
                "Neural networks are inspired by biological neurons",
                "Deep learning uses multiple layers of neural networks"
            };

            var embedder = new TfidfEmbeddingProvider(maxFeatures: 128);
            embedder.Fit(documents);

            var testDir = Path.Combine(Path.GetTempPath(), "test_index_" + Guid.NewGuid().ToString("N"));
            var index = new VectorIndex(embedder, indexDirectory: testDir);

            // Act
            index.AddBatch(documents);
            var results = index.Search("neural networks", k: 2);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.True(results[0].Score > 0);
            Assert.Contains("neural", results[0].Text.ToLower());
        }

        [Fact]
        public void VectorIndex_SaveAndLoad()
        {
            // Arrange
            var documents = new List<string>
            {
                "Document one about cats",
                "Document two about dogs"
            };

            var embedder = new TfidfEmbeddingProvider(maxFeatures: 64);
            embedder.Fit(documents);

            var indexPath = Path.Combine(Path.GetTempPath(), "test_index_save_load_" + Guid.NewGuid().ToString("N"));
            var index = new VectorIndex(embedder, indexDirectory: indexPath);

            // Act
            index.AddBatch(documents);
            index.Save();

            var index2 = new VectorIndex(embedder, indexDirectory: indexPath);
            index2.Load();

            // Assert
            Assert.Equal(2, index2.Count);
        }

    }
}

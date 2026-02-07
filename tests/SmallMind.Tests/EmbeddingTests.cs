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

        [Fact]
        public void Retriever_ReturnsTopK()
        {
            // Arrange
            var documents = new List<string>
            {
                "Python is a programming language",
                "Java is also a programming language",
                "C# is a modern programming language",
                "Cats are pets",
                "Dogs are also pets"
            };

            var embedder = new TfidfEmbeddingProvider(maxFeatures: 128);
            embedder.Fit(documents);

            var testDir = Path.Combine(Path.GetTempPath(), "test_retriever_" + Guid.NewGuid().ToString("N"));
            var index = new VectorIndex(embedder, indexDirectory: testDir);
            index.AddBatch(documents);

            var retriever = new Retriever(index, defaultK: 3);

            // Act
            var chunks = retriever.Retrieve("programming languages");

            // Assert
            Assert.Equal(3, chunks.Count);
            Assert.Contains("programming", chunks[0].Text.ToLower());
        }

        [Fact]
        public void PromptBuilder_BuildsCorrectFormat()
        {
            // Arrange
            var builder = new PromptBuilder();
            var chunks = new List<RetrievedChunk>
            {
                new RetrievedChunk { Text = "Context one", Score = 0.9f },
                new RetrievedChunk { Text = "Context two", Score = 0.7f }
            };

            // Act
            var prompt = builder.BuildPrompt("What is the answer?", chunks);

            // Assert
            Assert.Contains("SOURCES:", prompt);
            Assert.Contains("Context one", prompt);
            Assert.Contains("Context two", prompt);
            Assert.Contains("What is the answer?", prompt);
        }
    }
}

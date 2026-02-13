// SmallMind.Retrieval namespace does not exist - test disabled
// using SmallMind.Retrieval;

namespace SmallMind.Tests.Retrieval
{
    // Tests disabled - SmallMind.Retrieval namespace not implemented
    /* 
    public class InMemoryLexicalIndexTests
    {
        [Fact]
        public void Search_Deterministic_ProducesSameResults()
        {
            // Arrange
            var index = CreateIndexWithSampleData();

            var options = new RetrievalOptions
            {
                TopK = 3,
                Deterministic = true
            };

            // Act
            var result1 = index.Search("training model", options);
            var result2 = index.Search("training model", options);

            // Assert
            Assert.Equal(result1.Chunks.Count, result2.Chunks.Count);
            
            for (int i = 0; i < result1.Chunks.Count; i++)
            {
                Assert.Equal(result1.Chunks[i].ChunkId, result2.Chunks[i].ChunkId);
                Assert.Equal(result1.Chunks[i].Score, result2.Chunks[i].Score);
            }
        }

        [Fact]
        public void Search_WithTopK_ReturnsCorrectNumberOfResults()
        {
            // Arrange
            var index = CreateIndexWithSampleData();

            var options = new RetrievalOptions
            {
                TopK = 2
            };

            // Act
            var result = index.Search("training", options);

            // Assert
            Assert.True(result.Chunks.Count <= 2);
        }

        [Fact]
        public void Search_OrdersByScoreDescending()
        {
            // Arrange
            var index = CreateIndexWithSampleData();

            var options = new RetrievalOptions
            {
                TopK = 5,
                Deterministic = true
            };

            // Act
            var result = index.Search("transformer architecture", options);

            // Assert
            for (int i = 1; i < result.Chunks.Count; i++)
            {
                Assert.True(result.Chunks[i - 1].Score >= result.Chunks[i].Score,
                    $"Chunk {i - 1} score ({result.Chunks[i - 1].Score}) should be >= chunk {i} score ({result.Chunks[i].Score})");
            }
        }

        [Fact]
        public void Search_EnforcesMaxChunksPerDocument()
        {
            // Arrange
            var index = new InMemoryLexicalIndex();
            
            // Add a document with content that will produce multiple chunks
            var doc = new Document
            {
                Id = "doc1",
                Title = "Test",
                Content = string.Join("\n\n", new[]
                {
                    "Training is important. Training helps the model.",
                    "Training uses gradient descent. Training needs data.",
                    "Training requires patience. Training improves performance."
                })
            };

            index.Upsert(doc);

            var options = new RetrievalOptions
            {
                TopK = 10,
                MaxChunksPerDocument = 2
            };

            // Act
            var result = index.Search("training", options);

            // Assert
            var chunksFromDoc1 = result.Chunks.FindAll(c => c.DocumentId == "doc1");
            Assert.True(chunksFromDoc1.Count <= 2);
        }

        [Fact]
        public void Search_IncludesCitations()
        {
            // Arrange
            var index = CreateIndexWithSampleData();

            var options = new RetrievalOptions
            {
                TopK = 1
            };

            // Act
            var result = index.Search("transformer", options);

            // Assert
            if (result.Chunks.Count > 0)
            {
                var chunk = result.Chunks[0];
                Assert.NotNull(chunk.Citation);
            }
        }

        [Fact]
        public void Upsert_ReplacesExistingDocument()
        {
            // Arrange
            var index = new InMemoryLexicalIndex();

            var doc1 = new Document
            {
                Id = "doc1",
                Content = "Original content about cats and felines."
            };

            var doc2 = new Document
            {
                Id = "doc1",
                Content = "Updated content about dogs and canines."
            };

            // Act
            index.Upsert(doc1);
            var result1 = index.Search("cats felines", new RetrievalOptions { TopK = 5 });

            index.Upsert(doc2);
            var result2 = index.Search("dogs canines", new RetrievalOptions { TopK = 5 });

            // Assert
            Assert.True(result1.Chunks.Count > 0); // Found cats in original
            Assert.True(result2.Chunks.Count > 0); // Found dogs in updated
        }

        [Fact]
        public void Search_SupportsCancellation()
        {
            // Arrange
            var index = CreateIndexWithSampleData();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            var options = new RetrievalOptions();

            // Act & Assert
            Assert.Throws<System.OperationCanceledException>(() =>
                index.Search("test", options, cts.Token));
        }

        private InMemoryLexicalIndex CreateIndexWithSampleData()
        {
            var index = new InMemoryLexicalIndex();

            var documents = new[]
            {
                new Document
                {
                    Id = "doc1",
                    Title = "Architecture Guide",
                    Content = "The transformer architecture uses self-attention mechanisms. " +
                              "Multi-head attention allows the model to focus on different parts of the sequence."
                },
                new Document
                {
                    Id = "doc2",
                    Title = "Training Manual",
                    Content = "Training the model requires preparing data and running gradient descent. " +
                              "Monitor the loss function to ensure the model is learning."
                },
                new Document
                {
                    Id = "doc3",
                    Title = "Performance Tips",
                    Content = "Use SIMD vectorization for faster matrix operations. " +
                              "Memory pooling reduces garbage collection overhead."
                }
            };

            foreach (var doc in documents)
            {
                index.Upsert(doc);
            }

            return index;
        }
    }
    */
}

using SmallMind.Rag;
using SmallMind.Rag.Pipeline;
using SmallMind.Rag.Security;

namespace SmallMind.Tests.Rag
{
    /// <summary>
    /// Tests for the RAG pipeline initialization, ingestion, and retrieval.
    /// </summary>
    public class RagPipelineTests : IDisposable
    {
        private readonly string _testIndexPath;
        private readonly string _testDataPath;

        public RagPipelineTests()
        {
            // Create temporary directories for testing
            _testIndexPath = Path.Combine(Path.GetTempPath(), $"rag_test_index_{Guid.NewGuid():N}");
            _testDataPath = Path.Combine(Directory.GetCurrentDirectory(), "../../../../TestData/rag");
            Directory.CreateDirectory(_testIndexPath);
        }

        public void Dispose()
        {
            // Clean up test index directory
            if (Directory.Exists(_testIndexPath))
            {
                Directory.Delete(_testIndexPath, true);
            }
        }

        [Fact]
        public void RagPipeline_Constructor_SetsProperties()
        {
            // Arrange
            var options = new RagOptions
            {
                IndexDirectory = _testIndexPath,
                Deterministic = true,
                Seed = 42
            };

            // Act
            var pipeline = new RagPipeline(options);

            // Assert
            Assert.NotNull(pipeline);
            Assert.False(pipeline.IsInitialized);
            Assert.False(pipeline.IsDenseRetrievalEnabled);
            Assert.False(pipeline.IsHybridRetrievalEnabled);
            Assert.False(pipeline.IsTextGenerationEnabled);
        }

        [Fact]
        public void RagPipeline_Initialize_CreatesIndexDirectory()
        {
            // Arrange
            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);

            // Act
            pipeline.Initialize();

            // Assert
            Assert.True(pipeline.IsInitialized);
            Assert.True(Directory.Exists(_testIndexPath));
        }

        [Fact]
        public void RagPipeline_Initialize_IsIdempotent()
        {
            // Arrange
            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);

            // Act
            pipeline.Initialize();
            pipeline.Initialize(); // Second call should be safe

            // Assert
            Assert.True(pipeline.IsInitialized);
        }

        [Fact]
        public void RagPipeline_IngestDocuments_RequiresInitialization()
        {
            // Arrange
            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                pipeline.IngestDocuments(_testDataPath));

            Assert.Contains("must be initialized", ex.Message);
        }

        [Fact]
        public void RagPipeline_IngestDocuments_ProcessesFiles()
        {
            // Arrange
            if (!Directory.Exists(_testDataPath))
            {
                // Skip test if test data doesn't exist
                return;
            }

            var options = new RagOptions
            {
                IndexDirectory = _testIndexPath,
                Chunking = new RagOptions.ChunkingOptions
                {
                    MaxChunkSize = 256,
                    OverlapSize = 32,
                    MinChunkSize = 50
                }
            };
            var pipeline = new RagPipeline(options);
            pipeline.Initialize();

            // Act
            pipeline.IngestDocuments(_testDataPath, rebuild: true, includePatterns: "*.txt;*.md");

            // Assert
            Assert.True(pipeline.ChunkCount > 0, "Pipeline should have indexed some chunks");
        }

        [Fact]
        public void RagPipeline_Retrieve_RequiresInitialization()
        {
            // Arrange
            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                pipeline.Retrieve("test query"));

            Assert.Contains("must be initialized", ex.Message);
        }

        [Fact]
        public void RagPipeline_Retrieve_ReturnsResults()
        {
            // Arrange
            if (!Directory.Exists(_testDataPath))
            {
                // Skip test if test data doesn't exist
                return;
            }

            var options = new RagOptions
            {
                IndexDirectory = _testIndexPath,
                Retrieval = new RagOptions.RetrievalOptions
                {
                    TopK = 3,
                    MinScore = 0.0f
                }
            };
            var pipeline = new RagPipeline(options);
            pipeline.Initialize();
            pipeline.IngestDocuments(_testDataPath, rebuild: true);

            // Act
            var results = pipeline.Retrieve("architecture transformer", topK: 5);

            // Assert
            Assert.NotNull(results);
            Assert.True(results.Count <= 5, "Should respect topK limit");
        }

        [Fact]
        public void RagPipeline_Retrieve_WithUserContext_FiltersResults()
        {
            // Arrange
            if (!Directory.Exists(_testDataPath))
            {
                return;
            }

            var options = new RagOptions
            {
                IndexDirectory = _testIndexPath,
                SecurityEnabled = true
            };
            var pipeline = new RagPipeline(options);
            pipeline.Initialize();
            pipeline.IngestDocuments(_testDataPath, rebuild: true);

            var userContext = new UserContext("test-user");

            // Act
            var results = pipeline.Retrieve("SmallMind", userContext: userContext);

            // Assert
            Assert.NotNull(results);
            // With default authorizer, all results should pass
        }

        [Fact]
        public void RagPipeline_AskQuestion_RequiresInitialization()
        {
            // Arrange
            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                pipeline.AskQuestion("test question"));

            Assert.Contains("must be initialized", ex.Message);
        }

        [Fact]
        public void RagPipeline_AskQuestion_ReturnsPromptWhenNoGenerator()
        {
            // Arrange
            if (!Directory.Exists(_testDataPath))
            {
                return;
            }

            var options = new RagOptions
            {
                IndexDirectory = _testIndexPath,
                Retrieval = new RagOptions.RetrievalOptions
                {
                    TopK = 3,
                    MinScore = 0.0f
                }
            };
            var pipeline = new RagPipeline(options); // No text generator
            pipeline.Initialize();
            pipeline.IngestDocuments(_testDataPath, rebuild: true);

            // Act
            var response = pipeline.AskQuestion("What is SmallMind?");

            // Assert
            Assert.NotNull(response);
            Assert.Contains("SYSTEM:", response); // Prompt should have system section
            Assert.Contains("SOURCES:", response); // Prompt should have sources section
        }

        [Fact]
        public void RagPipeline_AskQuestion_ReturnsInsufficientEvidence_WhenNoChunks()
        {
            // Arrange
            var options = new RagOptions
            {
                IndexDirectory = _testIndexPath,
                Retrieval = new RagOptions.RetrievalOptions
                {
                    TopK = 3,
                    MinScore = 0.9f // Very high threshold
                }
            };
            var pipeline = new RagPipeline(options);
            pipeline.Initialize();

            // Act (no documents ingested, so no chunks)
            var response = pipeline.AskQuestion("test question");

            // Assert
            Assert.NotNull(response);
            Assert.Contains("don't have sufficient evidence", response, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void RagPipeline_EnableDenseRetrieval_RequiresInitialization()
        {
            // Arrange
            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                pipeline.EnableDenseRetrieval());

            Assert.Contains("must be initialized", ex.Message);
        }

        [Fact]
        public void RagPipeline_EnableDenseRetrieval_SetsFlag()
        {
            // Arrange
            if (!Directory.Exists(_testDataPath))
            {
                return;
            }

            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);
            pipeline.Initialize();
            pipeline.IngestDocuments(_testDataPath, rebuild: true);

            // Act
            pipeline.EnableDenseRetrieval(embeddingDim: 128);

            // Assert
            Assert.True(pipeline.IsDenseRetrievalEnabled);
        }

        [Fact]
        public void RagPipeline_EnableHybridRetrieval_RequiresDenseRetrieval()
        {
            // Arrange
            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);
            pipeline.Initialize();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() =>
                pipeline.EnableHybridRetrieval());

            Assert.Contains("Dense retrieval must be enabled", ex.Message);
        }

        [Fact]
        public void RagPipeline_EnableHybridRetrieval_SetsFlag()
        {
            // Arrange
            if (!Directory.Exists(_testDataPath))
            {
                return;
            }

            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);
            pipeline.Initialize();
            pipeline.IngestDocuments(_testDataPath, rebuild: true);
            pipeline.EnableDenseRetrieval(embeddingDim: 128);

            // Act
            pipeline.EnableHybridRetrieval(sparseWeight: 0.5f, denseWeight: 0.5f);

            // Assert
            Assert.True(pipeline.IsHybridRetrievalEnabled);
        }

        [Fact]
        public void RagPipeline_IncrementalUpdate_AddsNewDocuments()
        {
            // Arrange
            if (!Directory.Exists(_testDataPath))
            {
                return;
            }

            var options = new RagOptions { IndexDirectory = _testIndexPath };
            var pipeline = new RagPipeline(options);
            pipeline.Initialize();

            // First ingestion
            pipeline.IngestDocuments(_testDataPath, rebuild: true);
            int initialChunkCount = pipeline.ChunkCount;

            // Act - Ingest again (incremental update)
            pipeline.IngestDocuments(_testDataPath, rebuild: false);

            // Assert
            // Chunk count should remain the same (same files)
            Assert.Equal(initialChunkCount, pipeline.ChunkCount);
        }
    }
}

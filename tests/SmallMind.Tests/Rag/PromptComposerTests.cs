using SmallMind.Rag;
using SmallMind.Rag.Prompting;

namespace SmallMind.Tests.Rag
{
    /// <summary>
    /// Tests for the PromptComposer class.
    /// </summary>
    public class PromptComposerTests
    {
        [Fact]
        public void PromptComposer_Constructor_RequiresOptions()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new PromptComposer(null!));
        }

        [Fact]
        public void PromptComposer_ComposePrompt_RequiresQuestion()
        {
            // Arrange
            var options = new RagOptions.RetrievalOptions();
            var composer = new PromptComposer(options);
            var chunks = new List<RetrievedChunk>();
            var chunkStore = new Dictionary<string, Chunk>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                composer.ComposePrompt(null!, chunks, chunkStore));
        }

        [Fact]
        public void PromptComposer_ComposePrompt_RequiresChunks()
        {
            // Arrange
            var options = new RagOptions.RetrievalOptions();
            var composer = new PromptComposer(options);
            var chunkStore = new Dictionary<string, Chunk>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                composer.ComposePrompt("test question", null!, chunkStore));
        }

        [Fact]
        public void PromptComposer_ComposePrompt_RequiresChunkStore()
        {
            // Arrange
            var options = new RagOptions.RetrievalOptions();
            var composer = new PromptComposer(options);
            var chunks = new List<RetrievedChunk>();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                composer.ComposePrompt("test question", chunks, null!));
        }

        [Fact]
        public void PromptComposer_ComposePrompt_IncludesSystemSection()
        {
            // Arrange
            var options = new RagOptions.RetrievalOptions();
            var composer = new PromptComposer(options);
            var chunks = new List<RetrievedChunk>();
            var chunkStore = new Dictionary<string, Chunk>();

            // Act
            var prompt = composer.ComposePrompt("What is SmallMind?", chunks, chunkStore);

            // Assert
            Assert.Contains("SYSTEM:", prompt);
        }

        [Fact]
        public void PromptComposer_ComposePrompt_IncludesUserQuestion()
        {
            // Arrange
            var options = new RagOptions.RetrievalOptions();
            var composer = new PromptComposer(options);
            var chunks = new List<RetrievedChunk>();
            var chunkStore = new Dictionary<string, Chunk>();
            string question = "What is SmallMind?";

            // Act
            var prompt = composer.ComposePrompt(question, chunks, chunkStore);

            // Assert
            Assert.Contains(question, prompt);
            Assert.Contains("USER QUESTION:", prompt);
        }

        [Fact]
        public void PromptComposer_ComposePrompt_IncludesSourcesSection()
        {
            // Arrange
            var options = new RagOptions.RetrievalOptions();
            var composer = new PromptComposer(options);
            var chunks = new List<RetrievedChunk>();
            var chunkStore = new Dictionary<string, Chunk>();

            // Act
            var prompt = composer.ComposePrompt("test question", chunks, chunkStore);

            // Assert
            Assert.Contains("SOURCES:", prompt);
        }

        [Fact]
        public void PromptComposer_ComposePrompt_IncludesInstructions()
        {
            // Arrange
            var options = new RagOptions.RetrievalOptions();
            var composer = new PromptComposer(options);
            var chunks = new List<RetrievedChunk>();
            var chunkStore = new Dictionary<string, Chunk>();

            // Act
            var prompt = composer.ComposePrompt("test question", chunks, chunkStore);

            // Assert
            Assert.Contains("INSTRUCTIONS:", prompt);
        }

        [Fact]
        public void PromptComposer_ComposePrompt_IncludesRetrievedChunks()
        {
            // Arrange
            var options = new RagOptions.RetrievalOptions();
            var composer = new PromptComposer(options);

            var chunk = new Chunk
            {
                ChunkId = "chunk1",
                DocId = "doc1",
                Title = "Test Document",
                SourceUri = "test://doc1",
                Text = "SmallMind is a pure C# language model.",
                CharStart = 0,
                CharEnd = 40
            };

            var chunkStore = new Dictionary<string, Chunk>
            {
                { "chunk1", chunk }
            };

            var retrievedChunks = new List<RetrievedChunk>
            {
                new RetrievedChunk("chunk1", "doc1", 0.95f, 0)
            };

            // Act
            var prompt = composer.ComposePrompt("What is SmallMind?", retrievedChunks, chunkStore);

            // Assert
            Assert.Contains("Test Document", prompt);
            Assert.Contains("SmallMind is a pure C# language model", prompt);
        }

        [Fact]
        public void PromptComposer_ComposePrompt_SkipsMissingChunks()
        {
            // Arrange
            var options = new RagOptions.RetrievalOptions();
            var composer = new PromptComposer(options);
            var chunkStore = new Dictionary<string, Chunk>();

            var retrievedChunks = new List<RetrievedChunk>
            {
                new RetrievedChunk("missing_chunk", "doc1", 0.95f, 0)
            };

            // Act
            var prompt = composer.ComposePrompt("test question", retrievedChunks, chunkStore);

            // Assert
            // Should not crash, should just skip the missing chunk
            Assert.NotNull(prompt);
        }
    }
}

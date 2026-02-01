using System.Collections.Generic;
using System.Linq;
using Xunit;
using SmallMind.Retrieval;

namespace SmallMind.Tests.Retrieval
{
    public class DocumentChunkerTests
    {
        [Fact]
        public void Chunk_WithSmallDocument_ReturnsOneChunk()
        {
            // Arrange
            var document = new Document
            {
                Id = "test-doc",
                Title = "Test",
                Content = "This is a short document."
            };

            var options = new ChunkingOptions
            {
                MaxChars = 500,
                OverlapChars = 50
            };

            // Act
            var chunks = DocumentChunker.Chunk(document, options);

            // Assert
            Assert.Single(chunks);
            Assert.Equal("test-doc_chunk_0", chunks[0].ChunkId);
            Assert.Equal("This is a short document.", chunks[0].Text);
        }

        [Fact]
        public void Chunk_WithLongDocument_ReturnsMultipleChunks()
        {
            // Arrange
            var content = string.Join(" ", Enumerable.Repeat("This is a sentence.", 100));
            var document = new Document
            {
                Id = "long-doc",
                Content = content
            };

            var options = new ChunkingOptions
            {
                MaxChars = 200,
                OverlapChars = 20,
                MinChunkChars = 50
            };

            // Act
            var chunks = DocumentChunker.Chunk(document, options);

            // Assert
            Assert.True(chunks.Count > 1);
            
            // Verify chunk IDs are sequential
            for (int i = 0; i < chunks.Count; i++)
            {
                Assert.Equal($"long-doc_chunk_{i}", chunks[i].ChunkId);
            }

            // Verify metadata
            Assert.All(chunks, chunk =>
            {
                Assert.NotNull(chunk.Metadata);
                Assert.True(chunk.Metadata.ContainsKey("chunk_index"));
            });
        }

        [Fact]
        public void Chunk_Deterministic_ProducesSameResults()
        {
            // Arrange
            var document = new Document
            {
                Id = "det-doc",
                Content = "Paragraph one has some text.\n\nParagraph two has more text. It continues here."
            };

            var options = new ChunkingOptions
            {
                MaxChars = 50,
                OverlapChars = 10
            };

            // Act
            var chunks1 = DocumentChunker.Chunk(document, options);
            var chunks2 = DocumentChunker.Chunk(document, options);

            // Assert
            Assert.Equal(chunks1.Count, chunks2.Count);
            
            for (int i = 0; i < chunks1.Count; i++)
            {
                Assert.Equal(chunks1[i].ChunkId, chunks2[i].ChunkId);
                Assert.Equal(chunks1[i].Text, chunks2[i].Text);
                Assert.Equal(chunks1[i].StartOffset, chunks2[i].StartOffset);
                Assert.Equal(chunks1[i].EndOffset, chunks2[i].EndOffset);
            }
        }

        [Fact]
        public void Chunk_EmptyDocument_ReturnsEmptyList()
        {
            // Arrange
            var document = new Document
            {
                Id = "empty-doc",
                Content = ""
            };

            var options = new ChunkingOptions();

            // Act
            var chunks = DocumentChunker.Chunk(document, options);

            // Assert
            Assert.Empty(chunks);
        }

        [Fact]
        public void Chunk_CopiesDocumentMetadata()
        {
            // Arrange
            var document = new Document
            {
                Id = "meta-doc",
                Title = "Test Title",
                SourceUri = "http://example.com",
                Content = "Some content here.",
                Tags = new HashSet<string> { "tag1", "tag2" }
            };

            var options = new ChunkingOptions();

            // Act
            var chunks = DocumentChunker.Chunk(document, options);

            // Assert
            Assert.Single(chunks);
            var chunk = chunks[0];
            
            Assert.Equal("Test Title", chunk.Metadata["title"]);
            Assert.Equal("http://example.com", chunk.Metadata["source_uri"]);
            Assert.True(chunk.Metadata.ContainsKey("tag_tag1"));
            Assert.True(chunk.Metadata.ContainsKey("tag_tag2"));
        }
    }
}

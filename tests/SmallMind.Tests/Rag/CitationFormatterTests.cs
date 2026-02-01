using System;
using SmallMind.Rag.Prompting;
using Xunit;

namespace SmallMind.Tests.Rag
{
    /// <summary>
    /// Tests for the CitationFormatter class.
    /// </summary>
    public class CitationFormatterTests
    {
        [Fact]
        public void CitationFormatter_FormatCitation_IncludesIndex()
        {
            // Arrange
            int index = 1;
            string title = "Test Document";
            string sourceUri = "test://doc1";
            int charStart = 0;
            int charEnd = 100;

            // Act
            var citation = CitationFormatter.FormatCitation(index, title, sourceUri, charStart, charEnd);

            // Assert
            Assert.Contains($"[S{index}]", citation);
        }

        [Fact]
        public void CitationFormatter_FormatCitation_IncludesTitle()
        {
            // Arrange
            int index = 1;
            string title = "Test Document";
            string sourceUri = "test://doc1";
            int charStart = 0;
            int charEnd = 100;

            // Act
            var citation = CitationFormatter.FormatCitation(index, title, sourceUri, charStart, charEnd);

            // Assert
            Assert.Contains(title, citation);
        }

        [Fact]
        public void CitationFormatter_FormatCitation_IncludesSourceUri()
        {
            // Arrange
            int index = 1;
            string title = "Test Document";
            string sourceUri = "test://doc1";
            int charStart = 0;
            int charEnd = 100;

            // Act
            var citation = CitationFormatter.FormatCitation(index, title, sourceUri, charStart, charEnd);

            // Assert
            Assert.Contains(sourceUri, citation);
        }

        [Fact]
        public void CitationFormatter_FormatCitation_IncludesCharacterRange()
        {
            // Arrange
            int index = 1;
            string title = "Test Document";
            string sourceUri = "test://doc1";
            int charStart = 100;
            int charEnd = 200;

            // Act
            var citation = CitationFormatter.FormatCitation(index, title, sourceUri, charStart, charEnd);

            // Assert
            Assert.Contains($"{charStart}", citation);
            Assert.Contains($"{charEnd}", citation);
        }

        [Fact]
        public void CitationFormatter_FormatCitation_HandlesEmptyTitle()
        {
            // Arrange
            int index = 1;
            string title = "";
            string sourceUri = "test://doc1";
            int charStart = 0;
            int charEnd = 100;

            // Act
            var citation = CitationFormatter.FormatCitation(index, title, sourceUri, charStart, charEnd);

            // Assert
            // Should not throw, should handle empty string gracefully
            Assert.NotNull(citation);
        }

        [Fact]
        public void CitationFormatter_FormatCitation_DifferentIndexes()
        {
            // Arrange
            string title = "Test Document";
            string sourceUri = "test://doc1";
            int charStart = 0;
            int charEnd = 100;

            // Act
            var citation1 = CitationFormatter.FormatCitation(1, title, sourceUri, charStart, charEnd);
            var citation2 = CitationFormatter.FormatCitation(5, title, sourceUri, charStart, charEnd);

            // Assert
            Assert.Contains("[S1]", citation1);
            Assert.Contains("[S5]", citation2);
            Assert.NotEqual(citation1, citation2);
        }
    }
}

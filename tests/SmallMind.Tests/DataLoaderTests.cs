using System;
using System.IO;
using Xunit;
using SmallMind.Tokenizers;
using SmallMind.Transformers;
using SmallMind.Runtime;

namespace SmallMind.Tests
{
    /// <summary>
    /// Unit tests for the DataLoader class.
    /// Verifies that all data loading formats produce equivalent output.
    /// </summary>
    public class DataLoaderTests
    {
        private readonly string _sampleDataDir;
        private readonly string _expectedText = 
            "The quick brown fox jumps over the lazy dog.\n" +
            "A journey of a thousand miles begins with a single step.\n" +
            "To be or not to be, that is the question.\n" +
            "All that glitters is not gold.\n" +
            "Where there is a will, there is a way.";

        public DataLoaderTests()
        {
            // Get the path to the sample_data directory relative to the project root
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            _sampleDataDir = Path.Combine(projectRoot, "sample_data");
        }

        [Fact]
        public void FromTextFile_LoadsCorrectly()
        {
            // Arrange
            var filePath = Path.Combine(_sampleDataDir, "sample.txt");

            // Act
            var result = DataLoader.FromTextFile(filePath);

            // Assert
            Assert.Equal(_expectedText, result);
        }

        [Fact]
        public void FromJsonFile_LoadsCorrectly()
        {
            // Arrange
            var filePath = Path.Combine(_sampleDataDir, "sample.json");

            // Act
            var result = DataLoader.FromJsonFile(filePath);

            // Assert
            Assert.Equal(_expectedText, result);
        }

        [Fact]
        public void FromXmlFile_LoadsCorrectly()
        {
            // Arrange
            var filePath = Path.Combine(_sampleDataDir, "sample.xml");

            // Act
            var result = DataLoader.FromXmlFile(filePath);

            // Assert
            Assert.Equal(_expectedText, result);
        }

        [Fact]
        public void FromCsvFile_LoadsCorrectly()
        {
            // Arrange
            var filePath = Path.Combine(_sampleDataDir, "sample.csv");

            // Act
            var result = DataLoader.FromCsvFile(filePath, columnIndex: 0, hasHeader: true);

            // Assert
            Assert.Equal(_expectedText, result);
        }

        [Fact]
        public void AllFormats_ProduceEquivalentOutput()
        {
            // Arrange
            var txtPath = Path.Combine(_sampleDataDir, "sample.txt");
            var jsonPath = Path.Combine(_sampleDataDir, "sample.json");
            var xmlPath = Path.Combine(_sampleDataDir, "sample.xml");
            var csvPath = Path.Combine(_sampleDataDir, "sample.csv");

            // Act
            var txtResult = DataLoader.FromTextFile(txtPath);
            var jsonResult = DataLoader.FromJsonFile(jsonPath);
            var xmlResult = DataLoader.FromXmlFile(xmlPath);
            var csvResult = DataLoader.FromCsvFile(csvPath, 0, true);

            // Assert - All formats should produce identical output
            Assert.Equal(txtResult, jsonResult);
            Assert.Equal(txtResult, xmlResult);
            Assert.Equal(txtResult, csvResult);
            Assert.Equal(jsonResult, xmlResult);
            Assert.Equal(jsonResult, csvResult);
            Assert.Equal(xmlResult, csvResult);
        }

        [Fact]
        public void FromTextWithDelimiters_SplitsCorrectly()
        {
            // Arrange
            var text = "First sentence. Second sentence! Third sentence? Fourth sentence.";
            var expected = "First sentence\nSecond sentence\nThird sentence\nFourth sentence";

            // Act
            var result = DataLoader.FromTextWithDelimiters(text);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public void FromDirectory_LoadsMultipleFiles()
        {
            // Arrange & Act
            var result = DataLoader.FromDirectory(_sampleDataDir, "sample.*");

            // Assert
            Assert.NotEmpty(result);
            // Should contain content from all files
            Assert.Contains("quick brown fox", result);
            Assert.Contains("thousand miles", result);
            Assert.Contains("not to be", result);
        }

        [Fact]
        public void FromTextFile_ThrowsOnMissingFile()
        {
            // Arrange
            var filePath = "nonexistent.txt";

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => DataLoader.FromTextFile(filePath));
        }

        [Fact]
        public void FromJsonFile_ThrowsOnMissingFile()
        {
            // Arrange
            var filePath = "nonexistent.json";

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => DataLoader.FromJsonFile(filePath));
        }

        [Fact]
        public void FromXmlFile_ThrowsOnMissingFile()
        {
            // Arrange
            var filePath = "nonexistent.xml";

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => DataLoader.FromXmlFile(filePath));
        }

        [Fact]
        public void FromCsvFile_ThrowsOnMissingFile()
        {
            // Arrange
            var filePath = "nonexistent.csv";

            // Act & Assert
            Assert.Throws<FileNotFoundException>(() => DataLoader.FromCsvFile(filePath));
        }

        [Fact]
        public void FromDirectory_ThrowsOnMissingDirectory()
        {
            // Arrange
            var dirPath = "nonexistent_directory";

            // Act & Assert
            Assert.Throws<DirectoryNotFoundException>(() => DataLoader.FromDirectory(dirPath));
        }

        [Fact]
        public void FromTextWithDelimiters_ThrowsOnEmptyText()
        {
            // Arrange
            var text = "";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => DataLoader.FromTextWithDelimiters(text));
        }
    }
}

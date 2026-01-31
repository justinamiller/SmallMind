namespace SmallMind.Tests;

public class DataLoaderTests
{
    private readonly string _testDataDir;

    public DataLoaderTests()
    {
        // Use the examples/data directory for test data
        _testDataDir = Path.Combine("..", "..", "..", "..", "examples", "data");
    }

    [Fact]
    public void FromTextFile_LoadsCorrectly()
    {
        // Arrange
        string filePath = Path.Combine(_testDataDir, "training.txt");
        
        // Act
        string[] data = DataLoader.FromTextFile(filePath);
        
        // Assert
        Assert.NotEmpty(data);
        Assert.All(data, sentence => Assert.False(string.IsNullOrWhiteSpace(sentence)));
    }

    [Fact]
    public void FromJsonFile_LoadsCorrectly()
    {
        // Arrange
        string filePath = Path.Combine(_testDataDir, "training.json");
        
        // Act
        string[] data = DataLoader.FromJsonFile(filePath);
        
        // Assert
        Assert.NotEmpty(data);
        Assert.All(data, sentence => Assert.False(string.IsNullOrWhiteSpace(sentence)));
    }

    [Fact]
    public void FromXmlFile_LoadsCorrectly()
    {
        // Arrange
        string filePath = Path.Combine(_testDataDir, "training.xml");
        
        // Act
        string[] data = DataLoader.FromXmlFile(filePath);
        
        // Assert
        Assert.NotEmpty(data);
        Assert.All(data, sentence => Assert.False(string.IsNullOrWhiteSpace(sentence)));
    }

    [Fact]
    public void AllFormats_LoadSameData()
    {
        // Arrange
        string textPath = Path.Combine(_testDataDir, "training.txt");
        string jsonPath = Path.Combine(_testDataDir, "training.json");
        string xmlPath = Path.Combine(_testDataDir, "training.xml");
        
        // Act
        string[] textData = DataLoader.FromTextFile(textPath);
        string[] jsonData = DataLoader.FromJsonFile(jsonPath);
        string[] xmlData = DataLoader.FromXmlFile(xmlPath);
        
        // Assert - All formats should load the same number of sentences
        Assert.Equal(textData.Length, jsonData.Length);
        Assert.Equal(textData.Length, xmlData.Length);
        
        // Verify content matches (at least for first few sentences)
        for (int i = 0; i < Math.Min(5, textData.Length); i++)
        {
            Assert.Equal(textData[i], jsonData[i]);
            Assert.Equal(textData[i], xmlData[i]);
        }
    }

    [Fact]
    public void FromTextFile_SkipsEmptyLines()
    {
        // Create a temporary file with empty lines
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(tempFile, new[] 
            { 
                "Line 1", 
                "", 
                "Line 2", 
                "   ", 
                "Line 3" 
            });
            
            // Act
            string[] data = DataLoader.FromTextFile(tempFile, skipEmptyLines: true);
            
            // Assert
            Assert.Equal(3, data.Length);
            Assert.Equal("Line 1", data[0]);
            Assert.Equal("Line 2", data[1]);
            Assert.Equal("Line 3", data[2]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void FromTextWithDelimiters_SplitsBySentence()
    {
        // Create a temporary file with sentences
        string tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, "First sentence. Second sentence! Third sentence?");
            
            // Act
            string[] data = DataLoader.FromTextWithDelimiters(tempFile);
            
            // Assert
            Assert.Equal(3, data.Length);
            Assert.Contains("First sentence", data[0]);
            Assert.Contains("Second sentence", data[1]);
            Assert.Contains("Third sentence", data[2]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}

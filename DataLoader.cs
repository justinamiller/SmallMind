using System.Text.Json;
using System.Xml.Linq;

namespace SmallMind;

/// <summary>
/// Helper class for loading training data from various file formats.
/// Converts different data sources to the string[] format expected by LanguageModel.
/// </summary>
public static class DataLoader
{
    /// <summary>
    /// Loads training data from a plain text file, one sentence per line.
    /// </summary>
    /// <param name="filePath">Path to the .txt file</param>
    /// <param name="skipEmptyLines">Whether to skip empty lines (default: true)</param>
    /// <returns>Array of sentences</returns>
    public static string[] FromTextFile(string filePath, bool skipEmptyLines = true)
    {
        var lines = File.ReadAllLines(filePath);
        
        if (skipEmptyLines)
        {
            return lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        }
        
        return lines;
    }

    /// <summary>
    /// Loads training data from a JSON file.
    /// Expected format: { "sentences": ["sentence1", "sentence2", ...] }
    /// </summary>
    /// <param name="filePath">Path to the .json file</param>
    /// <returns>Array of sentences</returns>
    public static string[] FromJsonFile(string filePath)
    {
        string json = File.ReadAllText(filePath);
        var options = new JsonSerializerOptions 
        { 
            PropertyNameCaseInsensitive = true 
        };
        var data = JsonSerializer.Deserialize<JsonTrainingData>(json, options);
        return data?.Sentences ?? Array.Empty<string>();
    }

    /// <summary>
    /// Loads training data from an XML file.
    /// Expected format: &lt;training&gt;&lt;sentence&gt;text&lt;/sentence&gt;...&lt;/training&gt;
    /// </summary>
    /// <param name="filePath">Path to the .xml file</param>
    /// <param name="sentenceElementName">Name of the XML element containing sentences (default: "sentence")</param>
    /// <returns>Array of sentences</returns>
    public static string[] FromXmlFile(string filePath, string sentenceElementName = "sentence")
    {
        var xml = XDocument.Load(filePath);
        return xml.Descendants(sentenceElementName)
            .Select(e => e.Value)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    /// <summary>
    /// Loads training data from a CSV file.
    /// </summary>
    /// <param name="filePath">Path to the .csv file</param>
    /// <param name="columnIndex">Index of the column containing sentences (0-based, default: 0)</param>
    /// <param name="hasHeader">Whether the file has a header row to skip (default: true)</param>
    /// <param name="delimiter">CSV delimiter character (default: comma)</param>
    /// <returns>Array of sentences</returns>
    public static string[] FromCsvFile(string filePath, int columnIndex = 0, bool hasHeader = true, char delimiter = ',')
    {
        var lines = File.ReadAllLines(filePath);
        var startIndex = hasHeader ? 1 : 0;
        
        return lines
            .Skip(startIndex)
            .Select(line => SplitCsvLine(line, delimiter))
            .Where(parts => parts.Length > columnIndex)
            .Select(parts => parts[columnIndex].Trim().Trim('"'))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    /// <summary>
    /// Loads training data from multiple text files in a directory.
    /// </summary>
    /// <param name="directoryPath">Path to the directory containing text files</param>
    /// <param name="filePattern">File pattern to match (default: "*.txt")</param>
    /// <param name="skipEmptyLines">Whether to skip empty lines (default: true)</param>
    /// <returns>Array of sentences from all files</returns>
    public static string[] FromDirectory(string directoryPath, string filePattern = "*.txt", bool skipEmptyLines = true)
    {
        var files = Directory.GetFiles(directoryPath, filePattern);
        var allLines = files.SelectMany(file => File.ReadAllLines(file));
        
        if (skipEmptyLines)
        {
            return allLines.Where(line => !string.IsNullOrWhiteSpace(line)).ToArray();
        }
        
        return allLines.ToArray();
    }

    /// <summary>
    /// Loads training data from a single text file and splits by sentence delimiters.
    /// </summary>
    /// <param name="filePath">Path to the text file</param>
    /// <param name="delimiters">Sentence delimiter characters (default: period, exclamation, question mark)</param>
    /// <returns>Array of sentences</returns>
    public static string[] FromTextWithDelimiters(string filePath, char[]? delimiters = null)
    {
        delimiters ??= new[] { '.', '!', '?' };
        
        string content = File.ReadAllText(filePath);
        return content
            .Split(delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
    }

    // Helper method to split CSV line properly handling quoted fields
    private static string[] SplitCsvLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var currentField = new System.Text.StringBuilder();
        bool inQuotes = false;
        
        foreach (char c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == delimiter && !inQuotes)
            {
                fields.Add(currentField.ToString());
                currentField.Clear();
            }
            else
            {
                currentField.Append(c);
            }
        }
        
        fields.Add(currentField.ToString());
        return fields.ToArray();
    }

    // Helper class for JSON deserialization
    private class JsonTrainingData
    {
        public string[] Sentences { get; set; } = Array.Empty<string>();
    }
}

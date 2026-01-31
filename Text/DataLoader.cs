using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TinyLLM.Core;

namespace TinyLLM.Text
{
    /// <summary>
    /// Provides static methods for loading training data from various sources.
    /// Supports JSON, XML, CSV, text files, directories, and custom delimiters.
    /// All methods return a single concatenated string suitable for training.
    /// </summary>
    public static class DataLoader
    {
        /// <summary>
        /// Load data from a plain text file, one sentence per line.
        /// Each line is treated as a separate sentence.
        /// Optimized to use StreamReader instead of File.ReadAllLines.
        /// </summary>
        /// <param name="filePath">Path to the text file</param>
        /// <param name="separator">Separator to join sentences (default: newline)</param>
        /// <returns>Concatenated text from all lines</returns>
        public static string FromTextFile(string filePath, string separator = "\n")
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Text file not found: {filePath}");
            }

            var sentences = new List<string>();
            
            using (var reader = new StreamReader(filePath, Encoding.UTF8))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        sentences.Add(line);
                    }
                }
            }
            
            Console.WriteLine($"Loaded {sentences.Count} sentences from {filePath}");
            return string.Join(separator, sentences);
        }

        /// <summary>
        /// Load data from a JSON file.
        /// Expected format: { "sentences": ["sentence1", "sentence2", ...] }
        /// </summary>
        /// <param name="filePath">Path to the JSON file</param>
        /// <param name="separator">Separator to join sentences (default: newline)</param>
        /// <returns>Concatenated text from all sentences</returns>
        public static string FromJsonFile(string filePath, string separator = "\n")
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"JSON file not found: {filePath}");
            }

            var jsonText = File.ReadAllText(filePath);
            var doc = JsonDocument.Parse(jsonText);
            
            if (!doc.RootElement.TryGetProperty("sentences", out var sentencesElement))
            {
                throw new InvalidDataException("JSON file must contain a 'sentences' array");
            }

            var sentences = new List<string>();
            foreach (var element in sentencesElement.EnumerateArray())
            {
                var sentence = element.GetString();
                if (!string.IsNullOrWhiteSpace(sentence))
                {
                    sentences.Add(sentence);
                }
            }

            Console.WriteLine($"Loaded {sentences.Count} sentences from JSON file {filePath}");
            return string.Join(separator, sentences);
        }

        /// <summary>
        /// Load data from an XML file.
        /// Extracts text from elements with the specified element name.
        /// </summary>
        /// <param name="filePath">Path to the XML file</param>
        /// <param name="elementName">Name of the XML element to extract text from</param>
        /// <param name="separator">Separator to join sentences (default: newline)</param>
        /// <returns>Concatenated text from all matching elements</returns>
        public static string FromXmlFile(string filePath, string elementName = "sentence", string separator = "\n")
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"XML file not found: {filePath}");
            }

            var doc = XDocument.Load(filePath);
            var sentences = new List<string>();
            
            foreach (var element in doc.Descendants(elementName))
            {
                var value = element.Value;
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sentences.Add(value);
                }
            }

            Console.WriteLine($"Loaded {sentences.Count} sentences from XML file {filePath}");
            return string.Join(separator, sentences);
        }

        /// <summary>
        /// Load data from a CSV file.
        /// Supports column-based extraction with header handling.
        /// Optimized with StreamReader instead of File.ReadAllLines.
        /// </summary>
        /// <param name="filePath">Path to the CSV file</param>
        /// <param name="columnIndex">Index of the column to extract (0-based)</param>
        /// <param name="hasHeader">Whether the CSV file has a header row</param>
        /// <param name="separator">Separator to join sentences (default: newline)</param>
        /// <param name="delimiter">CSV delimiter (default: comma)</param>
        /// <returns>Concatenated text from the specified column</returns>
        public static string FromCsvFile(string filePath, int columnIndex = 0, bool hasHeader = true, 
            string separator = "\n", char delimiter = ',')
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"CSV file not found: {filePath}");
            }

            var sentences = new List<string>();
            bool skipHeader = hasHeader;

            using (var reader = new StreamReader(filePath, Encoding.UTF8))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    if (skipHeader)
                    {
                        skipHeader = false;
                        continue;
                    }
                    
                    var fields = ParseCsvLine(line, delimiter);
                    if (fields.Count > columnIndex)
                    {
                        var text = fields[columnIndex].Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            sentences.Add(text);
                        }
                    }
                }
            }

            Console.WriteLine($"Loaded {sentences.Count} sentences from CSV file {filePath}");
            return string.Join(separator, sentences);
        }

        /// <summary>
        /// Load data from multiple files in a directory.
        /// Supports text, JSON, XML, and CSV files based on file extension.
        /// </summary>
        /// <param name="directoryPath">Path to the directory</param>
        /// <param name="searchPattern">File search pattern (default: "*.*")</param>
        /// <param name="separator">Separator to join content from different files (default: double newline)</param>
        /// <returns>Concatenated text from all files</returns>
        public static string FromDirectory(string directoryPath, string searchPattern = "*.*", string separator = "\n\n")
        {
            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
            }

            var files = Directory.GetFiles(directoryPath, searchPattern);
            var allTexts = new List<string>();

            foreach (var file in files)
            {
                try
                {
                    var extension = Path.GetExtension(file);
                    // Use Ordinal comparison instead of ToLowerInvariant for performance
                    string text;
                    if (extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
                    {
                        text = FromTextFile(file);
                    }
                    else if (extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
                    {
                        text = FromJsonFile(file);
                    }
                    else if (extension.Equals(".xml", StringComparison.OrdinalIgnoreCase))
                    {
                        text = FromXmlFile(file);
                    }
                    else if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        text = FromCsvFile(file);
                    }
                    else
                    {
                        text = File.ReadAllText(file); // Try to read as plain text
                    }

                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        allTexts.Add(text);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to load file {file}: {ex.Message}");
                }
            }

            Console.WriteLine($"Loaded {allTexts.Count} files from directory {directoryPath}");
            return string.Join(separator, allTexts);
        }

        /// <summary>
        /// Load text and split by custom delimiters.
        /// Supports single-character delimiters for sentence splitting.
        /// Note: Multi-character delimiters are not supported in this optimized version.
        /// Use single characters like ".", "!", "?" as delimiters.
        /// </summary>
        /// <param name="text">Input text to split</param>
        /// <param name="delimiters">Array of single-character delimiters to split on (default: period, exclamation, question mark)</param>
        /// <param name="separator">Separator to join sentences (default: newline)</param>
        /// <returns>Concatenated text with sentences separated</returns>
        public static string FromTextWithDelimiters(string text, string[]? delimiters = null, string separator = "\n")
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new ArgumentException("Input text cannot be null or empty", nameof(text));
            }

            delimiters ??= new[] { ".", "!", "?" };
            
            // Validate that all delimiters are single characters for optimized parsing
            var delimiterChars = new HashSet<char>();
            for (int i = 0; i < delimiters.Length; i++)
            {
                if (delimiters[i].Length != 1)
                {
                    throw new ArgumentException($"Delimiter '{delimiters[i]}' must be a single character", nameof(delimiters));
                }
                delimiterChars.Add(delimiters[i][0]);
            }
            
            var sentences = new List<string>();
            
            // Manual parsing to avoid string.Split allocation
            int sentenceStart = 0;
            ReadOnlySpan<char> textSpan = text.AsSpan();
            
            for (int i = 0; i < textSpan.Length; i++)
            {
                if (delimiterChars.Contains(textSpan[i]))
                {
                    int sentenceLength = i - sentenceStart;
                    if (sentenceLength > 0)
                    {
                        string sentence = text.Substring(sentenceStart, sentenceLength).Trim();
                        if (!string.IsNullOrWhiteSpace(sentence))
                        {
                            sentences.Add(sentence);
                        }
                    }
                    sentenceStart = i + 1;
                }
            }
            
            // Add remaining text
            if (sentenceStart < text.Length)
            {
                string sentence = text.Substring(sentenceStart).Trim();
                if (!string.IsNullOrWhiteSpace(sentence))
                {
                    sentences.Add(sentence);
                }
            }

            Console.WriteLine($"Split text into {sentences.Count} sentences using delimiters");
            return string.Join(separator, sentences);
        }

        /// <summary>
        /// Helper method to parse a CSV line, handling quoted fields.
        /// </summary>
        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

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
            return fields;
        }
    }
}

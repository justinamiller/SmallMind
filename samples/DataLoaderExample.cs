using System;
using TinyLLM;

namespace SmallMind.Examples
{
    /// <summary>
    /// Example program demonstrating usage of the DataLoader class.
    /// Shows how to load training data from various sources: JSON, XML, CSV, text files, and directories.
    /// </summary>
    class DataLoaderExample
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== DataLoader Examples ===\n");

            // Example 1: Load from a plain text file
            Console.WriteLine("1. Loading from plain text file:");
            try
            {
                var textData = DataLoader.FromTextFile("sample_data/sample.txt");
                Console.WriteLine($"   Loaded {textData.Length} characters");
                Console.WriteLine($"   First 50 chars: {textData.Substring(0, Math.Min(50, textData.Length))}...\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error: {ex.Message}\n");
            }

            // Example 2: Load from JSON file
            Console.WriteLine("2. Loading from JSON file:");
            try
            {
                var jsonData = DataLoader.FromJsonFile("sample_data/sample.json");
                Console.WriteLine($"   Loaded {jsonData.Length} characters");
                Console.WriteLine($"   First 50 chars: {jsonData.Substring(0, Math.Min(50, jsonData.Length))}...\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error: {ex.Message}\n");
            }

            // Example 3: Load from XML file
            Console.WriteLine("3. Loading from XML file:");
            try
            {
                var xmlData = DataLoader.FromXmlFile("sample_data/sample.xml", elementName: "sentence");
                Console.WriteLine($"   Loaded {xmlData.Length} characters");
                Console.WriteLine($"   First 50 chars: {xmlData.Substring(0, Math.Min(50, xmlData.Length))}...\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error: {ex.Message}\n");
            }

            // Example 4: Load from CSV file
            Console.WriteLine("4. Loading from CSV file:");
            try
            {
                var csvData = DataLoader.FromCsvFile("sample_data/sample.csv", columnIndex: 0, hasHeader: true);
                Console.WriteLine($"   Loaded {csvData.Length} characters");
                Console.WriteLine($"   First 50 chars: {csvData.Substring(0, Math.Min(50, csvData.Length))}...\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error: {ex.Message}\n");
            }

            // Example 5: Load from directory (batch load)
            Console.WriteLine("5. Loading from directory (batch load):");
            try
            {
                var dirData = DataLoader.FromDirectory("sample_data", searchPattern: "*.txt");
                Console.WriteLine($"   Loaded {dirData.Length} characters from all text files");
                Console.WriteLine($"   First 50 chars: {dirData.Substring(0, Math.Min(50, dirData.Length))}...\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error: {ex.Message}\n");
            }

            // Example 6: Split text with delimiters
            Console.WriteLine("6. Splitting text with delimiters:");
            try
            {
                var sampleText = "First sentence. Second sentence! Third sentence? Fourth sentence.";
                var splitData = DataLoader.FromTextWithDelimiters(sampleText, delimiters: new[] { ".", "!", "?" });
                Console.WriteLine($"   Original: {sampleText}");
                Console.WriteLine($"   Split result: {splitData}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error: {ex.Message}\n");
            }

            // Example 7: Verify format equivalence
            Console.WriteLine("7. Verifying all formats produce equivalent output:");
            try
            {
                var txt = DataLoader.FromTextFile("sample_data/sample.txt");
                var json = DataLoader.FromJsonFile("sample_data/sample.json");
                var xml = DataLoader.FromXmlFile("sample_data/sample.xml");
                var csv = DataLoader.FromCsvFile("sample_data/sample.csv", 0, true);

                bool allEqual = txt == json && json == xml && xml == csv;
                Console.WriteLine($"   All formats equivalent: {allEqual}");
                Console.WriteLine($"   Text length: {txt.Length}, JSON: {json.Length}, XML: {xml.Length}, CSV: {csv.Length}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error: {ex.Message}\n");
            }

            // Example 8: Using loaded data with the Tokenizer
            Console.WriteLine("8. Using loaded data with Tokenizer:");
            try
            {
                var trainingText = DataLoader.FromJsonFile("sample_data/sample.json");
                var tokenizer = new Tokenizer(trainingText);
                var encoded = tokenizer.Encode("The quick brown fox");
                var decoded = tokenizer.Decode(encoded);
                
                Console.WriteLine($"   Training data: {trainingText.Length} characters");
                Console.WriteLine($"   Vocabulary size: {tokenizer.VocabSize} unique characters");
                Console.WriteLine($"   Sample encoding: \"The quick brown fox\" -> {string.Join(", ", encoded)}");
                Console.WriteLine($"   Decoded: \"{decoded}\"\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   Error: {ex.Message}\n");
            }

            Console.WriteLine("=== Examples Complete ===");
        }
    }
}

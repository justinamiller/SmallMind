using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Represents a labeled text sample for training.
    /// </summary>
    public class LabeledSample
    {
        /// <summary>
        /// Unique identifier for this sample.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Task type (e.g., sentiment, classification).
        /// </summary>
        public string Task { get; set; } = string.Empty;

        /// <summary>
        /// The label/category for this sample.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// The text content.
        /// </summary>
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Utility for loading pre-labeled datasets for training and fine-tuning.
    /// Supports both JSONL (preferred) and legacy pipe-delimited formats.
    /// </summary>
    public static class DatasetLoader
    {
        /// <summary>
        /// Load sentiment analysis dataset from a file.
        /// Supports both JSONL and legacy label|text formats.
        /// Labels should be: positive, negative, neutral
        /// </summary>
        /// <param name="filePath">Path to the dataset file</param>
        /// <returns>List of labeled samples</returns>
        public static List<LabeledSample> LoadSentimentData(string filePath)
        {
            return LoadLabeledData(filePath, new[] { "positive", "negative", "neutral" });
        }

        /// <summary>
        /// Load text classification dataset from a file.
        /// Supports both JSONL and legacy category|text formats.
        /// </summary>
        /// <param name="filePath">Path to the dataset file</param>
        /// <param name="expectedLabels">Expected labels/categories (optional validation)</param>
        /// <returns>List of labeled samples</returns>
        public static List<LabeledSample> LoadClassificationData(string filePath, string[]? expectedLabels = null)
        {
            return LoadLabeledData(filePath, expectedLabels);
        }

        /// <summary>
        /// Load labeled data from a JSONL file.
        /// Each line must be a JSON object with: id, task, text, label
        /// </summary>
        /// <param name="filePath">Path to the JSONL file</param>
        /// <param name="expectedLabels">Expected labels for validation (optional)</param>
        /// <returns>List of labeled samples</returns>
        public static List<LabeledSample> LoadFromJsonl(string filePath, string[]? expectedLabels = null)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Dataset file not found: {filePath}");
            }

            var samples = new List<LabeledSample>();
            var expectedLabelSet = expectedLabels != null
                ? new HashSet<string>(expectedLabels, StringComparer.OrdinalIgnoreCase)
                : null;

            using var reader = new StreamReader(filePath);
            string? line;
            int lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;

                // Skip empty lines
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var sample = JsonSerializer.Deserialize<LabeledSample>(line);
                    if (sample == null)
                    {
                        Console.WriteLine($"Warning: Failed to deserialize line {lineNumber}");
                        continue;
                    }

                    // Validate label if expected labels provided
                    if (expectedLabelSet != null && !string.IsNullOrWhiteSpace(sample.Label) 
                        && !expectedLabelSet.Contains(sample.Label))
                    {
                        Console.WriteLine($"Warning: Unexpected label '{sample.Label}' at line {lineNumber}. Expected one of: {string.Join(", ", expectedLabels!)}");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(sample.Text))
                    {
                        Console.WriteLine($"Warning: Empty text at line {lineNumber}");
                        continue;
                    }

                    samples.Add(sample);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"Warning: JSON parse error at line {lineNumber}: {ex.Message}");
                    continue;
                }
            }

            return samples;
        }

        /// <summary>
        /// Load labeled data from a file. Auto-detects format (JSONL or legacy pipe-delimited).
        /// Legacy format: label|text (one per line)
        /// JSONL format: {"id":"...", "task":"...", "text":"...", "label":"..."}
        /// </summary>
        /// <param name="filePath">Path to the dataset file</param>
        /// <param name="expectedLabels">Expected labels for validation (optional)</param>
        /// <returns>List of labeled samples</returns>
        public static List<LabeledSample> LoadLabeledData(string filePath, string[]? expectedLabels = null)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Dataset file not found: {filePath}");
            }

            // Auto-detect format by checking file extension or first line
            if (filePath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase))
            {
                return LoadFromJsonl(filePath, expectedLabels);
            }

            // Check first non-empty line to detect format
            using (var reader = new StreamReader(filePath))
            {
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                        continue;

                    // Check if it's JSON
                    if (line.StartsWith("{"))
                    {
                        return LoadFromJsonl(filePath, expectedLabels);
                    }
                    
                    // Otherwise assume legacy pipe-delimited format
                    break;
                }
            }

            // Load as legacy pipe-delimited format
            return LoadLegacyFormat(filePath, expectedLabels);
        }

        /// <summary>
        /// Load labeled data from legacy pipe-delimited format: label|text
        /// </summary>
        private static List<LabeledSample> LoadLegacyFormat(string filePath, string[]? expectedLabels = null)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Dataset file not found: {filePath}");
            }

            var samples = new List<LabeledSample>();
            var expectedLabelSet = expectedLabels != null
                ? new HashSet<string>(expectedLabels, StringComparer.OrdinalIgnoreCase)
                : null;

            using var reader = new StreamReader(filePath);
            string? line;
            int lineNumber = 0;

            while ((line = reader.ReadLine()) != null)
            {
                lineNumber++;

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                {
                    continue;
                }

                // Parse label|text format
                var parts = line.Split('|', 2);
                if (parts.Length != 2)
                {
                    Console.WriteLine($"Warning: Skipping malformed line {lineNumber}: {line}");
                    continue;
                }

                var label = parts[0].Trim();
                var text = parts[1].Trim();

                // Validate label if expected labels provided
                if (expectedLabelSet != null && !expectedLabelSet.Contains(label))
                {
                    Console.WriteLine($"Warning: Unexpected label '{label}' at line {lineNumber}. Expected one of: {string.Join(", ", expectedLabels!)}");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    Console.WriteLine($"Warning: Empty text at line {lineNumber}");
                    continue;
                }

                samples.Add(new LabeledSample
                {
                    Label = label,
                    Text = text
                });
            }

            return samples;
        }

        /// <summary>
        /// Split dataset into training and validation sets.
        /// </summary>
        /// <param name="samples">All samples</param>
        /// <param name="trainRatio">Ratio of samples for training (e.g., 0.8 for 80%)</param>
        /// <param name="seed">Random seed for shuffling</param>
        /// <returns>Tuple of (training samples, validation samples)</returns>
        public static (List<LabeledSample> train, List<LabeledSample> validation) SplitDataset(
            List<LabeledSample> samples,
            double trainRatio = 0.8,
            int seed = 42)
        {
            if (trainRatio <= 0 || trainRatio >= 1)
            {
                throw new ArgumentException("Train ratio must be between 0 and 1", nameof(trainRatio));
            }

            // Shuffle samples using Fisher-Yates algorithm
            var random = new Random(seed);
            var shuffled = new List<LabeledSample>(samples);
            int n = shuffled.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = shuffled[i];
                shuffled[i] = shuffled[j];
                shuffled[j] = temp;
            }

            // Split
            int trainCount = (int)(shuffled.Count * trainRatio);
            var train = new List<LabeledSample>(trainCount);
            for (int i = 0; i < trainCount; i++)
            {
                train.Add(shuffled[i]);
            }
            
            var validation = new List<LabeledSample>(shuffled.Count - trainCount);
            for (int i = trainCount; i < shuffled.Count; i++)
            {
                validation.Add(shuffled[i]);
            }

            return (train, validation);
        }

        /// <summary>
        /// Get unique labels from a dataset.
        /// </summary>
        /// <param name="samples">Labeled samples</param>
        /// <returns>Array of unique labels</returns>
        public static string[] GetUniqueLabels(List<LabeledSample> samples)
        {
            var labelSet = new HashSet<string>();
            for (int i = 0; i < samples.Count; i++)
            {
                labelSet.Add(samples[i].Label);
            }
            
            var labels = new List<string>(labelSet);
            labels.Sort();
            return labels.ToArray();
        }

        /// <summary>
        /// Get statistics about a dataset.
        /// </summary>
        /// <param name="samples">Labeled samples</param>
        /// <returns>Dictionary with label counts</returns>
        public static Dictionary<string, int> GetLabelDistribution(List<LabeledSample> samples)
        {
            var counts = new Dictionary<string, int>();
            
            // Count occurrences
            for (int i = 0; i < samples.Count; i++)
            {
                string label = samples[i].Label;
                if (counts.ContainsKey(label))
                {
                    counts[label]++;
                }
                else
                {
                    counts[label] = 1;
                }
            }
            
            return counts;
        }

        /// <summary>
        /// Print dataset statistics to console.
        /// </summary>
        /// <param name="samples">Labeled samples</param>
        /// <param name="datasetName">Name of the dataset (for display)</param>
        public static void PrintStatistics(List<LabeledSample> samples, string datasetName = "Dataset")
        {
            Console.WriteLine($"\n{datasetName} Statistics:");
            Console.WriteLine($"Total samples: {samples.Count}");
            
            var distribution = GetLabelDistribution(samples);
            Console.WriteLine("\nLabel distribution:");
            foreach (var kvp in distribution)
            {
                double percentage = (double)kvp.Value / samples.Count * 100;
                Console.WriteLine($"  {kvp.Key}: {kvp.Value} ({percentage:F1}%)");
            }

            // Text length statistics
            if (samples.Count > 0)
            {
                int min = int.MaxValue;
                int max = int.MinValue;
                long sum = 0;
                
                for (int i = 0; i < samples.Count; i++)
                {
                    int len = samples[i].Text.Length;
                    if (len < min) min = len;
                    if (len > max) max = len;
                    sum += len;
                }
                
                double average = (double)sum / samples.Count;
                
                Console.WriteLine($"\nText length statistics:");
                Console.WriteLine($"  Min: {min}");
                Console.WriteLine($"  Max: {max}");
                Console.WriteLine($"  Average: {average:F1}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Represents a labeled text sample for training.
    /// </summary>
    public class LabeledSample
    {
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
    /// </summary>
    public static class DatasetLoader
    {
        /// <summary>
        /// Load sentiment analysis dataset from a file.
        /// Expected format: label|text (one per line)
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
        /// Expected format: category|text (one per line)
        /// </summary>
        /// <param name="filePath">Path to the dataset file</param>
        /// <param name="expectedLabels">Expected labels/categories (optional validation)</param>
        /// <returns>List of labeled samples</returns>
        public static List<LabeledSample> LoadClassificationData(string filePath, string[]? expectedLabels = null)
        {
            return LoadLabeledData(filePath, expectedLabels);
        }

        /// <summary>
        /// Load labeled data from a file with format: label|text
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

            // Shuffle samples
            var random = new Random(seed);
            var shuffled = samples.OrderBy(x => random.Next()).ToList();

            // Split
            int trainCount = (int)(shuffled.Count * trainRatio);
            var train = shuffled.Take(trainCount).ToList();
            var validation = shuffled.Skip(trainCount).ToList();

            return (train, validation);
        }

        /// <summary>
        /// Get unique labels from a dataset.
        /// </summary>
        /// <param name="samples">Labeled samples</param>
        /// <returns>Array of unique labels</returns>
        public static string[] GetUniqueLabels(List<LabeledSample> samples)
        {
            return samples.Select(s => s.Label).Distinct().OrderBy(l => l).ToArray();
        }

        /// <summary>
        /// Get statistics about a dataset.
        /// </summary>
        /// <param name="samples">Labeled samples</param>
        /// <returns>Dictionary with label counts</returns>
        public static Dictionary<string, int> GetLabelDistribution(List<LabeledSample> samples)
        {
            return samples
                .GroupBy(s => s.Label)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());
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
            var textLengths = samples.Select(s => s.Text.Length).ToList();
            Console.WriteLine($"\nText length statistics:");
            Console.WriteLine($"  Min: {textLengths.Min()}");
            Console.WriteLine($"  Max: {textLengths.Max()}");
            Console.WriteLine($"  Average: {textLengths.Average():F1}");
        }
    }
}

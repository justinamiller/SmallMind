using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Represents a loaded pretrained data pack.
    /// </summary>
    internal class PretrainedPack
    {
        /// <summary>
        /// Pack manifest metadata.
        /// </summary>
        public PackManifest Manifest { get; set; } = new();

        /// <summary>
        /// Path to the pack directory.
        /// </summary>
        public string PackPath { get; set; } = string.Empty;

        /// <summary>
        /// Loaded samples from the task inputs.
        /// </summary>
        public List<LabeledSample> Samples { get; set; } = new();

        /// <summary>
        /// Evaluation labels (if available).
        /// </summary>
        public List<LabeledSample> EvaluationLabels { get; set; } = new();

        /// <summary>
        /// Categories (for classification tasks).
        /// </summary>
        public List<string> Categories { get; set; } = new();

        /// <summary>
        /// RAG documents (if RAG is enabled).
        /// </summary>
        public List<string> RagDocumentPaths { get; set; } = new();

        /// <summary>
        /// Load a pretrained pack from a directory.
        /// </summary>
        /// <param name="packPath">Path to the pack directory</param>
        /// <returns>Loaded pack</returns>
        public static PretrainedPack Load(string packPath)
        {
            if (!Directory.Exists(packPath))
            {
                throw new DirectoryNotFoundException($"Pack directory not found: {packPath}");
            }

            var pack = new PretrainedPack
            {
                PackPath = packPath
            };

            // Load manifest
            var manifestPath = Path.Combine(packPath, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                throw new FileNotFoundException($"Pack manifest not found: {manifestPath}");
            }

            var manifestJson = File.ReadAllText(manifestPath);
            pack.Manifest = JsonSerializer.Deserialize<PackManifest>(manifestJson)
                ?? throw new InvalidOperationException("Failed to deserialize pack manifest");

            // Load task inputs
            var taskInputsPath = Path.Combine(packPath, "task", "inputs.jsonl");
            if (File.Exists(taskInputsPath))
            {
                pack.Samples = DatasetLoader.LoadFromJsonl(taskInputsPath);
            }

            // Load evaluation labels
            var evalLabelsPath = Path.Combine(packPath, "eval", "labels.jsonl");
            if (File.Exists(evalLabelsPath))
            {
                pack.EvaluationLabels = DatasetLoader.LoadFromJsonl(evalLabelsPath);
            }
            else
            {
                // Try expected.jsonl for RAG packs
                var expectedPath = Path.Combine(packPath, "eval", "expected.jsonl");
                if (File.Exists(expectedPath))
                {
                    pack.EvaluationLabels = DatasetLoader.LoadFromJsonl(expectedPath);
                }
            }

            // Load categories (if available)
            var categoriesPath = Path.Combine(packPath, "task", "categories.json");
            if (File.Exists(categoriesPath))
            {
                var categoriesJson = File.ReadAllText(categoriesPath);
                var categoriesData = JsonSerializer.Deserialize<CategoryDefinitions>(categoriesJson);
                if (categoriesData?.Categories != null)
                {
                    foreach (var category in categoriesData.Categories)
                    {
                        pack.Categories.Add(category.Name);
                    }
                }
            }
            else if (pack.Manifest.Task?.Labels != null)
            {
                // Use labels from manifest if categories file not available
                pack.Categories.AddRange(pack.Manifest.Task.Labels);
            }

            // Load RAG documents (if enabled)
            if (pack.Manifest.Rag?.Enabled == true)
            {
                var ragDocsPath = Path.Combine(packPath, "rag", "documents");
                if (Directory.Exists(ragDocsPath))
                {
                    var docFiles = Directory.GetFiles(ragDocsPath, "*.md");
                    pack.RagDocumentPaths.AddRange(docFiles);
                }
            }

            return pack;
        }

        /// <summary>
        /// Get pack statistics summary.
        /// </summary>
        public string GetSummary()
        {
            var summary = $"Pack: {Manifest.Id}\n";
            summary += $"Domain: {Manifest.Domain}\n";
            summary += $"Type: {Manifest.Type}\n";
            summary += $"Samples: {Samples.Count}\n";
            
            if (Categories.Count > 0)
            {
                summary += $"Categories: {string.Join(", ", Categories)}\n";
            }
            
            if (Manifest.Rag?.Enabled == true)
            {
                summary += $"RAG Documents: {RagDocumentPaths.Count}\n";
            }
            
            return summary;
        }
    }

    /// <summary>
    /// Category definitions for classification tasks.
    /// </summary>
    internal class CategoryDefinitions
    {
        /// <summary>
        /// List of categories.
        /// </summary>
        [JsonPropertyName("categories")]
        public List<CategoryDefinition> Categories { get; set; } = new();

        /// <summary>
        /// Additional notes about categories.
        /// </summary>
        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;
    }

    /// <summary>
    /// Definition of a single category.
    /// </summary>
    internal class CategoryDefinition
    {
        /// <summary>
        /// Category identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Category name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Category description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Example texts for this category.
        /// </summary>
        [JsonPropertyName("examples")]
        public List<string> Examples { get; set; } = new();
    }
}

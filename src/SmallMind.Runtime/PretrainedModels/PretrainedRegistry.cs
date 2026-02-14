using System.Text.Json;
using System.Text.Json.Serialization;
using SmallMind.Core.Validation;

namespace SmallMind.Runtime.PretrainedModels
{
    /// <summary>
    /// Registry entry for a pretrained pack.
    /// </summary>
    internal class PackRegistryEntry
    {
        /// <summary>
        /// Pack identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable pack name.
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Relative path to pack directory.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Pack type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Pack domain.
        /// </summary>
        [JsonPropertyName("domain")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Supported tasks.
        /// </summary>
        [JsonPropertyName("tasks")]
        public List<string> Tasks { get; set; } = new();

        /// <summary>
        /// Whether RAG is enabled.
        /// </summary>
        [JsonPropertyName("rag_enabled")]
        public bool RagEnabled { get; set; }

        /// <summary>
        /// Pack status (e.g., "stable", "experimental").
        /// </summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;
    }

    /// <summary>
    /// Metadata for the pack registry.
    /// </summary>
    internal class RegistryMetadata
    {
        /// <summary>
        /// Total number of packs.
        /// </summary>
        [JsonPropertyName("total_packs")]
        public int TotalPacks { get; set; }

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        [JsonPropertyName("last_updated")]
        public string LastUpdated { get; set; } = string.Empty;

        /// <summary>
        /// Schema version.
        /// </summary>
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = string.Empty;
    }

    /// <summary>
    /// Registry of available pretrained packs.
    /// </summary>
    internal class PretrainedRegistry
    {
        /// <summary>
        /// Registry version.
        /// </summary>
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Registry description.
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// List of available packs.
        /// </summary>
        [JsonPropertyName("packs")]
        public List<PackRegistryEntry> Packs { get; set; } = new();

        /// <summary>
        /// Registry metadata.
        /// </summary>
        [JsonPropertyName("metadata")]
        public RegistryMetadata? Metadata { get; set; }

        /// <summary>
        /// Base path for pack directories (set when loading).
        /// </summary>
        [JsonIgnore]
        public string BasePath { get; set; } = string.Empty;

        /// <summary>
        /// Load the pack registry from a file.
        /// </summary>
        /// <param name="registryPath">Path to registry.json</param>
        /// <returns>Loaded registry</returns>
        public static PretrainedRegistry Load(string registryPath)
        {
            if (!File.Exists(registryPath))
            {
                throw new FileNotFoundException($"Registry file not found: {registryPath}");
            }

            var json = File.ReadAllText(registryPath);
            var registry = JsonSerializer.Deserialize<PretrainedRegistry>(json)
                ?? throw new InvalidOperationException("Failed to deserialize registry");

            // Set base path to registry directory
            registry.BasePath = System.IO.Path.GetDirectoryName(registryPath) ?? string.Empty;

            return registry;
        }

        /// <summary>
        /// Get the full path to a pack.
        /// </summary>
        /// <param name="packPath">Relative pack path</param>
        /// <returns>Full path to pack directory</returns>
        public string GetPackFullPath(string packPath)
        {
            // Validate that the combined path stays within the base directory to prevent path traversal
            return Guard.PathWithinDirectory(BasePath, packPath, nameof(packPath));
        }

        /// <summary>
        /// Find a pack by ID.
        /// </summary>
        /// <param name="id">Pack identifier</param>
        /// <returns>Pack entry or null if not found</returns>
        public PackRegistryEntry? FindPack(string id)
        {
            foreach (var pack in Packs)
            {
                if (pack.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
                {
                    return pack;
                }
            }
            return null;
        }

        /// <summary>
        /// Load a pack by ID.
        /// </summary>
        /// <param name="id">Pack identifier</param>
        /// <returns>Loaded pack</returns>
        public PretrainedPack LoadPack(string id)
        {
            var entry = FindPack(id);
            if (entry == null)
            {
                throw new InvalidOperationException($"Pack not found in registry: {id}");
            }

            var packPath = GetPackFullPath(entry.Path);
            return PretrainedPack.Load(packPath);
        }

        /// <summary>
        /// List all available packs.
        /// </summary>
        /// <returns>Summary of all packs</returns>
        public string ListPacks()
        {
            var summary = $"Available Pretrained Packs ({Packs.Count}):\n";
            summary += new string('-', 60) + "\n";

            foreach (var pack in Packs)
            {
                summary += $"\n{pack.Name} ({pack.Id})\n";
                summary += $"  Domain: {pack.Domain}\n";
                summary += $"  Tasks: {string.Join(", ", pack.Tasks)}\n";
                summary += $"  RAG: {(pack.RagEnabled ? "Enabled" : "Disabled")}\n";
                summary += $"  Status: {pack.Status}\n";
                summary += $"  Path: {pack.Path}\n";
            }

            return summary;
        }
    }
}
